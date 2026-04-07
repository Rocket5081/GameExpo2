using Godot;

public partial class SupportPlayer : Player
{
	public override void _Ready()
	{
		maxHp = 100;
		speed = 20;
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
		timer    = 0.15f;

		if (Buls.Count < 15)
			SpawnBullet(3, 0.05f);
		else
			ShootBullet(3, 0.05f);
	}
}
