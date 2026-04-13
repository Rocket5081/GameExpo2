using Godot;

public partial class WorldSounds : Node
{
	// ── Inspector slots ───────────────────────────────────────────────────────

	[Export] public AudioStreamPlayer LightningSound;
	[Export] public AudioStreamPlayer AirSound;
	[Export] public AudioStreamPlayer EnvironmentSound;
	[Export] public float LightningInterval = 20f;

	private Timer   _lightningTimer;
	private Node3D  _parentNode3D;   // the MainGame root node; watched for visibility

	public override void _Ready()
	{
		// Create fallback silent players for any un-wired slots
		if (LightningSound == null)
		{
			LightningSound = new AudioStreamPlayer();
			LightningSound.Name = "LightningSound";
			AddChild(LightningSound);
		}
		if (AirSound == null)
		{
			AirSound = new AudioStreamPlayer();
			AirSound.Name = "AirSound";
			AddChild(AirSound);
		}
		if (EnvironmentSound == null)
		{
			EnvironmentSound = new AudioStreamPlayer();
			EnvironmentSound.Name = "EnvironmentSound";
			AddChild(EnvironmentSound);
		}

		// ── Constant sounds — restart automatically when the clip ends ────────
		AirSound.Finished         += () => { if (AirSound.Stream != null && _IsGameVisible())         AirSound.Play(); };
		EnvironmentSound.Finished += () => { if (EnvironmentSound.Stream != null && _IsGameVisible()) EnvironmentSound.Play(); };

		// ── Lightning timer — created but NOT autostarted ─────────────────────
		_lightningTimer = new Timer();
		_lightningTimer.WaitTime  = LightningInterval;
		_lightningTimer.Autostart = false;
		AddChild(_lightningTimer);
		_lightningTimer.Timeout += () =>
		{
			if (LightningSound.Stream != null && _IsGameVisible())
				LightningSound.Play();
		};

		// ── Watch parent (MainGame root) visibility ───────────────────────────
		// generic_lobby_system loads MainGame with visible=false so sounds must
		// not start until the game world actually becomes visible to the player.
		if (GetParent() is Node3D parent)
		{
			_parentNode3D = parent;
			_parentNode3D.VisibilityChanged += OnParentVisibilityChanged;
		}

		// Apply current visibility state right now
		OnParentVisibilityChanged();
	}

	public override void _ExitTree()
	{
		if (_parentNode3D != null)
			_parentNode3D.VisibilityChanged -= OnParentVisibilityChanged;
	}

	// ── Called whenever the MainGame node is shown or hidden ─────────────────

	private void OnParentVisibilityChanged()
	{
		if (_IsGameVisible())
			StartSounds();
		else
			StopSounds();
	}

	private void StartSounds()
	{
		if (AirSound.Stream         != null && !AirSound.Playing)         AirSound.Play();
		if (EnvironmentSound.Stream != null && !EnvironmentSound.Playing) EnvironmentSound.Play();
		if (!_lightningTimer.IsStopped()) return;
		_lightningTimer.Start();
	}

	private void StopSounds()
	{
		AirSound?.Stop();
		EnvironmentSound?.Stop();
		LightningSound?.Stop();
		_lightningTimer?.Stop();
	}

	private bool _IsGameVisible() =>
		_parentNode3D != null ? _parentNode3D.Visible : true;
}
