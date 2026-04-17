using Godot;

public partial class Bat : Enemy
{
	[Export] public Vector3 SyncedVelocity
	{
		get => Velocity;
		set => Velocity = value;
	}
	[Export] public bool SyncedIsMoving = false;

	public int speed = 10;
	[Export] public float MaxFlyHeight = 12f;


	public override void _Ready()
	{
		maxHP  = 20;
		hp     = maxHP;
		damage = 10;
		base._Ready();
	}

	public override void _PhysicsProcess(double delta)
	{
		// MUST call base so Enemy._PhysicsProcess runs the damage tick
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
