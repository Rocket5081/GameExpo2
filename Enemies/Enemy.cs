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
        base._Ready();
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }

    public void TakeDamage(int amount)
    {
        hp-=amount;
    }

}
