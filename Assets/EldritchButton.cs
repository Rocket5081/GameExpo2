using Godot;

public partial class EldritchButton : Button
{
   
	[Export] public float HoverScale    = 2f;   
	[Export] public float TweenDuration = 0.12f;   

 
	[Export] public StyleBoxFlat HoverStyle;
	[Export] public StyleBoxFlat NormalStyle;

	private Tween _tween;

	public override void _Ready()
	{
	   
		if (NormalStyle == null)
		{
			NormalStyle = MakeStyle(
				new Color(0.08f, 0.0f, 0.15f, 0.85f),  
				new Color(0.45f, 0.0f, 0.75f, 1.0f),   
				borderWidth: 1,
				glowSize: 0,
				glowColor: new Color(0, 0, 0, 0)
			);
		}

		if (HoverStyle == null)
		{
			HoverStyle = MakeStyle(
				new Color(0.12f, 0.0f, 0.22f, 0.95f),  
				new Color(0.7f,  0.2f, 1.0f,  1.0f),   
				borderWidth: 2,
				glowSize: 10,
				glowColor: new Color(0.6f, 0.1f, 1.0f, 0.55f)
			);
		}

		// Apply normal style to all non-hover states
		AddThemeStyleboxOverride("normal",   NormalStyle);
		AddThemeStyleboxOverride("pressed",  NormalStyle);
		AddThemeStyleboxOverride("focus",    NormalStyle);
		AddThemeStyleboxOverride("disabled", NormalStyle);
		AddThemeStyleboxOverride("hover",    HoverStyle);

		// Connect signals
		MouseEntered += OnMouseEntered;
		MouseExited  += OnMouseExited;

		// Start at normal scale from pivot center
		PivotOffset = Size / 2.0f;
	}

	// Keep pivot centered if the button is resized
	public override void _Notification(int what)
	{
		base._Notification(what);
		if (what == NotificationResized)
			PivotOffset = Size / 2.0f;
	}

	private void OnMouseEntered()
	{
		PivotOffset = Size / 2.0f;
		AnimateTo(HoverScale);
	}

	private void OnMouseExited()
	{
		AnimateTo(1.0f);
	}

	private void AnimateTo(float targetScale)
	{
		_tween?.Kill();
		_tween = CreateTween();
		_tween.SetEase(Tween.EaseType.Out);
		_tween.SetTrans(Tween.TransitionType.Cubic);
		_tween.TweenProperty(this, "scale", new Vector2(targetScale, targetScale), TweenDuration);
	}

	// ── Helper: build a StyleBoxFlat in code ──────────────────────────────
	private static StyleBoxFlat MakeStyle(
		Color bg, Color border,
		int borderWidth, int glowSize, Color glowColor)
	{
		var s = new StyleBoxFlat();
		s.BgColor            = bg;
		s.BorderColor        = border;
		s.SetBorderWidthAll(borderWidth);
		s.SetCornerRadiusAll(4);
		s.ShadowSize         = glowSize;
		s.ShadowColor        = glowColor;
		s.ShadowOffset       = Vector2I.Zero;
		s.AntiAliasing       = true;
		return s;
	}
}
