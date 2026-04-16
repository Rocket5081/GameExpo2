using Godot;
using System.Collections.Generic;


public partial class TitleScreenFX : Control
{

	private RichTextLabel _title;
	private Label         _glyphTop;
	private Label         _glyphBot;
	private ColorRect     _flashRect;   // full-screen flash on strike


	private float _t = 0f;


	// GLYPH CYCLING
	
	private static readonly string[] GlyphRowFrames =
	{
		"⊗  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  ⊗",
		"◉  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  ◉",
		"◈  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  ◈",
		"⟁  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  ⟁",
		"✦  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  ✦",
		"⊛  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  ⊛",
	};
	private float _glyphCd    = 2.2f;
	private int   _glyphFrame = 0;


	// LIGHTNING

	private float _lightningCd   = 2.0f;   // first strike comes early
	private float _flashAlpha    = 0f;

	private struct Bolt
	{
		public Line2D Core;    // thin bright center
		public Line2D Glow;    // wide transparent halo
		public float  Life;
		public float  MaxLife;
	}
	private readonly List<Bolt> _bolts = new();

	
	// Ex tra glyphs

	private static readonly string[] CrawlyGlyphs =
	{
		"◉","⊗","◈","⟁","⧖","✦","⊕","⊛","◎","⦿",
		"☽","⌖","⋄","❖","✧","⊙","◬","⟐","⧗","〷",
		"⧫","⌬","⌭","⍟","⍩","⏣","⎔","†","‡","※","⁂","⸸"
	};
	private static readonly Color[] CrawlyColors =
	{
		new Color(0.60f, 0.10f, 0.90f, 1f),
		new Color(0.82f, 0.10f, 0.16f, 1f),
		new Color(0.92f, 0.78f, 0.18f, 1f),
		new Color(0.14f, 0.52f, 0.78f, 1f),
		new Color(0.36f, 0.80f, 0.40f, 1f),
	};
	private float _crawlCd = 1.0f;

	private struct Crawly
	{
		public Label   Node;
		public Vector2 Vel;
		public float   Life, MaxLife, RotSpeed;
	}
	private readonly List<Crawly> _crawlies = new();


	// LIFECYCLE

	public override void _Ready()
	{
		_title    = GetNodeOrNull<RichTextLabel>("TitleBlock/Title");
		_glyphTop = GetNodeOrNull<Label>("TitleBlock/Glyph");
		_glyphBot = GetNodeOrNull<Label>("TitleBlock/GlyphBottom");

		// Full-screen flash rect — sits above BG, below content
		_flashRect = new ColorRect();
		_flashRect.Color = new Color(0.72f, 0.62f, 1.0f, 0f);
		_flashRect.SetAnchorsPreset(LayoutPreset.FullRect);
		_flashRect.MouseFilter = MouseFilterEnum.Ignore;
		_flashRect.ZIndex = 1;
		AddChild(_flashRect);
	}

	public override void _Process(double delta)
	{
		if (!IsVisibleInTree()) return;
		float dt = (float)delta;
		_t += dt;

		PulseTitle();
		CycleGlyphs(dt);
		TickLightning(dt);
		TickCrawlies(dt);

		_lightningCd -= dt;
		if (_lightningCd <= 0f)
		{
			SpawnStrike();
			_lightningCd = 3.0f + GD.Randf() * 5.0f;
		}

		_crawlCd -= dt;
		if (_crawlCd <= 0f)
		{
			SpawnCrawly();
			_crawlCd = 0.8f + GD.Randf() * 1.8f;
		}
	}


	// TITLE PULSE

	private void PulseTitle()
	{
		if (_title == null) return;

		float breathe   = Mathf.Sin(_t * 1.05f) * 0.5f + 0.5f;
		float flicker   = Mathf.Sin(_t * 19.1f) * 0.018f + Mathf.Sin(_t * 37.3f) * 0.008f;
		float intensity = 0.84f + breathe * 0.16f + flicker;

		_title.Modulate = new Color(intensity, intensity * 0.96f, intensity * 0.88f, 1f);

		float glowP = Mathf.Sin(_t * 0.88f + 1.1f) * 0.5f + 0.5f;
		_title.AddThemeColorOverride("font_outline_color",
			new Color(0.97f, 0.55f, 0.02f, 0.18f + glowP * 0.65f));
	}


