using Godot;

/// <summary>
/// Draws a screen-space reticle for each player class.
/// Lives inside a CanvasLayer on the player scene.
/// Only shown when myId.IsLocal is true (handled by Player.cs).
/// </summary>
public partial class Reticle : Control
{
	public enum Style
	{
		Crosshair = 0,   // DPS  — tight crosshair with center dot
		Spread    = 1,   // Tank — wide bracket spread indicator
		Circle    = 2    // Support — laser circle with center dot
	}

	[Export] public Style ReticleStyle = Style.Crosshair;

	[Export] public float Gap       = 6f;
	[Export] public float Length    = 14f;
	[Export] public float Thickness = 2f;

	private static readonly Color ColorDps     = new Color(1.0f,  0.25f, 0.25f, 0.9f);
	private static readonly Color ColorTank    = new Color(0.3f,  0.6f,  1.0f,  0.9f);
	private static readonly Color ColorSupport = new Color(0.25f, 1.0f,  0.4f,  0.9f);

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;

		// Redraw whenever the viewport resizes so the center stays correct.
		GetViewport().SizeChanged += QueueRedraw;
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		// Fire _Draw() when this node (or any ancestor) becomes visible.
		if (what == NotificationVisibilityChanged || what == NotificationResized)
			QueueRedraw();
	}

	public override void _Draw()
	{
		// Always derive the center from the actual viewport size, not Size.
		// Size can be (0,0) on the first draw if the layout hasn't settled yet.
		Vector2 center = GetViewportRect().Size / 2f;

		switch (ReticleStyle)
		{
			case Style.Crosshair: DrawCrosshair(center); break;
			case Style.Spread:    DrawSpread(center);    break;
			case Style.Circle:    DrawCircleReticle(center); break;
		}
	}

	// ── DPS — classic tight crosshair ────────────────────────────────────────
	private void DrawCrosshair(Vector2 c)
	{
		Color col = ColorDps;

		DrawLine(c + new Vector2(-(Gap + Length), 0), c + new Vector2(-Gap, 0), col, Thickness, true);
		DrawLine(c + new Vector2(Gap, 0),             c + new Vector2(Gap + Length, 0), col, Thickness, true);
		DrawLine(c + new Vector2(0, -(Gap + Length)), c + new Vector2(0, -Gap), col, Thickness, true);
		DrawLine(c + new Vector2(0, Gap),             c + new Vector2(0, Gap + Length), col, Thickness, true);
		DrawCircle(c, 2f, col);
	}

	// ── Tank — wide bracket spread indicator ─────────────────────────────────
	private void DrawSpread(Vector2 c)
	{
		Color col    = ColorTank;
		float spread = Gap + Length;
		float bLen   = 10f;
		float bInset = 6f;

		// Left bracket  [
		DrawLine(c + new Vector2(-spread, -bLen), c + new Vector2(-spread,  bLen), col, Thickness, true);
		DrawLine(c + new Vector2(-spread, -bLen), c + new Vector2(-spread + bInset, -bLen), col, Thickness, true);
		DrawLine(c + new Vector2(-spread,  bLen), c + new Vector2(-spread + bInset,  bLen), col, Thickness, true);

		// Right bracket  ]
		DrawLine(c + new Vector2(spread, -bLen), c + new Vector2(spread,  bLen), col, Thickness, true);
		DrawLine(c + new Vector2(spread, -bLen), c + new Vector2(spread - bInset, -bLen), col, Thickness, true);
		DrawLine(c + new Vector2(spread,  bLen), c + new Vector2(spread - bInset,  bLen), col, Thickness, true);

		DrawCircle(c, 2.5f, col);
	}

	// ── Support — laser circle with center dot ───────────────────────────────
	private void DrawCircleReticle(Vector2 c)
	{
		Color col    = ColorSupport;
		float radius = Gap + Length * 0.5f;

		DrawArc(c, radius, 0f, Mathf.Tau, 48, col, Thickness, true);

		float tickLen = 5f;
		float inner   = radius - tickLen;
		DrawLine(c + new Vector2(0,  -radius), c + new Vector2(0,  -inner), col, Thickness, true);
		DrawLine(c + new Vector2(0,   radius), c + new Vector2(0,   inner), col, Thickness, true);
		DrawLine(c + new Vector2(-radius, 0),  c + new Vector2(-inner, 0),  col, Thickness, true);
		DrawLine(c + new Vector2( radius, 0),  c + new Vector2( inner, 0),  col, Thickness, true);

		DrawCircle(c, 2f, col);
	}
}
