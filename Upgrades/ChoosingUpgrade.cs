using Godot;
using System;

public partial class ChoosingUpgrade : MenuButton
{
	public string opt;
	void OnPressed()
	{
		GetParent<GridContainer>().GetParent<Options>().chosenOpt = opt;
	}
}
