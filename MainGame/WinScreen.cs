using Godot;
using System.Collections.Generic;

/// <summary>
/// Full-screen win overlay.  Lives as a child of MainGame, hidden by default.
/// Call ShowFor() when the boss dies; HideScreen() when returning to lobby.
/// </summary>
public partial class WinScreen : CanvasLayer
{
	public static WinScreen Instance { get; private set; }

	private VBoxContainer _scoreTable;
	private Button        _returnBtn;
	private Label         _waitLabel;
	private bool          _clicked = false;

	public override void _Ready()
	{
		Instance = this;
		Layer    = 20;
		Visible  = false;
		BuildUI();
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

	// ── Public API ────────────────────────────────────────────────────────────

	public void ShowFor(Godot.Collections.Array<Node> players)
	{
		_clicked            = false;
		_returnBtn.Disabled = false;
		_returnBtn.Text     = "Return to Lobby";
		_waitLabel.Text     = "Waiting for all players to leave…";

		// Capture the final game duration before showing the screen.
		ulong startTick   = MainGame.Instance?.GetNodeOrNull<HUD>("HUD")?.GameStartTick ?? 0;
		ulong durationMs  = startTick > 0 ? Time.GetTicksMsec() - startTick : 0;

		PopulateScoreboard(players, durationMs);
		Visible             = true;
		Input.MouseMode     = Input.MouseModeEnum.Visible;
	}

	public void HideScreen()
	{
		Visible = false;
	}

	// ── UI construction (runs once in _Ready) ─────────────────────────────────

	private void BuildUI()
	{
		// Dark backdrop
		var bg = new ColorRect();
		bg.Color      = new Color(0.01f, 0f, 0.06f, 0.95f);
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(bg);

		// Full-screen vertical layout
		var root = new VBoxContainer();
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddThemeConstantOverride("separation", 18);
		root.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(root);

		// ── Title ─────────────────────────────────────────────────────────────
		var title = new Label();
		title.Text = "✦  YOU WIN!  ✦";
		title.AddThemeFontSizeOverride("font_size", 68);
		title.AddThemeColorOverride("font_color",         new Color(1f, 0.85f, 0.2f));
		title.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 1f));
		title.AddThemeConstantOverride("outline_size", 4);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		title.CustomMinimumSize   = new Vector2(0, 100);
		root.AddChild(title);

		var sub = new Label();
		sub.Text = "The boss has been slain — your legend is sealed.";
		sub.AddThemeFontSizeOverride("font_size", 20);
		sub.AddThemeColorOverride("font_color", new Color(0.75f, 0.65f, 1f));
		sub.HorizontalAlignment = HorizontalAlignment.Center;
		sub.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		root.AddChild(sub);

		// ── Scoreboard panel ──────────────────────────────────────────────────
		var panel = new PanelContainer();
		panel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		panel.CustomMinimumSize   = new Vector2(760, 0);
		var ps = new StyleBoxFlat();
		ps.BgColor                 = new Color(0.05f, 0.02f, 0.12f, 0.92f);
		ps.BorderColor             = new Color(1f, 0.85f, 0.2f, 0.7f);
		ps.BorderWidthTop          = 2; ps.BorderWidthBottom = 2;
		ps.BorderWidthLeft         = 2; ps.BorderWidthRight  = 2;
		ps.CornerRadiusTopLeft     = 8; ps.CornerRadiusTopRight    = 8;
		ps.CornerRadiusBottomLeft  = 8; ps.CornerRadiusBottomRight = 8;
		panel.AddThemeStyleboxOverride("panel", ps);
		root.AddChild(panel);

		// _scoreTable is populated dynamically in PopulateScoreboard()
		_scoreTable = new VBoxContainer();
		_scoreTable.AddThemeConstantOverride("separation", 4);
		panel.AddChild(_scoreTable);

