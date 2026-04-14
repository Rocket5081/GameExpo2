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
	[Export] public AudioStreamPlayer MenuMusic;

	// Stats label — wired up in code, no scene export needed
	private RichTextLabel _statsLabel;

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
	private readonly Dictionary<int, int>    _readyRelics      = new();
	private bool _musicShouldPlay = false;

	// ── Per-class stats data ──────────────────────────────────────────────────
	private struct ClassStats
	{
		public string ClassName;
		public string Role;
		public Color  Color;
		public string Damage;
		public string Health;
		public string Ultimate;
		public string Description;
	}

	private static readonly ClassStats[] Stats = new ClassStats[]
	{
		new ClassStats
		{
			ClassName   = "Cowboy",
			Role        = "DPS",
			Color       = new Color("ff4444"),
			Damage      = "★★★★☆  High",
			Health      = "★★☆☆☆  Low",
			Ultimate    = "Triple Shot — fires 3 rapid bullets",
			Description = "High-risk, high-reward gunslinger. Shreds enemies fast but dies quick."
		},
		new ClassStats
		{
			ClassName   = "Pirate",
			Role        = "Tank",
			Color       = new Color("4488ff"),
			Damage      = "★★☆☆☆  Low",
			Health      = "★★★★★  Very High",
			Ultimate    = "Bubble Shield — blocks all incoming damage",
			Description = "Soaks damage for the team. Slow to kill but slow to deal damage."
		},
		new ClassStats
		{
			ClassName   = "Priest",
			Role        = "Support",
			Color       = new Color("44cc66"),
			Damage      = "★★★☆☆  Medium",
			Health      = "★★★☆☆  Medium",
			Ultimate    = "Laser Beam — sustained beam damages all enemies in line",
			Description = "Keeps allies alive with relic healing. Versatile and team-focused."
		},
	};

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

		if (MenuMusic != null && MenuMusic.Stream != null)
		{
			var loopedStream = (AudioStream)MenuMusic.Stream.Duplicate();
			if (loopedStream is AudioStreamMP3 mp3)
				mp3.Loop = true;
			else if (loopedStream is AudioStreamOggVorbis ogg)
				ogg.Loop = true;
			MenuMusic.Stream = loopedStream;
			_musicShouldPlay = true;
			MenuMusic.Play();
		}

		OfflinePanel.Visible = true;
		OnlinePanel.Visible  = false;

		BuildStatsLabel();
		RefreshClassDisplay(0);

		if (PlayerCountLabel != null)
			PlayerCountLabel.Text = $"Players: 0 / {RequiredPlayers}";
	}

	// ── Build the stats label and attach it to the preview area ──────────────
	private void BuildStatsLabel()
	{
		_statsLabel = new RichTextLabel();
		_statsLabel.BbcodeEnabled   = true;
		_statsLabel.FitContent      = true;
		_statsLabel.ScrollActive    = false;
		_statsLabel.MouseFilter     = MouseFilterEnum.Ignore;

		// Position it at the bottom of the CharacterPreviewArea
		_statsLabel.LayoutMode      = 1;
		_statsLabel.AnchorLeft      = 0f;
		_statsLabel.AnchorTop       = 0.72f;
		_statsLabel.AnchorRight     = 1f;
		_statsLabel.AnchorBottom    = 1f;

		// Semi-transparent dark background panel via stylebox
		var style = new StyleBoxFlat();
		style.BgColor              = new Color(0f, 0f, 0f, 0.55f);
		style.CornerRadiusTopLeft  = 8;
		style.CornerRadiusTopRight = 8;
		style.ContentMarginLeft    = 14f;
		style.ContentMarginRight   = 14f;
		style.ContentMarginTop     = 10f;
		style.ContentMarginBottom  = 10f;
		_statsLabel.AddThemeStyleboxOverride("normal", style);
		_statsLabel.AddThemeFontSizeOverride("normal_font_size", 15);

		CharacterPreviewArea.AddChild(_statsLabel);
	}

	// ── Write BBCode stats for the selected class ─────────────────────────────
	private void UpdateStatsLabel(int classIndex)
	{
		if (_statsLabel == null) return;

		var s   = Stats[classIndex];
		var hex = s.Color.ToHtml(false);

		_statsLabel.Text = 
			$"[color=#{hex}][b]{s.ClassName}[/b]  —  {s.Role}[/color]\n" +
			$"[color=white]⚔  Damage   [/color][color=orange]{s.Damage}[/color]\n" +
			$"[color=white]❤  Health    [/color][color=orange]{s.Health}[/color]\n" +
			$"[color=white]⚡  Ultimate  [/color][color=#{hex}]{s.Ultimate}[/color]\n" +
			$"[color=gray][i]{s.Description}[/i][/color]";
	}

	public override void _ExitTree()
	{
		if (NameEntry != null)     NameEntry.TextChanged      -= OnNameChanged;
		if (ClassDropdown != null) ClassDropdown.ItemSelected -= OnClassSelected;
		if (ItemDropdown != null)  ItemDropdown.ItemSelected  -= OnItemSelected;
		if (ConnectButton != null) ConnectButton.Pressed      -= OnConnectPressed;
		_musicShouldPlay = false;
	}

	public override void _Process(double delta)
	{
		if (_musicShouldPlay)
		{
			var gameRoot = GetParent();
			var mainGame = gameRoot?.GetNodeOrNull<Node3D>("MainGame");
			if (mainGame != null && mainGame.Visible)
			{
				_musicShouldPlay = false;
				MenuMusic?.Stop();
				return;
			}

			if (MenuMusic != null && !MenuMusic.Playing)
				MenuMusic.Play();
		}
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
		UpdateStatsLabel(classIndex);
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
			ServerRegisterPlayer(1, ClassChoice, PlayerName, ItemChoice);
		else
			RpcId(1, MethodName.ClientSendReady, ClassChoice, PlayerName, ItemChoice);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientSendReady(int classChoice, string playerName, int itemChoice)
	{
		if (!GenericCore.Instance.IsServer) return;
		ServerRegisterPlayer(Multiplayer.GetRemoteSenderId(), classChoice, playerName, itemChoice);
	}

	private async void ServerRegisterPlayer(long peerId, int classChoice, string playerName, int itemChoice)
	{
		_readyPlayers[(int)peerId]     = classChoice;
		_readyPlayerNames[(int)peerId] = playerName.Length > 0 ? playerName : "Player";
		_readyRelics[(int)peerId]      = itemChoice;

		GD.Print($"[MainMenuLobby] Registered peer {peerId} class={classChoice} ({_readyPlayers.Count}/{RequiredPlayers})");
		GD.Print($"[MainMenuLobby] _readyPlayers keys: {string.Join(", ", _readyPlayers.Keys)}");

		Rpc(MethodName.UpdateCountLabel, _readyPlayers.Count, RequiredPlayers);

		if (_readyPlayers.Count >= RequiredPlayers)
		{
			GD.Print("[MainMenuLobby] FIRING StartGame RPC now!");
			Rpc(MethodName.StopMenuMusicRpc);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Rpc(MethodName.StartGame);
		}
		else
		{
			GD.Print($"[MainMenuLobby] Not enough players yet: {_readyPlayers.Count} / {RequiredPlayers}");
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void StopMenuMusicRpc()
	{
		_musicShouldPlay = false;
		MenuMusic?.Stop();
		GD.Print($"[MainMenuLobby] Menu music stopped on peer {Multiplayer.GetUniqueId()}");
	}

	private bool _gameStarted = false;

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void StartGame()
	{
		if (_gameStarted) return;
		_gameStarted = true;

		GD.Print($"[MainMenuLobby] StartGame fired on peer {Multiplayer.GetUniqueId()}");

		OfflinePanel.Visible = false;
		OnlinePanel.Visible  = false;
		Visible = false;

		var gameRoot     = GetParent();
		var absoluteRoot = gameRoot.GetParent();

		var genericLobby = absoluteRoot.GetNodeOrNull<CanvasLayer>("GenericLobbySystem");
		if (genericLobby != null)
			genericLobby.Visible = false;
		else
			GD.PrintErr("[MainMenuLobby] GenericLobbySystem not found under AbsoluteRoot!");

		_musicShouldPlay = false;
		MenuMusic?.Stop();

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

			if (spawnedNode is Player spawnedPlayer)
			{
				string displayName = _readyPlayerNames.TryGetValue(peerId, out string n) ? n : "Player";
				int    relicChoice = _readyRelics.TryGetValue(peerId, out int r) ? r : 0;

				spawnedPlayer.PlayerDisplayName = displayName;

				int relicEnumValue = relicChoice + 1;
				spawnedPlayer.SyncRelicChosen(relicEnumValue);

				await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);

				spawnedPlayer.Rpc(Player.MethodName.SetDisplayName, displayName);
				spawnedPlayer.Rpc("SyncRelicChosen", relicEnumValue);
			}
			else
			{
				await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
			}
			i++;
		}

		_readyPlayers.Clear();
		_readyPlayerNames.Clear();
		_readyRelics.Clear();
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

	// ── Overlay panels ────────────────────────────────────────────────────────

	public void ShowControlsPanel()
	{
		OfflinePanel.Visible = false;
		OnlinePanel.Visible  = false;
		if (DevelopersOverlay != null) DevelopersOverlay.Visible = false;
		if (ControlsOverlay   != null) ControlsOverlay.Visible   = true;
	}

	public void ShowDevelopersPanel()
	{
		OfflinePanel.Visible = false;
		OnlinePanel.Visible  = false;
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
