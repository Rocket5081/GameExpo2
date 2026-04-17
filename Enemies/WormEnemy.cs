using Godot;

public partial class WormEnemy : Enemy
{
	[Export] public Vector3 SyncedVelocity
	{
		get => Velocity;
		set => Velocity = value;
	}
	[Export] public bool SyncedIsMoving = false;

	public int speed =20;

	public override void _Ready()
	{
		maxHP    = 60;
		hp       = maxHP;
		damage   = 20;
		DeathSfx = GD.Load<AudioStream>("res://Sounds/Dying Worm.mp3");
		base._Ready();
		SpawnAmbientSound("res://Sounds/universfield-worm-movement-277577.mp3", volumeDb: -6f, maxDist: 50f);
	}

	private void SpawnAmbientSound(string path, float volumeDb, float maxDist)
	{
		var raw = GD.Load<AudioStream>(path);
		if (raw == null) return;

		var stream = (AudioStream)raw.Duplicate();
		if (stream is AudioStreamMP3 mp3) mp3.Loop = true;

		var sfx = new AudioStreamPlayer3D();
		sfx.Stream      = stream;
		sfx.VolumeDb    = volumeDb;
		sfx.MaxDistance = maxDist;
		sfx.UnitSize    = 10f;
		sfx.Autoplay    = true;
		AddChild(sfx);
	}

	public override void _PhysicsProcess(double delta)
	{
		// MUST call base so Enemy._PhysicsProcess runs the damage tick
		base._PhysicsProcess(delta);

		if (!GenericCore.Instance.IsServer) UpdateAnimation();

		if(GenericCore.Instance.IsServer){

			if (!IsOnFloor())
			{
				var vel = Velocity;
				vel.Y  -= 20f * (float)delta;
				Velocity = vel;
			}
			var target = FindNearestPlayer();
			if (target == null)
			{
				Velocity       = Vector3.Zero;
				SyncedIsMoving = false;
			}
			else
			{
				navAgent.TargetPosition = target.GlobalPosition;
				MoveToward();
				SyncedIsMoving = true;
			}
		}
	}

	private void UpdateAnimation()
	{
		if (myAnimation == null) return;

		if (SyncedIsMoving)
			myAnimation.Play("Move");
		else
			myAnimation.Play("Move");
	}

	private Player FindNearestPlayer()
	{
		Player nearest  = null;
		float  bestDist = float.MaxValue;
		foreach (Node node in GetTree().GetNodesInGroup("Players"))
		{
			if (node is not Player p) continue;
			float d = GlobalPosition.DistanceTo(p.GlobalPosition);
			if (d < bestDist) { bestDist = d; nearest = p; }
		}
		return nearest;
	}

	private void MoveToward()
	{
		Vector3 destination = navAgent.GetNextPathPosition();
		Vector3 direction = (destination - GlobalPosition).Normalized();

		float currentY = Velocity.Y;
		Velocity = new Vector3(direction.X * speed, currentY, direction.Z * speed);

		Vector3 flatDirection = new Vector3(direction.X, 0, direction.Z).Normalized();
		if (flatDirection.Length() > 0.1f)
		{
			Transform3D t = Transform;
			t.Basis = Basis.LookingAt(flatDirection, Vector3.Up).Rotated(Vector3.Up, Mathf.DegToRad(0));
			Transform = t;
		}

		MoveAndSlide();
	}
}
