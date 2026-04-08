using Godot;
using System;

public partial class Options : Control
{

	GridContainer grid;

	private string[] RolledOptions = new string[3];

	RandomNumberGenerator random = new RandomNumberGenerator();

	string randomOpt;
	public string chosenOpt;

	public override void _Ready()
	{
		random.Randomize();
		grid = GetNode<GridContainer>("GridContainer");
		add();
		//clear();
	}


	public void add()
	{
		for(int i=0; i<3; i++)
		{
			
			int rand = random.RandiRange(0,14);
			randomOpt = GetParent<Upgrades>().options[rand];
			while(randomOpt == "empty")
			{
				rand = random.RandiRange(0,14);
				randomOpt = GetParent<Upgrades>().options[rand];
			}
			GetParent<Upgrades>().options[rand] = "empty";
			RolledOptions[i] = randomOpt;
			
			string button = "res://Upgrades/choosing_upgrade.tscn";
			PackedScene packedscene = GD.Load<PackedScene>(button);
			ChoosingUpgrade Opt = packedscene.Instantiate<ChoosingUpgrade>();
			if(randomOpt == "AC1")
			{
				Opt.Text = "Ability Cooldown \n\tLevel 1 \nReduces \n\t\tCooldown -2s";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "AC2")
			{
				Opt.Text = "Ability Cooldown \n\tLevel 1 \nReduces \n\t\tCooldown -5s";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "AC3")
			{
				Opt.Text = "Ability Cooldown \n\tLevel 1 \nReduces \n\t\tCooldown -10s";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "PC1")
			{
				Opt.Text = "Projectile Cooldown \n\tLevel 1 \nReduces \n\t\tCooldown -2s";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "PC2")
			{
				Opt.Text = "Projectile Cooldown \n\tLevel 1 \nReduces \n\t\tCooldown -5s";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "PC3")
			{
				Opt.Text = "Projectile Cooldown \n\tLevel 1 \nReduces \n\t\tCooldown -10s";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "MH1")
			{
				Opt.Text = "Max Health \n\tLevel 1\n\t\tHealth +5";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "MH2")
			{
				Opt.Text = "Max Health \n\tLevel 2\n\t\tHealth +10";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "MH3")
			{
				Opt.Text = "Max Health \n\tLevel 3\n\t\tHealth +20";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "D1")
			{
				Opt.Text = "Damage Up \n\tLevel 1\n\t\tDamage +???";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "D2")
			{
				Opt.Text = "Damage Up \n\tLevel 2\n\t\tDamage +???";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "D3")
			{
				Opt.Text = "Damage Up \n\tLevel 3\n\t\tDamage +???";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "AP1")
			{
				Opt.Text = "Additional Projectiles \n\tLevel 1\n\t\tProjectiles +1";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "AP2")
			{
				Opt.Text = "Additional Projectiles \n\tLevel 2\n\t\tProjectiles +2";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "AP3")
			{
				Opt.Text = "Additional Projectiles \n\tLevel 3\n\t\tProjectiles +3";
				Opt.opt = randomOpt;
			}
			else
			{
				Opt.Text = "";
				Opt.opt = "";
			}
			grid.AddChild(Opt);
		}
	}

	public void clear()
	{
		int count = grid.GetChildCount();
		for(int i=0; i<count; i++)
		{
			grid.GetChild(0).Free();
		}
		for(int i=0; i<3; i++)
		{
			for(int j=0; j<GetParent<Upgrades>().options.Length; j++)
			{
				if(GetParent<Upgrades>().options[j] == "empty")
				{
					GetParent<Upgrades>().options[j] =  RolledOptions[i];
				}
			}
			RolledOptions[i] = "empty";
		}
		}
}
