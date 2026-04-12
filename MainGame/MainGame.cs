using Godot;
using System;

public partial class MainGame : Node3D
{

	[Export] public Node3D[] EnemySpawners;
	[Export] public float SpawnInterval;

	private Timer SpawnTimer;
	private double _elapsedSec;

	private bool setUp = false;

	private readonly int[] LevelWeights = { 50, 30, 15, 5 };
	public override void _Ready()
	{
		var spawnerParent = GetNode<Node>("EnemySpawns");
		var children = spawnerParent.GetChildren();
		EnemySpawners = new Node3D[children.Count];
		for (int i = 0; i < children.Count; i++)
			EnemySpawners[i] = (Node3D)children[i];

		SpawnInterval = GetSpawnInterval(0);
		SpawnTimer = new Timer
		{
			WaitTime = SpawnInterval,
			Autostart = true
		};
		AddChild(SpawnTimer);
		SpawnTimer.Timeout += SpawnEnemy;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (IsVisibleInTree() && !setUp)
		{
			var spawnerParent = GetNode<Node>("EnemySpawns");
		var children = spawnerParent.GetChildren();
		EnemySpawners = new Node3D[children.Count];
		for (int i = 0; i < children.Count; i++)
			EnemySpawners[i] = (Node3D)children[i];

		SpawnInterval = GetSpawnInterval(0);
		SpawnTimer = new Timer
		{
			WaitTime = SpawnInterval,
			Autostart = true
		};
		AddChild(SpawnTimer);
		SpawnTimer.Timeout += SpawnEnemy;
		setUp = true;
		}
		
	}

	private void SpawnEnemy()
	{
		Rpc("SpawnEnemyRPC");
	}

[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
	 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SpawnEnemyRPC()
	{
		if (GenericCore.Instance.IsServer){
		if (EnemySpawners == null || EnemySpawners.Length == 0) { GD.PushWarning("No spawners assigned."); return; }
		// int maxAllowedLevel = GetMaxAllowedLevel(_elapsedSec);
		// int chosenLevel = GetWeightedLevel(maxAllowedLevel);
		var spawnPoint = EnemySpawners[GD.RandRange(0, EnemySpawners.Length - 1)];


		var enemySpawner = GetTree().Root.FindChild("EnemySpawner", true, false) as NetworkCore;
		var spawnedNode = enemySpawner.NetCreateObject(0, spawnPoint.Position, Quaternion.Identity, 1);
		}
	}

	private int GetWeightedLevel(int maxAllowedLevel)
	{
		int totalWeight = 0;

		for (int i = 0; i < maxAllowedLevel; i++)
			totalWeight += LevelWeights[i];

		int roll = GD.RandRange(1, totalWeight);

		int cumulative = 0;

		for (int i = 0; i < maxAllowedLevel; i++)
		{
			cumulative += LevelWeights[i];
			if (roll <= cumulative)
				return i + 1; 
		}

		return 1; 
	}

	private int GetMaxAllowedLevel(double elapsedSec)
	{
		if (elapsedSec < 15.0) return 1;
		if (elapsedSec < 30.0) return 2;
		if (elapsedSec < 45.0) return 3;
		return 4;
	}


	private float GetSpawnInterval(double time)
	{
		if (time < 15.0) return 2f;
		if (time < 30.0) return 1f;
		if (time < 45.0) return 0.5f;
		if(time < 60.0) return 0.25f;
		if(time < 75.0) return 0.1f;
		return 0.05f;
	}
	
}