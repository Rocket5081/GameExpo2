using Godot;
using System;

public partial class ChoosingUpgrade : MenuButton
{
	public string opt;
	public void OnOptionPressed(){

		string[] splitOpt = opt.Split(':');
		GetParent<GridContainer>().GetParent<Options>().GetParent<Upgrades>().GetParent<Player>().upgrade(splitOpt);
		Input.MouseMode = Input.MouseModeEnum.Hidden;
		GetParent<GridContainer>().GetParent<Options>().clear();
	}

}
