using Godot;
using System;

public partial class Boss : Enemy
{
    [Export] public Vector3 SyncedVelocity
	{
		get => Velocity;
		set => Velocity = value;
	}
	[Export] public bool SyncedIsMoving = false;

    public int speed = 30;

    public bool phaseTwo = false;

    public override void _Ready()
    {
        maxHP  = 150;
		hp     = maxHP;
		damage = 30;
        base._Ready();
    }

    public override void _PhysicsProcess(double delta)
	{
		// MUST call base so Enemy._PhysicsProcess runs the damage tick
		base._PhysicsProcess(delta);

		if (!GenericCore.Instance.IsServer) return;

		if (!IsOnFloor())
		{
			var vel = Velocity;
			vel.Y  -= 20f * (float)delta;
			Velocity = vel;
		}

	// 	var target = FindNearestPlayer();
	// 	if (target == null)
	// 	{
	// 		Velocity       = Vector3.Zero;
	// 		SyncedIsMoving = false;
	// 	}
	// 	else
	// 	{
	// 		MoveToward(target);
	// 	}
	// }

    // private Node3D FindNextLocation()
    // {
    //     Node3D next = null;

    } 

    
}
