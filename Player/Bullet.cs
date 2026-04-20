using System.Linq;
using Godot;

public partial class Bullet : RigidBody3D
{
	[Export] public NetID myId;

	private float _lifetime = 0f;
	private const float MaxLifetime = 6f;
	public float damage;

	// The player who fired this bullet — set by the spawning player
	public Player Shooter = null;

	private bool _isDying = false;

	public Godot.Collections.Dictionary rewindValues = new Godot.Collections.Dictionary
	{
		{"position", new Godot.Collections.Array {}},
		{"rotation", new Godot.Collections.Array {}},
	};

	[Export] public bool rewinding = false;

	public bool _rewindRecordingStarted = false;

	public override void _Ready()
	{
		if (GenericCore.Instance != null && !GenericCore.Instance.IsServer)
			Freeze = true;
	}

	public override void _Process(double delta)
	{
		if (myId == null || !myId.IsNetworkReady) return;
		if (!GenericCore.Instance.IsServer) return;

		_lifetime += (float)delta;
		if (_lifetime >= MaxLifetime)
			HideBullet();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!rewinding)
		{
			// Only record once the boss has actually spawned so the buffer only
			// contains frames from the boss-fight start onward.
			if (GenericCore.Instance.BossHasSpawned)
			{
				if (!_rewindRecordingStarted)
				{
					((Godot.Collections.Array)rewindValues["position"]).Clear();
					((Godot.Collections.Array)rewindValues["rotation"]).Clear();
					_rewindRecordingStarted = true;
				}
				((Godot.Collections.Array)rewindValues["position"]).Add(Position);
				((Godot.Collections.Array)rewindValues["rotation"]).Add(Rotation);
			}
		}
		else if (GenericCore.Instance.IsServer)
		{
			// Drain X frames per physics tick → ~5× faster rewind.

			for (int i = 0; i < 5; i++)
			{
				computeRewind();
				if (!rewinding) break;   // EndRewind was called inside computeRewind
			}
		}
	}

	private void OnAreaEntered(Node body)
	{
		
		if (!GenericCore.Instance.IsServer) return;
		if (_isDying) return;

		if (body is Player) return;

		if (body is Enemy enemy && IsInstanceValid(enemy))
		{
			// Pass the shooter so the enemy can credit score correctly
			enemy.OnHitByBullet((int)damage, Shooter);
			HideBullet();
		}
		if(body.GetParent().GetParent().GetParent().GetParent().GetParent() is Enemy boss && IsInstanceValid(boss))
		{
			boss.OnHitByBullet((int)damage, Shooter);
			HideBullet();
		}
	}

	private void HideBullet()
	{
		if (_isDying) return;
		_isDying  = true;
		_lifetime = 0f;

		Hide();

		var col  = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col  != null) col.SetDeferred("disabled", true);

		var area = GetNodeOrNull<Area3D>("Area3D");
		if (area != null) area.SetDeferred("monitoring", false);
	}

	public void Reset()
	{
		_isDying  = false;
		_lifetime = 0f;
		Shooter   = null;

		var col  = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (col  != null) col.SetDeferred("disabled", false);

		var area = GetNodeOrNull<Area3D>("Area3D");
		if (area != null) area.SetDeferred("monitoring", true);
	}

	public void rewind()
	{
		rewinding = true;
	}

	//https://www.youtube.com/watch?v=XoETrCrSkks a link for a complete description of rewind feature: 1:12 - 3:44
	public void computeRewind()
	{
		var posArr = (Godot.Collections.Array)rewindValues["position"];
		var rotArr = (Godot.Collections.Array)rewindValues["rotation"];
		var velArr = (Godot.Collections.Array)rewindValues["velocity"];


		if (posArr.Count == 0)
		{
			rewinding = false;
			if (Multiplayer.HasMultiplayerPeer())
				GenericCore.Instance.Rpc(nameof(GenericCore.EndRewind));
			else
				GenericCore.Instance.EndRewind();
			return;
		}

		var pos = posArr.Last();
		var rot = rotArr.Last();
		posArr.RemoveAt(posArr.Count - 1);
		rotArr.RemoveAt(rotArr.Count - 1);

		Position = (Vector3)pos;
		Rotation = (Vector3)rot;
		Rpc("computeRewindRPC", (Vector3)pos, (Vector3)rot);

		if (posArr.Count == 0)
		{
			GetNode<CollisionShape3D>("CollisionShape3D").SetDeferred("disabled", false);
			rewinding      = false;
			if (Multiplayer.HasMultiplayerPeer())
				GenericCore.Instance.Rpc(nameof(GenericCore.EndRewind));
			else
				GenericCore.Instance.EndRewind();
		}
	}
	// Authority sends this; all peers (incl. server via CallLocal) apply the position.
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void computeRewindRPC(Vector3 pos, Vector3 rot)
	{
		Position = pos;
		Rotation = rot;
	}
}
