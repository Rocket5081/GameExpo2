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
        
    }
}