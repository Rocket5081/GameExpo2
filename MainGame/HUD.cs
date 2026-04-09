using Godot;

/// <summary>
/// Standalone HUD CanvasLayer.
/// Lives directly in MainGame.tscn so it is always present regardless of which
/// player scene spawns.  In _Process it watches for the local player to become
/// ready, then un-hides the matching reticle exactly once.
/// </summary>
public partial class HUD : CanvasLayer
{
	private Control _dpsReticle;
	private Control _tankReticle;
	private Control _supportReticle;

	private bool _reticleSet = false;

	public override void _Ready()
	{
		_dpsReticle     = GetNode<Control>("DpsReticle");
		_tankReticle    = GetNode<Control>("TankReticle");
		_supportReticle = GetNode<Control>("SupportReticle");

		// All hidden until we know which class this client chose.
		_dpsReticle.Visible     = false;
		_tankReticle.Visible    = false;
		_supportReticle.Visible = false;
	}

	public override void _Process(double delta)
	{
		if (_reticleSet) return;

		// Poll until the local player node is spawned and its NetID is ready.
		foreach (Node node in GetTree().GetNodesInGroup("Players"))
		{
			if (node is Player player && player.myId != null && player.myId.IsLocal)
			{
				if      (player is DpsPlayer)     _dpsReticle.Visible     = true;
				else if (player is TankPlayer)    _tankReticle.Visible    = true;
				else if (player is SupportPlayer) _supportReticle.Visible = true;

				_reticleSet = true;
				return;
			}
		}
	}
}
