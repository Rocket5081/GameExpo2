using Godot;

public partial class Bullet : RigidBody3D
{
	[Export] public NetID myId;

	private float _lifetime = 0f;
	private const float MaxLifetime = 6f;
	public float damage;

	// The player who fired this bullet — set by the spawning player
	public Player Shooter = null;

	private bool _isDying = false;

	public override void _Ready()
	{
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

		if (body is Player) return;

		if (body is Enemy enemy && IsInstanceValid(enemy))
		{
			// Pass the shooter so the enemy can credit score correctly
			enemy.OnHitByBullet((int)damage, Shooter);
			HideBullet();
		}
		if(body.GetParent().GetParent().GetParent().GetParent().GetParent() is Enemy boss && IsInstanceValid(boss))
		{
			boss.OnHitByBullet((int)damage, Shooter);
			HideBullet();
		}
	}

	private void HideBullet()
	{
		if (_isDying) return;
		_isDying  = true;
		_lifetime = 0f;

		Hide();

		var col  = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col  != null) col.SetDeferred("disabled", true);

		var area = GetNodeOrNull<Area3D>("Area3D");
		if (area != null) area.SetDeferred("monitoring", false);
	}

	public void Reset()
	{
		_isDying  = false;
		_lifetime = 0f;
		Shooter   = null;

		var col  = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col  != null) col.SetDeferred("disabled", false);

		var area = GetNodeOrNull<Area3D>("Area3D");
		if (area != null) area.SetDeferred("monitoring", true);
	}
}
