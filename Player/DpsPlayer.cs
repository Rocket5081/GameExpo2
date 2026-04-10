using Godot;

public partial class DpsPlayer : Player
{
	private int   burstCount = 3;
	private float burstDelay = 0.1f;

	[Export] public AudioStreamPlayer3D ShootSoundPlayer;

	// ── Ultimate audio: drag your .mp3/.wav into this slot in the Inspector ──
	[Export] public AudioStream UltimateSfx;
	private AudioStreamPlayer3D _ultPlayer;

	// ── Ultra state (server-side) ─────────────────────────────────────────────
	private bool  _ultraActive = false;
	private float _ultraTimer  = 0f;

	public override void _Ready()
	{
		maxHp = 150;
		speed = 20;
		hp    = maxHp;
		base._Ready();

		// Audio player for the ultimate activation sound
		_ultPlayer = new AudioStreamPlayer3D();
		AddChild(_ultPlayer);
	}

	// Plays the ult activation sound locally the instant Q is pressed
	protected override void OnLocalUltimateActivated()
	{
		if (UltimateSfx != null)
		{
			_ultPlayer.Stream = UltimateSfx;
			_ultPlayer.Play();
		}
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		// Ultra: force canShoot=true every frame so fire rate is instant
		if (!GenericCore.Instance.IsServer) return;
		if (!_ultraActive) return;

		_ultraTimer -= (float)delta;
		if (_ultraTimer <= 0f)
			_ultraActive = false;
		else
			canShoot = true;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void Fire()
	{
		if (!GenericCore.Instance.IsServer) return;
		if (!canShoot) return;

		canShoot = false;
		timer    = 0.5f;

		ShootSoundPlayer?.Play();

		if (Buls.Count < 9)
			SpawnBullet(burstCount, burstDelay);
		else
			ShootBullet(burstCount, burstDelay);
	}

	/// <summary>
	/// DPS Ultimate: fires with zero cooldown for 5 seconds.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void UseUltimate()
	{
		if (!GenericCore.Instance.IsServer) return;

		_ultraActive = true;
		_ultraTimer  = 5f;
	}
}
