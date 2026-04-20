using Godot;

public partial class TankPlayer : Player
{
	[Export] public AudioStreamPlayer3D ShootSoundPlayer;

	// ── Ultimate audio ────────────────────────────────────────────────────────

	[Export] public AudioStreamPlayer3D UltimateSound;

	[Export] public GpuParticles3D gunFlash;

	// Spread angles are generated at fire-time from burstCount so AP upgrades
	// automatically widen the shotgun blast.  Base burstCount=3 → 6 pellets.
	private float[] GetSpreadAngles()
	{
		int   count     = burstCount * 2;           // 6 base, +2 per AP level
		float maxSpread = 0.5f;
		var   angles    = new float[count];
		if (count == 1) { angles[0] = 0f; return angles; }
		for (int i = 0; i < count; i++)
			angles[i] = Mathf.Lerp(maxSpread, -maxSpread, (float)i / (count - 1));
		return angles;
	}

	public override void _Ready()
	{
		maxHp = 200;
		speed = 30;
		hp    = maxHp ; 
		damage = 10f;
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
		if (!GenericCore.Instance.IsServer)
			myAnimation?.Play("SpAttack");
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
		gunFlash?.Restart();

		float[] angles = GetSpreadAngles();
		if (Buls.Count < 20)
			SpawnBulletSpread(angles);
		else
			ShootBulletSpread(angles);
	}

	// ── Tank Ultimate: Bubble Shield ──────────────────────────────────────────
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void UseUltimate()
	{
		if (!GenericCore.Instance.IsServer){
			myAnimation?.Play("SpecialAttackI");
			return;}

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

	private async void SpawnBulletSpread(float[] angles)
	{
		for (int i = 0; i < angles.Length; i++)
		{
			Vector3 spawnPos = GetBulletSpawnPos();
			var obj = GenericCore.Instance.MainNetworkCore?.NetCreateObject(
				3, spawnPos, Transform.Basis.GetRotationQuaternion(), 1);

			if (obj == null) return;

			if (obj is RigidBody3D rb)
			{
				rb.RotateY(angles[i]);
				rb.CollisionLayer = 4;
				rb.CollisionMask  = 1;
				rb.LinearVelocity = rb.Transform.Basis.X * 200f;
			}

			if (obj is Bullet b)
			{
				b.damage  = damage;
				b.Shooter = this;
				Buls.Add(b);
			}

			await ToSignal(GetTree().CreateTimer(0f), SceneTreeTimer.SignalName.Timeout);
		}
	}

	private async void ShootBulletSpread(float[] angles)
	{
		// Purge any bullet references that were freed unexpectedly.
		Buls.RemoveAll(b => !IsInstanceValid(b));

		// If the pool is smaller than this shot's pellet count, grow it.
		if (Buls.Count < angles.Length)
		{
			SpawnBulletSpread(angles);
			return;
		}

		if (bulCount >= Buls.Count)
			bulCount = 0;

		for (int i = 0; i < angles.Length; i++)
		{
			if (!IsInstanceValid(Buls[bulCount]))
			{
				Buls.RemoveAt(bulCount);
				if (bulCount >= Buls.Count) bulCount = 0;
				SpawnBulletSpread(angles);   // pool too small, grow it
				return;
			}

			Vector3 spawnPos = GetBulletSpawnPos();
			var bul = Buls[bulCount];

			bul.Reset();
			bul.damage  = damage;
			bul.Shooter = this;
			bul.Show();
			bul.CollisionLayer = 4;
			bul.CollisionMask  = 1;
			bul.Rotation       = Rotation;
			bul.GlobalPosition = spawnPos;
			bul.RotateY(angles[i]);
			bul.LinearVelocity = bul.Transform.Basis.X * 200f;

			bulCount++;
			if (bulCount >= Buls.Count)
				bulCount = 0;

			await ToSignal(GetTree().CreateTimer(0f), SceneTreeTimer.SignalName.Timeout);
		}
	}
}
