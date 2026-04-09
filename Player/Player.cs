using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody3D
{
	[Export] public AnimationPlayer myAnimation;
	[Export] public NetID myId;
	[Export] public Marker3D BulletSpawn;
	[Export] public CanvasLayer ReticleLayer;  // kept for legacy wiring; HUD.cs handles display
	[Export] public Label3D NameLabel;

	// Synced via SceneReplicationConfig (replication_mode=2, spawn=true).
	// Server sets this after spawn; clients receive the value through the normal
	// sync channel and _Process applies it to the Label3D.
	[Export] public string PlayerDisplayName = "";

	[Export] public Vector3 SyncedVelocity
	{
		get => Velocity;
		set => Velocity = value;
	}
	[Export] public int   speed     = 15;
	[Export] public float jumpForce = 10f;
	[Export] public float gravity   = 20f;

	[Export] public bool canShoot        = true;
	[Export] public bool canJump         = true;
	[Export] public bool SyncedIsOnFloor = true;
	[Export] public bool SyncedIsJumping = false;

	private float _mouseDeltaX = 0f;
	private bool  _mouseCaptured = false;

	public float timer = 0.5f;
	public float abilityCooldown;
	public float damage = 10f;
	public int   hp;
	public int   maxHp;

	public List<Bullet> Buls     = new List<Bullet>();
	public int          bulCount = 0;

	public override void _Ready()
	{
		base._Ready();
		AddToGroup("Players");
	}

	public override void _Input(InputEvent @event)
	{
		if (myId == null || !myId.IsLocal) return;

		if (@event is InputEventMouseMotion mouseMotion)
			_mouseDeltaX += mouseMotion.Relative.X;

		if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
			Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (myId == null) return;

		// ── Nameplate ────────────────────────────────────────────────────────────
		// PlayerDisplayName is synced from the server via SceneReplicationConfig.
		// Poll every frame until the value arrives, then apply once and stop.
		if (NameLabel != null)
		{
			if (myId.IsLocal)
			{
				// Never show the name above your own head
				NameLabel.Visible = false;
			}
			else if (PlayerDisplayName.Length > 0 && NameLabel.Text != PlayerDisplayName)
			{
				// First time the synced name lands — set text + class colour
				NameLabel.Text = PlayerDisplayName;

				// Colours match the class colours in the main menu exactly:
				//   Cowboy / DPS     → ff4444 (red)
				//   Pirate / Tank    → 4488ff (blue)
				//   Priest / Support → 44cc66 (green)
				if      (this is DpsPlayer)     NameLabel.Modulate = new Color("ff4444");
				else if (this is TankPlayer)    NameLabel.Modulate = new Color("4488ff");
				else if (this is SupportPlayer) NameLabel.Modulate = new Color("44cc66");

				NameLabel.Visible = true;
			}
		}

		// ── Mouse capture ─────────────────────────────────────────────────────
		if (myId.IsLocal && !_mouseCaptured)
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
			_mouseCaptured = true;
		}

		if (!myId.IsNetworkReady) return;

		// ── Shoot cooldown ────────────────────────────────────────────────────
		if (!canShoot)
		{
			timer -= (float)delta;
			if (timer <= 0)
			{
				canShoot = true;
				timer    = 0.5f;
			}
		}

		// ── Server-side physics ───────────────────────────────────────────────
		if (GenericCore.Instance.IsServer)
		{
			// 1. Apply gravity while airborne
			if (!IsOnFloor())
			{
				Vector3 vel = SyncedVelocity;
				vel.Y -= gravity * (float)delta;
				SyncedVelocity = vel;
			}

			// 2. Move and resolve collisions
			MoveAndSlide();

			// 3. After sliding: clamp Y if on floor.
			//    Clears both negative (landing) and small positive (penetration
			//    artifacts). Intentional jumps (10f) are above the 3f threshold.
			if (IsOnFloor())
			{
				Vector3 vel = SyncedVelocity;
				if (vel.Y < jumpForce * 0.3f)
					vel.Y = 0f;
				SyncedVelocity = vel;
			}

			SyncedIsOnFloor = IsOnFloor();
			SyncedIsJumping = !IsOnFloor();
		}

		// ── Local player input ────────────────────────────────────────────────
		if (myId.IsLocal)
		{
			float moveInput   = Input.GetAxis("forward", "back");
			float strafeInput = Input.GetAxis("right", "left");
			float turnInput   = _mouseDeltaX / 200f;
			_mouseDeltaX = 0f;

			Rpc("MoveMe", new Vector3(turnInput, moveInput, strafeInput));

			if (Input.IsActionJustPressed("primary") && canShoot)
			{
				canShoot = false;
				RpcId(1, "Fire");
				Rpc("PlayAttackAnimation");
			}

			if (Input.IsActionJustPressed("jump") && canJump)
				RpcId(1, "Jump");
		}

		if (!GenericCore.Instance.IsServer)
			UpdateAnimation();
	}

	private void UpdateAnimation()
	{
		if (myAnimation == null) return;
		if (myAnimation.CurrentAnimation == "Attack" && myAnimation.IsPlaying()) return;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void PlayAttackAnimation() { }

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void Jump()
	{
		if (GenericCore.Instance.IsServer && IsOnFloor())
		{
			Vector3 vel = SyncedVelocity;
			vel.Y = jumpForce;
			SyncedVelocity = vel;
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public virtual void Fire() { }

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void MoveMe(Vector3 input)
	{
		if (!GenericCore.Instance.IsServer) return;

		RotateY(input.X * -0.5f);

		Vector3 forward  = -Transform.Basis.X;
		Vector3 right    = -Transform.Basis.Z;
		float   currentY = SyncedVelocity.Y;

		Vector3 horizontal = (forward * input.Y) + (right * input.Z);
		horizontal = horizontal.Length() > 0.001f
			? horizontal.Normalized() * speed
			: Vector3.Zero;

		SyncedVelocity = new Vector3(horizontal.X, currentY, horizontal.Z);
	}

	public Vector3 GetBulletSpawnPos()
	{
		if (BulletSpawn != null)
			return BulletSpawn.GlobalPosition;
		return GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
	}

	public async void SpawnBullet(int count, float cooldown)
	{
		for (int i = 0; i < count; i++)
		{
			Vector3 spawnPos = GetBulletSpawnPos();
			var t1 = GenericCore.Instance.MainNetworkCore?.NetCreateObject(
				3, spawnPos, Transform.Basis.GetRotationQuaternion(), 1);

			if (t1 == null) return;
			if (t1 is RigidBody3D rb)
			{
				// Layer 4 = bullet layer; Mask 1 = only collide with environment (layer 1).
				// Players are on layer 2 — bullets pass through them entirely.
				rb.CollisionLayer = 4;
				rb.CollisionMask  = 1;
				rb.LinearVelocity = Transform.Basis.X * 150f;
			}
			if (t1 is Bullet b) Buls.Add(b);

			await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
		}
	}

	public async void ShootBullet(int count, float cooldown)
	{
		for (int i = 0; i < count; i++)
		{
			Vector3 spawnPos = GetBulletSpawnPos();
			Buls[bulCount].Show();
			// Layer 4 = bullet layer; Mask 1 = only collide with environment.
			Buls[bulCount].CollisionLayer = 4;
			Buls[bulCount].CollisionMask  = 1;
			Buls[bulCount].GlobalPosition = spawnPos;
			Buls[bulCount].LinearVelocity = Transform.Basis.X * 150f;
			bulCount++;
			if (bulCount >= Buls.Count) bulCount = 0;

			await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
		}
	}
}
