using Godot;

public partial class WorldSounds : Node
{
	// ── Inspector slots — world ambience ──────────────────────────────────────

	[Export] public AudioStreamPlayer LightningSound;
	[Export] public AudioStreamPlayer AirSound;
	[Export] public AudioStreamPlayer EnvironmentSound;
	[Export] public float LightningInterval = 20f;

	// ── Inspector slots — enemy SFX ───────────────────────────────────────────
	// Drop up to three enemy audio files here. The system picks one at random
	// every 5–10 seconds so the world feels alive without per-enemy overhead.

	[Export] public AudioStreamPlayer Enemy1Sound;
	[Export] public AudioStreamPlayer Enemy2Sound;
	[Export] public AudioStreamPlayer Enemy3Sound;

	[Export] public float EnemySfxMinInterval = 5f;
	[Export] public float EnemySfxMaxInterval = 10f;

	// ── Private ───────────────────────────────────────────────────────────────

	private Timer   _lightningTimer;
	private Timer   _enemySfxTimer;
	private Node3D  _parentNode3D;
	private RandomNumberGenerator _rng = new RandomNumberGenerator();

	public override void _Ready()
	{
		_rng.Randomize();

		// ── Fallback silent players for un-wired world slots ──────────────────
		if (LightningSound == null)   { LightningSound   = _MakePlayer("LightningSound"); }
		if (AirSound == null)         { AirSound         = _MakePlayer("AirSound"); }
		if (EnvironmentSound == null) { EnvironmentSound = _MakePlayer("EnvironmentSound"); }

		// ── Fallback silent players for un-wired enemy slots ─────────────────
		if (Enemy1Sound == null) { Enemy1Sound = _MakePlayer("Enemy1Sound"); }
		if (Enemy2Sound == null) { Enemy2Sound = _MakePlayer("Enemy2Sound"); }
		if (Enemy3Sound == null) { Enemy3Sound = _MakePlayer("Enemy3Sound"); }

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

		// ── Enemy SFX timer — random interval, NOT autostarted ───────────────
		_enemySfxTimer = new Timer { WaitTime = EnemySfxMinInterval, Autostart = false, OneShot = true };
		AddChild(_enemySfxTimer);
		_enemySfxTimer.Timeout += OnEnemySfxTick;

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
		if (_enemySfxTimer.IsStopped())   _ScheduleNextEnemySfx();
	}

	private void StopSounds()
	{
		AirSound?.Stop();
		EnvironmentSound?.Stop();
		LightningSound?.Stop();
		_lightningTimer?.Stop();

		Enemy1Sound?.Stop();
		Enemy2Sound?.Stop();
		Enemy3Sound?.Stop();
		_enemySfxTimer?.Stop();
	}

	// ── Enemy SFX logic ───────────────────────────────────────────────────────

	private void OnEnemySfxTick()
	{
		if (!_IsGameVisible()) return;

		// Collect whichever slots actually have a stream assigned
		var available = new System.Collections.Generic.List<AudioStreamPlayer>();
		if (Enemy1Sound.Stream != null) available.Add(Enemy1Sound);
		if (Enemy2Sound.Stream != null) available.Add(Enemy2Sound);
		if (Enemy3Sound.Stream != null) available.Add(Enemy3Sound);

		if (available.Count > 0)
		{
			var pick = available[_rng.RandiRange(0, available.Count - 1)];
			if (!pick.Playing) pick.Play();
		}

		// Schedule the next tick at a new random interval
		_ScheduleNextEnemySfx();
	}

	private void _ScheduleNextEnemySfx()
	{
		_enemySfxTimer.WaitTime = _rng.RandfRange(EnemySfxMinInterval, EnemySfxMaxInterval);
		_enemySfxTimer.Start();
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
