using Godot;

public partial class DpsPlayer : Player
{

	[Export] public AudioStreamPlayer3D ShootSoundPlayer;
	[Export] public AudioStreamPlayer3D UltimateSound;

	// ── Ultra state (server-side) ─────────────────────────────────────────────
	private bool  _ultraActive = false;
	private float _ultraTimer  = 0f;

	public override void _Ready()
	{
		maxHp = 150;
		speed = 20;
		hp    = maxHp / 2;   // DEBUG: halved for relic testing
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
		timer    = maxTimer;

		ShootSoundPlayer?.Play();
		GD.Print(burstCount);
		if (Buls.Count < 60)
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
		_ultraTimer  = 5f;   // 5-second rapid-fire window
	}
}
