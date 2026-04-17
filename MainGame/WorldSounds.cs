using Godot;

public partial class WorldSounds : Node
{
	// ── Inspector slots — world ambience ──────────────────────────────────────

	[Export] public AudioStreamPlayer LightningSound;
	[Export] public AudioStreamPlayer AirSound;
	[Export] public AudioStreamPlayer EnvironmentSound;
	[Export] public float LightningInterval = 20f;

	// ── Private ───────────────────────────────────────────────────────────────

	private Timer   _lightningTimer;
	private Node3D  _parentNode3D;
	private RandomNumberGenerator _rng = new RandomNumberGenerator();

	public override void _Ready()
	{
		_rng.Randomize();

		// ── Fallback silent players for un-wired world slots ──────────────────
		if (LightningSound == null)   { LightningSound   = _MakePlayer("LightningSound"); }
		if (AirSound == null)         { AirSound         = _MakePlayer("AirSound"); }
		if (EnvironmentSound == null) { EnvironmentSound = _MakePlayer("EnvironmentSound"); }

		// ── Constant ambient — restart when clip ends ─────────────────────────
		AirSound.Finished         += () => { if (AirSound.Stream != null         && _IsGameVisible()) AirSound.Play(); };
		EnvironmentSound.Finished += () => { if (EnvironmentSound.Stream != null && _IsGameVisible()) EnvironmentSound.Play(); };

		// ── Lightning timer — NOT autostarted; started in StartSounds() ───────
		_lightningTimer = new Timer { WaitTime = LightningInterval, Autostart = false };
		AddChild(_lightningTimer);
		_lightningTimer.Timeout += () =>
		{
			if (LightningSound.Stream != null && _IsGameVisible())
				LightningSound.Play();
		};

		// ── Watch parent (MainGame root) visibility ───────────────────────────
		if (GetParent() is Node3D parent)
		{
			_parentNode3D = parent;
			_parentNode3D.VisibilityChanged += OnParentVisibilityChanged;
		}

		OnParentVisibilityChanged();
	}

	public override void _ExitTree()
	{
		if (_parentNode3D != null)
			_parentNode3D.VisibilityChanged -= OnParentVisibilityChanged;
	}

	// ── Visibility gate ───────────────────────────────────────────────────────

	private void OnParentVisibilityChanged()
	{
		if (_IsGameVisible()) StartSounds();
		else                  StopSounds();
	}

	private void StartSounds()
	{
		if (AirSound.Stream         != null && !AirSound.Playing)         AirSound.Play();
		if (EnvironmentSound.Stream != null && !EnvironmentSound.Playing) EnvironmentSound.Play();

		if (_lightningTimer.IsStopped())  _lightningTimer.Start();
	}

	private void StopSounds()
	{
		AirSound?.Stop();
		EnvironmentSound?.Stop();
		LightningSound?.Stop();
		_lightningTimer?.Stop();
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private AudioStreamPlayer _MakePlayer(string nodeName)
	{
		var p = new AudioStreamPlayer { Name = nodeName };
		AddChild(p);
		return p;
	}

	private bool _IsGameVisible() =>
		_parentNode3D != null ? _parentNode3D.Visible : true;
}
