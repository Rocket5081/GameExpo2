using Godot;
using System.Collections.Generic;
using System;

public partial class MainGame : Node3D
{
	public static MainGame Instance { get; private set; }

	[Export] public Node3D[] EnemySpawners;
	[Export] public Marker3D StatueRespawnPoint;

	private Timer              _spawnTimer;
	private double             _elapsedSec  = 0.0;
	private bool               _started     = false;   // true once we're visible and running
	private AudioStreamPlayer  _musicPlayer;
	private AudioStreamPlayer  _bossMusicPlayer;

	[Export] public AudioStream BossMusicStream;

	private readonly int[] LevelWeights = { 50, 30, 5, 1 };

	// Boss once-only guard
	private bool _bossSpawned         = false;
	private bool _bossSequenceStarted = false;

	public int RoundNum = 0;

	// Round-based enemy budget: Round 1 = 15, Round 2 = 30, Round 3 = 45 …
	private int _enemiesSpawnedThisRound = 0;
	private int _lastTrackedRound        = -1;

	public float RoundTimer = 15f;
	public float waitTimer = 0f;
	public bool upgrading = false;

	public List<Enemy> Enms = new List<Enemy>();

	/// <summary>Enemies spawned so far this round — used by HUD for kill-progress.</summary>
	public int EnemiesSpawnedThisRound => _enemiesSpawnedThisRound;

	public override void _Ready()
	{
		Instance = this;
		_RefreshSpawners();

		// Win screen lives in the scene tree from the start, hidden until boss dies.
		AddChild(new WinScreen());


		var raw = GD.Load<AudioStream>("res://Sounds/RoundMusicGameExpo2.2.mp3");
		if (raw != null)
		{
			_musicPlayer = new AudioStreamPlayer { Name = "GameMusic" };
			var stream = (AudioStream)raw.Duplicate();
			if (stream is AudioStreamMP3 mp3) mp3.Loop = true;
			_musicPlayer.Stream   = stream;
			_musicPlayer.VolumeDb = -15f;
			AddChild(_musicPlayer);
		}

		// Boss music player — stream assigned via BossMusicStream export in Inspector
		_bossMusicPlayer = new AudioStreamPlayer { Name = "BossMusic" };
		_bossMusicPlayer.VolumeDb = -15f;
		AddChild(_bossMusicPlayer);
		if (BossMusicStream != null)
		{
			var bossStream = (AudioStream)BossMusicStream.Duplicate();
			if (bossStream is AudioStreamMP3 bmp3) bmp3.Loop = true;
			_bossMusicPlayer.Stream = bossStream;
		}
	}

	public override void _Process(double delta)
	{
		// Wait until this node is actually visible to players before starting spawns.
		if (!IsVisibleInTree()) return;

		// First visible frame — kick off the spawn timer and start the game music.
		if (!_started)
		{
			_started = true;
			_elapsedSec = 0.0;
			// NOTE: do NOT call _RefreshSpawners() here — enemies may already be
			// children of EnemySpawns by this frame and would pollute the array.

			_spawnTimer = new Timer
			{
				WaitTime  = GetSpawnInterval(0.0),
				Autostart = true,
				OneShot   = false,
			};
			AddChild(_spawnTimer);
			_spawnTimer.Timeout += OnSpawnTick;

			// Start game music. Menu music is already stopped on all peers via
			// StopMenuMusicRpc() before StartGame fires, so there's no overlap.
			_musicPlayer?.Play();

			// Re-show the HUD — it was hidden when players returned to lobby last time.
			// CanvasLayer ignores parent visibility so we must set it explicitly.
			GetNodeOrNull<HUD>("HUD")?.Show();

			GD.Print("[MainGame] Game visible — enemy spawning and music started.");
		}

		_elapsedSec += delta;

		// Continuously update the spawn interval so it accelerates over time.
		if (_spawnTimer != null)
		{
			float newInterval = GetSpawnInterval(_elapsedSec);
			if (!Mathf.IsEqualApprox(_spawnTimer.WaitTime, newInterval))
				_spawnTimer.WaitTime = newInterval;
		}

		Enms.RemoveAll(b => !IsInstanceValid(b));
		if(RoundTimer > 0f)
			RoundTimer -= (float)delta;
		else if(RoundTimer <= 0f && Enms.Count == 0){
			if(!upgrading){
				foreach (Player player in GetTree().GetNodesInGroup("Players"))
				{
					player.ShowUpgradeUI();
				}
				upgrading = true;
				RoundNum++;
			}
			if(waitTimer <= 0f){
				waitTimer = 10f;
				RoundTimer = 45f;
				upgrading = false;
			}
			else
			waitTimer -= (float)delta;
		}
	}

