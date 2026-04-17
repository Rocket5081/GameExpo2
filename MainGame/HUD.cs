using Godot;


public partial class HUD : CanvasLayer
{
	// ── Reticles ──────────────────────────────────────────────────────────────
	private Control _dpsReticle;
	private Control _tankReticle;
	private Control _supportReticle;

	private TextureRect scorebg;

	private TextureRect healthborder;
	private bool _reticleSet = false;

	// ── Shared local-player reference ─────────────────────────────────────────
	private Player _localPlayer;

	// ── Timer ─────────────────────────────────────────────────────────────────
	private ulong _startTickMsec = 0;
	private bool  _gameStartSent = false;
	private Label _timerLabel;
	private Panel _timerPanel;

	// ── HP bar ────────────────────────────────────────────────────────────────
	private Panel        _hpPanel;
	private ProgressBar  _hpBar;
	private StyleBoxFlat _hpFill;
	private Label        _hpLabel;

	// ── Relic system ──────────────────────────────────────────────────────────
	private Panel  _relicIcon;
	private Label  _relicIconLabel;
	private Tween  _relicGlowTween;

	// ── Ultimate cooldown widget ──────────────────────────────────────────────
	private Panel        _ultPanel;
	private Label        _ultTitleLabel;
	private ProgressBar  _ultBar;
	private Label        _ultTimeLabel;

	// ── Score widget ──────────────────────────────────────────────────────────
	private Panel  _scorePanel;
	private Label  _scoreValueLabel;
	private Label  _multLabel;
	private Tween  _multPulseTween;
	private float  _lastMultiplier = 1f;

	// ── Round info widget — middle-left ──────────────────────────────────────
	private Panel        _roundPanel;
	private Label        _roundLabel;
	private ProgressBar  _enemyBar;
	private StyleBoxFlat _enemyBarFill;
	private Label        _enemyCountLabel;

	// ── Boss HP widget — top-centre ───────────────────────────────────────────
	private Panel        _bossHpPanel;
	private Label        _bossNameLabel;
	private ProgressBar  _bossHpBar;
	private StyleBoxFlat _bossHpFill;
	private Label        _bossHpValueLabel;
	private Tween        _bossPulseTween;

	// Class accent colours
	private static readonly Color ColDps     = new Color("ff4444");
	private static readonly Color ColTank    = new Color("4488ff");
	private static readonly Color ColSupport = new Color("44cc66");

	// ─────────────────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		_dpsReticle     = GetNode<Control>("DpsReticle");
		_tankReticle    = GetNode<Control>("TankReticle");
		_supportReticle = GetNode<Control>("SupportReticle");
		scorebg = GetNode<TextureRect>("ScorePanel");
		healthborder = GetNode<TextureRect>("HealthBar");
		healthborder.ZIndex = 1;  // above HP bar panel

		_dpsReticle.Visible     = false;
		_tankReticle.Visible    = false;
		_supportReticle.Visible = false;

