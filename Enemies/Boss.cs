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

	public int speed = 5;

	public float waitTime = 8f;
	public string curLocation = "BossL1";
	public bool phaseTwo = false;
	Node3D target = null;

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

		if (GenericCore.Instance.IsServer){
			
			if (!SyncedIsMoving && waitTime <= 0f)
			{
				target = FindNextLocation();
				SyncedIsMoving = true;
			}
			else
				waitTime -= (float)delta;
			MoveToNext(target);
		
		}

		if (!GenericCore.Instance.IsServer) 
			UpdateAnimation();
			

	} 

	private void UpdateAnimation()
	{
		if (myAnimation == null) return;

		if (SyncedIsMoving)
			myAnimation.Play("Swipe");
		else
			myAnimation.Play("Swipe"); 
	}

	private Node3D FindNextLocation()
	{
		var parent = GetParent();
		var child = parent.GetChildren();
		for(int i=0; i<child.Count; i++)
		{
			if(child[i].Name == curLocation)
			{
				if(i != 3)
				{
					curLocation = child[i+1].Name;
					return (Node3D)child[i+1];
				}
				else
				{
					curLocation = child[0].Name;
					return(Node3D)child[0];
				}	
			}
		}
		return null;
	}

	private void MoveToNext(Node3D target)
	{
	if(target == null) return;
	if (GlobalPosition.DistanceTo(target.GlobalPosition) > 5.0f) {
		LookAt(new Vector3(0,0,0));
		GlobalPosition += GlobalPosition.DirectionTo(target.GlobalPosition) * speed;
		waitTime = 8f;
	}
	else
	{
		SyncedIsMoving = false;
	}
	}
	// private void SetupContactArea()
	// {
	// 	foreach(Area3D area in )
	// 	area.BodyEntered += OnPlayerBodyEntered;
	// 	area.BodyExited  += OnPlayerBodyExited;
	// }

	
}
