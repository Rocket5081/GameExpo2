using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Player : CharacterBody3D
{
	[Export] public AnimationPlayer myAnimation;
	[Export] public NetID myId;
	[Export] public Marker3D BulletSpawn;
	[Export] public CanvasLayer ReticleLayer;  // kept for legacy wiring; HUD.cs handles display
	[Export] public Label3D NameLabel;

	// ── Sound ────────────────────────────────────────────────────────────────
	[Export] public AudioStreamPlayer3D FootstepPlayer;
	[Export] public float FootstepInterval = 0.45f;

	private float _footstepTimer = 0f;

	[Export] public string PlayerDisplayName = "";

	[Export] public Vector3 SyncedVelocity
	{
		get => Velocity;
		set => Velocity = value;
	}
	[Export] public int   speed     = 15;
	[Export] public float jumpForce = 10f;
	[Export] public float gravity   = 20f;

	[Export] public bool canShoot        = true;
	[Export] public bool canJump         = true;
	[Export] public bool SyncedIsOnFloor = true;
	[Export] public bool SyncedIsJumping = false;

	[Export] public bool SpecialActive = false;

	private bool _isAttacking = false;

	//[Export] public AnimationTree myAnimTree;

	private float _mouseDeltaX  = 0f;
	private bool  _mouseCaptured = false;

	public float maxTimer = 0.5f;
	public float timer = 0.5f;
	public float damage = 10f;
	[Export] public int hp;
	[Export] public int maxHp;

	// ── Ultimate ability ─────────────────────────────────────────────────────
	public float UltimateCooldownMax   = 30f;
	public float UltimateCooldownTimer = 30f;   // counts down to 0; 0 = ready
	public bool  IsShielded            = false; // set by Tank bubble

	// ── Relic system ──────────────────────────────────────────────────────────
	public enum RelicType { None, Health, Cooldown }
	public RelicType ChosenRelic = RelicType.None;
	private float _relicHealthTimer = 0f;

	// ── Score system ────────────────────────────────────────────────────────
	public int   Score          = 0;
	public float Multiplier     = 1f;
	private float _noDamageTimer = 0f;
	private const float MultiplierResetTime = 15f;
	private const float DamageScorePerPoint = 0.5f;  // score per damage point dealt
	private const int   KillScoreBase       = 50;    // bonus score per kill

	// ── Upgrade system ───────────────────────────────────────────────────────
	private int _killsSinceUpgrade  = 0;
	[Export] public int KillsPerUpgrade = 3;  // change in Inspector to taste

	
	[Export] public float FallDeathY = -20f;

	private bool  _isDead              = false;
	private bool  _respawnPending      = false;  // true while a respawn timer is in flight
	private float _respawnImmunityTimer = 0f;    // seconds of post-respawn immunity
	public List<Bullet> Buls     = new List<Bullet>();
	public int          bulCount = 0;

	public int   burstCount = 3;
	public float burstDelay = 0.1f;

	public bool rewinding = false;
	

	public Godot.Collections.Dictionary rewindValues = new Godot.Collections.Dictionary
	{
		{"position", new Godot.Collections.Array {}},
		{"rotation", new Godot.Collections.Array {}},
		{"velocity", new Godot.Collections.Array {}}
	};
	

	public override void _Ready()
	{
		GD.Print(myId.IsLocal);
		base._Ready();
		AddToGroup("Players");

		//if (myAnimTree != null){
		//	myAnimTree.Active = true;
		//}

		// Register the "ability" (Q) action at runtime so it works without
		// needing to be added manually in Project Settings → Input Map.
		if (!InputMap.HasAction("ability"))
		{
			InputMap.AddAction("ability");
			var ev = new InputEventKey();
			ev.Keycode = Key.Q;
			InputMap.ActionAddEvent("ability", ev);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (myId == null || !myId.IsLocal) return;

		if (@event is InputEventMouseMotion mouseMotion)
			_mouseDeltaX += mouseMotion.Relative.X;

		if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
			Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public override void _Process(double delta)
	{
		if(rewinding){return;}
		base._Process(delta);
		if (myId == null) return;

		// ── Nameplate ────────────────────────────────────────────────────────────
		if (NameLabel != null)
		{
			if (myId.IsLocal)
			{
				NameLabel.Visible = false;
			}
			else if (PlayerDisplayName.Length > 0 && NameLabel.Text != PlayerDisplayName)
			{
				NameLabel.Text = PlayerDisplayName;
				if      (this is DpsPlayer)     NameLabel.Modulate = new Color("ff4444");
				else if (this is TankPlayer)    NameLabel.Modulate = new Color("4488ff");
				else if (this is SupportPlayer) NameLabel.Modulate = new Color("44cc66");
				NameLabel.Visible = true;
			}
		}

		// ── Mouse capture ─────────────────────────────────────────────────────
		if (myId.IsLocal && !_mouseCaptured)
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
			_mouseCaptured = true;
		}

		//added or part cause he said so. if it breaks something just remove it.
		if (!myId.IsNetworkReady || !GenericCore.Instance.IsGenericCoreConnected) return;

		// ── Shoot cooldown ────────────────────────────────────────────────────
		if (!canShoot)
		{
			timer -= (float)delta;
			if (timer <= 0)
			{
				canShoot = true;
				timer    = 0.5f;
			}
		}

		// ── Ultimate cooldown tick ─────────────────────────────────────────────
		if (UltimateCooldownTimer > 0f)
			UltimateCooldownTimer -= (float)delta;

		// ── Score: no-damage multiplier decay ───────────────────────────────
		if (Multiplier > 1f)
		{
			_noDamageTimer += (float)delta;
			if (_noDamageTimer >= MultiplierResetTime)
				ResetMultiplier();
		}

		// ── Death / respawn — SERVER ONLY ─────────────────────────────────────
		if (GenericCore.Instance.IsServer)
		{
			// Tick post-respawn immunity window
			if (_respawnImmunityTimer > 0f)
				_respawnImmunityTimer -= (float)delta;

			// Fall-off detection — blocked while any death/respawn is in progress
			if (!_isDead && !_respawnPending && _respawnImmunityTimer <= 0f
				&& GlobalPosition.Y < FallDeathY)
				hp = 0;

			// Death — can only trigger when fully alive: not dead, no pending timer, not immune
			if (hp <= 0 && !_isDead && !_respawnPending && _respawnImmunityTimer <= 0f)
			{
				_isDead = true;
				NotifyDied();
			}
			// Revive — only after DoRespawn has finished AND immunity has expired.
			// _respawnPending stays true until DoRespawn clears it, so relic/healing
			// can never raise hp > 0 and accidentally trigger this while the timer runs.
			else if (hp > 0 && _isDead && !_respawnPending && _respawnImmunityTimer <= 0f)
			{
				_isDead = false;
			}
		}

		// ── Relic: Health regen (server-authoritative, +1 HP/s) ──────────────
		if (GenericCore.Instance.IsServer && ChosenRelic == RelicType.Health)
		{
			_relicHealthTimer += (float)delta;
			if (_relicHealthTimer >= 1f)
			{
				_relicHealthTimer -= 1f;
				if (hp < maxHp)
					hp += 1;
			}
		}

		// ── Server-side physics ───────────────────────────────────────────────
		if (GenericCore.Instance.IsServer)
		{
			if (!IsOnFloor())
			{
				Vector3 vel = SyncedVelocity;
				vel.Y -= gravity * (float)delta;
				SyncedVelocity = vel;
			}

			MoveAndSlide();

			if (IsOnFloor())
			{
				Vector3 vel = SyncedVelocity;
				if (vel.Y < jumpForce * 0.3f)
					vel.Y = 0f;
				SyncedVelocity = vel;
			}

			SyncedIsOnFloor = IsOnFloor();
			SyncedIsJumping = !IsOnFloor();
		}

		// ── Local player input ────────────────────────────────────────────────
		if (myId.IsLocal)
		{
			float moveInput   = Input.GetAxis("forward", "back");
			float strafeInput = Input.GetAxis("right", "left");
			float turnInput   = _mouseDeltaX / 200f;
			_mouseDeltaX = 0f;

			Rpc("MoveMe", new Vector3(turnInput, moveInput, strafeInput));

			// ── Footstep sounds ───────────────────────────────────────────────
			bool isMoving = Mathf.Abs(moveInput) > 0.01f || Mathf.Abs(strafeInput) > 0.01f;
			if (isMoving && SyncedIsOnFloor && FootstepPlayer != null)
			{
				_footstepTimer -= (float)delta;
				if (_footstepTimer <= 0f)
				{
					FootstepPlayer.Play();
					_footstepTimer = FootstepInterval;
				}
			}
			else if (!isMoving || !SyncedIsOnFloor)
			{
				FootstepPlayer?.Stop();
				_footstepTimer = 0f;
			}

			if (Input.IsActionPressed("primary") && canShoot)
			{
				//canShoot = false;
				RpcId(1, "Fire");
				Rpc("PlayAttackAnimation");
			}

			if (Input.IsActionJustPressed("jump") && canJump)
				RpcId(1, "Jump");

			// ── Ultimate ability input ────────────────────────────────────────
			// Registered at runtime in _Ready(); mapped to Q.
			if (Input.IsActionJustPressed("ability") && UltimateCooldownTimer <= 0f)
			{
				UltimateCooldownTimer = UltimateCooldownMax;
				OnLocalUltimateActivated();   // subclass plays its activation sound
				RpcId(1, "UseUltimate");
			}

				

		}

		if (!GenericCore.Instance.IsServer)
			UpdateAnimation();

		if (GenericCore.Instance.rewind)
		{
			rewind();
		}

	}

	public override void _PhysicsProcess(double delta)
	{
		if (!rewinding)
		{
			((Godot.Collections.Array)rewindValues["position"]).Add(Position);
			((Godot.Collections.Array)rewindValues["rotation"]).Add(Rotation);
			((Godot.Collections.Array)rewindValues["velocity"]).Add(SyncedVelocity);
		}
		else
		{
			computeRewind();
			
		}
	}


	protected virtual void UpdateAnimation()
	{
		if (myAnimation == null) return;

		if (myAnimation.CurrentAnimation == "Shoot" && myAnimation.IsPlaying()){
			return;
		}

		if (myAnimation.CurrentAnimation == "SpecialIntro" && myAnimation.IsPlaying())
			return;

		if (myAnimation.CurrentAnimation == "SpecialAttackI" && myAnimation.IsPlaying())
			return;

		if (SyncedIsJumping)
		{
			myAnimation.Play("Falling");
		}
		else
		{
			Vector3 flatVel = new Vector3(SyncedVelocity.X, 0, SyncedVelocity.Z);
			if (flatVel.Length() > 0.15f)
				myAnimation.Play("WalkCycle");
			else
				myAnimation.Play("BaseStance");
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void PlayAttackAnimation()
	{
		if (myAnimation == null) return;
		myAnimation.Play("Shoot");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void Jump()
	{
		if (GenericCore.Instance.IsServer && IsOnFloor())
		{
			Vector3 vel = SyncedVelocity;
			vel.Y = jumpForce;
			SyncedVelocity = vel;
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public virtual void Fire() { }

	/// <summary>
	/// Called locally on the pressing client the moment Q is accepted.
	/// Override in each class to play an activation sound.
	/// </summary>
	protected virtual void OnLocalUltimateActivated() { }

	/// <summary>
	/// Override in each class to define the ultimate ability.
	/// Called on the server via RpcId(1, "UseUltimate") from the local client.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public virtual void UseUltimate() { }

	// ── Relic RPCs ───────────────────────────────────────────────────────────

	/// <summary>
	/// Local client sends this to the server to request a relic.
	/// Server validates, then broadcasts SyncRelicChosen to all peers.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void SelectRelic(int relicIndex)
	{
		if (!GenericCore.Instance.IsServer) return;
		if (ChosenRelic != RelicType.None)    return; // already locked in
		Rpc("SyncRelicChosen", relicIndex);
	}

	/// <summary>
	/// Server broadcasts the confirmed relic choice to every peer.
	/// CallLocal = true so the server also applies it immediately.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void SyncRelicChosen(int relicIndex)
	{
		if (ChosenRelic != RelicType.None) return;   // prevent double-apply
		ChosenRelic = (RelicType)relicIndex;
		if (ChosenRelic == RelicType.Cooldown)
		{
			UltimateCooldownMax   = Mathf.Max(UltimateCooldownMax - 10f, 5f);
			UltimateCooldownTimer = Mathf.Min(UltimateCooldownTimer, UltimateCooldownMax);
		}
	}

	/// <summary>
	/// Called when this player is hit by a damaging attack.
	/// IsShielded (set by the Tank bubble) blocks all damage.
	/// </summary>
	public virtual void OnHitByBullet()
	{
		if (IsShielded) return;
		// Damage is handled by the game's health system; hook here as needed.
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void MoveMe(Vector3 input)
	{
		if (!GenericCore.Instance.IsServer) return;

		RotateY(input.X * -0.5f);

		Vector3 forward  = -Transform.Basis.X;
		Vector3 right    = -Transform.Basis.Z;
		float   currentY = SyncedVelocity.Y;

		Vector3 horizontal = (forward * input.Y) + (right * input.Z);
		horizontal = horizontal.Length() > 0.001f
			? horizontal.Normalized() * speed
			: Vector3.Zero;

		SyncedVelocity = new Vector3(horizontal.X, currentY, horizontal.Z);
	}

	// ── Nameplate RPC ────────────────────────────────────────────────────────
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void SetDisplayName(string name)
	{
		PlayerDisplayName = name;

		if (NameLabel == null) return;
		if (myId != null && myId.IsLocal) { NameLabel.Visible = false; return; }

		NameLabel.Text = name;
		if      (this is DpsPlayer)     NameLabel.Modulate = new Color("ff4444");
		else if (this is TankPlayer)    NameLabel.Modulate = new Color("4488ff");
		else if (this is SupportPlayer) NameLabel.Modulate = new Color("44cc66");
		NameLabel.Visible = true;
	}

	public Vector3 GetBulletSpawnPos()
	{
		if (BulletSpawn != null)
			return BulletSpawn.GlobalPosition;
		return GlobalPosition + (Transform.Basis.X * 1.5f) + new Vector3(0, 1f, 0);
	}

	public async void SpawnBullet(int count, float cooldown)
	{
		for (int i = 0; i < count; i++)
		{
			Vector3 spawnPos = GetBulletSpawnPos();
			var t1 = GenericCore.Instance.MainNetworkCore?.NetCreateObject(
				3, spawnPos, Transform.Basis.GetRotationQuaternion(), 1);

			if (t1 == null) return;
			if (t1 is RigidBody3D rb)
			{
				rb.CollisionLayer = 4;
				rb.CollisionMask  = 1;
				rb.LinearVelocity = Transform.Basis.X * 150f;
			}
			if (t1 is Bullet b)
			{
				b.damage  = damage;
				b.Shooter = this;
				Buls.Add(b);
			} 

			await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
		}
	}

	public async void ShootBullet(int count, float cooldown)
	{

		for (int i = 0; i < count; i++)
		{
			if (bulCount >= Buls.Count) bulCount = 0;
			if (!IsInstanceValid(Buls[bulCount])) { bulCount = 0; continue; }

			Vector3 spawnPos = GetBulletSpawnPos();
			var bul = Buls[bulCount];

			bul.Reset();
			bul.Show();
			bul.Rotation          = Rotation;
			bul.GlobalPosition    = spawnPos;
			bul.LinearVelocity    = bul.Transform.Basis.X * 200f;
			bul.damage            = damage;
			bul.Shooter           = this;

			bulCount++;
			if (bulCount >= Buls.Count) bulCount = 0;

			await ToSignal(GetTree().CreateTimer(cooldown), SceneTreeTimer.SignalName.Timeout);
		}
	}

	// ── Respawn ───────────────────────────────────────────────────────────────

	/// <summary>Export slot — drag a RespawnSound AudioStreamPlayer3D onto the player
	/// in the editor. Played on all peers when this player respawns.</summary>
	[Export] public AudioStreamPlayer3D RespawnSound;

	private const float RespawnDelay = 5f;

	/// <summary>Called by the server the first frame hp hits 0.
	/// Sets the respawn latch so no second timer can be queued, then starts the countdown.</summary>
	private void NotifyDied()
	{
		_respawnPending = true;   // latch: blocks all death re-entry until DoRespawn clears it
		ResetMultiplier();
		GD.Print($"[Player] {PlayerDisplayName} died — respawning in {RespawnDelay}s");
		var t = GetTree().CreateTimer(RespawnDelay);
		t.Timeout += DoRespawn;
	}

	private void DoRespawn()
	{
		// Guard: if somehow a stale callback fires after the latch was already cleared, ignore it.
		if (!_respawnPending)
		{
			GD.PrintErr($"[Player] DoRespawn: stale timer on {PlayerDisplayName} — ignored.");
			return;
		}

		var marker = GetTree().Root.FindChild("StatueRespawnPoint", true, false) as Marker3D;
		Vector3 pos = marker != null
			? marker.GlobalPosition
			: GetFallbackStatuePosition();

		// Clear the latch BEFORE touching hp/position so the revive-check can't fire early
		_respawnPending       = false;
		hp                    = maxHp;
		GlobalPosition        = pos;
		SyncedVelocity        = Vector3.Zero;
		_isDead               = true;   // keep dead during immunity window
		_respawnImmunityTimer = 3f;

		GD.Print($"[Player] {PlayerDisplayName} respawning at {pos}");
		Rpc(MethodName.PlayRespawnSFX);
	}

	private static Vector3 GetFallbackStatuePosition()
	{
		// Hard-coded fallback: just in front of Statue2's known world position
		return new Vector3(-171.5f, 2f, 233f);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void PlayRespawnSFX()
	{
		RespawnSound?.Play();
	}

	// ── Score ─────────────────────────────────────────────────────────────────

	/// <summary>Called by Enemy when this player's bullet deals damage.</summary>
	public void NotifyDamageDealt(int amount)
	{
		// 0.5 score per damage point, scaled by current multiplier
		Score          += Mathf.RoundToInt(amount * DamageScorePerPoint * Multiplier);
		Multiplier      = Mathf.Min(Multiplier + 0.05f, 4f);
		_noDamageTimer  = 0f;
		// Push updated values to all peers so the HUD stays in sync
		Rpc(MethodName.SyncScoreRpc, Score, Multiplier);
	}

	/// <summary>Called by Enemy.Die() when this player's bullet lands the kill.</summary>
	public void NotifyKill()
	{
		Score          += Mathf.RoundToInt(KillScoreBase * Multiplier);
		Multiplier      = Mathf.Min(Multiplier + 0.25f, 4f);
		_noDamageTimer  = 0f;
		Rpc(MethodName.SyncScoreRpc, Score, Multiplier);

	}

	public void ShowUpgradeUI()
	{
		if (myId == null || !myId.IsLocal) return;
		GetNodeOrNull<Upgrades>("Upgrades")
			?.GetNodeOrNull<Options>("Options")
			?.add();
	}

	public void ResetMultiplier()
	{
		Multiplier     = 1f;
		_noDamageTimer = 0f;
		// Only the server (authority) may broadcast SyncScoreRpc
		if (GenericCore.Instance.IsServer)
			Rpc(MethodName.SyncScoreRpc, Score, Multiplier);
	}


	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void SyncScoreRpc(int score, float multiplier)
	{
		Score      = score;
		Multiplier = multiplier;
	}

	// ── Upgrades ──────────────────────────────────────────────────────────────

	/// <summary>
	/// Client calls RpcId(1, nameof(ServerApplyUpgrade), opt) to send the chosen
	/// upgrade to the server. The server applies the stat change authoritatively,
	/// then broadcasts the new values back to all peers so every display stays in sync.
	/// </summary>
	public void upgrade(string[] splitOpt)
	{
		int level = splitOpt[1].ToInt();
		switch (splitOpt[0])
		{
			case "AC":
				float acReduction     = level * 2.5f;
				UltimateCooldownMax   = Mathf.Max(UltimateCooldownMax - acReduction, 5f);
				UltimateCooldownTimer = Mathf.Min(UltimateCooldownTimer, UltimateCooldownMax);
				break;

			case "PC":
				maxTimer = Mathf.Max(maxTimer - level * 0.05f, 0.05f);
				break;

			case "MH":
				int gain = level * 5;
				maxHp   += gain;
				hp       = Mathf.Min(hp + gain, maxHp);
				break;

			case "D":
				damage += level * 5f;
				break;

			case "AP":
				burstCount += level;
				break;
		}

		// Broadcast the updated stats to every peer so HUD and local values stay current
		Rpc(MethodName.SyncUpgradeRpc, maxHp, hp, damage, maxTimer, burstCount,
			UltimateCooldownMax, UltimateCooldownTimer);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SyncUpgradeRpc(int newMaxHp, int newHp, float newDamage, float newMaxTimer,
								int newBurstCount, float newUltMax, float newUltTimer)
	{
		maxHp                 = newMaxHp;
		hp                    = newHp;
		damage                = newDamage;
		maxTimer              = newMaxTimer;
		burstCount            = newBurstCount;
		UltimateCooldownMax   = newUltMax;
		UltimateCooldownTimer = newUltTimer;
	}

	// ── Rewind ────────────────────────────────────────────────────────────────

	//RPC REWIND NEEDS TO EXSIST SO THAT THE SERVER WILL UPDATE POSITION OF THE PLAYER WHILE REWINDING

	public void rewind()
	{
		rewinding = true;
	}

	//https://www.youtube.com/watch?v=XoETrCrSkks a link for a complete description of rewind feature: 1:12 - 3:44
	public void computeRewind()
	{
		var pos = ((Godot.Collections.Array)rewindValues["position"]).Last();
		var rot = ((Godot.Collections.Array)rewindValues["rotation"]).Last();
		((Godot.Collections.Array)rewindValues["position"]).RemoveAt(((Godot.Collections.Array)rewindValues["position"]).Count -1);
		((Godot.Collections.Array)rewindValues["rotation"]).RemoveAt(((Godot.Collections.Array)rewindValues["rotation"]).Count -1);
		if(((Godot.Collections.Array)rewindValues["position"]).Count == 0)
		{
			GetNode<CollisionShape3D>("CollisionShape3D").SetDeferred("disabled", false);
			rewinding = false;
			Position = (Vector3)pos;
			Rotation = (Vector3)rot;
			SyncedVelocity = (Vector3)((Godot.Collections.Array)rewindValues["velocity"]).First();
			Rpc("computeRewindRPC", pos, rot);
			

		}
		Position = (Vector3)pos;
		Rotation = (Vector3)rot;
		Rpc("computeRewindRPC", pos, rot);
	}
[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void computeRewindRPC( Vector3 pos, Vector3 rot)
	{
		if (GenericCore.Instance.IsServer)
		{
			SyncedVelocity = (Vector3)((Godot.Collections.Array)rewindValues["velocity"]).First();
			Position = pos;
			Rotation = rot;
		}
	}
}
