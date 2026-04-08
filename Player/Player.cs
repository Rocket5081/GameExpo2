using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody3D
{
	[Export] public AnimationPlayer myAnimation;
	[Export] public NetID myId;
	[Export] public Vector3 SyncedVelocity
	{
		get => Velocity;
		set => Velocity = value;
	}
	[Export] public int speed = 15;
	[Export] public float jumpForce = 10f;
	[Export] public float gravity = 20f;

	[Export] public bool canShoot = true;
	[Export] public bool canJump = true;
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

	private bool _menuHidden = false;

	public override void _Ready()
{
	base._Ready();
	AddToGroup("Players");

	if (myId.IsLocal)
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}
}

private void HideMenuOnConnect()
{
	var mainMenu = GetTree().Root.GetNodeOrNull("GameRoot/MainMenu");
	if (mainMenu == null)
	{
		GD.PrintErr("[Player] MainMenu NOT FOUND at GameRoot/MainMenu");
		return;
	}

	GD.Print("[Player] Found: " + mainMenu.GetType().Name);

	if (mainMenu is CanvasItem ci)
	{
		ci.Visible = false;
		GD.Print("[Player] MainMenu hidden from Player.");
	}
}

	public override void _Input(InputEvent @event)
	{
		if (!myId.IsLocal) return;

		if (@event is InputEventMouseMotion mouseMotion)
			_mouseDeltaX += mouseMotion.Relative.X;

		if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
			Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		if (myId.IsLocal && !_mouseCaptured)
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
			_mouseCaptured = true;
		}

		if (!myId.IsNetworkReady) return;

		if (!canShoot)
		{
			timer -= (float)delta;
			if (timer <= 0)
			{
				canShoot = true;
				timer = 0.5f;
			}
		}

		if (GenericCore.Instance.IsServer)
		{
			if (!IsOnFloor())
			{
				Vector3 vel = SyncedVelocity;
				vel.Y -= gravity * (float)delta;
				SyncedVelocity = vel;
			}
			else
			{
				Vector3 vel = SyncedVelocity;
				if (vel.Y < 0) vel.Y = 0;
				SyncedVelocity = vel;
			}

			SyncedIsOnFloor = IsOnFloor();
			SyncedIsJumping = !IsOnFloor();
			MoveAndSlide();
		}

		if (myId.IsLocal)
		{
			float moveInput   = Input.GetAxis("forward", "back");
			float strafeInput = Input.GetAxis("right", "left");
			float turnInput   = _mouseDeltaX / 200f;
			_mouseDeltaX = 0f;

			Vector3 myInputAxis = new Vector3(turnInput, moveInput, strafeInput);
			Rpc("MoveMe", myInputAxis);

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
	public void PlayAttackAnimation()
	{
	}

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
	public virtual void Fire()
	{
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

		if (horizontal.Length() > 0.001f)
			horizontal = horizontal.Normalized() * speed;
		else
			horizontal = Vector3.Zero;

		SyncedVelocity = new Vector3(horizontal.X, currentY, horizontal.Z);
	}

	public async void SpawnBullet(int count, float cooldown)
	{
		Vector3 spawnPos = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);

		for (int i = 0; i < count; i++)
		{
			var t1 = GenericCore.Instance.MainNetworkCore?.NetCreateObject(
				1, spawnPos, Transform.Basis.GetRotationQuaternion(), 1);

			if (t1 == null) return;

			if (t1 is RigidBody3D rb)
				rb.LinearVelocity = Transform.Basis.X * 150f;

			if (t1 is Bullet b)
				Buls.Add(b);

			await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
		}
	}

	public async void ShootBullet(int count, float cooldown)
	{
		for (int i = 0; i < count; i++)
		{
			Buls[bulCount].Show();
			Buls[bulCount].CollisionLayer = 1;
			Buls[bulCount].CollisionMask  = 1;
			Buls[bulCount].GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
			Buls[bulCount].LinearVelocity = Transform.Basis.X * 150f;
			bulCount++;
			if (bulCount >= Buls.Count) bulCount = 0;
			await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
		}
	}
}
