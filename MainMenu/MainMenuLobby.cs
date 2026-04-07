using Godot;
using System.Threading.Tasks;
using System.Collections.Generic;

public partial class MainMenuLobby : Control
{
	[Export] public LineEdit NameEntry;
	[Export] public OptionButton ClassDropdown;
	[Export] public OptionButton ItemDropdown;
	[Export] public Label NameColorLabel;
	[Export] public Control CharacterPreviewArea;
	[Export] public SubViewportContainer CowboyPreview;
	[Export] public SubViewportContainer PiratePreview;
	[Export] public SubViewportContainer PriestPreview;
	[Export] public Button ConnectButton;
	[Export] public Control OfflinePanel;
	[Export] public Control OnlinePanel;
	[Export] public Label PlayerCountLabel;

	public string PlayerName  = "Player";
	public int    ClassChoice = 0;   // 0=DPS  1=Tank  2=Support
	public int    ItemChoice  = 0;
	public int    LastPort    = 0;
	public int    HP          = 100;
	public int    Score       = 0;

	private static readonly Color ColorCowboy = new Color("ff4444");
	private static readonly Color ColorPirate = new Color("4488ff");
	private static readonly Color ColorPriest = new Color("44cc66");

	private const int RequiredPlayers = 2;

	private static readonly Dictionary<int, int> _readyPlayers = new();

	public override void _Ready()
	{
		_readyPlayers.Clear();

		ClassDropdown.Clear();
		ClassDropdown.AddItem("Cowboy  (DPS)");
		ClassDropdown.AddItem("Pirate  (Tank)");
		ClassDropdown.AddItem("Priest  (Support)");
		ClassDropdown.Selected = 0;

		ItemDropdown.Clear();
		ItemDropdown.AddItem("Relic of Health");
		ItemDropdown.AddItem("Relic of Cooldown");
		ItemDropdown.Selected = 0;

		NameEntry.TextChanged      += OnNameChanged;
		ClassDropdown.ItemSelected += OnClassSelected;
		ItemDropdown.ItemSelected  += OnItemSelected;
		ConnectButton.Pressed      += OnConnectPressed;

		OfflinePanel.Visible = true;
		OnlinePanel.Visible  = false;

		RefreshClassDisplay(0);

		if (PlayerCountLabel != null)
			PlayerCountLabel.Text = $"Players: 0 / {RequiredPlayers}";
	}

	public override void _ExitTree()
	{
		if (NameEntry != null)     NameEntry.TextChanged      -= OnNameChanged;
		if (ClassDropdown != null) ClassDropdown.ItemSelected -= OnClassSelected;
		if (ItemDropdown != null)  ItemDropdown.ItemSelected  -= OnItemSelected;
		if (ConnectButton != null) ConnectButton.Pressed      -= OnConnectPressed;
	}

	// ── UI ────────────────────────────────────────────────────────────────────

	private void OnNameChanged(string newText)
	{
		PlayerName = newText.Length > 0 ? newText : "Player";
		RefreshNameLabel();
	}

	private void OnClassSelected(long index)
	{
		ClassChoice = (int)index;
		RefreshClassDisplay(ClassChoice);
	}

	private void OnItemSelected(long index)
	{
		ItemChoice = (int)index;
	}

	public void OnHoverCowboy() => ShowPreview(0);
	public void OnHoverPirate() => ShowPreview(1);
	public void OnHoverPriest() => ShowPreview(2);

	private void ShowPreview(int classIndex)
	{
		CowboyPreview.Visible = classIndex == 0;
		PiratePreview.Visible = classIndex == 1;
		PriestPreview.Visible = classIndex == 2;
	}

	private void RefreshClassDisplay(int classIndex)
	{
		ShowPreview(classIndex);
		RefreshNameLabel();
	}

	private void RefreshNameLabel()
	{
		if (NameColorLabel == null) return;
		NameColorLabel.Text = PlayerName;
		NameColorLabel.AddThemeColorOverride("font_color", GetClassColor(ClassChoice));
	}

	private Color GetClassColor(int classIndex)
	{
		return classIndex switch
		{
			0 => ColorCowboy,
			1 => ColorPirate,
			2 => ColorPriest,
			_ => Colors.White
		};
	}

	// ── Connect ───────────────────────────────────────────────────────────────

