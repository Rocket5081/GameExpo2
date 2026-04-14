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
	}

	public void add()
	{
			
		if(GetParent<Upgrades>().GetParent<Player>().myId.IsLocal){
		Input.MouseMode = Input.MouseModeEnum.Visible;
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
			if(randomOpt == "AC:1")
			{
				Opt.Text = "Ability Cooldown \n\tLevel 1 \nReduces \n\t\tCooldown -2.5s";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "AC:2")
			{
				Opt.Text = "Ability Cooldown \n\tLevel 2 \nReduces \n\t\tCooldown -5s";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "AC:3")
			{
				Opt.Text = "Ability Cooldown \n\tLevel 3 \nReduces \n\t\tCooldown -7.5s";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "PC:1")
			{
				Opt.Text = "Projectile Cooldown \n\tLevel 1 \nReduces \n\t\tCooldown -.05s";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "PC:2")
			{
				Opt.Text = "Projectile Cooldown \n\tLevel 2 \nReduces \n\t\tCooldown -.1s";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "PC:3")
			{
				Opt.Text = "Projectile Cooldown \n\tLevel 3 \nReduces \n\t\tCooldown -.15s";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "MH:1")
			{
				Opt.Text = "Max Health \n\tLevel 1\n\t\tHealth +5";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "MH:2")
			{
				Opt.Text = "Max Health \n\tLevel 2\n\t\tHealth +10";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "MH:3")
			{
				Opt.Text = "Max Health \n\tLevel 3\n\t\tHealth +15";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "D:1")
			{
				Opt.Text = "Damage Up \n\tLevel 1\n\t\tDamage +???";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "D:2")
			{
				Opt.Text = "Damage Up \n\tLevel 2\n\t\tDamage +???";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "D:3")
			{
				Opt.Text = "Damage Up \n\tLevel 3\n\t\tDamage +???";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "AP:1")
			{
				Opt.Text = "Additional Projectiles \n\tLevel 1\n\t\tProjectiles +1";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "AP:2")
			{
				Opt.Text = "Additional Projectiles \n\tLevel 2\n\t\tProjectiles +2";
				Opt.opt = randomOpt;
			}
			else if(randomOpt == "AP:3")
			{
				Opt.Text = "Additional Projectiles \n\tLevel 3\n\t\tProjectiles +3";
				Opt.opt = randomOpt;
			}
			else
			{
				Opt.Text = "";
				Opt.opt = "";
			}
			Opt.Pressed += Opt.OnOptionPressed;
			grid.AddChild(Opt);
		}
		}
	}

	public void clear()
	{
		int count = grid.GetChildCount();
		for(int i=0; i<count; i++)
		{
			grid.GetChild(i).QueueFree();
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

