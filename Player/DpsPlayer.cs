using Godot;
using System.Collections.Generic;

public partial class DpsPlayer : Player
{
	[Export] public PackedScene projectileScene;

	private int burstCount = 3;
	private float burstDelay = 0.1f;

	public override void _Ready()
	{
		maxHp = 150;
		speed  = 20;
		hp     = maxHp;
		base._Ready();
	}

	public override void _Process(double delta)
	{
		// Only the local player should request to fire
		if (myId.IsLocal && canShoot)
		{
			RpcId(1, MethodName.Fire);
		}
	}


	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void Fire()
	{
		if (GenericCore.Instance.IsServer)
		{
			canShoot = false;
			timer = 0.5f;

			//sets max amount of created bullets, before teleporting old bullets back.
			if(Buls.Count < 9)
			{
				
				SpawnBullet(burstCount, burstDelay);
			}
			//shoots old bullets instead of creating new ones
			else
			{
				ShootBullet(burstCount, burstDelay);
			}
			
		}
   }
}