	private void OnConnectPressed()
	{
		if (!GenericCore.Instance.IsGenericCoreConnected)
		{
			GD.PrintErr("[MainMenuLobby] Not connected yet.");
			return;
		}

		ConnectButton.Disabled = true;
		ConnectButton.Text     = "Waiting for players...";

		if (GenericCore.Instance.IsServer)
			ServerRegisterPlayer(1, ClassChoice);
		else
			RpcId(1, MethodName.ClientSendReady, ClassChoice);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientSendReady(int classChoice)
	{
		if (!GenericCore.Instance.IsServer) return;
		ServerRegisterPlayer(Multiplayer.GetRemoteSenderId(), classChoice);
	}

	private void ServerRegisterPlayer(long peerId, int classChoice)
	{
		_readyPlayers[(int)peerId] = classChoice;

		GD.Print($"[MainMenuLobby] Registered peer {peerId} class={classChoice} ({_readyPlayers.Count}/{RequiredPlayers})");
		GD.Print($"[MainMenuLobby] _readyPlayers keys: {string.Join(", ", _readyPlayers.Keys)}");

		Rpc(MethodName.UpdateCountLabel, _readyPlayers.Count, RequiredPlayers);

		if (_readyPlayers.Count >= RequiredPlayers)
		{
			GD.Print("[MainMenuLobby] FIRING StartGame RPC now!");
			Rpc(MethodName.StartGame);
		}
		else
		{
			GD.Print($"[MainMenuLobby] Not enough players yet: {_readyPlayers.Count} / {RequiredPlayers}");
		}
	}

	// ── StartGame RPC ─────────────────────────────────────────────────────────
	// Fires on ALL peers including server (CallLocal = true)
	// Hides the menu — MainGame is already in the scene tree under GameRoot
	// so no scene loading needed at all

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void StartGame()
	{
		GD.Print($"[MainMenuLobby] StartGame fired on peer {Multiplayer.GetUniqueId()}");

		// Hide this menu — MainGame is already visible underneath
		Hide();

		// Make sure the camera in MainGame is active
		var camera = GetTree().Root.FindChild("Camera3D", true, false) as Camera3D;
		if (camera != null)
			camera.MakeCurrent();
		else
			GD.PrintErr("[MainMenuLobby] Camera3D not found!");

		// Only server spawns characters
		if (GenericCore.Instance.IsServer)
			ServerSpawnWithDelay();
	}

	// ── Spawn ─────────────────────────────────────────────────────────────────

	private async void ServerSpawnWithDelay()
	{
		// Wait 2 seconds for all clients to have processed StartGame
		await ToSignal(GetTree().CreateTimer(2.0f), SceneTreeTimer.SignalName.Timeout);
		await ServerSpawnCharacters();
	}

	private async Task ServerSpawnCharacters()
	{
		string[] spawnPointNames = { "SpawnPoints0", "SpawnPoints1", "SpawnPoints2", "SpawnPoints3" };

		// Find the NetworkCore (MultiplayerSpawner) that has the player scenes
		// Index 0=dps_player, 1=tank_player, 2=support_player in its Spawnable Scenes list
		var characterSpawner = GetTree().Root.FindChild("MultiplayerSpawner", true, false) as NetworkCore;
		if (characterSpawner == null)
		{
			GD.PrintErr("[MainMenuLobby] FATAL: NetworkCore MultiplayerSpawner not found!");
			return;
		}

		GD.Print($"[MainMenuLobby] Spawning {_readyPlayers.Count} players...");

		int i = 0;
		foreach (var kvp in _readyPlayers)
		{
			int peerId      = kvp.Key;
			int classChoice = kvp.Value;

			var marker  = GetTree().Root.FindChild(spawnPointNames[i], true, false) as Node3D;
			Vector3 pos = marker != null ? marker.GlobalPosition : new Vector3(i * 3f, 0, 0);

			if (marker == null)
				GD.PrintErr($"[MainMenuLobby] Spawn point {spawnPointNames[i]} not found, using fallback position");

			GD.Print($"[MainMenuLobby] Spawning peer {peerId} class={classChoice} at {pos}");

			// NetCreateObject handles everything:
			// - Picks the right player scene by classChoice index
			// - Adds it under the spawner's spawn path (PlayerSpawns)
			// - Replicates to all clients via MultiplayerSpawner
			// - Calls netId.Rpc("Initialize", peerId) so OwnerId syncs to all clients
			characterSpawner.NetCreateObject(classChoice, pos, Quaternion.Identity, peerId);

			await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
			i++;
		}

		_readyPlayers.Clear();
		GD.Print("[MainMenuLobby] All players spawned!");
	}

	// ── Counter label RPC ─────────────────────────────────────────────────────

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void UpdateCountLabel(int current, int required)
	{
		if (PlayerCountLabel != null)
			PlayerCountLabel.Text = $"Players: {current} / {required}";
		if (ConnectButton != null)
			ConnectButton.Text = $"Waiting... {current}/{required}";
	}

	// ── Disconnect ────────────────────────────────────────────────────────────

	public void Disconnect()
	{
		GenericCore.Instance.DisconnectFromGame();
		OfflinePanel.Visible   = true;
		OnlinePanel.Visible    = false;
		ConnectButton.Disabled = false;
		ConnectButton.Text     = "Connect";
		LastPort = 0;
		GenericCore.Instance.SetPort("7000");
		HP    = 100;
		Score = 0;
	}
}