	// GLYPH CYCLING

	private void CycleGlyphs(float dt)
	{
		_glyphCd -= dt;
		if (_glyphCd <= 0f)
		{
			_glyphCd    = 1.8f + GD.Randf() * 1.2f;
			_glyphFrame = (_glyphFrame + 1) % GlyphRowFrames.Length;
			_glyphTop?.SetDeferred("text", GlyphRowFrames[_glyphFrame]);
			_glyphBot?.SetDeferred("text", GlyphRowFrames[(_glyphFrame + 3) % GlyphRowFrames.Length]);
		}

		float alpha = 0.50f + Mathf.Sin(_t * 0.62f + 0.9f) * 0.38f;
		float pulse = Mathf.Sin(_t * 0.9f + 1.2f) * 0.5f + 0.5f;
		var col = new Color(0.52f, 0.14f + pulse * 0.10f, 0.74f + pulse * 0.12f, alpha);
		_glyphTop?.AddThemeColorOverride("font_color", col);
		_glyphBot?.AddThemeColorOverride("font_color", col);
	}


	// LIGHTNING


	/// <summary>Spawns a main bolt + 1-2 branch bolts.</summary>
	private void SpawnStrike()
	{
		var vp = GetViewportRect().Size;

		// Main bolt — top to somewhere mid-screen
		float sx = vp.X * (0.1f + GD.Randf() * 0.8f);
		float ex = sx + (GD.Randf() * 160f - 80f);
		float ey = vp.Y * (0.35f + GD.Randf() * 0.45f);

		var mainPts = MakeBoltPoints(new Vector2(sx, -10f), new Vector2(ex, ey), 14, 48f);
		AddBolt(mainPts, 2.4f, 10f,
			new Color(0.92f, 0.88f, 1.00f, 1.0f),   // core: near-white
			new Color(0.55f, 0.40f, 1.00f, 0.22f),  // glow: purple
			0.16f + GD.Randf() * 0.10f);

		// Flash
		_flashAlpha = 0.22f + GD.Randf() * 0.14f;

		// 1–2 branches forking from a random mid-point of the main bolt
		int branches = 1 + (int)(GD.Randi() % 2);
		for (int b = 0; b < branches; b++)
		{
			int seg = 4 + (int)(GD.Randi() % (uint)(mainPts.Length - 6));
			var origin = mainPts[seg];
			var bEnd = origin + new Vector2(
				GD.Randf() * 140f - 70f,
				60f + GD.Randf() * 100f);
			var branchPts = MakeBoltPoints(origin, bEnd, 8, 26f);
			AddBolt(branchPts, 1.4f, 6f,
				new Color(0.80f, 0.70f, 1.00f, 0.85f),
				new Color(0.50f, 0.35f, 0.90f, 0.14f),
				0.12f + GD.Randf() * 0.08f);
		}
	}

	/// <summary>Build zigzag bolt points using sine-weighted perpendicular offsets.</summary>
	private static Vector2[] MakeBoltPoints(Vector2 from, Vector2 to, int segs, float maxOff)
	{
		var pts = new Vector2[segs + 1];
		pts[0]    = from;
		pts[segs] = to;
		var dir  = (to - from).Normalized();
		var perp = new Vector2(-dir.Y, dir.X);

		for (int i = 1; i < segs; i++)
		{
			float t    = (float)i / segs;
			float wave = Mathf.Sin(t * Mathf.Pi);              // 0 at ends, 1 at midpoint
			float off  = (GD.Randf() * 2f - 1f) * maxOff * wave;
			pts[i]     = from.Lerp(to, t) + perp * off;
		}
		return pts;
	}

	/// <summary>Creates a paired core+glow Line2D bolt.</summary>
	private void AddBolt(Vector2[] pts, float coreW, float glowW,
						 Color coreCol, Color glowCol, float lifetime)
	{
		var glow = new Line2D
		{
			Points       = pts,
			Width        = glowW,
			DefaultColor = glowCol,
			JointMode    = Line2D.LineJointMode.Round,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode   = Line2D.LineCapMode.Round,
		};
		var core = new Line2D
		{
			Points       = pts,
			Width        = coreW,
			DefaultColor = coreCol,
			JointMode    = Line2D.LineJointMode.Round,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode   = Line2D.LineCapMode.Round,
		};

		AddChild(glow);
		AddChild(core);

		_bolts.Add(new Bolt { Core = core, Glow = glow, Life = lifetime, MaxLife = lifetime });
	}

