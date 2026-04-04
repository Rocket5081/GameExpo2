using Godot;
using System;

public partial class WormEnemy : Enemy
{
    private NavigationAgent3D navAgent;

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

        navAgent = GetNode<NavigationAgent3D>("NavigationAgent3D");
        navAgent.PathPostprocessing = NavigationPathQueryParameters3D.PathPostProcessing.None;

        base._Ready();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!myId.IsNetworkReady) return;
        if (GenericCore.Instance.IsServer)
        {
            if (!IsOnFloor())
            {
                Vector3 vel = Velocity;
                vel.Y -= 20f * (float)delta;
                Velocity = vel;
            }

            // Refresh target every frame in case players join/die
            var player = FindPlayer();

            if (player != null)
            {
                navAgent.TargetPosition = player.GlobalPosition;

                if (!navAgent.IsNavigationFinished())
                {
                    MoveAlongPath();
                    SyncedIsMoving = true;
                }
                else
                {
                    SyncedIsMoving = false;
                }
            }
            else
            {
                // No players found, stand still
                Velocity = new Vector3(0, Velocity.Y, 0);
                SyncedIsMoving = false;
            }
        }
    }

    public Player FindPlayer()
    {
        Player near = null;
        float nearestDistance = float.MaxValue;

        foreach (Player player in GetTree().GetNodesInGroup("players"))
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

    public void MoveAlongPath()
    {
        Vector3 destination = navAgent.GetNextPathPosition();
        Vector3 direction = (destination - GlobalPosition).Normalized();

        float currentY = Velocity.Y;
        Velocity = new Vector3(direction.X * speed, currentY, direction.Z * speed);

        Vector3 flatDirection = new Vector3(direction.X, 0, direction.Z).Normalized();
        if (flatDirection.Length() > 0.1f)
        {
            Transform3D t = Transform;
            t.Basis = Basis.LookingAt(flatDirection, Vector3.Up).Rotated(Vector3.Up, Mathf.DegToRad(0));
            Transform = t;
        }

        MoveAndSlide();
    }

}