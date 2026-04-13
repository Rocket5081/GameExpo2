using Godot;
using System;

public partial class WormEnemy : Enemy
{
    private Player player;

    [Export] public Vector3 SyncedVelocity
    {
        get => Velocity;
        set => Velocity = value;
    }
    [Export] public bool SyncedIsMoving = false;

    public int speed = 5;

    public override void _Ready()
    {
        maxHP = 50;
        hp = maxHP;
        damage = 10;

        base._Ready();
    }

    public override void _PhysicsProcess(double delta)
    {
//        if (!myId.IsNetworkReady) return;
        if (GenericCore.Instance.IsServer)
        {
            if (!IsOnFloor())
            {
                Vector3 vel = Velocity;
                vel.Y -= 20f * (float)delta;
                Velocity = vel;
            }

            // Refresh target every frame in case players join/die
            player = FindPlayer();
            if (player == null)
            {
                // No players found, stand still
                Velocity = new Vector3(0, 0, 0);
                SyncedIsMoving = false;
            }
            else
            {
                moveWorm();
            }

        }
    }

    public Player FindPlayer()
    {
        Player near = null;
        float nearestDistance = float.MaxValue;
       
        foreach (Player player in GetTree().GetNodesInGroup("Players"))
        {
            
            // Skip dead players
            //if (player._isDead) continue;
            float distance = GlobalPosition.DistanceTo(player.GlobalPosition);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                near = player;
            }
        }
        return near;
    }

    private void moveWorm()
    {
        Vector3 direction = new Vector3 (player.GlobalPosition.X - GlobalPosition.X, -5f, player.GlobalPosition.Z - GlobalPosition.Z).Normalized();
        SyncedVelocity = direction*speed;
        LookAt(player.GlobalPosition);
        MoveAndSlide();
    }

    private void OnHitByBullet()
    {
        QueueFree();
    }

}