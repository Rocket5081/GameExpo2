using Godot;

public partial class SupportPlayer : Player
{
	private int   spread = 1;
	private int   burstCount = 1;
	private float burstDelay = 0.05f;
	public override void _Ready()
	{
		maxHp = 100;
		speed = 20;
		hp    = maxHp;
		GetNode("Upgrades").GetNode<Options>("Options").add();
		base._Ready();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void Fire()
	{
		if (!GenericCore.Instance.IsServer) return;
		if (!canShoot) return;

		canShoot = false;
		timer    = 0.05f;

		if (Buls.Count < 50*spread)
			SpawnBulletSpread(spread, burstCount, burstDelay);
		else
			ShootBulletSpread(spread, burstCount, burstDelay);
	}

	private async void SpawnBulletSpread(int spread, int count, float cooldown)
	{
		float[] zOffsets = {0f, -0.5f, 0.5f };
		float[] yAngles  = {0f,  0.785f, -0.785f };

		for (int j =0; j < spread; j++){
		for (int i = 0; i < count; i++)
		{
			Vector3 spawnPos = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, zOffsets[j]);
			var t1 = GenericCore.Instance.MainNetworkCore?.NetCreateObject(
				3, spawnPos, Transform.Basis.GetRotationQuaternion(), 1);

			if (t1 == null) return;

			if (t1 is RigidBody3D rb)
			{
				rb.RotateY(yAngles[j]);
				rb.LinearVelocity = rb.Transform.Basis.X * 300f;
			}

			if (t1 is Bullet b)
				Buls.Add(b);

			await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
		}
		await ToSignal(GetTree().CreateTimer(cooldown*count), SceneTreeTimer.SignalName.Timeout);
		
		}
	}

	private async void ShootBulletSpread(int spread, int count, float cooldown)
	{
		float[] zOffsets = {0f, -0.5f, 0.5f };
		float[] yAngles  = {0f,  0.785f, -0.785f };

		for (int j =0; j < spread; j++){
		for (int i = 0; i < count; i++)
		{
			Buls[bulCount].Show();
			Buls[bulCount].CollisionLayer = 1;
			Buls[bulCount].CollisionMask  = 1;
			Buls[bulCount].Rotation       = Rotation;
			Buls[bulCount].GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, zOffsets[j]);
			Buls[bulCount].RotateY(yAngles[j]);
			Buls[bulCount].LinearVelocity = Buls[bulCount].Transform.Basis.X * 300f;
			bulCount++;
			if (bulCount >= Buls.Count) bulCount = 0;
			await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
		}
		await ToSignal(GetTree().CreateTimer(cooldown*count), SceneTreeTimer.SignalName.Timeout);
		}
	}
}