		BuildTimerWidget();
		BuildCooldownWidget();
		BuildHPWidget();
		BuildRelicIcon();
		BuildScoreWidget();
		BuildRoundWidget();
		BuildBossHpWidget();
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Score widget — top-left
	// ─────────────────────────────────────────────────────────────────────────
	private void BuildScoreWidget()
	{
		var root = new Control();
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(root);

		const float W = 160f, H = 70f, Margin = 16f;
		_scorePanel = new Panel();
		_scorePanel.AnchorLeft   = 0f; _scorePanel.AnchorTop    = 0f;
		_scorePanel.AnchorRight  = 0f; _scorePanel.AnchorBottom = 0f;
		_scorePanel.OffsetLeft   = Margin+10f;
		_scorePanel.OffsetTop    = Margin+10f;
		_scorePanel.OffsetRight  = Margin + W;
		_scorePanel.OffsetBottom = Margin + H;
		_scorePanel.MouseFilter  = Control.MouseFilterEnum.Ignore;
		_scorePanel.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0f, 0f, 0f, 0.52f)));
		_scorePanel.Visible = false;   // shown once local player found
		root.AddChild(_scorePanel);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 2);
		vbox.OffsetLeft = 10; vbox.OffsetRight  = -10;
		vbox.OffsetTop  =  6; vbox.OffsetBottom = -6;
		_scorePanel.AddChild(vbox);

		// "SCORE" title
		var title = new Label();
		title.Text = "SCORE";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 10);
		title.Modulate = new Color(1f, 1f, 1f, 0.55f);
		vbox.AddChild(title);

		// Big score number
		_scoreValueLabel = new Label();
		_scoreValueLabel.Text = "0";
		_scoreValueLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_scoreValueLabel.AddThemeFontSizeOverride("font_size", 24);
		_scoreValueLabel.Modulate = new Color(1f, 1f, 0.6f, 1f);
		vbox.AddChild(_scoreValueLabel);

		// Multiplier badge  e.g. "× 1.0"
		_multLabel = new Label();
		_multLabel.Text = "× 1.0";
		_multLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_multLabel.AddThemeFontSizeOverride("font_size", 13);
		_multLabel.Modulate = new Color(1f, 1f, 1f, 0.7f);
		vbox.AddChild(_multLabel);
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Timer widget — top-right
	// ─────────────────────────────────────────────────────────────────────────
	private void BuildTimerWidget()
	{
		var root = new Control();
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(root);

		const float W = 130f, H = 50f, Margin = 16f;
		_timerPanel = new Panel();
		_timerPanel.AnchorLeft   = 1f; _timerPanel.AnchorTop    = 0f;
		_timerPanel.AnchorRight  = 1f; _timerPanel.AnchorBottom = 0f;
		_timerPanel.OffsetLeft   = -(W + Margin);
		_timerPanel.OffsetTop    = Margin;
		_timerPanel.OffsetRight  = -Margin;
		_timerPanel.OffsetBottom = H + Margin;
		_timerPanel.MouseFilter  = Control.MouseFilterEnum.Ignore;
		_timerPanel.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0f, 0f, 0f, 0.52f)));
		_timerPanel.Visible = false;
		root.AddChild(_timerPanel);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 0);
		vbox.OffsetLeft = 8; vbox.OffsetRight = -8;
		vbox.OffsetTop  = 6; vbox.OffsetBottom = -6;
		_timerPanel.AddChild(vbox);

		var title = new Label();
		title.Text = "TIME";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 10);
		title.Modulate = new Color(1f, 1f, 1f, 0.55f);
		vbox.AddChild(title);

		_timerLabel = new Label();
		_timerLabel.Text = "0:00";
		_timerLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_timerLabel.AddThemeFontSizeOverride("font_size", 22);
		_timerLabel.Modulate = new Color(1f, 1f, 0.6f, 1f);
		vbox.AddChild(_timerLabel);
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  HP bar — bottom-center
	// ─────────────────────────────────────────────────────────────────────────
	private void BuildHPWidget()
	{
		var root = new Control();
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(root);

		const float W = 280f, H = 55f, BottomMargin = 28f;
		_hpPanel = new Panel();
		_hpPanel.AnchorLeft   = 0.5f; _hpPanel.AnchorTop    = 1f;
		_hpPanel.AnchorRight  = 0.5f; _hpPanel.AnchorBottom = 1f;
		_hpPanel.OffsetLeft   = -(W / 2f);
		_hpPanel.OffsetTop    = -(H + BottomMargin);
		_hpPanel.OffsetRight  =  (W / 2f);
		_hpPanel.OffsetBottom = -BottomMargin;
		_hpPanel.MouseFilter  = Control.MouseFilterEnum.Ignore;
		_hpPanel.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0f, 0f, 0f, 0.55f)));
		root.AddChild(_hpPanel);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 4);
		vbox.OffsetLeft = 14; vbox.OffsetRight = -14;
		vbox.OffsetTop  = 8;  vbox.OffsetBottom = -8;
		_hpPanel.AddChild(vbox);

		_hpLabel = new Label();
		_hpLabel.Text = "HEALTH";
		_hpLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_hpLabel.AddThemeFontSizeOverride("font_size", 11);
		_hpLabel.Modulate = new Color(1f, 1f, 1f, 0.7f);
		vbox.AddChild(_hpLabel);

		_hpFill = MakeBarStyle(new Color(0.2f, 0.85f, 0.2f, 1f));
		_hpBar = new ProgressBar();
		_hpBar.MinValue = 0; _hpBar.MaxValue = 1; _hpBar.Value = 1;
		_hpBar.ShowPercentage    = false;
		_hpBar.CustomMinimumSize = new Vector2(0, 16);
		_hpBar.AddThemeStyleboxOverride("background", MakeBarStyle(new Color(0.12f, 0.12f, 0.12f, 1f)));
		_hpBar.AddThemeStyleboxOverride("fill", _hpFill);
		vbox.AddChild(_hpBar);

		_hpPanel.Visible = false;
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Ultimate cooldown widget — bottom-right
	// ─────────────────────────────────────────────────────────────────────────
	private void BuildCooldownWidget()
	{
		var root = new Control();
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(root);

		const float W = 190f, H = 86f, Margin = 20f;
		_ultPanel = new Panel();
		_ultPanel.AnchorLeft   = 1f; _ultPanel.AnchorTop    = 1f;
		_ultPanel.AnchorRight  = 1f; _ultPanel.AnchorBottom = 1f;
		_ultPanel.OffsetLeft   = -(W + Margin);
		_ultPanel.OffsetTop    = -(H + Margin);
		_ultPanel.OffsetRight  = -Margin;
		_ultPanel.OffsetBottom = -Margin;
		_ultPanel.MouseFilter  = Control.MouseFilterEnum.Ignore;
		_ultPanel.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0f, 0f, 0f, 0.55f)));
		root.AddChild(_ultPanel);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 4);
		vbox.OffsetLeft = 10; vbox.OffsetRight = -10;
		vbox.OffsetTop  = 8;  vbox.OffsetBottom = -8;
		_ultPanel.AddChild(vbox);

		_ultTitleLabel = new Label();
		_ultTitleLabel.Text = "⚡ ULTIMATE  [Q]";
		_ultTitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_ultTitleLabel.AddThemeFontSizeOverride("font_size", 12);
		_ultTitleLabel.Modulate = new Color(1f, 1f, 1f, 0.85f);
		vbox.AddChild(_ultTitleLabel);

		_ultBar = new ProgressBar();
		_ultBar.MinValue = 0.0; _ultBar.MaxValue = 1.0; _ultBar.Value = 1.0;
		_ultBar.ShowPercentage    = false;
		_ultBar.CustomMinimumSize = new Vector2(0, 14);
		_ultBar.AddThemeStyleboxOverride("background", MakeBarStyle(new Color(0.15f, 0.15f, 0.15f, 1f)));
		_ultBar.AddThemeStyleboxOverride("fill",       MakeBarStyle(ColDps));
		vbox.AddChild(_ultBar);

		_ultTimeLabel = new Label();
		_ultTimeLabel.Text = "30.0s";
		_ultTimeLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_ultTimeLabel.AddThemeFontSizeOverride("font_size", 13);
		vbox.AddChild(_ultTimeLabel);

		_ultPanel.Visible = false;
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Relic icon — bottom-right above ult panel
	// ─────────────────────────────────────────────────────────────────────────
	private void BuildRelicIcon()
	{
		var root = new Control();
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(root);

		const float Size = 40f, Margin = 20f, UltH = 86f, Gap = 8f;
		_relicIcon = new Panel();
		_relicIcon.AnchorLeft   = 1f; _relicIcon.AnchorTop    = 1f;
		_relicIcon.AnchorRight  = 1f; _relicIcon.AnchorBottom = 1f;
		_relicIcon.OffsetLeft   = -(Size + Margin);
		_relicIcon.OffsetTop    = -(UltH + Margin + Gap + Size);
		_relicIcon.OffsetRight  = -Margin;
		_relicIcon.OffsetBottom = -(UltH + Margin + Gap);
		_relicIcon.MouseFilter  = Control.MouseFilterEnum.Ignore;

		var iconStyle = MakePanelStyle(new Color(0f, 0f, 0f, 0.65f));
		iconStyle.CornerRadiusTopLeft = iconStyle.CornerRadiusTopRight =
		iconStyle.CornerRadiusBottomLeft = iconStyle.CornerRadiusBottomRight = 10;
		iconStyle.BorderWidthTop = iconStyle.BorderWidthRight =
		iconStyle.BorderWidthBottom = iconStyle.BorderWidthLeft = 2;
		iconStyle.BorderColor = new Color(0.2f, 1f, 0.4f, 0.8f);
		_relicIcon.AddThemeStyleboxOverride("panel", iconStyle);
		root.AddChild(_relicIcon);

		_relicIconLabel = new Label();
		_relicIconLabel.Text = "➕";
		_relicIconLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_relicIconLabel.VerticalAlignment   = VerticalAlignment.Center;
		_relicIconLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_relicIconLabel.AddThemeFontSizeOverride("font_size", 18);
		_relicIcon.AddChild(_relicIconLabel);

		_relicIcon.Visible = false;
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Round info widget — middle-left
	// ─────────────────────────────────────────────────────────────────────────
	private void BuildRoundWidget()
	{
		const float W = 210f, H = 108f, Margin = 18f;

		var style = new StyleBoxFlat();
		style.BgColor                  = new Color(0.04f, 0.01f, 0.12f, 0.90f);
		style.BorderWidthTop           = style.BorderWidthBottom =
		style.BorderWidthLeft          = style.BorderWidthRight  = 2;
		style.BorderColor              = new Color(0.62f, 0.18f, 1f,   1f);
		style.CornerRadiusTopLeft      = style.CornerRadiusTopRight =
		style.CornerRadiusBottomLeft   = style.CornerRadiusBottomRight = 10;
		style.ShadowColor              = new Color(0.45f, 0.08f, 0.95f, 0.70f);
		style.ShadowSize               = 10;

		_roundPanel = new Panel();
		_roundPanel.AnchorLeft   = 0f;  _roundPanel.AnchorRight  = 0f;
		_roundPanel.AnchorTop    = 0.5f; _roundPanel.AnchorBottom = 0.5f;
		_roundPanel.OffsetLeft   = Margin;
		_roundPanel.OffsetRight  = Margin + W;
		_roundPanel.OffsetTop    = -(H * 0.5f);
		_roundPanel.OffsetBottom =  (H * 0.5f);
		_roundPanel.MouseFilter  = Control.MouseFilterEnum.Ignore;
		_roundPanel.AddThemeStyleboxOverride("panel", style);
		_roundPanel.Visible = false;
		AddChild(_roundPanel);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 4);
		vbox.OffsetLeft = 12; vbox.OffsetRight  = -12;
		vbox.OffsetTop  =  8; vbox.OffsetBottom = -8;
		_roundPanel.AddChild(vbox);

		// ⚔  ROUND 1
		_roundLabel = new Label();
		_roundLabel.Text = "⚔  ROUND 1";
		_roundLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_roundLabel.AddThemeFontSizeOverride("font_size", 17);
		_roundLabel.AddThemeColorOverride("font_color",         new Color(1f,   0.9f, 0.3f, 1f));
		_roundLabel.AddThemeColorOverride("font_outline_color", new Color(0.3f, 0f,   0.6f, 1f));
		_roundLabel.AddThemeConstantOverride("outline_size", 3);
		vbox.AddChild(_roundLabel);

		// Thin separator line
		var sep = new ColorRect();
		sep.Color               = new Color(0.62f, 0.18f, 1f, 0.45f);
		sep.CustomMinimumSize   = new Vector2(0, 1);
		sep.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		vbox.AddChild(sep);

		// Enemy kill progress bar
		_enemyBarFill = new StyleBoxFlat();
		_enemyBarFill.BgColor                = new Color(0.1f, 0.85f, 1f, 1f);
		_enemyBarFill.CornerRadiusTopLeft    = _enemyBarFill.CornerRadiusTopRight =
		_enemyBarFill.CornerRadiusBottomLeft = _enemyBarFill.CornerRadiusBottomRight = 4;

		_enemyBar = new ProgressBar();
		_enemyBar.MinValue         = 0; _enemyBar.MaxValue = 1; _enemyBar.Value = 0;
		_enemyBar.ShowPercentage   = false;
		_enemyBar.CustomMinimumSize = new Vector2(0, 12);
		_enemyBar.AddThemeStyleboxOverride("background", MakeBarStyle(new Color(0.1f, 0.1f, 0.2f, 0.8f)));
		_enemyBar.AddThemeStyleboxOverride("fill",       _enemyBarFill);
		vbox.AddChild(_enemyBar);

		// "7 / 15  ENEMIES KILLED" counter
		_enemyCountLabel = new Label();
		_enemyCountLabel.Text = "0 / 0  KILLED";
		_enemyCountLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_enemyCountLabel.AddThemeFontSizeOverride("font_size", 13);
		_enemyCountLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.65f, 1f, 1f));
		vbox.AddChild(_enemyCountLabel);

		// Small label below bar
		var hint = new Label();
		hint.Text = "kill all  →  next round";
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeFontSizeOverride("font_size", 10);
		hint.AddThemeColorOverride("font_color", new Color(0.55f, 0.45f, 0.75f, 0.8f));
		vbox.AddChild(hint);
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Boss HP widget — top-centre
	// ─────────────────────────────────────────────────────────────────────────
	private void BuildBossHpWidget()
	{
		const float W = 440f, H = 72f, TopMargin = 14f;

		var style = new StyleBoxFlat();
		style.BgColor                  = new Color(0.08f, 0.01f, 0.01f, 0.92f);
		style.BorderWidthTop           = style.BorderWidthBottom =
		style.BorderWidthLeft          = style.BorderWidthRight  = 2;
		style.BorderColor              = new Color(1f, 0.25f, 0.08f, 1f);
		style.CornerRadiusTopLeft      = style.CornerRadiusTopRight =
		style.CornerRadiusBottomLeft   = style.CornerRadiusBottomRight = 10;
		style.ShadowColor              = new Color(1f, 0.1f, 0.05f, 0.75f);
		style.ShadowSize               = 14;

		_bossHpPanel = new Panel();
		_bossHpPanel.AnchorLeft   = 0.5f; _bossHpPanel.AnchorRight  = 0.5f;
		_bossHpPanel.AnchorTop    = 0f;   _bossHpPanel.AnchorBottom = 0f;
		_bossHpPanel.OffsetLeft   = -(W * 0.5f);
		_bossHpPanel.OffsetRight  =  (W * 0.5f);
		_bossHpPanel.OffsetTop    = TopMargin;
		_bossHpPanel.OffsetBottom = TopMargin + H;
		_bossHpPanel.MouseFilter  = Control.MouseFilterEnum.Ignore;
		_bossHpPanel.AddThemeStyleboxOverride("panel", style);
		_bossHpPanel.Visible = false;
		AddChild(_bossHpPanel);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 3);
		vbox.OffsetLeft = 14; vbox.OffsetRight  = -14;
		vbox.OffsetTop  =  6; vbox.OffsetBottom = -6;
		_bossHpPanel.AddChild(vbox);

		// ◈  BOSS  ◈  name row
		_bossNameLabel = new Label();
		_bossNameLabel.Text = "◈  THE BOSS  ◈";
		_bossNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_bossNameLabel.AddThemeFontSizeOverride("font_size", 14);
		_bossNameLabel.AddThemeColorOverride("font_color",         new Color(1f,   0.5f, 0.12f, 1f));
		_bossNameLabel.AddThemeColorOverride("font_outline_color", new Color(0.4f, 0f,   0f,    1f));
		_bossNameLabel.AddThemeConstantOverride("outline_size", 3);
		vbox.AddChild(_bossNameLabel);

		// HP bar
		_bossHpFill = new StyleBoxFlat();
		_bossHpFill.BgColor                = new Color(0.95f, 0.18f, 0.05f, 1f);
		_bossHpFill.CornerRadiusTopLeft    = _bossHpFill.CornerRadiusTopRight =
		_bossHpFill.CornerRadiusBottomLeft = _bossHpFill.CornerRadiusBottomRight = 4;

		_bossHpBar = new ProgressBar();
		_bossHpBar.MinValue          = 0; _bossHpBar.MaxValue = 1; _bossHpBar.Value = 1;
		_bossHpBar.ShowPercentage    = false;
		_bossHpBar.CustomMinimumSize = new Vector2(0, 16);
		_bossHpBar.AddThemeStyleboxOverride("background", MakeBarStyle(new Color(0.15f, 0.04f, 0.04f, 1f)));
		_bossHpBar.AddThemeStyleboxOverride("fill",       _bossHpFill);
		vbox.AddChild(_bossHpBar);

		// HP value "1200 / 3000"
		_bossHpValueLabel = new Label();
		_bossHpValueLabel.Text = "";
		_bossHpValueLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_bossHpValueLabel.AddThemeFontSizeOverride("font_size", 12);
		_bossHpValueLabel.AddThemeColorOverride("font_color", new Color(1f, 0.75f, 0.75f, 1f));
		vbox.AddChild(_bossHpValueLabel);
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Per-frame
	// ─────────────────────────────────────────────────────────────────────────
	public override void _Process(double delta)
	{
		// ── Timer ─────────────────────────────────────────────────────────────
		if (!_gameStartSent && GenericCore.Instance != null && GenericCore.Instance.IsServer)
		{
			if (GetTree().GetNodesInGroup("Players").Count > 0)
			{
				_gameStartSent = true;
				Rpc(nameof(SyncGameStart), (long)Time.GetTicksMsec());
			}
		}

		if (_startTickMsec > 0 && _timerLabel != null)
		{
			if (_timerPanel != null && !_timerPanel.Visible)
				_timerPanel.Visible = true;
			int total = (int)((Time.GetTicksMsec() - _startTickMsec) / 1000UL);
			_timerLabel.Text = $"{total / 60}:{total % 60:00}";
		}

		// ── Find local player (once) ──────────────────────────────────────────
		if (!_reticleSet)
		{
			foreach (Node node in GetTree().GetNodesInGroup("Players"))
			{
				if (node is Player player && player.myId != null && player.myId.IsLocal)
				{
					if      (player is DpsPlayer)     { _dpsReticle.Visible     = true; ApplyClassColour(ColDps); }
					else if (player is TankPlayer)    { _tankReticle.Visible    = true; ApplyClassColour(ColTank); }
					else if (player is SupportPlayer) { _supportReticle.Visible = true; ApplyClassColour(ColSupport); }

					_localPlayer       = player;
					_ultPanel.Visible  = true;
					_hpPanel.Visible   = true;
					_scorePanel.Visible = true;
					_reticleSet        = true;
					scorebg.Visible = true;
					healthborder.Visible = true;
					break;
				}
			}
		}

		if (_localPlayer == null || !IsInstanceValid(_localPlayer)) return;

		// ── Ultimate cooldown ─────────────────────────────────────────────────
		float remaining = Mathf.Max(_localPlayer.UltimateCooldownTimer, 0f);
		float maxCd     = _localPlayer.UltimateCooldownMax;
		bool  ready     = remaining <= 0f;

		_ultBar.Value = maxCd > 0f ? remaining / maxCd : 0.0;

		if (ready)
		{
			_ultTimeLabel.Text     = "READY!";
			_ultTimeLabel.Modulate = new Color(1f, 1f, 0.5f, 1f);
		}
		else
		{
			_ultTimeLabel.Text     = $"{remaining:0.0}s";
			_ultTimeLabel.Modulate = new Color(1f, 1f, 1f, 0.8f);
		}

		// ── HP bar ────────────────────────────────────────────────────────────
		float hpFrac = _localPlayer.maxHp > 0
			? Mathf.Clamp((float)_localPlayer.hp / _localPlayer.maxHp, 0f, 1f)
			: 0f;

		_hpBar.Value  = hpFrac;
		_hpLabel.Text = $"HP   {_localPlayer.hp} / {_localPlayer.maxHp}";

		if      (hpFrac > 0.6f) _hpFill.BgColor = new Color(0.18f, 0.85f, 0.18f, 1f);
		else if (hpFrac > 0.3f) _hpFill.BgColor = new Color(0.95f, 0.72f, 0.08f, 1f);
		else                    _hpFill.BgColor = new Color(0.88f, 0.14f, 0.14f, 1f);

		// ── Score widget ──────────────────────────────────────────────────────
		_scoreValueLabel.Text = _localPlayer.Score.ToString("N0");

		float mult = _localPlayer.Multiplier;
		_multLabel.Text = $"× {mult:0.0}";

		// Colour: white at 1×, yellow at 2×, orange at 4×, red at 8×
		if      (mult >= 6f) _multLabel.Modulate = new Color(1f,   0.3f, 0.3f, 1f);
		else if (mult >= 3f) _multLabel.Modulate = new Color(1f,   0.6f, 0.1f, 1f);
		else if (mult >= 2f) _multLabel.Modulate = new Color(1f,   1f,   0.3f, 1f);
		else                 _multLabel.Modulate = new Color(1f,   1f,   1f,   0.7f);

		// Pulse the multiplier label whenever it increases
		if (mult > _lastMultiplier + 0.05f)
		{
			_multPulseTween?.Kill();
			_multPulseTween = CreateTween();
			_multPulseTween.TweenProperty(_multLabel, "scale", new Vector2(1.4f, 1.4f), 0.08f)
						   .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			_multPulseTween.TweenProperty(_multLabel, "scale", Vector2.One, 0.15f)
						   .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		}
		_lastMultiplier = mult;

		// ── Round info widget ─────────────────────────────────────────────────
		UpdateRoundWidget();

		// ── Boss HP widget ─────────────────────────────────────────────────────
		UpdateBossHpWidget();

		// ── Relic icon ────────────────────────────────────────────────────────
		if (_localPlayer.ChosenRelic != Player.RelicType.None && !_relicIcon.Visible)
		{
			bool isHealth = _localPlayer.ChosenRelic == Player.RelicType.Health;

			_relicIconLabel.Text     = isHealth ? "➕" : "⏱";
			_relicIconLabel.Modulate = isHealth
				? new Color(0.2f, 1f, 0.45f, 1f)
				: new Color(1f, 0.85f, 0.2f, 1f);

			var style = MakePanelStyle(new Color(0f, 0f, 0f, 0.65f));
			style.CornerRadiusTopLeft = style.CornerRadiusTopRight =
			style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 10;
			style.BorderWidthTop = style.BorderWidthRight =
			style.BorderWidthBottom = style.BorderWidthLeft = 2;
			style.BorderColor = isHealth
				? new Color(0.2f, 1f, 0.4f, 0.85f)
				: new Color(1f, 0.85f, 0.2f, 0.85f);
			_relicIcon.AddThemeStyleboxOverride("panel", style);

			_relicIcon.Visible = true;

			_relicGlowTween?.Kill();
			_relicGlowTween = CreateTween();
			_relicGlowTween.SetLoops();
			_relicGlowTween.TweenProperty(_relicIconLabel, "modulate:a", 0.45f, 0.8f)
						   .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			_relicGlowTween.TweenProperty(_relicIconLabel, "modulate:a", 1f, 0.8f)
						   .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		}
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Round / Boss update helpers
	// ─────────────────────────────────────────────────────────────────────────

	private void UpdateRoundWidget()
	{
		if (_roundPanel == null) return;
		var mg = MainGame.Instance;
		if (mg == null || !mg.IsVisibleInTree()) { _roundPanel.Visible = false; return; }

		// Hide during boss round (round 3+) — boss HP bar takes over
		if (mg.RoundNum >= 2) { _roundPanel.Visible = false; return; }

		_roundPanel.Visible = true;

		int target  = mg.GetRoundEnemyTarget();
		int alive   = mg.Enms.Count;
		int spawned = mg.EnemiesSpawnedThisRound;
		int killed  = Mathf.Max(0, spawned - alive);

		_roundLabel.Text = $"⚔  ROUND {mg.RoundNum + 1}";

		float progress = target > 0 ? Mathf.Clamp((float)killed / target, 0f, 1f) : 0f;
		_enemyBar.Value  = progress;
		_enemyCountLabel.Text = $"{killed} / {target}  KILLED";

		// Colour shifts cyan → gold as you near completion
		if (progress >= 0.75f)
			_enemyBarFill.BgColor = new Color(1f, 0.85f, 0.1f, 1f);
		else if (progress >= 0.5f)
			_enemyBarFill.BgColor = new Color(0.4f, 1f, 0.5f, 1f);
		else
			_enemyBarFill.BgColor = new Color(0.1f, 0.85f, 1f, 1f);
	}

	private void UpdateBossHpWidget()
	{
		if (_bossHpPanel == null) return;

		// Find the first valid boss in the scene
		Boss boss = null;
		foreach (Node n in GetTree().GetNodesInGroup("Bosses"))
		{
			if (n is Boss b && IsInstanceValid(b)) { boss = b; break; }
		}

		if (boss == null)
		{
			_bossHpPanel.Visible = false;
			_bossPulseTween?.Kill();
			return;
		}

		_bossHpPanel.Visible = true;

		float frac = boss.maxHP > 0
			? Mathf.Clamp((float)boss.hp / boss.maxHP, 0f, 1f)
			: 0f;

		_bossHpBar.Value          = frac;
		_bossHpValueLabel.Text    = $"{boss.hp:N0}  /  {boss.maxHP:N0}";

		// Colour: red at full health → dark crimson when low
		if (frac > 0.5f)
			_bossHpFill.BgColor = new Color(0.95f, 0.18f, 0.05f, 1f);
		else if (frac > 0.25f)
			_bossHpFill.BgColor = new Color(0.85f, 0.08f, 0.35f, 1f);
		else
			_bossHpFill.BgColor = new Color(0.6f, 0.0f, 0.55f, 1f);  // ominous purple at low HP

		// Pulse the panel border when boss is below 25 %
		if (frac < 0.25f && (_bossPulseTween == null || !_bossPulseTween.IsRunning()))
		{
			_bossPulseTween?.Kill();
			_bossPulseTween = CreateTween().SetLoops();
			_bossPulseTween.TweenProperty(_bossHpPanel, "modulate",
				new Color(1f, 0.5f, 0.5f, 1f), 0.35f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			_bossPulseTween.TweenProperty(_bossHpPanel, "modulate",
				Colors.White, 0.35f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		}
		else if (frac >= 0.25f)
		{
			_bossPulseTween?.Kill();
			_bossHpPanel.Modulate = Colors.White;
		}
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Lobby reset — call when all players return to lobby
	// ─────────────────────────────────────────────────────────────────────────
	public void ResetForLobby()
	{
		// Clear player tracking so the next game finds a fresh player.
		_localPlayer   = null;
		_reticleSet    = false;
		_gameStartSent = false;
		_startTickMsec = 0;
		_lastMultiplier = 1f;

		// Kill any looping tweens.
		_relicGlowTween?.Kill();
		_multPulseTween?.Kill();
		_bossPulseTween?.Kill();
		if (_bossHpPanel != null) _bossHpPanel.Modulate = Colors.White;

		// Hide every widget panel so nothing bleeds through to the lobby screen.
		_dpsReticle.Visible     = false;
		_tankReticle.Visible    = false;
		_supportReticle.Visible = false;
		if (scorebg       != null) scorebg.Visible       = false;
		if (healthborder  != null) healthborder.Visible  = false;
		if (_hpPanel      != null) _hpPanel.Visible      = false;
		if (_ultPanel     != null) _ultPanel.Visible     = false;
		if (_scorePanel   != null) _scorePanel.Visible   = false;
		if (_timerPanel   != null) _timerPanel.Visible   = false;
		if (_relicIcon    != null) _relicIcon.Visible    = false;
		if (_roundPanel   != null) _roundPanel.Visible   = false;
		if (_bossHpPanel  != null) _bossHpPanel.Visible  = false;

		// Reset displayed values so stale numbers don't flash when the next
		// game starts before the first SyncStats RPC arrives.
		if (_scoreValueLabel != null) _scoreValueLabel.Text = "0";
		if (_multLabel       != null) _multLabel.Text       = "× 1.0";
		if (_timerLabel      != null) _timerLabel.Text      = "0:00";

		GD.Print("[HUD] ResetForLobby complete.");
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Networked timer sync
	// ─────────────────────────────────────────────────────────────────────────
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void SyncGameStart(long serverTickMsec)
	{
		if (_startTickMsec != 0) return;
		_startTickMsec = (ulong)serverTickMsec;
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Helpers
	// ─────────────────────────────────────────────────────────────────────────
	private void ApplyClassColour(Color col)
	{
		if (_ultBar != null)
			_ultBar.AddThemeStyleboxOverride("fill", MakeBarStyle(col));
		if (_ultTitleLabel != null)
			_ultTitleLabel.Modulate = col with { A = 1f };
	}

	private static StyleBoxFlat MakePanelStyle(Color bg)
	{
		var s = new StyleBoxFlat();
		s.BgColor = bg;
		s.CornerRadiusTopLeft = s.CornerRadiusTopRight =
		s.CornerRadiusBottomLeft = s.CornerRadiusBottomRight = 8;
		s.BorderWidthTop = s.BorderWidthRight =
		s.BorderWidthBottom = s.BorderWidthLeft = 1;
		s.BorderColor = new Color(1f, 1f, 1f, 0.15f);
		return s;
	}

	private static StyleBoxFlat MakeBarStyle(Color fill)
	{
		var s = new StyleBoxFlat();
		s.BgColor = fill;
		s.CornerRadiusTopLeft = s.CornerRadiusTopRight =
		s.CornerRadiusBottomLeft = s.CornerRadiusBottomRight = 4;
		return s;
	}
}