		// ── Status / waiting label ─────────────────────────────────────────────
		_waitLabel = new Label();
		_waitLabel.Text = "Waiting for all players to leave…";
		_waitLabel.AddThemeFontSizeOverride("font_size", 16);
		_waitLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.8f));
		_waitLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_waitLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		root.AddChild(_waitLabel);

		// ── Return button ──────────────────────────────────────────────────────
		var btnWrap = new HBoxContainer();
		btnWrap.Alignment           = BoxContainer.AlignmentMode.Center;
		btnWrap.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		root.AddChild(btnWrap);

		_returnBtn = new Button();
		_returnBtn.Text             = "Return to Lobby";
		_returnBtn.AddThemeFontSizeOverride("font_size", 20);
		_returnBtn.CustomMinimumSize = new Vector2(260, 44);
		btnWrap.AddChild(_returnBtn);

		_returnBtn.Pressed += OnReturnPressed;
	}

	private void OnReturnPressed()
	{
		if (_clicked) return;
		_clicked            = true;
		_returnBtn.Disabled = true;
		_returnBtn.Text     = "Waiting for others…";
		_waitLabel.Text     = "You're ready — waiting for other players…";

		// Send confirmation to the server.  If WE are the server, call directly
		// (Rpc() without an id excludes self, so the server would never count itself).
		if (GenericCore.Instance.IsServer)
			GenericCore.Instance.PlayerReadyToLeave();
		else if (Multiplayer.HasMultiplayerPeer())
			GenericCore.Instance.RpcId(1, "PlayerReadyToLeave");
		else
			GenericCore.Instance.PlayerReadyToLeave();   // offline / solo
	}

	// ── Scoreboard population (called each time ShowFor fires) ────────────────

	private void PopulateScoreboard(Godot.Collections.Array<Node> players, ulong durationMs)
	{
		// Clear any rows from a previous show
		foreach (Node child in _scoreTable.GetChildren())
			child.QueueFree();

		// Header row
		var gold = new Color(1f, 0.85f, 0.2f);
		var hdr  = new HBoxContainer();
		hdr.AddThemeConstantOverride("separation", 8);
		hdr.AddChild(MakeCell("PLAYER", 15, gold));
		hdr.AddChild(MakeCell("CLASS",  15, gold));
		hdr.AddChild(MakeCell("SCORE",  15, gold));
		hdr.AddChild(MakeCell("KILLS",  15, gold));
		hdr.AddChild(MakeCell("DEATHS", 15, gold));
		_scoreTable.AddChild(hdr);

		var div = new ColorRect();
		div.Color               = new Color(1f, 0.85f, 0.2f, 0.4f);
		div.CustomMinimumSize   = new Vector2(0, 2);
		div.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		_scoreTable.AddChild(div);

		// Collect & sort by score descending
		var sorted = new List<Player>();
		foreach (Node n in players)
			if (n is Player p) sorted.Add(p);
		sorted.Sort((a, b) => b.Score.CompareTo(a.Score));

		var white  = new Color(1f, 1f, 1f);
		var dimmed = new Color(0.75f, 0.70f, 0.85f);
		int rank   = 0;

		foreach (Player p in sorted)
		{
			rank++;
			string cls = p switch
			{
				DpsPlayer     => "DPS",
				TankPlayer    => "Tank",
				SupportPlayer => "Support",
				_             => "???",
			};
			string name     = p.PlayerDisplayName.Length > 0 ? p.PlayerDisplayName : $"Player {rank}";
			Color  rowColor = rank == 1 ? gold : (rank % 2 == 0 ? dimmed : white);

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);
			row.AddChild(MakeCell(name,                14, rowColor));
			row.AddChild(MakeCell(cls,                 14, rowColor));
			row.AddChild(MakeCell(p.Score.ToString(),  14, rowColor));
			row.AddChild(MakeCell(p.Kills.ToString(),  14, rowColor));
			row.AddChild(MakeCell(p.Deaths.ToString(), 14, rowColor));
			_scoreTable.AddChild(row);
		}

		// ── Clear time row ────────────────────────────────────────────────────────
		var timeDivider = new ColorRect();
		timeDivider.Color               = new Color(0.6f, 0.5f, 1f, 0.35f);
		timeDivider.CustomMinimumSize   = new Vector2(0, 2);
		timeDivider.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		_scoreTable.AddChild(timeDivider);

		int totalSec = (int)(durationMs / 1000UL);
		int mins     = totalSec / 60;
		int secs     = totalSec % 60;
		string timeStr = $"{mins}:{secs:00}";

		var timeRow = new HBoxContainer();
		timeRow.Alignment           = BoxContainer.AlignmentMode.Center;
		timeRow.AddThemeConstantOverride("separation", 12);
		timeRow.SizeFlagsHorizontal = Control.SizeFlags.Fill;

		var clockIcon = new Label();
		clockIcon.Text = "⏱";
		clockIcon.AddThemeFontSizeOverride("font_size", 18);
		clockIcon.AddThemeColorOverride("font_color", new Color(0.7f, 0.9f, 1f));
		timeRow.AddChild(clockIcon);

		var timeLabel = new Label();
		timeLabel.Text = "CLEAR TIME";
		timeLabel.AddThemeFontSizeOverride("font_size", 15);
		timeLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.9f, 1f));
		timeRow.AddChild(timeLabel);

		var timeValue = new Label();
		timeValue.Text = timeStr;
		timeValue.AddThemeFontSizeOverride("font_size", 20);
		timeValue.AddThemeColorOverride("font_color",         new Color(0.55f, 1f, 0.85f));
		timeValue.AddThemeColorOverride("font_outline_color", new Color(0f, 0.1f, 0.2f));
		timeValue.AddThemeConstantOverride("outline_size", 3);
		timeValue.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		timeValue.HorizontalAlignment = HorizontalAlignment.Center;
		timeRow.AddChild(timeValue);

		_scoreTable.AddChild(timeRow);
	}

	private static Label MakeCell(string text, int fontSize, Color col)
	{
		var l = new Label();
		l.Text                = text;
		l.AddThemeFontSizeOverride("font_size", fontSize);
		l.AddThemeColorOverride("font_color",   col);
		l.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		l.HorizontalAlignment = HorizontalAlignment.Center;
		return l;
	}
}
