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
		maxHP  = 300;
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
		Input.MouseMode = Input.MouseModeEnum.Visible;

		// ── Full-screen canvas ─────────────────────────────────────────────────
		var canvas = new CanvasLayer();
		canvas.Layer = 20;
		GetTree().Root.AddChild(canvas);

		// Dark backdrop
		var bg            = new ColorRect();
		bg.Color          = new Color(0.01f, 0f, 0.06f, 0.93f);
		bg.AnchorLeft     = 0f;  bg.AnchorTop    = 0f;
		bg.AnchorRight    = 1f;  bg.AnchorBottom = 1f;
		bg.GrowHorizontal = Control.GrowDirection.Both;
		bg.GrowVertical   = Control.GrowDirection.Both;
		bg.MouseFilter    = Control.MouseFilterEnum.Ignore;
		canvas.AddChild(bg);

		// "YOU WIN!" title
		var title              = new Label();
		title.Text             = "✦  YOU WIN!  ✦";
		title.AddThemeFontSizeOverride("font_size", 72);
		title.AddThemeColorOverride("font_color",         new Color(1f, 0.85f, 0.2f));
		title.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 1f));
		title.AddThemeConstantOverride("outline_size", 4);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AnchorLeft     = 0f;    title.AnchorTop    = 0.5f;
		title.AnchorRight    = 1f;    title.AnchorBottom = 0.5f;
		title.OffsetTop      = -120f; title.OffsetBottom = -40f;
		title.GrowHorizontal = Control.GrowDirection.Both;
		title.GrowVertical   = Control.GrowDirection.Both;
		title.MouseFilter    = Control.MouseFilterEnum.Ignore;
		canvas.AddChild(title);

		// Subtitle
		var sub              = new Label();
		sub.Text             = "The boss has been slain — your legend is sealed.";
		sub.AddThemeFontSizeOverride("font_size", 22);
		sub.AddThemeColorOverride("font_color", new Color(0.75f, 0.65f, 1f));
		sub.HorizontalAlignment = HorizontalAlignment.Center;
		sub.AnchorLeft     = 0f;   sub.AnchorTop    = 0.5f;
		sub.AnchorRight    = 1f;   sub.AnchorBottom = 0.5f;
		sub.OffsetTop      = -20f; sub.OffsetBottom = 20f;
		sub.GrowHorizontal = Control.GrowDirection.Both;
		sub.GrowVertical   = Control.GrowDirection.Both;
		sub.MouseFilter    = Control.MouseFilterEnum.Ignore;
		canvas.AddChild(sub);

		// "Return to Lobby" button
		var btn              = new Button();
		btn.Text             = "Return to Lobby";
		btn.AddThemeFontSizeOverride("font_size", 20);
		btn.AnchorLeft   = 0.5f;  btn.AnchorTop    = 0.5f;
		btn.AnchorRight  = 0.5f;  btn.AnchorBottom = 0.5f;
		btn.OffsetLeft   = -130f; btn.OffsetRight  = 130f;
		btn.OffsetTop    =  60f;  btn.OffsetBottom = 100f;
		btn.GrowHorizontal = Control.GrowDirection.Both;
		btn.GrowVertical   = Control.GrowDirection.Both;
		canvas.AddChild(btn);

		btn.Pressed += () =>
		{
			GenericCore.Instance.BossHasSpawned = false;
			GenericCore.Instance.rewind         = false;
			GenericCore.Instance.DisconnectFromGame();
			_ = SceneTransition.Instance.TransitionTo(
				"res://NetworkCore/WanLobbySystem/BetterLobby/streamlinedLobby.tscn");
		};
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
