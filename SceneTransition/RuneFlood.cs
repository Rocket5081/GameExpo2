using Godot;

/// <summary>
/// Full-screen animated rune flood wipe effect.
/// Columns of Elder Futhark runes sweep left→right to cover/reveal the screen.
/// Controlled externally via Progress (0 = transparent, 1 = fully covered).
/// </summary>
public partial class RuneFlood : Control
{
	// ── Rune glyphs ──────────────────────────────────────────────────────────
	private static readonly string[] Runes =
	{
		"ᚠ","ᚢ","ᚦ","ᚨ","ᚱ","ᚲ","ᚷ","ᚹ","ᚺ","ᚾ",
		"ᛁ","ᛃ","ᛇ","ᛈ","ᛉ","ᛊ","ᛏ","ᛒ","ᛖ","ᛗ",
		"ᛚ","ᛜ","ᛞ","ᛟ","᛫","᛬","ᚸ","ᚳ","ᛣ","ᛥ"
	};

	// ── Visual settings ───────────────────────────────────────────────────────
	private const int    CellW        = 36;
	private const int    CellH        = 40;
	private const float  TransitionW  = 3.5f;   // wave-front width in columns
	private const float  ScrambleSec  = 0.06f;  // how often runes randomise
	private const float  RowStagger   = 0.18f;  // per-row alpha delay fraction

	private static readonly Color BgColor   = new Color(0.004f, 0.001f, 0.014f, 1f);
	private static readonly Color RuneColor = new Color(0.88f,  0.56f,  0.08f,  1f);
	private static readonly Color GlowColor = new Color(0.55f,  0.20f,  0.80f,  1f);

	// ── State ─────────────────────────────────────────────────────────────────
	private Font     _font;
	private int      _cols, _rows;
	private string[,] _grid;
	private float    _scrambleTimer;

	/// 0 = screen clear/transparent,  1 = screen fully covered by runes + dark bg
	public float Progress { get; set; } = 0f;

	// ─────────────────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		_font = ThemeDB.FallbackFont;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;
		RebuildGrid();
	}

	private void RebuildGrid()
	{
		var sz = GetViewportRect().Size;
		_cols = Mathf.CeilToInt(sz.X / CellW) + 2;
		_rows = Mathf.CeilToInt(sz.Y / CellH) + 1;
		_grid = new string[_cols, _rows];
		ScrambleAll();
	}

	private void ScrambleAll()
	{
		if (_grid == null) return;
		for (int c = 0; c < _cols; c++)
			for (int r = 0; r < _rows; r++)
				_grid[c, r] = Runes[GD.RandRange(0, Runes.Length - 1)];
	}

	// ── Per-frame ─────────────────────────────────────────────────────────────
	public override void _Process(double delta)
	{
		if (!Visible) return;

		_scrambleTimer -= (float)delta;
		if (_scrambleTimer <= 0f)
		{
			_scrambleTimer = ScrambleSec;
			ScrambleAll();
		}
		QueueRedraw();
	}

	// ── Drawing ───────────────────────────────────────────────────────────────
	public override void _Draw()
	{
		if (_grid == null) return;

		var sz    = GetViewportRect().Size;
		// Wave front in column-space: 0 = left edge, _cols+TransitionW = right edge
		// At Progress=0 wave is fully left (nothing covered)
		// At Progress=1 wave is fully right (everything covered)
		float totalCols = _cols + TransitionW;
		float waveHead  = Progress * (totalCols + TransitionW) - TransitionW;  // leading edge

		for (int c = 0; c < _cols; c++)
		{
			float x       = c * CellW;
			float colFrac = waveHead - c; // >TransitionW = fully covered, <0 = untouched

			// ── Fully covered: dark rectangle, no rune ────────────────────────
			if (colFrac >= TransitionW)
			{
				DrawRect(new Rect2(x, 0, CellW, sz.Y), BgColor);
				continue;
			}

			// ── Untouched: fully transparent, skip ───────────────────────────
			if (colFrac <= 0f) continue;

			// ── Active transition zone ────────────────────────────────────────
			float colAlpha = colFrac / TransitionW;  // 0→1 as wave passes

			// Dark background fades in
			DrawRect(new Rect2(x, 0, CellW, sz.Y),
					 new Color(BgColor.R, BgColor.G, BgColor.B, colAlpha));

			// Runes: brightest at wave front, fading as column settles
			float runeAlpha = Mathf.Sin(colAlpha * Mathf.Pi); // peaks at 0.5, zero at edges
			runeAlpha = Mathf.Clamp(runeAlpha * 1.6f, 0f, 1f);

			for (int r = 0; r < _rows; r++)
			{
				float y       = r * CellH;
				float rowFrac = (float)r / _rows;

				// Row stagger: lower rows activate slightly later → cascade feel
				float staggered = Mathf.Clamp(colAlpha - rowFrac * RowStagger, 0f, 1f);
				float a = runeAlpha * staggered;
				if (a < 0.02f) continue;

				// Glow pass (slightly offset, purple tint)
				Color glow = new Color(GlowColor.R, GlowColor.G, GlowColor.B, a * 0.45f);
				DrawString(_font, new Vector2(x + 3, y + CellH * 0.88f),
						   _grid[c, r], HorizontalAlignment.Left, -1, CellW, glow);

				// Core rune
				Color rune = new Color(RuneColor.R, RuneColor.G, RuneColor.B, a);
				DrawString(_font, new Vector2(x + 2, y + CellH * 0.86f),
						   _grid[c, r], HorizontalAlignment.Left, -1, CellW, rune);
			}
		}
	}
}
