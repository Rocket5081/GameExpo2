using Godot;

public partial class TankPlayer : Player
{
	private int  spreadCount = 3;
	public override void _Ready()
	{
		maxHp = 200;
		speed = 10;
		hp    = maxHp;
		base._Ready();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void Fire()
	{
		if (!GenericCore.Instance.IsServer) return;
		if (!canShoot) return;

		canShoot = false;
		timer    = 0.5f;

		if (Buls.Count < 12)
			SpawnBulletSpread(spreadCount);
		else
			ShootBulletSpread(spreadCount);
	}

	private async void SpawnBulletSpread(int count)
	{
		float[] zOffsets = { -0.5f, 0f, 0.5f };
		float[] yAngles  = {  0.785f, 0f, -0.785f };

		for (int i = 0; i < count; i++)
		{
			Vector3 spawnPos = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, zOffsets[i]);
			var t1 = GenericCore.Instance.MainNetworkCore?.NetCreateObject(
				1, spawnPos, Transform.Basis.GetRotationQuaternion(), 1);

			if (t1 == null) return;

			if (t1 is RigidBody3D rb)
			{
				rb.RotateY(yAngles[i]);
				rb.LinearVelocity = rb.Transform.Basis.X * 10f;
			}

			if (t1 is Bullet b)
				Buls.Add(b);

			await ToSignal(GetTree().CreateTimer(0f), SceneTreeTimer.SignalName.Timeout);
		}
	}

	private async void ShootBulletSpread(int count)
	{
		float[] zOffsets = { -0.5f, 0f, 0.5f };
		float[] yAngles  = {  0.785f, 0f, -0.785f };

		for (int i = 0; i < count; i++)
		{
			Buls[bulCount].Show();
			Buls[bulCount].CollisionLayer = 1;
			Buls[bulCount].CollisionMask  = 1;
			Buls[bulCount].Rotation       = Rotation;
			Buls[bulCount].GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, zOffsets[i]);
			Buls[bulCount].RotateY(yAngles[i]);
			Buls[bulCount].LinearVelocity = Buls[bulCount].Transform.Basis.X * 10f;
			bulCount++;
			if (bulCount >= Buls.Count) bulCount = 0;
			await ToSignal(GetTree().CreateTimer(0f), SceneTreeTimer.SignalName.Timeout);
		}
	}
}
