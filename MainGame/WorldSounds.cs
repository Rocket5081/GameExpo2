using Godot;
public partial class WorldSounds : Node
{
	// ── Inspector slots ───────────────────────────────────────────────────────

	[Export] public AudioStreamPlayer LightningSound;
	[Export] public AudioStreamPlayer AirSound;
	[Export] public AudioStreamPlayer EnvironmentSound;
	[Export] public float LightningInterval = 20f;

	private Timer _lightningTimer;

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
		AirSound.Finished         += () => { if (AirSound.Stream != null)         AirSound.Play(); };
		EnvironmentSound.Finished += () => { if (EnvironmentSound.Stream != null) EnvironmentSound.Play(); };

		if (AirSound.Stream != null)         AirSound.Play();
		if (EnvironmentSound.Stream != null) EnvironmentSound.Play();

		// ── Lightning — fires every LightningInterval seconds ─────────────────
		_lightningTimer = new Timer();
		_lightningTimer.WaitTime  = LightningInterval;
		_lightningTimer.Autostart = true;
		AddChild(_lightningTimer);
		_lightningTimer.Timeout += () =>
		{
			if (LightningSound.Stream != null)
				LightningSound.Play();
		};
	}
}
