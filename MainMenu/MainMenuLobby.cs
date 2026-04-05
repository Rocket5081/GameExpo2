using Godot;
using System.Threading.Tasks;

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

	public string PlayerName  = "Player";
	public int    ClassChoice = 0;   // 0=Cowboy(DPS)  1=Pirate(Tank)  2=Priest(Support)
	public int    ItemChoice  = 0;   // 0=Relic of Health  1=Relic of Cooldown
	public int    LastPort    = 0;
	public int    HP          = 100;
	public int    Score       = 0;

	private static readonly Color ColorCowboy = new Color("ff4444");
	private static readonly Color ColorPirate = new Color("4488ff");
	private static readonly Color ColorPriest = new Color("44cc66");

	// Tracks which spawn slot the next connecting player gets
	private static int _spawnCounter = 0;

	// ── Ready ────────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		_spawnCounter = 0;

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
	}

	// ── Name / Class / Item ──────────────────────────────────────────────────

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
		GD.Print($"[MainMenuLobby] Relic selected: {ItemChoice}");
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

	// ── Connect button ───────────────────────────────────────────────────────

	private void OnConnectPressed()
	{
		if (!GenericCore.Instance.IsGenericCoreConnected)
		{
			GD.PrintErr("[MainMenuLobby] Not connected to a game server yet! Join a game from the lobby first.");
			return;
		}

		ConnectButton.Disabled = true;
		ConnectButton.Text = "Joining…";

		// Tell the server to spawn us with our chosen class
		RpcId(1, MethodName.RequestSpawn, ClassChoice);
		GD.Print($"[MainMenuLobby] Requesting spawn — Class:{ClassChoice}");
	}

	// ── Server: receives spawn request from a client ─────────────────────────

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RequestSpawn(int classChoice)
	{
		if (!GenericCore.Instance.IsServer) return;

		int peerId    = Multiplayer.GetRemoteSenderId();
		int spawnSlot = _spawnCounter;
		_spawnCounter++;

		GD.Print($"[MainMenuLobby] Server: spawning peer {peerId} at slot {spawnSlot} with class {classChoice}");

		// Tell ALL clients to load MainGame — server does NOT change scene
		Rpc(MethodName.LoadMainGameOnClient);

		// Server spawns the player after a short delay to let clients load
		_ = SpawnAfterDelay(peerId, classChoice, spawnSlot);
	}

	// ── Client: load the MainGame scene ──────────────────────────────────────

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void LoadMainGameOnClient()
	{
		// Only clients run this — server never changes scene
		if (GenericCore.Instance.IsServer) return;

		GD.Print("[MainMenuLobby] Client loading MainGame scene...");
		GetTree().ChangeSceneToFile("res://MainGame/MainGame.tscn");
	}

	// ── Server: load MainGame additively (once) then spawn the player ─────────

	private async Task SpawnAfterDelay(int peerId, int classChoice, int spawnSlot)
	{
		// Load MainGame additively on the server if it hasn't been loaded yet
		if (GetTree().Root.FindChild("PlayerSpawns", true, false) == null)
		{
			GD.Print("[MainMenuLobby] Server loading MainGame additively...");
			var mainGameScene    = GD.Load<PackedScene>("res://MainGame/MainGame.tscn");
			var mainGameInstance = mainGameScene.Instantiate();
			GetTree().Root.AddChild(mainGameInstance);
		}

		// Give clients time to finish loading their scene before we spawn
		await ToSignal(GetTree().CreateTimer(2.0f), SceneTreeTimer.SignalName.Timeout);

		string[] classPrefabs =
		{
			"res://Player/dps_player.tscn",     // 0 = Cowboy / DPS
			"res://Player/tank_player.tscn",    // 1 = Pirate / Tank
			"res://Player/support_player.tscn"  // 2 = Priest / Support
		};

		Node spawnRoot = GetTree().Root.FindChild("PlayerSpawns", true, false);
		if (spawnRoot == null)
		{
			GD.PrintErr("[MainMenuLobby] FATAL: PlayerSpawns still not found after loading MainGame!");
			return;
		}

		// Clamp so we never go out of bounds if somehow more than 4 players join
		int clampedSlot   = Mathf.Clamp(spawnSlot, 0, spawnRoot.GetChildCount() - 1);
		Node3D spawnPoint = spawnRoot.GetChild<Node3D>(clampedSlot);

		var scene  = GD.Load<PackedScene>(classPrefabs[classChoice]);
		var player = scene.Instantiate<Node3D>();

		// Give authority to the peer who pressed Connect
		player.SetMultiplayerAuthority(peerId);
		player.GlobalPosition = spawnPoint.GlobalPosition;

		// Add to PlayerSpawns — MultiplayerSpawner replicates this to all clients
		spawnRoot.AddChild(player, true);

		GD.Print($"[MainMenuLobby] Spawned peer {peerId} (class {classChoice}) at SpawnPoints{clampedSlot}");
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

	// ── Process ───────────────────────────────────────────────────────────────

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (GenericCore.Instance != null && GenericCore.Instance.IsServer)
		{
			OfflinePanel.Visible = false;
			OnlinePanel.Visible  = false;
		}
	}
}
