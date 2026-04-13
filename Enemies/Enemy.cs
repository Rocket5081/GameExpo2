using Godot;
using System;

public partial class Enemy : CharacterBody3D
{
    [Export] public int damage;
    [Export] public int hp;

    [Export] public int maxHP;

    [Export] public NetID myId;

    public override void _Ready()
    {
        AddToGroup("Enemies");
        base._Ready();
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }

    public virtual void TakeDamage(int amount)
    {
        if (!GenericCore.Instance.IsServer) return;
        hp -= amount;
        if (hp <= 0)
        {
            RemoveFromGroup("Enemies");
            QueueFree();
        }
    }

}
