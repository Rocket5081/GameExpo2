using Godot;
using System;
using System.Linq;
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

<<<<<<< Updated upstream
    public override void _Process(double delta)
    {
        RpcId(1, "Fire");
    }


    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public override void Fire()
    {
        if (GenericCore.Instance.IsServer)
        {
            canShoot = false;
            timer = 0.5f;

            if(Buls.Count <= 0)
            {
                Vector3 spawnPos = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
=======
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void Fire()
	{
		if (GenericCore.Instance.IsServer)
		{
			canShoot = false;
			timer = 0.5f;

			Vector3 spawnPos = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
>>>>>>> Stashed changes

			var t = GenericCore.Instance.MainNetworkCore.NetCreateObject(
				1,
				spawnPos,
				Transform.Basis.GetRotationQuaternion(),
				1
			);

<<<<<<< Updated upstream
            ((RigidBody3D)t).LinearVelocity =Transform.Basis.X * 20f;
            Buls.Add((Bullet)t);
            }
            else
            {
                for(int i=0; i<Buls.Count; i++)
                {
                    GD.Print(Buls.Count);
                    Buls[i].Show();
                    this.CollisionLayer = 1;
		            this.CollisionMask = 1;
                    Buls[i].GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
                    Buls[i].LinearVelocity = Transform.Basis.X * 20f;
                }
            }
            
        }
   }
=======
			((RigidBody3D)t).LinearVelocity = Transform.Basis.X * 40f;
		}
	}
>>>>>>> Stashed changes
}
