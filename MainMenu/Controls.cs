using Godot;


public partial class Controls : Button
{
	[Export] public float HoverScale    = 1.5f;
	[Export] public float TweenDuration = 0.12f;

	private Tween _tween;

	public override void _Ready()
	{
		
		var normal = MakeStyle(
			new Color(0.08f, 0.0f, 0.15f, 0.85f),
			new Color(0.45f, 0.0f, 0.75f, 1.0f),
			borderWidth: 1, glowSize: 0, glowColor: new Color(0, 0, 0, 0)
		);

		var hover = MakeStyle(
			new Color(0.12f, 0.0f, 0.22f, 0.95f),
			new Color(0.7f,  0.2f, 1.0f,  1.0f),
			borderWidth: 2, glowSize: 6, glowColor: new Color(0.6f, 0.1f, 1.0f, 0.55f)
		);

		
		AddThemeStyleboxOverride("normal",   normal);
		AddThemeStyleboxOverride("hover",    hover);
		AddThemeStyleboxOverride("pressed",  normal);   
		AddThemeStyleboxOverride("focus",    normal);
		AddThemeStyleboxOverride("disabled", normal);

		MouseEntered += OnMouseEntered;
		MouseExited  += OnMouseExited;
		Pressed      += OnPressed;

		PivotOffset = Size / 2.0f;
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		if (what == NotificationResized)
			PivotOffset = Size / 2.0f;
	}

	private void OnMouseEntered() { PivotOffset = Size / 2.0f; AnimateTo(HoverScale); }
	private void OnMouseExited()  { AnimateTo(1.0f); }

	private void OnPressed()
	{
		GetTree().ChangeSceneToFile("res://ControlsScene/controls.tscn");
	}

	private void AnimateTo(float target)
	{
		_tween?.Kill();
		_tween = CreateTween();
		_tween.SetEase(Tween.EaseType.Out);
		_tween.SetTrans(Tween.TransitionType.Cubic);
		_tween.TweenProperty(this, "scale", new Vector2(target, target), TweenDuration);
	}

	private static StyleBoxFlat MakeStyle(Color bg, Color border, int borderWidth, int glowSize, Color glowColor)
	{
		var s = new StyleBoxFlat();
		s.BgColor      = bg;
		s.BorderColor  = border;
		s.SetBorderWidthAll(borderWidth);
		s.SetCornerRadiusAll(4);
		s.ShadowSize   = glowSize;
		s.ShadowColor  = glowColor;
		s.ShadowOffset = Vector2I.Zero;
		s.AntiAliasing = true;
		return s;
	}
}