	// ── Spawn helpers ─────────────────────────────────────────────────────────

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}


	public int GetRoundEnemyTarget() => (RoundNum + 1) * 15;

	// Max simultaneously alive enemies scales with the round budget, capped at 12.
	private int GetMaxAliveEnemies() => Mathf.Clamp((RoundNum + 1) * 4, 5, 12);

	private void OnSpawnTick()
	{
		if (!GenericCore.Instance.IsServer) return;
		if (RoundTimer <= 0f) return;

		if (RoundNum >= 1)
		{
			if (!_bossSpawned)
				StartBossSequence();
			return;
		}

		// Reset per-round counter whenever a new round starts.
		if (RoundNum != _lastTrackedRound)
		{
			_enemiesSpawnedThisRound = 0;
			_lastTrackedRound        = RoundNum;
		}

		// Stop spawning once we've hit this round's total budget.
		if (_enemiesSpawnedThisRound >= GetRoundEnemyTarget()) return;

		// Still respect the alive-at-once cap so the server isn't overwhelmed.
		Enms.RemoveAll(b => !IsInstanceValid(b));
		if (Enms.Count >= GetMaxAliveEnemies()) return;

		SpawnEnemyRPC();
	}

	// ── Boss intro + single spawn ─────────────────────────────────────────────
	private async void StartBossSequence()
	{
		if (_bossSequenceStarted) return;
		_bossSequenceStarted = true;

		// Broadcast the fly-up animation to ALL clients (and run locally).
		if (Multiplayer.HasMultiplayerPeer())
			Rpc(nameof(AnimateIdleBossRpc));
		else
			AnimateIdleBossRpc();

		// Server waits for the same animation duration before spawning.
		await ToSignal(GetTree().CreateTimer(3.5f), SceneTreeTimer.SignalName.Timeout);

		// Switch music on all peers: stop round music, start boss music.
		if (Multiplayer.HasMultiplayerPeer())
			Rpc(nameof(StartBossMusicRpc));
		else
			StartBossMusicRpc();

		_bossSpawned = true;
		SpawnBossRPC();
	}


	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void AnimateIdleBossRpc()
	{
		var idleBoss = GetNodeOrNull<Node3D>("Boss");
		if (idleBoss != null)
		{
			var tween = CreateTween()
				.SetEase(Tween.EaseType.In)
				.SetTrans(Tween.TransitionType.Quad);
			tween.TweenProperty(idleBoss, "position:y", idleBoss.Position.Y + 600f, 3.5f);
			tween.TweenCallback(Callable.From(() =>
			{
				if (IsInstanceValid(idleBoss)) idleBoss.Visible = false;
			}));
		}

		// After the animation window, mark boss-fight recording as live on this peer.
		GetTree().CreateTimer(3.5f).Timeout += () =>
		{
			GenericCore.Instance.BossHasSpawned = true;
		};
	}


	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void StartBossMusicRpc()
	{
		_musicPlayer?.Stop();
		if (_bossMusicPlayer != null && _bossMusicPlayer.Stream != null)
			_bossMusicPlayer.Play();
	}

	private void SpawnBossRPC()
	{
		var spawnPoint = GetNode<Node3D>("BossLocations").GetChildren()[0];

		// Guard: the array is built once in _Ready from static marker nodes so this
		// should never be stale, but protect against any edge-case disposal.
		if (!IsInstanceValid(spawnPoint))
		{
			GD.PushWarning("[MainGame] Spawn point reference is no longer valid — skipping tick.");
			return;
		}

		var spawner = GetTree().Root.FindChild("BossSpawner", true, false) as NetworkCore;
		if (spawner == null)
		{
			GD.PrintErr("[MainGame] BossSpawner NetworkCore not found!");
			return;
		}

		// Spawn 35 units above the fight position so the boss descends into the arena.
		var fightPos  = ((Node3D)spawnPoint).GlobalPosition;
		var entryPos  = fightPos + new Vector3(0f, 35f, 0f);
		spawner.NetCreateObject(0, entryPos, Quaternion.Identity, 1);
	
	}

	private void SpawnEnemyRPC()
	{
		if (!GenericCore.Instance.IsServer) return;
		if (EnemySpawners == null || EnemySpawners.Length == 0)
		{
			GD.PushWarning("[MainGame] No spawn points assigned.");
			return;
		}

		int maxLevel   = GetMaxAllowedLevel(_elapsedSec);
		int level      = GetWeightedLevel(maxLevel);
		if (level <= 0) return;   // not yet time to spawn anything

		var spawnPoint = EnemySpawners[GD.RandRange(0, EnemySpawners.Length - 1)];

		// Guard: the array is built once in _Ready from static marker nodes so this
		// should never be stale, but protect against any edge-case disposal.
		if (!IsInstanceValid(spawnPoint))
		{
			GD.PushWarning("[MainGame] Spawn point reference is no longer valid — skipping tick.");
			return;
		}

		var spawner    = GetTree().Root.FindChild("EnemySpawner", true, false) as NetworkCore;
		if (spawner == null)
		{
			GD.PrintErr("[MainGame] EnemySpawner NetworkCore not found!");
			return;
		}

		// EnemySpawner scene indices: 0 = Worm, 1 = Bat, 2 = Boss (never spawned here)
		// GetWeightedLevel returns 1 or 2 — map to the correct scene slot.
		int sceneIndex = level switch
		{
			1 => 1,   // Bat
			2 => 0,   // Worm
			_ => 1,
		};
		spawner.NetCreateObject(sceneIndex, spawnPoint.GlobalPosition, Quaternion.Identity, 1);
		_enemiesSpawnedThisRound++;
		GD.Print($"[MainGame] Spawned enemy (level={level}, index={sceneIndex}) — {_enemiesSpawnedThisRound}/{GetRoundEnemyTarget()} this round.");
	}

	private void _RefreshSpawners()
	{
		var parent = GetNodeOrNull<Node>("EnemySpawns");
		if (parent == null) return;
		var children = parent.GetChildren();
		EnemySpawners = new Node3D[children.Count];
		for (int i = 0; i < children.Count; i++)
			EnemySpawners[i] = (Node3D)children[i];
	}

	// ── Difficulty curve ──────────────────────────────────────────────────────

	private int GetMaxAllowedLevel(double t)
	{
		if (t < 5.0) return 0;   // very short grace period at game start
		return 2;                  // bats + worms available immediately
	}

	private int GetWeightedLevel(int maxLevel)
	{
		if (maxLevel <= 0) return 0;

		int totalWeight = 0;
		for (int i = 0; i < maxLevel; i++)
			totalWeight += LevelWeights[i];

		int roll = GD.RandRange(0, totalWeight - 1);
		int cumulative = 0;
		for (int i = 0; i < maxLevel; i++)
		{
			cumulative += LevelWeights[i];
			if (roll < cumulative) return i + 1;
		}
		return 1;
	}

	private float GetSpawnInterval(double t)
	{
		if (t < 5.0)   return 8f;    // brief grace period at game start
		// Scales faster per round so later rounds feel more intense.
		return RoundNum switch
		{
			0 => 2.5f,   // Round 1 — moderate pace
			1 => 2.0f,   // Round 2 — faster
			_ => 1.5f,   // Round 3+ — fast
		};
	}

	// ── Return-to-lobby reset ─────────────────────────────────────────────────

	/// <summary>
	/// Called by GenericCore.AllPlayersReadyToLeave() on every client.
	/// Stops all in-game music, tears down the spawn timer, and resets all
	/// per-match state so the next StartGame() fires clean.
	/// </summary>
	public void ResetForLobby()
	{
		// ── Stop music ────────────────────────────────────────────────────────
		_bossMusicPlayer?.Stop();
		_musicPlayer?.Stop();

		// ── Tear down spawn timer ─────────────────────────────────────────────
		if (_spawnTimer != null)
		{
			_spawnTimer.Stop();
			_spawnTimer.QueueFree();
			_spawnTimer = null;
		}

		// ── Reset all per-match flags and counters ────────────────────────────
		_started                 = false;
		_elapsedSec              = 0.0;
		_bossSpawned             = false;
		_bossSequenceStarted     = false;
		_enemiesSpawnedThisRound = 0;
		_lastTrackedRound        = -1;
		RoundNum                 = 0;
		RoundTimer               = 15f;
		waitTimer                = 0f;
		upgrading                = false;
		Enms.Clear();

		// ── Free all spawned player nodes ─────────────────────────────────────
		// MultiplayerSpawner does NOT auto-free nodes when the ENet peer closes,
		// so stale player nodes would persist into the next game and the HUD
		// would read their old Score / HP / Multiplier / UltimateCooldown.
		foreach (Node node in GetTree().GetNodesInGroup("Players"))
		{
			if (IsInstanceValid(node))
				node.QueueFree();
		}

		// ── Free all remaining enemy nodes ────────────────────────────────────
		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (IsInstanceValid(node))
				node.GetParent()?.QueueFree();   // free the CharacterBody3D root
		}
		foreach (Node node in GetTree().GetNodesInGroup("Bosses"))
		{
			if (IsInstanceValid(node))
				node.QueueFree();
		}

		// ── Reset HUD ─────────────────────────────────────────────────────────
		GetNodeOrNull<HUD>("HUD")?.ResetForLobby();

		GD.Print("[MainGame] ResetForLobby complete.");
	}
}
