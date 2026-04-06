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
            Vector3 spin = spawnPos.Rotated(spawnPos,(float)Math.PI);
            ((RigidBody3D)t1).LinearVelocity = spin * 150f;
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
        Buls[bulCount].GlobalPosition = GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
        Buls[bulCount].LinearVelocity = Transform.Basis.X * 150f;
		bulCount++;
		if(bulCount >= Buls.Count){ bulCount = 0; }
		await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
        }
	}
}