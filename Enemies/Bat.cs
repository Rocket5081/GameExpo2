using Godot;

public partial class Bat : Enemy
{
	[Export] public Vector3 SyncedVelocity
	{
		get => Velocity;
		set => Velocity = value;
	}
	[Export] public bool SyncedIsMoving = false;

	public int speed = 28;
	[Export] public float MaxFlyHeight = 12f;


	public override void _Ready()
	{
		maxHP    = 60;
		hp       = maxHP;
		damage   = 10;
		DeathSfx = GD.Load<AudioStream>("res://Sounds/Dying bat.mp3");
		base._Ready();
		SpawnAmbientSound("res://Sounds/freesound_community-crazy-bat-43208.mp3", volumeDb: -10f, maxDist: 40f);
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
		sfx.UnitSize    = 8f;
		sfx.Autoplay    = true;
		AddChild(sfx);
	}

	public override void _PhysicsProcess(double delta)
	{
	
		base._PhysicsProcess(delta);

		if(GenericCore.Instance.IsServer){
			var target = FindNearestPlayer();
			if (!IsOnFloor())
			{
				Velocity       = new Vector3(0, -5, 0);
				MoveAndSlide();
			}
			else
			{
				Velocity       = new Vector3(0, 0, 0);
			}
			if (target == null)
			{
				Velocity       = new Vector3(0, 0, 0);
				SyncedIsMoving = false;
				MoveAndSlide();
			}
			else
			{
				navAgent.TargetPosition = target.GlobalPosition;
				MoveToward();
				SyncedIsMoving = true;
			}
		}
		

		if (!GenericCore.Instance.IsServer) 
			UpdateAnimation();
			
	}

	private void UpdateAnimation()
	{
		if (myAnimation == null) return;

		if (SyncedIsMoving)
			myAnimation.Play("Fly");
		else
			myAnimation.Play("Fly");
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

		Velocity = new Vector3(direction.X * speed, 0, direction.Z * speed);

		Vector3 flatDirection = new Vector3(direction.X, 0, direction.Z).Normalized();
		if (flatDirection.Length() > 0.1f)
		{
			Transform3D t = Transform;
			t.Basis = Basis.LookingAt(flatDirection, Vector3.Up).Rotated(Vector3.Up, Mathf.DegToRad(0));
			Transform = t;
		}

		MoveAndSlide();

		if (GlobalPosition.Y > MaxFlyHeight)
		{
			var pos = GlobalPosition;
			pos.Y = MaxFlyHeight;
			GlobalPosition = pos;
		}
	}
}
