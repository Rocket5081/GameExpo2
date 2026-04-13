using Godot;

public partial class TankPlayer : Player
{
	[Export] public AudioStreamPlayer3D ShootSoundPlayer;

	// ── Ultimate audio ────────────────────────────────────────────────────────
	// Add an AudioStreamPlayer3D node to this player in the scene, then drag it
	// here in the Inspector. Set its Stream, Volume Db, and Pitch Scale freely.
	// Leave empty and a silent placeholder is created automatically.
	[Export] public AudioStreamPlayer3D UltimateSound;

	// 6 evenly spaced angles across a ~60-degree arc (-0.5 to +0.5 radians)
	private static readonly float[] SpreadAngles = { 0.5f, 0.3f, 0.1f, -0.1f, -0.3f, -0.5f };

	public override void _Ready()
	{
		maxHp = 200;
		speed = 10;
		hp    = maxHp / 2;   // DEBUG: halved for relic testing
		base._Ready();

		if (UltimateSound == null)
		{
			UltimateSound = new AudioStreamPlayer3D();
			AddChild(UltimateSound);
		}
	}

	protected override void OnLocalUltimateActivated()
	{
		UltimateSound?.Play();
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

		int count = SpreadAngles.Length;

		if (Buls.Count < count * 4)
			SpawnBulletSpread();
		else
			ShootBulletSpread();
	}

	// ── Tank Ultimate: Bubble Shield ──────────────────────────────────────────
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void UseUltimate()
	{
		if (!GenericCore.Instance.IsServer) return;

		const float BubbleRadius = 18f;
		Vector3 spawnPos = GlobalPosition + new Vector3(0, BubbleRadius * 0.5f, 0);

		var bubble = new Area3D();
		bubble.CollisionLayer = 0;
		bubble.CollisionMask  = 2;

		var shape = new CollisionShape3D();
		shape.Shape = new SphereShape3D { Radius = BubbleRadius };
		bubble.AddChild(shape);

		bubble.BodyEntered += (body) => { if (body is Player p) p.IsShielded = true; };
		bubble.BodyExited  += (body) => { if (body is Player p) p.IsShielded = false; };

		GetParent().AddChild(bubble);
		bubble.GlobalPosition = spawnPos;

		GetTree().CreateTimer(8f).Timeout += () =>
		{
			foreach (var body in bubble.GetOverlappingBodies())
				if (body is Player p) p.IsShielded = false;
			bubble.QueueFree();
		};

		Rpc("ShowBubbleVisual", spawnPos, BubbleRadius, 8f);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ShowBubbleVisual(Vector3 pos, float radius, float duration)
	{
		var meshInst   = new MeshInstance3D();
		var sphereMesh = new SphereMesh();
		sphereMesh.Radius         = radius;
		sphereMesh.Height         = radius * 2f;
		sphereMesh.RadialSegments = 48;
		sphereMesh.Rings          = 24;

		var mat = new StandardMaterial3D();
		mat.Transparency             = BaseMaterial3D.TransparencyEnum.Alpha;
		mat.AlbedoColor              = new Color(0.4f, 0.7f, 1f, 0.07f);
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
