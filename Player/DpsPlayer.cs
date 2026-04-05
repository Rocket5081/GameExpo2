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

            //sets max amount of created bullets, before teleporting old bullets back.
            if(Buls.Count < 9)
            {
                
				SpawnBullet();
            }
            //shoots old bullets instead of creating new ones
            else
            {
                ShootBullet();
            }
            
        }
   }

   //This function can definetly be optimized, I just can't do that at 1am
	public async void SpawnBullet()
	{
		Vector3 spawnPos = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
        //creates a new bullet, shoots it, and adds it to the Buls array in Player.cs, then waits .1 seconds to do it 2 more times
		var t1 = GenericCore.Instance.MainNetworkCore.NetCreateObject(
				1,
				spawnPos,
				Transform.Basis.GetRotationQuaternion(),
				1
			);
            ((RigidBody3D)t1).LinearVelocity =Transform.Basis.X * 20f;
			Buls.Add((Bullet)t1);
			await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
			var t2 = GenericCore.Instance.MainNetworkCore.NetCreateObject(
				1,
				spawnPos,
				Transform.Basis.GetRotationQuaternion(),
				1
			);
            ((RigidBody3D)t2).LinearVelocity =Transform.Basis.X * 20f;
			Buls.Add((Bullet)t2);
			await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
			var t3 = GenericCore.Instance.MainNetworkCore.NetCreateObject(
				1,
				spawnPos,
				Transform.Basis.GetRotationQuaternion(),
				1
			);
            ((RigidBody3D)t3).LinearVelocity =Transform.Basis.X * 20f;
			Buls.Add((Bullet)t3);

	}

	//This function can definetly be optimized, I just can't do that at 1am
	public async void ShootBullet()
	{
        //shoots old bullets
        Buls[bulCount].Show();
        Buls[bulCount].CollisionLayer = 1;
		Buls[bulCount].CollisionMask = 1;
        Buls[bulCount].GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
        Buls[bulCount].LinearVelocity = Transform.Basis.X * 20f;
		bulCount++;
		if(bulCount >= Buls.Count){ bulCount = 0; }
		await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
        Buls[bulCount].Show();
        Buls[bulCount].CollisionLayer = 1;
		Buls[bulCount].CollisionMask = 1;
        Buls[bulCount].GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
        Buls[bulCount].LinearVelocity = Transform.Basis.X * 20f;
		bulCount++;
		if(bulCount >= Buls.Count){ bulCount = 0; }
		await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
		Buls[bulCount].Show();
        Buls[bulCount].CollisionLayer = 1;
		Buls[bulCount].CollisionMask = 1;
        Buls[bulCount].GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
        Buls[bulCount].LinearVelocity = Transform.Basis.X * 20f;
		bulCount++;
		if(bulCount >= Buls.Count){ bulCount = 0; }
	}
}
