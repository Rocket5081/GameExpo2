using Godot;

public partial class PlayerCamera : Camera3D
{
	[Export] public float followSpeed = 10f;
	[Export] public float mouseSensitivity = 0.002f;
	[Export] public float pitchMin = -75f;   // negative = looking UP in Godot
	[Export] public float pitchMax = 40f;    // positive = looking DOWN

	//offset will change once the actual models are in, this is just a placeholder
	[Export] public Vector3 offset = new Vector3(-5.0f,2.0f, 2.0f);

	private Player localPlayer;
	private float _cameraPitch = 0f;

	public override void _UnhandledInput(InputEvent @event)
	{
		if (localPlayer == null) return;
		if (!localPlayer.myId.IsLocal) return;

		if (@event is InputEventMouseMotion mouseMotion)
		{
			// Relative.Y is negative when mouse moves UP (screen coords).
			// In Godot, negative Rotation.X = looking UP, so += is correct.
			_cameraPitch += mouseMotion.Relative.Y * mouseSensitivity;
			_cameraPitch = Mathf.Clamp(
				_cameraPitch,
				0,0 //This fixed the camera after the upgrades change
				// Mathf.DegToRad(pitchMin), 
				// Mathf.DegToRad(pitchMax)
			);
		}

		if (@event is InputEventKey key
			&& key.Pressed
			&& key.Keycode == Key.Escape)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
	}

	public override void _Process(double delta)
	{
		if (localPlayer == null)
		{
			FindLocalPlayer();
			return;
		}

		Vector3 rotatedOffset = localPlayer.Transform.Basis * offset;

		GlobalPosition = GlobalPosition.Lerp(
			localPlayer.GlobalPosition + rotatedOffset,
			followSpeed * (float)delta
		);

		//rotate camera to face the player. my be changed when we have actual models
		Rotation = new Vector3(_cameraPitch, localPlayer.Rotation.Y + Mathf.DegToRad(-90), 0);
	}

	private void FindLocalPlayer()
	{
		foreach (Node node in GetTree().GetNodesInGroup("Players"))
		{
			if (node is Player player && player.myId != null && player.myId.IsLocal)
			{
				localPlayer = player;
				MakeCurrent();  // take over as the active camera
				GD.Print("Camera locked onto local player: " + player.Name);
				return;
			}
		}
	}
}
