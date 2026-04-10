using Godot;

public partial class SupportPlayer : Player
{
	[Export] public MeshInstance3D      LaserBeam;
	[Export] public AudioStreamPlayer3D ShootSoundPlayer;

	// ── Ultimate audio: drag your .mp3/.wav into this slot in the Inspector ──
	[Export] public AudioStream UltimateSfx;
	private AudioStreamPlayer3D _ultPlayer;

	private const float LaserRange = 40f;
	private bool _wasPressingFire = false;

	public override void _Ready()
	{
		maxHp = 100;
		speed = 20;
		hp    = maxHp;
		GetNode("Upgrades").GetNode<Options>("Options").add();
		base._Ready();

		// Ultimate audio player
		_ultPlayer = new AudioStreamPlayer3D();
		AddChild(_ultPlayer);

		// Build the laser beam in code so it works even if the TSCN export is null
		if (LaserBeam == null)
		{
			var mat = new StandardMaterial3D();
			mat.EmissionEnabled          = true;
			mat.Emission                 = new Color(0.2f, 1f, 0.2f, 1f);
			mat.EmissionEnergyMultiplier = 6f;
			mat.AlbedoColor              = new Color(0.2f, 1f, 0.2f, 1f);

			var mesh = new BoxMesh();
			mesh.Size     = new Vector3(40f, 0.3f, 0.3f);
			mesh.Material = mat;

			LaserBeam            = new MeshInstance3D();
			LaserBeam.Mesh       = mesh;
			LaserBeam.Position   = new Vector3(21.5f, 1f, 0f);
			LaserBeam.Visible    = false;
			LaserBeam.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
			AddChild(LaserBeam);
		}
	}

	// Plays the ult activation sound locally the instant Q is pressed
	protected override void OnLocalUltimateActivated()
	{
		if (UltimateSfx != null)
		{
			_ultPlayer.Stream = UltimateSfx;
			_ultPlayer.Play();
		}
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		if (myId == null || !myId.IsNetworkReady) return;

		if (myId.IsLocal)
		{
			bool pressing = Input.IsActionPressed("primary");

			// ── Local view: set directly every frame ──────────────────────────
			if (LaserBeam != null)
				LaserBeam.Visible = pressing;

			// ── Sound ─────────────────────────────────────────────────────────
			if (pressing && ShootSoundPlayer != null && !ShootSoundPlayer.Playing)
				ShootSoundPlayer.Play();
			else if (!pressing)
				ShootSoundPlayer?.Stop();

			// ── Tell other peers about the state change ────────────────────────
			if (pressing != _wasPressingFire)
				Rpc("SetLaserActive", pressing);

			_wasPressingFire = pressing;
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void SetLaserActive(bool active)
	{
		if (LaserBeam != null)
			LaserBeam.Visible = active;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void Fire()
	{
		if (!GenericCore.Instance.IsServer) return;
		if (!canShoot) return;

		canShoot = false;
		timer    = 0.15f;

		DoLaserRaycast();
	}

	private void DoLaserRaycast()
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		Vector3 origin    = GetBulletSpawnPos();
		Vector3 direction = Transform.Basis.X.Normalized();
		Vector3 end       = origin + direction * LaserRange;

		var query = PhysicsRayQueryParameters3D.Create(origin, end, 0b1001);
		query.Exclude.Add(GetRid());

		var result = spaceState.IntersectRay(query);
		if (result.Count == 0) return;

		if (result["collider"].As<Node>() is Node hit && hit.IsInGroup("enemy"))
			hit.Call("OnHitByBullet");
	}

	// ── Support Ultimate: Healing Circle ──────────────────────────────────────
	/// <summary>
	/// Drops a large healing zone at the Support's feet.
	/// Radius 9 — very roomy. Heals 10 HP every 0.5 s for 10 s.
	/// Green particles rise from the glowing disc.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void UseUltimate()
	{
		if (!GenericCore.Instance.IsServer) return;

		const float PoolRadius = 9f;
		Vector3 spawnPos = GlobalPosition;

		// ── Server-side logic zone ─────────────────────────────────────────────
		var zone = new Area3D();
		zone.CollisionLayer = 0;
		zone.CollisionMask  = 2;   // layer 2 = players

		var shape = new CollisionShape3D();
		shape.Shape = new CylinderShape3D { Radius = PoolRadius, Height = 3f };
		zone.AddChild(shape);

		GetParent().AddChild(zone);
		zone.GlobalPosition = spawnPos;

		const float healInterval = 0.5f;
		const float duration     = 10f;
		float elapsed = 0f;

		var healTimer = new Timer();
		healTimer.WaitTime  = healInterval;
		healTimer.Autostart = true;
		zone.AddChild(healTimer);

		healTimer.Timeout += () =>
		{
			elapsed += healInterval;
			foreach (var body in zone.GetOverlappingBodies())
			{
				if (body is Player p)
					p.hp = Mathf.Min(p.hp + 10, p.maxHp);
			}
			if (elapsed >= duration)
			{
				healTimer.Stop();
				zone.QueueFree();
			}
		};

		// ── Show visual on every client ───────────────────────────────────────
		Rpc("ShowHealingCircleVisual", spawnPos, PoolRadius, duration);
	}

	/// <summary>
	/// Runs on every peer (CallLocal=true so the server also sees it).
	/// Creates a glowing green disc with dense rising particles.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ShowHealingCircleVisual(Vector3 pos, float radius, float duration)
	{
		var root = new Node3D();
		GetParent().AddChild(root);
		root.GlobalPosition = pos;

		// ── Glowing disc ──────────────────────────────────────────────────────
		var disc    = new MeshInstance3D();
		var cylMesh = new CylinderMesh();
		cylMesh.TopRadius      = radius;
		cylMesh.BottomRadius   = radius;
		cylMesh.Height         = 0.12f;
		cylMesh.RadialSegments = 48;

		var discMat = new StandardMaterial3D();
		discMat.Transparency             = BaseMaterial3D.TransparencyEnum.Alpha;
		discMat.AlbedoColor              = new Color(0.15f, 1f, 0.3f, 0.5f);
		discMat.EmissionEnabled          = true;
		discMat.Emission                 = new Color(0.15f, 1f, 0.3f);
		discMat.EmissionEnergyMultiplier = 4f;
		discMat.CullMode                 = BaseMaterial3D.CullModeEnum.Disabled;
		cylMesh.Material = discMat;

		disc.Mesh       = cylMesh;
		disc.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		root.AddChild(disc);

		// ── Rising green particles ─────────────────────────────────────────────
		var particles = new GpuParticles3D();
		particles.Amount           = 80;    // dense cloud
		particles.Lifetime         = 2.5f;
		particles.Emitting         = true;

		var pMat = new ParticleProcessMaterial();
		pMat.EmissionShape        = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
		pMat.EmissionSphereRadius = radius - 0.5f;   // fill the whole disc
		pMat.Direction            = new Vector3(0f, 1f, 0f);
		pMat.Spread               = 10f;              // mostly straight up
		pMat.InitialVelocityMin   = 2f;
		pMat.InitialVelocityMax   = 5f;
		pMat.Gravity              = new Vector3(0f, -0.3f, 0f);   // gentle float
		pMat.ScaleMin             = 0.1f;
		pMat.ScaleMax             = 0.25f;
		pMat.Color                = new Color(0.2f, 1f, 0.35f, 1f);

		particles.ProcessMaterial = pMat;
		particles.Position        = new Vector3(0f, 0.1f, 0f);
		root.AddChild(particles);

		GetTree().CreateTimer(duration).Timeout += () => root.QueueFree();
	}
}
