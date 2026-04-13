using Godot;
using System;

public partial class Bullet : RigidBody3D
{
	[Export] public NetID myId;

	private float _lifetime = 0f;
	private const float MaxLifetime = 6f;
	public float damage;

	public override void _Ready()
	{
		// Bullets live on layer 4, only interact with environment (layer 1).
		// Players are on layer 2 — they are invisible to bullets.
		CollisionLayer = 4;
		CollisionMask  = 1;

		// Clients don't simulate bullet physics — the server is authoritative.
		// The MultiplayerSynchronizer syncs position + linear_velocity every frame.
		if (GenericCore.Instance != null && !GenericCore.Instance.IsServer)
			Freeze = true;
	}

	public override void _Process(double delta)
	{
		if (myId == null || !myId.IsNetworkReady) return;
		if (!GenericCore.Instance.IsServer) return;

		_lifetime += (float)delta;
		if (_lifetime >= MaxLifetime)
			HideBullet();
	}

	private void OnAreaEntered(Node body)
	{
		if (!GenericCore.Instance.IsServer) return;

		// Players are on layer 2; bullets are on layer 4 with mask 1 (layer 1 only),
		// so this callback will never fire for a Player — but guard just in case.
		if (body is Player) return;
		if (body.IsInGroup("enemy"))
		{
			HideBullet();
			body.Call("OnHitByBullet", damage);
		}
	}

	private void HideBullet()
	{
		Hide();
		GetNode<CollisionShape3D>("CollisionShape3D").SetDeferred("disabled", true);
		GetNode<Area3D>("Area3D").SetDeferred("disabled", true);
	}
}
