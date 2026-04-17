using Godot;
using System;
using System.Linq;

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

	public bool rewinding = false;
	public Godot.Collections.Dictionary rewindValues = new Godot.Collections.Dictionary
	{
		{"position", new Godot.Collections.Array {}},
		{"rotation", new Godot.Collections.Array {}}
	};
	

	public override void _Ready()
	{
		maxHP  = 150;
		hp     = maxHP;
		damage = 30;
		LookAt(new Vector3(0,0,0));
		base._Ready();
	}

    public override void _Process(double delta)
    {
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

		if (!GenericCore.Instance.IsServer){} 
			UpdateAnimation();

		if (GenericCore.Instance.rewind)
		{
			rewind();
		}
    }


	public override void _PhysicsProcess(double delta)
	{
		// MUST call base so Enemy._PhysicsProcess runs the damage tick
		base._PhysicsProcess(delta);
		if (GenericCore.Instance.IsServer){
		if (!rewinding )//&& hp>0 && !phaseTwo)
		{
			((Godot.Collections.Array)rewindValues["position"]).Add(Position);
			((Godot.Collections.Array)rewindValues["rotation"]).Add(Rotation);
		}
		else //if(hp <= 0 && !phaseTwo && rewinding)
		{
			computeRewind();
			
		}
		}
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
	
	public override void SetupContactArea()
	{
		foreach(Area3D area in GetTree().GetNodesInGroup("enemy"))
		{
			area.SetCollisionMaskValue(2, true);
			area.BodyEntered += OnPlayerBodyEntered;
			area.BodyExited  += OnPlayerBodyExited;
		}
		
	}

	public void rewind()
	{
		rewinding = true;
	}

	//https://www.youtube.com/watch?v=XoETrCrSkks a link for a complete description of rewind feature: 1:12 - 3:44
	public void computeRewind()
	{
		var pos = ((Godot.Collections.Array)rewindValues["position"]).Last();
		var rot = ((Godot.Collections.Array)rewindValues["rotation"]).Last();
		((Godot.Collections.Array)rewindValues["position"]).RemoveAt(((Godot.Collections.Array)rewindValues["position"]).Count -1);
		((Godot.Collections.Array)rewindValues["rotation"]).RemoveAt(((Godot.Collections.Array)rewindValues["rotation"]).Count -1);
		waitTime = 8f;
		curLocation = "BossL1";
		target = null;
		hp = maxHP;
		if(((Godot.Collections.Array)rewindValues["position"]).Count == 0)
		{
			GetNode<CollisionShape3D>("CollisionShape3D").SetDeferred("disabled", false);
			rewinding = false;
			GlobalPosition = (Vector3)pos;
			Rotation = (Vector3)rot;
			GenericCore.Instance.rewind = false;
			GD.Print(GlobalPosition);
		}
		GlobalPosition = (Vector3)pos;
		Rotation = (Vector3)rot;
	}
}