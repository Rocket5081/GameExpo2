using Godot;

public partial class TankPlayer : Player
{
	[Export] public AudioStreamPlayer3D ShootSoundPlayer;

	// ── Ultimate audio: drag your .mp3/.wav into this slot in the Inspector ──
	[Export] public AudioStream UltimateSfx;
	private AudioStreamPlayer3D _ultPlayer;

	// 6 evenly spaced angles across a ~60-degree arc (-0.5 to +0.5 radians)
	private static readonly float[] SpreadAngles = { 0.5f, 0.3f, 0.1f, -0.1f, -0.3f, -0.5f };

	public override void _Ready()
	{
		maxHp = 200;
		speed = 10;
		hp    = maxHp;
		base._Ready();

		// Audio player for the ultimate activation sound
		_ultPlayer = new AudioStreamPlayer3D();
		AddChild(_ultPlayer);
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

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void Fire()
	{
		if (!GenericCore.Instance.IsServer) return;
		if (!canShoot) return;

		canShoot = false;
		timer    = 0.5f;

		ShootSoundPlayer?.Play();

		int count = SpreadAngles.Length;   // 6

		if (Buls.Count < count * 4)
			SpawnBulletSpread();
		else
			ShootBulletSpread();
	}

	// ── Tank Ultimate: Bubble Shield ──────────────────────────────────────────
	/// <summary>
	/// Spawns a large stationary protective bubble at the Tank's position.
	/// Radius 8 — fits ~4 players comfortably.
	/// Any player inside takes no damage for 8 seconds.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void UseUltimate()
	{
		if (!GenericCore.Instance.IsServer) return;

		// Centre the bubble at waist height so the bottom sits at ground level
		const float BubbleRadius = 8f;
		Vector3 spawnPos = GlobalPosition + new Vector3(0, BubbleRadius * 0.5f, 0);

		// ── Server-side collision zone ─────────────────────────────────────────
		var bubble = new Area3D();
		bubble.CollisionLayer = 0;
		bubble.CollisionMask  = 2;   // layer 2 = players

		var shape = new CollisionShape3D();
		shape.Shape = new SphereShape3D { Radius = BubbleRadius };
		bubble.AddChild(shape);

		bubble.BodyEntered += (body) => { if (body is Player p) p.IsShielded = true; };
		bubble.BodyExited  += (body) => { if (body is Player p) p.IsShielded = false; };

		GetParent().AddChild(bubble);
		bubble.GlobalPosition = spawnPos;

		// Auto-remove after 8 s, clearing shields first
		GetTree().CreateTimer(8f).Timeout += () =>
		{
			foreach (var body in bubble.GetOverlappingBodies())
				if (body is Player p) p.IsShielded = false;
			bubble.QueueFree();
		};

		// ── Show visual on every client ───────────────────────────────────────
		Rpc("ShowBubbleVisual", spawnPos, BubbleRadius, 8f);
	}

	/// <summary>
	/// Runs on every peer (CallLocal=true so the server also sees it).
	/// Creates a large translucent blue sphere mesh.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ShowBubbleVisual(Vector3 pos, float radius, float duration)
	{
		var meshInst  = new MeshInstance3D();
		var sphereMesh = new SphereMesh();
		sphereMesh.Radius         = radius;
		sphereMesh.Height         = radius * 2f;
		sphereMesh.RadialSegments = 48;
		sphereMesh.Rings          = 24;

		var mat = new StandardMaterial3D();
		mat.Transparency             = BaseMaterial3D.TransparencyEnum.Alpha;
		mat.AlbedoColor              = new Color(0.4f, 0.7f, 1f, 0.18f);
		mat.EmissionEnabled          = true;
		mat.Emission                 = new Color(0.4f, 0.7f, 1f);
		mat.EmissionEnergyMultiplier = 2.5f;
		mat.CullMode                 = BaseMaterial3D.CullModeEnum.Disabled;

		sphereMesh.Material = mat;
		meshInst.Mesh       = sphereMesh;
		meshInst.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

		GetParent().AddChild(meshInst);
		meshInst.GlobalPosition = pos;

		GetTree().CreateTimer(duration).Timeout += () => meshInst.QueueFree();
	}

	// ── Spread bullets ────────────────────────────────────────────────────────

	private async void SpawnBulletSpread()
	{
		for (int i = 0; i < SpreadAngles.Length; i++)
		{
			Vector3 spawnPos = GetBulletSpawnPos();
			var obj = GenericCore.Instance.MainNetworkCore?.NetCreateObject(
				3, spawnPos, Transform.Basis.GetRotationQuaternion(), 1);

			if (obj == null) return;

			if (obj is RigidBody3D rb)
			{
				rb.RotateY(SpreadAngles[i]);
				rb.CollisionLayer = 4;
				rb.CollisionMask  = 1;
				rb.LinearVelocity = rb.Transform.Basis.X * 200f;
			}

			if (obj is Bullet b)
				Buls.Add(b);

			await ToSignal(GetTree().CreateTimer(0f), SceneTreeTimer.SignalName.Timeout);
		}
	}

	private async void ShootBulletSpread()
	{
		for (int i = 0; i < SpreadAngles.Length; i++)
		{
			Vector3 spawnPos = GetBulletSpawnPos();

			Buls[bulCount].Show();
			Buls[bulCount].CollisionLayer = 4;
			Buls[bulCount].CollisionMask  = 1;
			Buls[bulCount].Rotation       = Rotation;
			Buls[bulCount].GlobalPosition = spawnPos;
			Buls[bulCount].RotateY(SpreadAngles[i]);
			Buls[bulCount].LinearVelocity = Buls[bulCount].Transform.Basis.X * 200f;

			bulCount++;
			if (bulCount >= Buls.Count)
				bulCount = 0;

			await ToSignal(GetTree().CreateTimer(0f), SceneTreeTimer.SignalName.Timeout);
		}
	}
}
