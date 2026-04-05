using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Player : CharacterBody3D
{
    [Export] public AnimationPlayer myAnimation;
    [Export] public NetID myId;
    [Export] public Vector3 SyncedVelocity
    {
        get => Velocity;
        set => Velocity = value;
    }
    [Export] public int speed = 15;
    [Export] public float jumpForce = 10f;
    [Export] public float gravity = 20f;

    [Export] public bool canShoot = true;
    [Export] public bool canJump = true;
    [Export] public bool SyncedIsOnFloor = true;
    [Export] public bool SyncedIsJumping = false;

    private float _mouseDeltaX = 0f;

    private bool _mouseCaptured = false;

    //Player Stats

    public float timer = 0.5f;

    public float abilityCooldown;

    public float damage = 10f;

    public int hp;

    public int maxHp;

    public List<Bullet> Buls = new List<Bullet>();

    public int bulCount = 0;

    public override void _Ready()
    {
        base._Ready();
        RemoveUI();

        if (myId.IsLocal)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public void RemoveUI()
    {
        //remove UI stuff for the player
    }

    public override void _Input(InputEvent @event)
    {
        if (!myId.IsLocal) return;

        if (@event is InputEventMouseMotion mouseMotion)
        {
            _mouseDeltaX += mouseMotion.Relative.X;
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (myId.IsLocal && !_mouseCaptured)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            _mouseCaptured = true;
        }

        if (!myId.IsNetworkReady) return;

        if (!canShoot)
        {
            timer -= (float)delta;
            if (timer <= 0)
            {
                canShoot = true;
                timer = 0.5f;
            }
        }

        if (GenericCore.Instance.IsServer)
        {
            if (!IsOnFloor())
            {
                Vector3 vel = SyncedVelocity;
                vel.Y -= gravity * (float)delta;
                SyncedVelocity = vel;
            }
            else
            {
                Vector3 vel = SyncedVelocity;
                if (vel.Y < 0) vel.Y = 0;
                SyncedVelocity = vel;
            }

            SyncedIsOnFloor = IsOnFloor();
            SyncedIsJumping = !IsOnFloor();
            MoveAndSlide();
        }

        if (myId.IsLocal)
        {
            float moveInput = Input.GetAxis("forward", "back");
            float strafeInput = Input.GetAxis("right", "left");
            float turnInput = _mouseDeltaX / 200f;
            _mouseDeltaX = 0f;

            // Pack all three axes: X = turn, Y = forward/back, Z = strafe
            Vector3 myInputAxis = new Vector3(turnInput, moveInput, strafeInput);
            Rpc("MoveMe", myInputAxis);

            if (Input.IsActionJustPressed("primary") && canShoot)
            {
                RpcId(1, "Fire");
                Rpc("PlayAttackAnimation");
            }

            if (Input.IsActionJustPressed("jump") && canJump)
                RpcId(1, "Jump");
        }

        if (!GenericCore.Instance.IsServer)
            UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        if (myAnimation == null) return;

        if (myAnimation.CurrentAnimation == "Attack" && myAnimation.IsPlaying())
            return;

        if (SyncedIsJumping)
        {
            //myAnimation.Play("Jump");
        }
        else
        {
            Vector3 flatVel = new Vector3(SyncedVelocity.X, 0, SyncedVelocity.Z);
            if (flatVel.Length() > 0.15f){
                //myAnimation.Play("RunCycle");
            }
            else{
                //myAnimation.Play("BaseStance");
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void PlayAttackAnimation()
    {
        if (myAnimation == null) return;
        //myAnimation.Play("primary");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void Jump()
    {
        if (GenericCore.Instance.IsServer && IsOnFloor())
        {
            Vector3 vel = SyncedVelocity;
            vel.Y = jumpForce;
            SyncedVelocity = vel;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public virtual void Fire()
    {
        // This will be overridden in the character class scripts
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void MoveMe(Vector3 input)
    {
        if (GenericCore.Instance.IsServer)
        {
            // Remove the delta multiplication — input.X already represents
            // one frame of mouse movement so delta would double-apply time
            RotateY(input.X * -0.5f);

            Vector3 forward = -Transform.Basis.X;
            Vector3 right = -Transform.Basis.Z;

            float currentY = SyncedVelocity.Y;
            Vector3 horizontal = (forward * input.Y) + (right * input.Z);
            
            if (horizontal.Length() > 0.001f)
                horizontal = horizontal.Normalized() * speed;
            else
                horizontal = Vector3.Zero;

            SyncedVelocity = new Vector3(horizontal.X, currentY, horizontal.Z);
        }
    }
}