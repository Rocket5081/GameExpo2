using Godot;
using System;
using System.Net.Http;

public partial class DpsPlayer : Player
{

    [Export] public PackedScene projectileScene;

    private int burstCount = 3;
    private float burstDelay = 0.1f;

    public override void _Ready()
    {
        maxHp = 150;
        speed = 20;
        hp = maxHp;
        base._Ready();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public override void Fire()
    {
        if (GenericCore.Instance.IsServer)
        {
            canShoot = false;
            timer = 0.5f;

            Vector3 spawnPos = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);

            var t = GenericCore.Instance.MainNetworkCore.NetCreateObject(
                1,
                spawnPos,
                Transform.Basis.GetRotationQuaternion(),
                1
            );

            ((RigidBody3D)t).LinearVelocity = Transform.Basis.X * 40f;
        }
    }
}
