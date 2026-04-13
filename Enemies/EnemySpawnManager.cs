using Godot;
using System;

/// <summary>
/// Server-authoritative enemy spawn manager.
/// Spawns enemies from 6 visual portal positions surrounding the arena.
/// </summary>
public partial class EnemySpawnManager : Node
{
	[Export] public float SpawnInterval = 5f;
	[Export] public int   MaxEnemies    = 20;
	[Export] public int   WaveBatchSize = 1; // enemies per tick; raise for harder waves

	// Matches the 6 SpawnPortals in MainGame.tscn (radius ≈ 155)
	private static readonly Vector3[] PortalPositions =
	{
		new Vector3(   0f, 1f, -155f),   // Portal_1  (North)
		new Vector3( 134f, 1f,  -78f),   // Portal_2  (NE)
		new Vector3( 134f, 1f,   78f),   // Portal_3  (SE)
		new Vector3(   0f, 1f,  155f),   // Portal_4  (South)
		new Vector3(-134f, 1f,   78f),   // Portal_5  (SW)
		new Vector3(-134f, 1f,  -78f),   // Portal_6  (NW)
	};

	private NetworkCore _enemyCore;
	private Timer       _spawnTimer;
	private int         _portalIndex = 0;

	public override void _Ready()
	{
		// Only the server manages spawning
		if (!GenericCore.Instance.IsServer) return;

		// Grab the sibling EnemyNetworkCore spawner
		_enemyCore = GetParent().GetNode<NetworkCore>("EnemyNetworkCore");
		if (_enemyCore == null)
		{
			GD.PrintErr("[EnemySpawnManager] Could not find EnemyNetworkCore sibling!");
			return;
		}

		_spawnTimer = new Timer();
		_spawnTimer.WaitTime  = SpawnInterval;
		_spawnTimer.Autostart = true;
		AddChild(_spawnTimer);
		_spawnTimer.Timeout += OnSpawnTick;

		GD.Print($"[EnemySpawnManager] Ready — interval={SpawnInterval}s, max={MaxEnemies}");
	}

	private void OnSpawnTick()
	{
		if (!GenericCore.Instance.IsServer) return;

		var enemies = GetTree().GetNodesInGroup("Enemies");
		if (enemies.Count >= MaxEnemies) return;

		int toSpawn = Math.Min(WaveBatchSize, MaxEnemies - enemies.Count);
		for (int i = 0; i < toSpawn; i++)
		{
			SpawnAtNextPortal();
		}
	}

	private void SpawnAtNextPortal()
	{
		Vector3 pos = PortalPositions[_portalIndex];
		_portalIndex = (_portalIndex + 1) % PortalPositions.Length;

		// Index 0 → first spawnable scene in EnemyNetworkCore = enemy_1.tscn
		// Owner = 1 (server owns enemies)
		
	}
}
