using Godot;

public partial class Bullet : RigidBody3D
{
	[Export] public NetID myId;

	private float _lifetime = 0f;
	private const float MaxLifetime = 6f;
	public float damage;

	// Guard: prevents HideBullet from running more than once per bullet instance
	private bool _isDying = false;

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
		if (_isDying) return;

		// Players are on layer 2; bullets only have mask 1 so this is a safety guard.
		if (body is Player) return;

		// Use a direct C# cast instead of Call() — Call() cannot reliably find
		// methods inherited from a C# base class through Godot's scripting reflection.
		// All enemy types (Bat, WormEnemy, etc.) extend Enemy, so this always works.
		if (body is Enemy enemy && IsInstanceValid(enemy))
		{
			enemy.OnHitByBullet((int)damage);
			HideBullet();
		}
	}

	private void HideBullet()
	{
		if (_isDying) return;
		_isDying  = true;
		_lifetime = 0f;

		Hide();

		// Disable physics/detection deferred so we don't interrupt the physics step.
		// We do NOT QueueFree here — bullets are pooled and reused by TankPlayer.
		// Calling Reset() will re-enable these deferred fields when the bullet fires again.
		var col  = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col  != null) col.SetDeferred("disabled", true);

		var area = GetNodeOrNull<Area3D>("Area3D");
		if (area != null) area.SetDeferred("monitoring", false);
	}

	/// <summary>
	/// Called by TankPlayer before reusing a pooled bullet.
	/// Resets all per-shot state so the bullet behaves as if newly spawned.
	/// </summary>
	public void Reset()
	{
		_isDying  = false;
		_lifetime = 0f;

		var col  = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col  != null) col.SetDeferred("disabled", false);

		var area = GetNodeOrNull<Area3D>("Area3D");
		if (area != null) area.SetDeferred("monitoring", true);
	}
}
