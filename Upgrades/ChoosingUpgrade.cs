using Godot;

/// <summary>
/// A styled upgrade card. Options.cs calls Configure() before adding it to the
/// scene tree; all visuals are built inside _Ready().
/// </summary>
public partial class ChoosingUpgrade : PanelContainer
{
	public string opt;

	// Set by Options.cs before the node enters the tree
	private string _title       = "";
	private string _subtitle    = "";  // e.g. "Level 2"
	private string _description = "";
	private string _symbol      = "◈";
	private Color  _accent      = new Color(0.5f, 0.3f, 1f);

	private Tween  _hoverTween;

	// ── Called by Options.cs right after Instantiate() ───────────────────────
	public void Configure(string option,
						  string title,
						  string subtitle,
						  string symbol,
						  Color  accent,
						  string description)
	{
		opt          = option;
		_title       = title;
		_subtitle    = subtitle;
		_symbol      = symbol;
		_accent      = accent;
		_description = description;
	}

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(210f, 300f);
		PivotOffset        = CustomMinimumSize / 2f;

		// ── Card background ───────────────────────────────────────────────────
		var bg          = new StyleBoxFlat();
		bg.BgColor      = new Color(0.04f, 0.01f, 0.10f, 0.97f);
		bg.BorderColor  = _accent;
		bg.SetBorderWidthAll(2);
		bg.SetCornerRadiusAll(10);
		bg.ShadowSize   = 12;
		bg.ShadowOffset = Vector2I.Zero;
		bg.ShadowColor  = new Color(_accent.R, _accent.G, _accent.B, 0.45f);
		AddThemeStyleboxOverride("panel", bg);

		// ── Outer VBox ────────────────────────────────────────────────────────
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 0);
		AddChild(vbox);

		// ── Coloured header strip ─────────────────────────────────────────────
		var header       = new ColorRect();
		header.Color     = new Color(_accent.R, _accent.G, _accent.B, 0.28f);
		header.CustomMinimumSize = new Vector2(0f, 68f);
		vbox.AddChild(header);

		// Symbol inside header (anchor it by adding it as a child of header)
		var symbolLabel  = new Label();
		symbolLabel.Text = _symbol;
		symbolLabel.AddThemeFontSizeOverride("font_size", 38);
		symbolLabel.AddThemeColorOverride("font_color", _accent);
		symbolLabel.HorizontalAlignment = HorizontalAlignment.Center;
		symbolLabel.VerticalAlignment   = VerticalAlignment.Center;
		symbolLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		header.AddChild(symbolLabel);

		// ── Inner padding VBox ────────────────────────────────────────────────
		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 6);
		var innerMargin = new MarginContainer();
		innerMargin.AddThemeConstantOverride("margin_left",   14);
		innerMargin.AddThemeConstantOverride("margin_right",  14);
		innerMargin.AddThemeConstantOverride("margin_top",    10);
		innerMargin.AddThemeConstantOverride("margin_bottom", 14);
		innerMargin.AddChild(inner);
		vbox.AddChild(innerMargin);

		// Upgrade title
		var nameLabel = new Label();
		nameLabel.Text = _title;
		nameLabel.AddThemeFontSizeOverride("font_size", 15);
		nameLabel.AddThemeColorOverride("font_color", _accent);
		nameLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 1f));
		nameLabel.AddThemeConstantOverride("outline_size", 1);
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		nameLabel.AutowrapMode        = TextServer.AutowrapMode.Word;
		inner.AddChild(nameLabel);

		// Level subtitle
		var levelLabel = new Label();
		levelLabel.Text = _subtitle;
		levelLabel.AddThemeFontSizeOverride("font_size", 12);
		levelLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.6f, 0.9f));
		levelLabel.HorizontalAlignment = HorizontalAlignment.Center;
		inner.AddChild(levelLabel);

		// Divider
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", new Color(_accent.R, _accent.G, _accent.B, 0.4f));
		var sepMargin = new MarginContainer();
		sepMargin.AddThemeConstantOverride("margin_top",    4);
		sepMargin.AddThemeConstantOverride("margin_bottom", 4);
		sepMargin.AddChild(sep);
		inner.AddChild(sepMargin);

		// Description
		var descLabel = new Label();
		descLabel.Text = _description;
		descLabel.AddThemeFontSizeOverride("font_size", 13);
		descLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.80f, 0.95f));
		descLabel.HorizontalAlignment = HorizontalAlignment.Center;
		descLabel.AutowrapMode        = TextServer.AutowrapMode.Word;
		inner.AddChild(descLabel);

		// "SELECT" hint at bottom
		var hint = new Label();
		hint.Text = "▶  SELECT";
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.AddThemeColorOverride("font_color", new Color(_accent.R, _accent.G, _accent.B, 0.6f));
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		inner.AddChild(hint);

		// ── Interaction ───────────────────────────────────────────────────────
		MouseEntered += OnHover;
		MouseExited  += OnUnhover;
		GuiInput     += OnGuiInput;
	}

	// ── Hover effects ─────────────────────────────────────────────────────────

	private void OnHover()
	{
		PivotOffset = Size / 2f;
		_hoverTween?.Kill();
		_hoverTween = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
		_hoverTween.TweenProperty(this, "scale", new Vector2(1.07f, 1.07f), 0.12f);

		// Brighten border
		var bg         = new StyleBoxFlat();
		bg.BgColor     = new Color(0.07f, 0.02f, 0.16f, 0.97f);
		bg.BorderColor = new Color(
			Mathf.Min(_accent.R * 1.4f, 1f),
			Mathf.Min(_accent.G * 1.4f, 1f),
			Mathf.Min(_accent.B * 1.4f, 1f));
		bg.SetBorderWidthAll(3);
		bg.SetCornerRadiusAll(10);
		bg.ShadowSize   = 20;
		bg.ShadowOffset = Vector2I.Zero;
		bg.ShadowColor  = new Color(_accent.R, _accent.G, _accent.B, 0.7f);
		AddThemeStyleboxOverride("panel", bg);
	}

	private void OnUnhover()
	{
		PivotOffset = Size / 2f;
		_hoverTween?.Kill();
		_hoverTween = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		_hoverTween.TweenProperty(this, "scale", Vector2.One, 0.12f);

		var bg         = new StyleBoxFlat();
		bg.BgColor     = new Color(0.04f, 0.01f, 0.10f, 0.97f);
		bg.BorderColor = _accent;
		bg.SetBorderWidthAll(2);
		bg.SetCornerRadiusAll(10);
		bg.ShadowSize   = 12;
		bg.ShadowOffset = Vector2I.Zero;
		bg.ShadowColor  = new Color(_accent.R, _accent.G, _accent.B, 0.45f);
		AddThemeStyleboxOverride("panel", bg);
	}

	private void OnGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			OnOptionPressed();
	}

	// ── Selection ─────────────────────────────────────────────────────────────

	public void OnOptionPressed()
	{
		string[] splitOpt = opt.Split(':');

		Player localPlayer = null;
		foreach (Node n in GetTree().GetNodesInGroup("Players"))
		{
			if (n is Player p && p.myId != null && p.myId.IsLocal)
			{
				localPlayer = p;
				break;
			}
		}

		localPlayer?.upgrade(splitOpt);

		GetParent<GridContainer>().GetParent<Options>().clear();
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}
}
