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

	// ── Sound ────────────────────────────────────────────────────────────────
	[Export] public AudioStreamPlayer3D FootstepPlayer;
	[Export] public float FootstepInterval = 0.45f;

	private float _footstepTimer = 0f;

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

	private float _mouseDeltaX  = 0f;
	private bool  _mouseCaptured = false;

	public float maxTimer = 0.5f;
	public float timer = 0.5f;
	public float damage = 10f;
	public int   hp;
	public int   maxHp;

	// ── Ultimate ability ─────────────────────────────────────────────────────
	public float UltimateCooldownMax   = 30f;
	public float UltimateCooldownTimer = 30f;   // counts down to 0; 0 = ready
	public bool  IsShielded            = false; // set by Tank bubble

	public List<Bullet> Buls     = new List<Bullet>();
	public int          bulCount = 0;

	public int   burstCount = 3;
	public float burstDelay = 0.1f;

	public override void _Ready()
	{
		base._Ready();
		AddToGroup("Players");

		// Register the "ability" (Q) action at runtime so it works without
		// needing to be added manually in Project Settings → Input Map.
		if (!InputMap.HasAction("ability"))
		{
			InputMap.AddAction("ability");
			var ev = new InputEventKey();
			ev.Keycode = Key.Q;
			InputMap.ActionAddEvent("ability", ev);
		}
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
		if (NameLabel != null)
		{
			if (myId.IsLocal)
			{
				NameLabel.Visible = false;
			}
			else if (PlayerDisplayName.Length > 0 && NameLabel.Text != PlayerDisplayName)
			{
				NameLabel.Text = PlayerDisplayName;
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

		// ── Ultimate cooldown tick ─────────────────────────────────────────────
		// Ticks on local client for HUD display; server also ticks for validation.
		if (UltimateCooldownTimer > 0f)
			UltimateCooldownTimer -= (float)delta;

		// ── Server-side physics ───────────────────────────────────────────────
		if (GenericCore.Instance.IsServer)
		{
			if (!IsOnFloor())
			{
				Vector3 vel = SyncedVelocity;
				vel.Y -= gravity * (float)delta;
				SyncedVelocity = vel;
			}

			MoveAndSlide();

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

			// ── Footstep sounds ───────────────────────────────────────────────
			bool isMoving = Mathf.Abs(moveInput) > 0.01f || Mathf.Abs(strafeInput) > 0.01f;
			if (isMoving && SyncedIsOnFloor && FootstepPlayer != null)
			{
				_footstepTimer -= (float)delta;
				if (_footstepTimer <= 0f)
				{
					FootstepPlayer.Play();
					_footstepTimer = FootstepInterval;
				}
			}
			else if (!isMoving || !SyncedIsOnFloor)
			{
				FootstepPlayer?.Stop();
				_footstepTimer = 0f;
			}

			if (Input.IsActionPressed("primary") && canShoot)
			{
				canShoot = false;
				RpcId(1, "Fire");
				Rpc("PlayAttackAnimation");
			}

			if (Input.IsActionJustPressed("jump") && canJump)
				RpcId(1, "Jump");

			// ── Ultimate ability input ────────────────────────────────────────
			// Registered at runtime in _Ready(); mapped to Q.
			if (Input.IsActionJustPressed("ability") && UltimateCooldownTimer <= 0f)
			{
				UltimateCooldownTimer = UltimateCooldownMax;
				OnLocalUltimateActivated();   // subclass plays its activation sound
				RpcId(1, "UseUltimate");
			}
		}

		if (!GenericCore.Instance.IsServer)
			UpdateAnimation();
	}

	private void UpdateAnimation()
	{
		if (myAnimation == null) return;

		if (myAnimation.CurrentAnimation == "Attack" && myAnimation.IsPlaying()) return;

		Vector3 horizVel   = new Vector3(SyncedVelocity.X, 0f, SyncedVelocity.Z);
		bool isMovingHoriz = horizVel.Length() > 0.5f;
		bool shouldWalk    = isMovingHoriz && SyncedIsOnFloor;

		if (shouldWalk)
		{
			if (myAnimation.CurrentAnimation != "Walk" || !myAnimation.IsPlaying())
				myAnimation.Play("Walk");
		}
		else
		{
			if (myAnimation.CurrentAnimation == "Walk" && myAnimation.IsPlaying())
				myAnimation.Stop();
		}
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

	/// <summary>
	/// Called locally on the pressing client the moment Q is accepted.
	/// Override in each class to play an activation sound.
	/// </summary>
	protected virtual void OnLocalUltimateActivated() { }

	/// <summary>
	/// Override in each class to define the ultimate ability.
	/// Called on the server via RpcId(1, "UseUltimate") from the local client.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public virtual void UseUltimate() { }

	/// <summary>
	/// Called when this player is hit by a damaging attack.
	/// IsShielded (set by the Tank bubble) blocks all damage.
	/// </summary>
	public virtual void OnHitByBullet()
	{
		if (IsShielded) return;
		// Damage is handled by the game's health system; hook here as needed.
	}

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

	// ── Nameplate RPC ────────────────────────────────────────────────────────
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void SetDisplayName(string name)
	{
		PlayerDisplayName = name;

		if (NameLabel == null) return;
		if (myId != null && myId.IsLocal) { NameLabel.Visible = false; return; }

		NameLabel.Text = name;
		if      (this is DpsPlayer)     NameLabel.Modulate = new Color("ff4444");
		else if (this is TankPlayer)    NameLabel.Modulate = new Color("4488ff");
		else if (this is SupportPlayer) NameLabel.Modulate = new Color("44cc66");
		NameLabel.Visible = true;
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
			Buls[bulCount].CollisionLayer = 4;
			Buls[bulCount].CollisionMask  = 1;
			Buls[bulCount].GlobalPosition = spawnPos;
			Buls[bulCount].LinearVelocity = Transform.Basis.X * 150f;
			bulCount++;
			if (bulCount >= Buls.Count)
				bulCount = 0;

			await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
		}
	}
}
