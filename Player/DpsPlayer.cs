using Godot;

public partial class DpsPlayer : Player
{
	private int   burstCount = 3;
	private float burstDelay = 0.1f;

	[Export] public AudioStreamPlayer3D ShootSoundPlayer;
	[Export] public AudioStreamPlayer3D UltimateSound;

	// ── Ultra state (server-side) ─────────────────────────────────────────────
	private bool  _ultraActive = false;
	private float _ultraTimer  = 0f;

	public override void _Ready()
	{
		maxHp = 150;
		speed = 20;
		hp    = maxHp;
		base._Ready();

		if (UltimateSound == null)
		{
			UltimateSound = new AudioStreamPlayer3D();
			AddChild(UltimateSound);
		}
	}

	protected override void OnLocalUltimateActivated()
	{
		UltimateSound?.Play();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

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

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public override void UseUltimate()
	{
		if (!GenericCore.Instance.IsServer) return;

		_ultraActive = true;
		_ultraTimer  = 5f;
	}
}
