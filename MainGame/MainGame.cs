using Godot;
using System.Collections.Generic;
using System;

public partial class MainGame : Node3D
{
	[Export] public Node3D[] EnemySpawners;

	/// <summary>
	/// Place a Marker3D node in the editor in front of Statue2, then drag it
	/// into this slot. Players will respawn here on death.
	/// If left empty the code falls back to a hard-coded position near the statue.
	/// </summary>
	[Export] public Marker3D StatueRespawnPoint;

	private Timer              _spawnTimer;
	private double             _elapsedSec  = 0.0;
	private bool               _started     = false;   // true once we're visible and running
	private AudioStreamPlayer  _musicPlayer;

	private readonly int[] LevelWeights = { 50, 30, 5, 1 };

	int RoundNum = 0;

	public float RoundTimer = 15f;
	public float waitTimer = 0f;
	public bool upgrading = false;

	public List<Enemy> Enms     = new List<Enemy>();

	public override void _Ready()
	{
		_RefreshSpawners();


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

	private void OnSpawnTick()
	{
		// Only the server spawns enemies.
		if (!GenericCore.Instance.IsServer) return;
		if(RoundTimer > 0f)
			// SpawnEnemyRPC();
			// if(RoundNum >= 7)
			SpawnBossRPC();
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

		var spawner    = GetTree().Root.FindChild("BossSpawner", true, false) as NetworkCore;
		if (spawner == null)
		{
			GD.PrintErr("[MainGame] EnemySpawner NetworkCore not found!");
			return;
		}

		spawner.NetCreateObject(0, ((Node3D)spawnPoint).GlobalPosition, Quaternion.Identity, 1);
	
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

		spawner.NetCreateObject(level, spawnPoint.GlobalPosition, Quaternion.Identity, 1);
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
		if (t < 15.0) return 0;
		return 1;
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
		if (t < 15.0)  return 5f;    // grace period — nothing spawns yet
		if (t < 30.0)  return 3f;
		if (t < 60.0)  return 2f;
		if (t < 120.0) return 1f;
		return 0.5f;
	}
}
