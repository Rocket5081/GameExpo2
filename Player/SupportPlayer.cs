using Godot;
using System;
using System.Net.Http;

public partial class SupportPlayer : Player
{
    
    [Export] public PackedScene projectileScene;
    public override void _Ready()
    {
        maxHp = 100;
        speed = 20;
        hp = maxHp;
        base._Ready();
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public override void Fire()
    {
        if (GenericCore.Instance.IsServer && canShoot)
        {
            canShoot = false;
			timer = .15f;

            if(Buls.Count < 15)
            {
                
				SpawnBullet(3,0.05f);
            
            }
            else
            {
                ShootBullet(3,0.05f);
            }
            
        }
    }
}