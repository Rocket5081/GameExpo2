using Godot;
using System;

public partial class Bullet : RigidBody3D
{
    [Export] public NetID myId;

    private float _lifetime = 0f;
    private const float MaxLifetime = 6f;

    public override void _Ready()
    {
        
    }

    public override void _Process(double delta)
    {
        if (!myId.IsNetworkReady) return;
        if (!GenericCore.Instance.IsServer) return;

        _lifetime += (float)delta;

        if (_lifetime >= MaxLifetime)
            HideBullet();
    }

    private void OnBodyEntered(Node body)
    {
        if (!GenericCore.Instance.IsServer) return;

        if (body is Player || body.IsInGroup("boxes"))
            HideBullet();
        if (body.IsInGroup("enemy"))
        {
            HideBullet();
            body.Call("OnHitByBullet");
        }
    }

    private void HideBullet()
    {
        this.Hide();
		this.CollisionLayer = 8;
		this.CollisionMask = 8;
    }
}
