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

		var target = FindNearestPlayer();
		if (target == null)
		{
			Velocity       = Vector3.Zero;
			SyncedIsMoving = false;
		}
		else
		{
			MoveToward(target);
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

	private void MoveToward(Player target)
	{
		float yPull = GlobalPosition.Y > MaxFlyHeight ? -30f : -5f;

		Vector3 dir = new Vector3(
			target.GlobalPosition.X - GlobalPosition.X,
			yPull,
			target.GlobalPosition.Z - GlobalPosition.Z
		).Normalized();

		SyncedVelocity = dir * speed;
		SyncedIsMoving = true;

		if (GlobalPosition.DistanceTo(target.GlobalPosition) > 0.5f)
			LookAt(target.GlobalPosition, Vector3.Up);

		MoveAndSlide();

		if (GlobalPosition.Y > MaxFlyHeight)
		{
			var pos = GlobalPosition;
			pos.Y = MaxFlyHeight;
			GlobalPosition = pos;
		}
	}
}
