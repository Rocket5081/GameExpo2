using Godot;

/// <summary>
/// Standalone HUD CanvasLayer.
/// Manages:
///   • Class reticle display (shown once the local player is identified).
///   • Bottom-right ultimate-ability cooldown widget (updated every frame).
/// </summary>
public partial class HUD : CanvasLayer
{
	// ── Reticles ──────────────────────────────────────────────────────────────
	private Control _dpsReticle;
	private Control _tankReticle;
	private Control _supportReticle;
	private bool _reticleSet = false;

	// ── Ultimate cooldown widget ──────────────────────────────────────────────
	private Player       _localPlayer;
	private Panel        _ultPanel;
	private Label        _ultTitleLabel;
	private ProgressBar  _ultBar;
	private Label        _ultTimeLabel;

	// Class accent colours
	private static readonly Color ColDps     = new Color("ff4444");
	private static readonly Color ColTank    = new Color("4488ff");
	private static readonly Color ColSupport = new Color("44cc66");

	public override void _Ready()
	{
		_dpsReticle     = GetNode<Control>("DpsReticle");
		_tankReticle    = GetNode<Control>("TankReticle");
		_supportReticle = GetNode<Control>("SupportReticle");

		_dpsReticle.Visible     = false;
		_tankReticle.Visible    = false;
		_supportReticle.Visible = false;

		BuildCooldownWidget();
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Build the cooldown UI entirely in code for reliability
	// ─────────────────────────────────────────────────────────────────────────
	private void BuildCooldownWidget()
	{
		// Full-screen invisible root so we can freely anchor children.
		// MouseFilter = Ignore means it won't block game clicks.
		var root = new Control();
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(root);

		// Panel pinned to the bottom-right corner with a 20 px inset.
		const float W = 190f, H = 86f, Margin = 20f;
		_ultPanel = new Panel();
		_ultPanel.AnchorLeft   = 1f;
		_ultPanel.AnchorTop    = 1f;
		_ultPanel.AnchorRight  = 1f;
		_ultPanel.AnchorBottom = 1f;
		_ultPanel.OffsetLeft   = -(W + Margin);
		_ultPanel.OffsetTop    = -(H + Margin);
		_ultPanel.OffsetRight  = -Margin;
		_ultPanel.OffsetBottom = -Margin;
		_ultPanel.MouseFilter  = Control.MouseFilterEnum.Ignore;

		var style = new StyleBoxFlat();
		style.BgColor                 = new Color(0f, 0f, 0f, 0.55f);
		style.CornerRadiusTopLeft     = 8;
		style.CornerRadiusTopRight    = 8;
		style.CornerRadiusBottomLeft  = 8;
		style.CornerRadiusBottomRight = 8;
		style.BorderWidthTop    = 1;
		style.BorderWidthRight  = 1;
		style.BorderWidthBottom = 1;
		style.BorderWidthLeft   = 1;
		style.BorderColor       = new Color(1f, 1f, 1f, 0.15f);
		_ultPanel.AddThemeStyleboxOverride("panel", style);
		root.AddChild(_ultPanel);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 4);
		// Padding inside the panel
		vbox.OffsetLeft   = 10;
		vbox.OffsetRight  = -10;
		vbox.OffsetTop    = 8;
		vbox.OffsetBottom = -8;
		_ultPanel.AddChild(vbox);

		// Title row
		_ultTitleLabel = new Label();
		_ultTitleLabel.Text              = "⚡ ULTIMATE  [Q]";
		_ultTitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_ultTitleLabel.AddThemeFontSizeOverride("font_size", 12);
		_ultTitleLabel.Modulate = new Color(1f, 1f, 1f, 0.85f);
		vbox.AddChild(_ultTitleLabel);

		// Progress bar — value 0 = ready (full), 1 = full cooldown remaining
		_ultBar = new ProgressBar();
		_ultBar.MinValue            = 0.0;
		_ultBar.MaxValue            = 1.0;
		_ultBar.Value               = 1.0;   // starts on cooldown
		_ultBar.ShowPercentage      = false;
		_ultBar.CustomMinimumSize   = new Vector2(0, 14);

		var barBg = new StyleBoxFlat();
		barBg.BgColor               = new Color(0.15f, 0.15f, 0.15f, 1f);
		barBg.CornerRadiusTopLeft   = 4;
		barBg.CornerRadiusTopRight  = 4;
		barBg.CornerRadiusBottomLeft  = 4;
		barBg.CornerRadiusBottomRight = 4;
		_ultBar.AddThemeStyleboxOverride("background", barBg);

		var barFill = new StyleBoxFlat();
		barFill.BgColor              = ColDps;   // will be updated when player class is known
		barFill.CornerRadiusTopLeft  = 4;
		barFill.CornerRadiusTopRight = 4;
		barFill.CornerRadiusBottomLeft  = 4;
		barFill.CornerRadiusBottomRight = 4;
		_ultBar.AddThemeStyleboxOverride("fill", barFill);
		vbox.AddChild(_ultBar);

		// Time remaining label
		_ultTimeLabel = new Label();
		_ultTimeLabel.Text               = "30.0s";
		_ultTimeLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_ultTimeLabel.AddThemeFontSizeOverride("font_size", 13);
		vbox.AddChild(_ultTimeLabel);

		// Hidden until a local player is found
		_ultPanel.Visible = false;
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Per-frame updates
	// ─────────────────────────────────────────────────────────────────────────
	public override void _Process(double delta)
	{
		// Step 1: find local player and show reticle (runs once then stops)
		if (!_reticleSet)
		{
			foreach (Node node in GetTree().GetNodesInGroup("Players"))
			{
				if (node is Player player && player.myId != null && player.myId.IsLocal)
				{
					if      (player is DpsPlayer)     { _dpsReticle.Visible     = true; ApplyClassColour(ColDps); }
					else if (player is TankPlayer)    { _tankReticle.Visible    = true; ApplyClassColour(ColTank); }
					else if (player is SupportPlayer) { _supportReticle.Visible = true; ApplyClassColour(ColSupport); }

					_localPlayer     = player;
					_ultPanel.Visible = true;
					_reticleSet      = true;
					break;
				}
			}
		}

		// Step 2: update cooldown display
		if (_localPlayer == null || !IsInstanceValid(_localPlayer)) return;

		float remaining = Mathf.Max(_localPlayer.UltimateCooldownTimer, 0f);
		float maxCd     = _localPlayer.UltimateCooldownMax;
		bool  ready     = remaining <= 0f;

		// ProgressBar: 0 = ready (empty bar), 1 = full cooldown
		_ultBar.Value = maxCd > 0f ? remaining / maxCd : 0.0;

		if (ready)
		{
			_ultTimeLabel.Text    = "READY!";
			_ultTimeLabel.Modulate = new Color(1f, 1f, 0.5f, 1f);   // bright yellow
		}
		else
		{
			_ultTimeLabel.Text    = $"{remaining:0.0}s";
			_ultTimeLabel.Modulate = new Color(1f, 1f, 1f, 0.8f);
		}
	}

	// Tints the progress bar fill to match the player's class colour
	private void ApplyClassColour(Color col)
	{
		if (_ultBar == null) return;

		var barFill = new StyleBoxFlat();
		barFill.BgColor               = col;
		barFill.CornerRadiusTopLeft   = 4;
		barFill.CornerRadiusTopRight  = 4;
		barFill.CornerRadiusBottomLeft  = 4;
		barFill.CornerRadiusBottomRight = 4;
		_ultBar.AddThemeStyleboxOverride("fill", barFill);

		_ultTitleLabel.Modulate = col with { A = 1f };
	}
}
