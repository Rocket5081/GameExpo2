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

	// ── Entry sequence ────────────────────────────────────────────────────────
	// Boss spawns 35 units above the arena and descends into position.
	private bool  _isEntering   = true;
	private float _entryFloorY;
	private const float EntrySpeed = 22f;   // units per second descent speed

	// Synced animation name — server sets this, MultiplayerSynchronizer replicates
	// it to all clients so every peer plays the correct clip.
	[Export] public string SyncedAnimName = "FastMove";

	public float waitTime = 8f;
	public string curLocation = "BossL1";
	public bool phaseTwo = false;
	Node3D target = null;

	[Export] public bool rewinding = false;
	public Godot.Collections.Dictionary rewindValues = new Godot.Collections.Dictionary
	{
		{"position", new Godot.Collections.Array {}},
		{"rotation", new Godot.Collections.Array {}}
	};
	

	public override void _Ready()
	{
		maxHP  = 1000;
		hp     = maxHP;
		damage = 50;
		DeathSfx = GD.Load<AudioStream>("res://Sounds/Dying Boss.mp3");
		LookAt(new Vector3(0,0,0));
		base._Ready();
		AddToGroup("Bosses");
		SpawnAmbientSound("res://Sounds/dragon-studio-alien-sounds-463202.mp3", volumeDb: -14f, maxDist: 180f);

		// Boss spawns 35 units above its fight position — record where the floor is.
		_entryFloorY = GlobalPosition.Y - 35f;
	}

	public override void _Process(double delta)
	{
		// ── Entry descent — boss flies down from above into the arena ─────────
		if (_isEntering)
		{
			if (GenericCore.Instance.IsServer)
			{
				var pos  = GlobalPosition;
				pos.Y   -= EntrySpeed * (float)delta;

				if (pos.Y <= _entryFloorY)
				{
					pos.Y      = _entryFloorY;
					_isEntering = false;
					waitTime    = 2f;   // brief pause before patrol begins
					GD.Print("[Boss] Entry complete — starting patrol.");
				}

				GlobalPosition = pos;
				LookAt(new Vector3(GlobalPosition.X, GlobalPosition.Y, 0f));
			}
			UpdateAnimation();
			return;   // skip patrol and rewind logic until fully landed
		}

		// ── Normal patrol ─────────────────────────────────────────────────────
		if (GenericCore.Instance.IsServer)
		{
			if (!SyncedIsMoving && waitTime <= 0f)
			{
				target         = FindNextLocation();
				SyncedIsMoving = true;
				SyncedAnimName = "FastMove";
			}
			else if (!SyncedIsMoving)
			{
				waitTime      -= (float)delta;
				RandomNumberGenerator random = new RandomNumberGenerator();
				random.Randomize();
				int rand = random.RandiRange(0,2);
				switch(rand){
					case 0:
						SyncedAnimName = "SmashLeft";
						break;
					case 1:
						SyncedAnimName = "Swipe";
						break;
					case 2:
						SyncedAnimName = "SmashRight";
						break;
				}
			}
			MoveToNext(target);
		}

		UpdateAnimation();

		if (GenericCore.Instance.rewind)
		{
			rewind();
		}
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
		sfx.UnitSize    = 15f;
		sfx.Autoplay    = true;
		AddChild(sfx);
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
		else
		{
			for (int i = 0; i < 5; i++)
			{
				computeRewind();
				if (!rewinding) break;   // EndRewind was called inside computeRewind
			}
			
		}
		}
	} 

	private void UpdateAnimation()
	{
		if (myAnimation == null) return;
		// SyncedAnimName is set server-side and replicated to all clients via
		// MultiplayerSynchronizer, so every peer plays the correct clip.
		if (myAnimation.CurrentAnimation != SyncedAnimName)
			myAnimation.Play(SyncedAnimName);
	}

	private Node3D FindNextLocation()
	{
		var parent = GetParent();
		var child = parent.GetChildren();
		if (!phaseTwo)
		{
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
		}
		else
		{
			RandomNumberGenerator random = new RandomNumberGenerator();
			random.Randomize();
			int rand = random.RandiRange(0,3);
			curLocation = child[rand].Name;
			return (Node3D)child[rand];
		}

		return null;
	}

	private void MoveToNext(Node3D target)
	{
		if (target == null) return;
		if (GlobalPosition.DistanceTo(target.GlobalPosition) > 5.0f)
		{
			LookAt(new Vector3(0, 0, 0));
			GlobalPosition += GlobalPosition.DirectionTo(target.GlobalPosition) * speed;
			waitTime        = 8f;
		}
		else
		{
			// Arrived — switch to idle/attack animation and let the wait timer run.
			SyncedIsMoving = false;
			SyncedAnimName = "Swipe";
		}
	}
	
	public override void SetupContactArea()
	{
		foreach(Area3D area in GetTree().GetNodesInGroup("enemy"))
		{
			GD.Print(area.GetParent().Name);
			area.SetCollisionLayerValue(1, false);
			area.SetCollisionMaskValue(1, false);
			area.SetCollisionLayerValue(8, true);
			area.SetCollisionMaskValue(2, true);
			area.BodyEntered += OnPlayerBodyEntered;
			area.BodyExited  += OnPlayerBodyExited;
		}
		
	}

	protected override void Die()
	{
		if (!phaseTwo)
		{
			// First kill — broadcast rewind to ALL peers via RPC so every client
			// sets GenericCore.rewind = true and starts their rewind logic.
			if (Multiplayer.HasMultiplayerPeer())
				GenericCore.Instance.Rpc(nameof(GenericCore.StartRewind));
			else
				GenericCore.Instance.StartRewind();
			hp       = maxHP;
			phaseTwo = true;
		}
		else
		{
			// Second kill — show the win screen on every peer, then free the boss.
			if (Multiplayer.HasMultiplayerPeer())
				Rpc(nameof(ShowWinScreenRpc));
			else
				ShowWinScreenRpc();

			QueueFree();
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ShowWinScreenRpc()
	{
		// WinScreen lives in the scene tree already (added by MainGame._Ready).
		// Just tell it to populate the scoreboard and make itself visible.
		WinScreen.Instance?.ShowFor(GetTree().GetNodesInGroup("Players"));
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
		curLocation = "BossL4";
		target = FindNextLocation();
		hp = maxHP;
		if(((Godot.Collections.Array)rewindValues["position"]).Count == 0)
		{
			GetNode<CollisionShape3D>("CollisionShape3D").SetDeferred("disabled", false);
			rewinding = false;
			GlobalPosition = (Vector3)pos;
			Rotation = (Vector3)rot;
			// Broadcast EndRewind to ALL peers so their players stop rewinding too
			if (Multiplayer.HasMultiplayerPeer())
				GenericCore.Instance.Rpc(nameof(GenericCore.EndRewind));
			else
				GenericCore.Instance.EndRewind();
		}
		GlobalPosition = (Vector3)pos;
		Rotation = (Vector3)rot;
	}
}
