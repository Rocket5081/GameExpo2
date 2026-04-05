using Godot;
using System;
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

	public override void _Ready()
	{
		// Populate class dropdown
		ClassDropdown.Clear();
		ClassDropdown.AddItem("Cowboy  (DPS)");
		ClassDropdown.AddItem("Pirate  (Tank)");
		ClassDropdown.AddItem("Priest  (Support)");
		ClassDropdown.Selected = 0;

		// Populate item/relic dropdown
		ItemDropdown.Clear();
		ItemDropdown.AddItem("Relic of Health");
		ItemDropdown.AddItem("Relic of Cooldown");
		ItemDropdown.Selected = 0;

		// Connect signals
		NameEntry.TextChanged      += OnNameChanged;
		ClassDropdown.ItemSelected += OnClassSelected;
		ItemDropdown.ItemSelected  += OnItemSelected;
		ConnectButton.Pressed      += OnConnectPressed;

		// Start with offline panel visible
		OfflinePanel.Visible = true;
		OnlinePanel.Visible  = false;

		// Show default preview
		RefreshClassDisplay(0);
	}

	
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

	private void OnItemSelected(long index)
	{
		ItemChoice = (int)index;
		GD.Print($"[MainMenuLobby] Relic selected: {ItemChoice}");
	}

	
	private void OnConnectPressed()
	{
		_ = TaskConnect();
	}

	private async Task TaskConnect()
	{
		ConnectButton.Disabled = true;
		ConnectButton.Text = "Connecting…";

		await GenericCore.Instance.JoinWan();

		if (GenericCore.Instance.IsGenericCoreConnected)
		{
			OfflinePanel.Visible = false;
			OnlinePanel.Visible  = true;

			// Tell the server this player's class and relic choice
			// RpcId(1, ...) sends only to the server (peer ID 1)
			RpcId(1, MethodName.RegisterPlayerData, ClassChoice, ItemChoice);
			GD.Print($"[MainMenuLobby] Sent RegisterPlayerData — Class:{ClassChoice} Relic:{ItemChoice}");
		}
		else
		{
			ConnectButton.Disabled = false;
			ConnectButton.Text = "Connect";
			GD.PrintErr("[MainMenuLobby] Failed to connect.");
		}
	}

	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RegisterPlayerData(int classChoice, int relicChoice)
	{
		// Only the server should process this
		if (!GenericCore.Instance.IsServer) return;

		int senderId = Multiplayer.GetRemoteSenderId();
		GD.Print($"[MainMenuLobby] Server received data from peer {senderId} — Class:{classChoice} Relic:{relicChoice}");

		// Hand off to GameStarter which tracks readiness and launches the game
		GameStarter.Instance.RegisterPlayer(senderId, classChoice, relicChoice);
	}

	
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
