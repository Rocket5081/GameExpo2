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
	[Export] public Control ControlsOverlay;
	[Export] public Control DevelopersOverlay;

	public string PlayerName  = "Player";
	public int    ClassChoice = 0;
	public int    ItemChoice  = 0;
	public int    LastPort    = 0;
	public int    HP          = 100;
	public int    Score       = 0;

	private static readonly Color ColorCowboy = new Color("ff4444");
	private static readonly Color ColorPirate = new Color("4488ff");
	private static readonly Color ColorPriest = new Color("44cc66");

	private const int RequiredPlayers = 2;

	private readonly Dictionary<int, int>    _readyPlayers     = new();
	private readonly Dictionary<int, string> _readyPlayerNames = new();

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
		Rpc(MethodName.NameRPC, newText);
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void NameRPC(string name)
	{
		if (!GenericCore.Instance.IsServer) return;
		PlayerName     = name;
		NameEntry.Text = name;
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

		OfflinePanel.Visible = false;
		OnlinePanel.Visible  = true;

		if (GenericCore.Instance.IsServer)
			ServerRegisterPlayer(1, ClassChoice, PlayerName);
		else
			RpcId(1, MethodName.ClientSendReady, ClassChoice, PlayerName);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientSendReady(int classChoice, string playerName)
	{
		if (!GenericCore.Instance.IsServer) return;
		ServerRegisterPlayer(Multiplayer.GetRemoteSenderId(), classChoice, playerName);
	}

	private async void ServerRegisterPlayer(long peerId, int classChoice, string playerName)
	{
		_readyPlayers[(int)peerId]     = classChoice;
		_readyPlayerNames[(int)peerId] = playerName.Length > 0 ? playerName : "Player";

		GD.Print($"[MainMenuLobby] Registered peer {peerId} class={classChoice} ({_readyPlayers.Count}/{RequiredPlayers})");
		GD.Print($"[MainMenuLobby] _readyPlayers keys: {string.Join(", ", _readyPlayers.Keys)}");

		Rpc(MethodName.UpdateCountLabel, _readyPlayers.Count, RequiredPlayers);

		if (_readyPlayers.Count >= RequiredPlayers)
		{
			GD.Print("[MainMenuLobby] FIRING StartGame RPC now!");

			// Wait one frame so all peers finish processing before StartGame fires
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

			Rpc(MethodName.StartGame);
		}
		else
		{
			GD.Print($"[MainMenuLobby] Not enough players yet: {_readyPlayers.Count} / {RequiredPlayers}");
		}
	}



	// ── StartGame RPC ─────────────────────────────────────────────────────────
private bool _gameStarted = false;
[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
	 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
private void StartGame()
{
	if (_gameStarted) return;
	_gameStarted = true;

	GD.Print($"[MainMenuLobby] StartGame fired on peer {Multiplayer.GetUniqueId()}");

	// Hide menu panels
	OfflinePanel.Visible = false;
	OnlinePanel.Visible  = false;
	Visible = false;

	// Navigate up: MainMenu → GameRoot → AbsoluteRoot
	var gameRoot     = GetParent();           // GameRoot (Node)
	var absoluteRoot = gameRoot.GetParent();  // AbsoluteRoot (Node)

	// Hide the generic lobby CanvasLayer (sibling of GameRoot under AbsoluteRoot)
	var genericLobby = absoluteRoot.GetNodeOrNull<CanvasLayer>("GenericLobbySystem");
	if (genericLobby != null)
		genericLobby.Visible = false;
	else
		GD.PrintErr("[MainMenuLobby] GenericLobbySystem not found under AbsoluteRoot!");

	// Show MainGame (sibling of MainMenu under GameRoot) and activate its camera
	var mainGame = gameRoot.GetNodeOrNull<Node3D>("MainGame");
	if (mainGame != null)
		mainGame.Visible = true;
	else
		GD.PrintErr("[MainMenuLobby] MainGame not found under GameRoot!");

	var camera = gameRoot.GetNodeOrNull<Camera3D>("MainGame/Camera3D");
	if (camera != null)
	{
		camera.MakeCurrent();
		GD.Print("[MainMenuLobby] Camera3D activated.");
	}
	else
		GD.PrintErr("[MainMenuLobby] Camera3D not found at GameRoot/MainGame/Camera3D!");

	if (GenericCore.Instance.IsServer)
		ServerSpawnWithDelay();
}

	// ── Spawn ─────────────────────────────────────────────────────────────────

	private async void ServerSpawnWithDelay()
	{
		await ToSignal(GetTree().CreateTimer(2.0f), SceneTreeTimer.SignalName.Timeout);
		await ServerSpawnCharacters();
	}

	private async Task ServerSpawnCharacters()
	{
		string[] spawnPointNames = { "SpawnPoints0", "SpawnPoints1", "SpawnPoints2", "SpawnPoints3" };

		var characterSpawner = GetTree().Root.FindChild("PlayerSpawner", true, false) as NetworkCore;
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
				GD.PrintErr($"[MainMenuLobby] Spawn point {spawnPointNames[i]} not found, using fallback");

			GD.Print($"[MainMenuLobby] Spawning peer {peerId} class={classChoice} at {pos}");

			var spawnedNode = characterSpawner.NetCreateObject(classChoice, pos, Quaternion.Identity, peerId);

			// Set the display name directly on the server-side node.
			// PlayerDisplayName is in the SceneReplicationConfig (spawn=true, on_change),
			// so the value propagates to all clients automatically on the next sync tick —
			// no RPC race condition with the spawn packet.
			if (spawnedNode is Player spawnedPlayer)
			{
				string displayName = _readyPlayerNames.TryGetValue(peerId, out string n) ? n : "Player";
				spawnedPlayer.PlayerDisplayName = displayName;

				// Belt-and-suspenders: also send via RPC so the name arrives even if
				// the replication sync tick fires before NameLabel is ready on clients.
				await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
				spawnedPlayer.Rpc(Player.MethodName.SetDisplayName, displayName);
			}
			else
			{
				await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
			}
			i++;
		}

		_readyPlayers.Clear();
		_readyPlayerNames.Clear();
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

	// ── Overlay panels ───────────────────────────────────────────────────────────

	public void ShowControlsPanel()
	{
		OfflinePanel.Visible      = false;
		OnlinePanel.Visible       = false;
		if (DevelopersOverlay != null) DevelopersOverlay.Visible = false;
		if (ControlsOverlay   != null) ControlsOverlay.Visible   = true;
	}

	public void ShowDevelopersPanel()
	{
		OfflinePanel.Visible      = false;
		OnlinePanel.Visible       = false;
		if (ControlsOverlay   != null) ControlsOverlay.Visible   = false;
		if (DevelopersOverlay != null) DevelopersOverlay.Visible = true;
	}

	public void ShowMainPanel()
	{
		if (ControlsOverlay   != null) ControlsOverlay.Visible   = false;
		if (DevelopersOverlay != null) DevelopersOverlay.Visible = false;
		OfflinePanel.Visible = true;
		OnlinePanel.Visible  = false;
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