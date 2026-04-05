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

<<<<<<< Updated upstream

    public override void _Process(double delta)
    {
        RpcId(1, "Fire");
    }
=======
	public override void _Process(double delta)
	{
		base._Process(delta);
>>>>>>> Stashed changes

		// Only the local player should request to fire
		if (myId.IsLocal && canShoot)
		{
			RpcId(1, MethodName.Fire);
		}
	}

<<<<<<< Updated upstream
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
=======
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void Fire()
	{
		if (!GenericCore.Instance.IsServer) return;

		canShoot = false;
		timer = 0.5f;

		if (Buls.Count <= 0)
		{
			// No pooled bullet exists yet — create a new one
			Vector3 spawnPos = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);

			var t = GenericCore.Instance.MainNetworkCore.NetCreateObject(
>>>>>>> Stashed changes
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

<<<<<<< Updated upstream
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
=======
			((RigidBody3D)t).LinearVelocity = Transform.Basis.X * 40f;
			Buls.Add((Bullet)t);
		}
		else
		{
			// Reuse pooled bullets
			for (int i = 0; i < Buls.Count; i++)
			{
				Buls[i].Show();
				this.CollisionLayer = 1;
				this.CollisionMask  = 1;
				Buls[i].GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
				Buls[i].LinearVelocity = Transform.Basis.X * 40f;
			}
		}
	}
>>>>>>> Stashed changes
}
