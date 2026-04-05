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
        if (GenericCore.Instance.IsServer)
        {

            //sets max amount of created bullets, before teleporting old bullets back.
            if(Buls.Count < 100)
            {
				SpawnBullet(1,0);
            }
            //shoots old bullets instead of creating new ones
            else
            {
                ShootBullet(1,0);
            }
            
        }
    }
}