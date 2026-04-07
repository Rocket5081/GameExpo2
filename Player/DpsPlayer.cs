using Godot;

public partial class DpsPlayer : Player
{
	private int   burstCount = 3;
	private float burstDelay = 0.1f;

	public override void _Ready()
	{
		maxHp = 150;
		speed = 20;
		hp    = maxHp;
		base._Ready();
	}

	// No _Process override — base Player._Process handles input and calls Fire on click

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void Fire()
	{
		if (!GenericCore.Instance.IsServer) return;
		if (!canShoot) return;

		canShoot = false;
		timer    = 0.5f;

		if (Buls.Count < 9)
			SpawnBullet(burstCount, burstDelay);
		else
			ShootBullet(burstCount, burstDelay);
	}
}
