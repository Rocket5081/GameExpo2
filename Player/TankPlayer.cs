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
         //sets max amount of created bullets, before teleporting old bullets back.
            if(Buls.Count < 12)
            {
				SpawnBullet(3,0);
            }
            //shoots old bullets instead of creating new ones
            else
            {
                ShootBullet(3,0);
            }
            
    }

    public new async void SpawnBullet(int count, float cooldown)
	{
		Vector3 spawnPos = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
        //creates a new bullet, shoots it, and adds it to the Buls array in Player.cs, then waits .1 seconds to do it 2 more times
        for(int i=0; i<count; i++)
        {
            var t1 = GenericCore.Instance.MainNetworkCore.NetCreateObject(
				1,
				spawnPos,
				Transform.Basis.GetRotationQuaternion(),
				1
			);
			if (i == 0)
			{
				((RigidBody3D)t1).GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, -.5f);
				((RigidBody3D)t1).RotateY(.7853982f);
				((RigidBody3D)t1).LinearVelocity = ((RigidBody3D)t1).Transform.Basis.X * 10f;
			}
            else if (i == 1)
			{
				((RigidBody3D)t1).Rotation = Rotation;
				((RigidBody3D)t1).GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
				((RigidBody3D)t1).LinearVelocity = Transform.Basis.X * 10f;
			}
			else if(i == 2)
			{
				((RigidBody3D)t1).GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, .5f);
				((RigidBody3D)t1).RotateY(-.7853982f);
				((RigidBody3D)t1).LinearVelocity = ((RigidBody3D)t1).Transform.Basis.X * 10f;
			}
            
			Buls.Add((Bullet)t1);
			await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
        }
	}

	public new async void ShootBullet(int count, float cooldown)
	{
        //shoots old bullets
        for(int i=0; i<count; i++)
        {
        Buls[bulCount].Show();
        Buls[bulCount].CollisionLayer = 1;
		Buls[bulCount].CollisionMask = 1;
        
        if (i == 0)
			{
				Buls[bulCount].Rotation = Rotation;
				Buls[bulCount].GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, -.5f);
				Buls[bulCount].RotateY(.7853982f);
				Buls[bulCount].LinearVelocity = Buls[bulCount].Transform.Basis.X * 10f;
			}
            else if (i == 1)
			{
				Buls[bulCount].Rotation = Rotation;
				Buls[bulCount].GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
				Buls[bulCount].LinearVelocity = Transform.Basis.X * 10f;
			}
			else if(i == 2)
			{
				Buls[bulCount].Rotation = Rotation;
				Buls[bulCount].GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, .5f);
				Buls[bulCount].RotateY(-.7853982f);
				Buls[bulCount].LinearVelocity = Buls[bulCount].Transform.Basis.X * 10f;
			}
		bulCount++;
		if(bulCount >= Buls.Count){ bulCount = 0; }
		await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
        }
	}
}