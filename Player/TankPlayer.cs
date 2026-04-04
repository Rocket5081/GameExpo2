using Godot;
using System;
using System.Net.Http;

public partial class TankPlayer : Player
{
    [Export] public PackedScene projectileScene;

    public int projectileCount = 5;

    public float spread = 0.3f;
    
    public override void _Ready()
    {
        maxHp = 200;
        speed = 10;
        hp = maxHp;
        base._Ready();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public override void Fire()
    {
        
    }
}