	private void TickLightning(float dt)
	{
		// Fade flash rect
		if (_flashAlpha > 0f)
		{
			_flashAlpha = Mathf.Max(0f, _flashAlpha - dt * 5.5f);
			_flashRect.Color = new Color(0.72f, 0.62f, 1.0f, _flashAlpha);
		}

		// Tick bolts
		for (int i = _bolts.Count - 1; i >= 0; i--)
		{
			var b = _bolts[i];
			b.Life -= dt;
			if (b.Life <= 0f || !IsInstanceValid(b.Core))
			{
				b.Core?.QueueFree();
				b.Glow?.QueueFree();
				_bolts.RemoveAt(i);
				continue;
			}

			// Sharp flash at birth, then fade — bolts don't linger
			float norm  = b.Life / b.MaxLife;
			float alpha = norm < 0.5f
				? norm * 2f                         // fade out second half
				: 1.0f;                             // full bright first half

			b.Core.Modulate = new Color(1f, 1f, 1f, alpha);
			b.Glow.Modulate = new Color(1f, 1f, 1f, alpha * 0.6f);
			_bolts[i] = b;
		}
	}


	// CRAWLIES

	private void TickCrawlies(float dt)
	{
		for (int i = _crawlies.Count - 1; i >= 0; i--)
		{
			var cr = _crawlies[i];
			cr.Life -= dt;
			if (cr.Life <= 0f || !IsInstanceValid(cr.Node))
			{
				cr.Node?.QueueFree();
				_crawlies.RemoveAt(i);
				continue;
			}
			cr.Node.Position        += cr.Vel * dt;
			cr.Node.RotationDegrees += cr.RotSpeed * dt;
			float norm  = cr.Life / cr.MaxLife;
			float alpha = Mathf.Min(Mathf.Min((1f - norm) * 5f, norm * 5f), 1f);
			cr.Node.Modulate = new Color(1f, 1f, 1f, alpha);
			_crawlies[i] = cr;
		}
	}

	private void SpawnCrawly()
	{
		var vp = GetViewportRect().Size;
		float x, y;
		switch ((int)(GD.Randi() % 4))
		{
			case 0:  x = GD.Randf() * vp.X;  y = -28f;        break;
			case 1:  x = GD.Randf() * vp.X;  y = vp.Y + 28f;  break;
			case 2:  x = -28f;                y = GD.Randf() * vp.Y; break;
			default: x = vp.X + 28f;          y = GD.Randf() * vp.Y; break;
		}

		var lbl = new Label();
		lbl.Text = CrawlyGlyphs[GD.Randi() % (uint)CrawlyGlyphs.Length];
		lbl.AddThemeFontSizeOverride("font_size", 10 + (int)(GD.Randi() % 26));
		lbl.AddThemeColorOverride("font_color",
			CrawlyColors[GD.Randi() % (uint)CrawlyColors.Length]);
		lbl.Modulate = new Color(1f, 1f, 1f, 0f);
		lbl.Position = new Vector2(x, y);

		float tx  = vp.X * (0.15f + GD.Randf() * 0.70f);
		float ty  = vp.Y * (0.15f + GD.Randf() * 0.70f);
		var   dir = new Vector2(tx - x, ty - y).Normalized();
		dir.X    += GD.Randf() * 0.5f - 0.25f;
		dir.Y    += GD.Randf() * 0.5f - 0.25f;
		float life = 5f + GD.Randf() * 7f;

		AddChild(lbl);
		_crawlies.Add(new Crawly
		{
			Node     = lbl,
			Vel      = dir * (14f + GD.Randf() * 28f),
			Life     = life,
			MaxLife  = life,
			RotSpeed = GD.Randf() * 50f - 25f,
		});
	}


	// CLEANUP

	public override void _ExitTree()
	{
		foreach (var b  in _bolts)   { b.Core?.QueueFree(); b.Glow?.QueueFree(); }
		foreach (var cr in _crawlies)  cr.Node?.QueueFree();
		_bolts.Clear();
		_crawlies.Clear();
	}
}
