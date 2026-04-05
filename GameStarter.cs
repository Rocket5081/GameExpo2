using Godot;
using System.Collections.Generic;

public partial class GameStarter : Node
{
	public static GameStarter Instance;

	// peerId -> (classChoice, relicChoice)
	private Dictionary<int, (int classChoice, int relicChoice)> _playerData = new();
	private const int RequiredPlayers = 2;

	public override void _Ready()
	{
		Instance = this;
	}

	public void RegisterPlayer(int peerId, int classChoice, int relicChoice)
	{
		_playerData[peerId] = (classChoice, relicChoice);
		GD.Print($"Player {peerId} registered. Class:{classChoice} Relic:{relicChoice}. Total: {_playerData.Count}");

		if (_playerData.Count >= RequiredPlayers)
			StartGame();
	}

	private void StartGame()
	{
		GD.Print("All players ready! Starting game.");
		// Tell ALL clients to load the main game scene
		Rpc(MethodName.ClientLoadMainGame);
		// Server loads it too
		LoadMainGameAndSpawn();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientLoadMainGame()
	{
		// Clients just load the scene; spawning is handled by server's MultiplayerSpawner
		GetTree().ChangeSceneToFile("res://MainGame/MainGame.tscn");
	}

	private async void LoadMainGameAndSpawn()
	{
		GetTree().ChangeSceneToFile("res://MainGame/MainGame.tscn");

		// Wait one frame for scene to load
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		SpawnAllPlayers();
	}

	private void SpawnAllPlayers()
	{
		// Scene paths for each class (0=Cowboy/Tank, 1=Pirate/DPS, 2=Priest/Support)
		string[] classPrefabs = new string[]
		{
			"res://Player/tank_player.tscn",
			"res://Player/dps_player.tscn",
            "res://Player/support_player.tscn"
		};

		// Get your spawn points from MainGame.tscn
		Node spawnRoot = GetTree().Root.FindChild("PlayerSpawns", true, false);

		int spawnIndex = 0;
		foreach (var kvp in _playerData)
		{
			int peerId     = kvp.Key;
			int classIdx   = kvp.Value.classChoice;
			int relicIdx   = kvp.Value.relicChoice;

			// Load the right player scene
			var scene  = GD.Load<PackedScene>(classPrefabs[classIdx]);
			var player = scene.Instantiate<Node3D>();

			// Set multiplayer authority so that player is controlled by the right peer
			player.SetMultiplayerAuthority(peerId);

			// Place at the correct spawn point
			Node3D spawnPoint = spawnRoot.GetChild<Node3D>(spawnIndex);
			player.GlobalPosition = spawnPoint.GlobalPosition;

			// Add to scene (MultiplayerSpawner will replicate this)
			spawnRoot.GetParent().AddChild(player, true);

			// Apply relic passive — call a method on your player script
			// player.Call("ApplyRelic", relicIdx);

			spawnIndex++;
		}

		_playerData.Clear();
	}
}
