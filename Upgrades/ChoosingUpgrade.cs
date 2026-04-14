using Godot;

public partial class ChoosingUpgrade : MenuButton
{
	public string opt;

	public void OnOptionPressed()
	{
		// Find the local player
		Player localPlayer = null;
		foreach (Node n in GetTree().GetNodesInGroup("Players"))
		{
			if (n is Player p && p.myId != null && p.myId.IsLocal)
			{
				localPlayer = p;
				break;
			}
		}

		if (localPlayer != null)
		{
			// Send to the server — it applies the stat change authoritatively and
			// broadcasts the result back to all peers via SyncUpgradeRpc.
			localPlayer.RpcId(1, Player.MethodName.ServerApplyUpgrade, opt);
		}

		// Close the upgrade UI and return to gameplay
		GetParent<GridContainer>().GetParent<Options>().clear();
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}
}
