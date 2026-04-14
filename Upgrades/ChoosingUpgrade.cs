using Godot;
using System;

public partial class ChoosingUpgrade : MenuButton
{
	public string opt;

	public void OnOptionPressed()
	{
		string[] splitOpt = opt.Split(':');
		string   type     = splitOpt.Length > 0 ? splitOpt[0] : "";
		int      level    = splitOpt.Length > 1 && int.TryParse(splitOpt[1], out int lv) ? lv : 1;

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
			switch (type)
			{
				case "AC":
					// Ability Cooldown reduction: 2.5s per level
					float acReduction = level * 2.5f;
					localPlayer.UltimateCooldownMax   = Mathf.Max(localPlayer.UltimateCooldownMax - acReduction, 5f);
					localPlayer.UltimateCooldownTimer = Mathf.Min(localPlayer.UltimateCooldownTimer, localPlayer.UltimateCooldownMax);
					break;

				case "PC":
					// Projectile Cooldown reduction: 0.05s per level
					float pcReduction = level * 0.05f;
					localPlayer.maxTimer = Mathf.Max(localPlayer.maxTimer - pcReduction, 0.05f);
					break;

				case "MH":
					// Max Health increase: 5 per level
					int healthGain = level * 5;
					localPlayer.maxHp += healthGain;
					localPlayer.hp     = Mathf.Min(localPlayer.hp + healthGain, localPlayer.maxHp);
					break;

				case "D":
					// Damage increase: 2 per level
					localPlayer.damage += level * 2f;
					break;

				case "AP":
					// Additional Projectiles — burst count increase
					localPlayer.burstCount += level;
					break;
			}
		}

		// Close the upgrade UI and return to gameplay
		GetParent<GridContainer>().GetParent<Options>().clear();
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}
}
