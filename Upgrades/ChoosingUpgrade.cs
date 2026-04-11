using Godot;
using System;

public partial class ChoosingUpgrade : MenuButton
{
	public string opt;
	public void OnOptionPressed(){

		string[] splitOpt = opt.Split(':');
		GD.Print(splitOpt[0]);

		if(splitOpt[0] == "AC")
		{
			GetParent<GridContainer>().GetParent<Options>().GetParent<Upgrades>().GetParent<Player>().UltimateCooldownMax = GetParent<GridContainer>().GetParent<Options>().GetParent<Upgrades>().GetParent<Player>().UltimateCooldownMax - 2.5f*splitOpt[1].ToInt();
		}

		else if(splitOpt[0] == "PC")
		{
			GetParent<GridContainer>().GetParent<Options>().GetParent<Upgrades>().GetParent<Player>().maxTimer = GetParent<GridContainer>().GetParent<Options>().GetParent<Upgrades>().GetParent<Player>().maxTimer - .5f*splitOpt[1].ToInt();
		}

		else if(splitOpt[0] == "MH")
		{
			GetParent<GridContainer>().GetParent<Options>().GetParent<Upgrades>().GetParent<Player>().maxHp += 5*splitOpt[1].ToInt();
		}

		else if(splitOpt[0] == "D")
		{
			GetParent<GridContainer>().GetParent<Options>().GetParent<Upgrades>().GetParent<Player>().damage += splitOpt[1].ToInt();
		}

		else if(splitOpt[0] == "AP")
		{
			GetParent<GridContainer>().GetParent<Options>().GetParent<Upgrades>().GetParent<Player>().burstCount += splitOpt[1].ToInt();
		}
		GD.Print(GetParent<GridContainer>().GetParent<Options>().GetParent<Upgrades>().GetParent<Player>().burstCount);
	}

}
