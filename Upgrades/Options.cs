using Godot;
using System;

public partial class Options : Control
{
	[Export] public AudioStreamPlayer UpgradeSfx;

	GridContainer grid;

	private string[] RolledOptions = new string[3];

	RandomNumberGenerator random = new RandomNumberGenerator();

	string randomOpt;
	public string chosenOpt;

	// ── Persistent UI — must NOT be children of grid so clear() can't free them ──
	private ColorRect _backdrop;
	private Label     _titleLabel;

	public override void _Ready()
	{
		random.Randomize();
		grid = GetNode<GridContainer>("GridContainer");

		// ── Full-screen dark backdrop ─────────────────────────────────────────
		_backdrop              = new ColorRect();
		_backdrop.Color        = new Color(0f, 0f, 0f, 0.72f);
		_backdrop.AnchorLeft   = 0f;
		_backdrop.AnchorTop    = 0f;
		_backdrop.AnchorRight  = 1f;
		_backdrop.AnchorBottom = 1f;
		_backdrop.GrowHorizontal = Control.GrowDirection.Both;
		_backdrop.GrowVertical   = Control.GrowDirection.Both;
		_backdrop.MouseFilter    = Control.MouseFilterEnum.Ignore;
		_backdrop.Visible        = false;
		AddChild(_backdrop);          // child of Options, not grid
		MoveChild(_backdrop, 0);      // render behind GridContainer

		// ── Title label — also a child of Options, not grid ───────────────────
		_titleLabel = new Label();
		_titleLabel.Text = "Hurry!!! ✦ CHOOSE YOUR UPGRADE ✦";
		_titleLabel.AddThemeFontSizeOverride("font_size", 22);
		_titleLabel.AddThemeColorOverride("font_color",         new Color(0.85f, 0.70f, 1f));
		_titleLabel.AddThemeColorOverride("font_outline_color", new Color(0f,    0f,    0f, 1f));
		_titleLabel.AddThemeConstantOverride("outline_size", 2);
		_titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		// Anchor to vertical centre of screen; shift upward 215 px to clear the cards
		_titleLabel.AnchorLeft   = 0f;
		_titleLabel.AnchorTop    = 0.5f;
		_titleLabel.AnchorRight  = 1f;
		_titleLabel.AnchorBottom = 0.5f;
		_titleLabel.OffsetTop    = -215f;
		_titleLabel.OffsetBottom = -185f;
		_titleLabel.GrowHorizontal = Control.GrowDirection.Both;
		_titleLabel.GrowVertical   = Control.GrowDirection.Both;
		_titleLabel.MouseFilter    = Control.MouseFilterEnum.Ignore;
		_titleLabel.Visible        = false;
		AddChild(_titleLabel);        // child of Options, not grid
	}

	public void add()
	{
		if (GetParent<Upgrades>().GetParent<Player>().myId.IsLocal)
		{
			Input.MouseMode    = Input.MouseModeEnum.Visible;
			_backdrop.Visible  = true;
			_titleLabel.Visible = true;
			UpgradeSfx?.Play();

			for (int i = 0; i < 3; i++)
			{
				int rand = random.RandiRange(0, 14);
				randomOpt = GetParent<Upgrades>().options[rand];
				while (randomOpt == "empty")
				{
					rand      = random.RandiRange(0, 14);
					randomOpt = GetParent<Upgrades>().options[rand];
				}
				GetParent<Upgrades>().options[rand] = "empty";
				RolledOptions[i] = randomOpt;

				PackedScene packedscene = GD.Load<PackedScene>("res://Upgrades/choosing_upgrade.tscn");
				ChoosingUpgrade Opt     = packedscene.Instantiate<ChoosingUpgrade>();

				ConfigureCard(Opt, randomOpt);
				grid.AddChild(Opt);
			}
		}
	}

	// ── Map option codes → card data ──────────────────────────────────────────
	private static void ConfigureCard(ChoosingUpgrade card, string opt)
	{
		switch (opt)
		{
			case "AC:1":
				card.Configure(opt, "Ability Cooldown", "Level 1",
					"⏱", new Color(0.4f, 0.8f, 1f),
					"Reduces your ultimate cooldown by 2.5 seconds.");
				break;
			case "AC:2":
				card.Configure(opt, "Ability Cooldown", "Level 2",
					"⏱", new Color(0.2f, 0.7f, 1f),
					"Reduces your ultimate cooldown by 5 seconds.");
				break;
			case "AC:3":
				card.Configure(opt, "Ability Cooldown", "Level 3",
					"⏱", new Color(0.0f, 0.55f, 1f),
					"Reduces your ultimate cooldown by 7.5 seconds.");
				break;

			case "PC:1":
				card.Configure(opt, "Fire Rate", "Level 1",
					"⚡", new Color(1f, 0.85f, 0.2f),
					"Fire 5% faster — shoot cooldown −0.05s.");
				break;
			case "PC:2":
				card.Configure(opt, "Fire Rate", "Level 2",
					"⚡", new Color(1f, 0.70f, 0.0f),
					"Fire 10% faster — shoot cooldown −0.1s.");
				break;
			case "PC:3":
				card.Configure(opt, "Fire Rate", "Level 3",
					"⚡", new Color(1f, 0.50f, 0.0f),
					"Fire 15% faster — shoot cooldown −0.15s.");
				break;

			case "MH:1":
				card.Configure(opt, "Max Health", "Level 1",
					"♥", new Color(1f, 0.35f, 0.45f),
					"Increases maximum health by 5.");
				break;
			case "MH:2":
				card.Configure(opt, "Max Health", "Level 2",
					"♥", new Color(1f, 0.2f, 0.3f),
					"Increases maximum health by 10.");
				break;
			case "MH:3":
				card.Configure(opt, "Max Health", "Level 3",
					"♥", new Color(0.9f, 0.0f, 0.2f),
					"Increases maximum health by 15.");
				break;

			case "D:1":
				card.Configure(opt, "Damage Up", "Level 1",
					"⚔", new Color(1f, 0.55f, 0.2f),
					"Your attacks deal more damage. Level 1 boost.");
				break;
			case "D:2":
				card.Configure(opt, "Damage Up", "Level 2",
					"⚔", new Color(1f, 0.38f, 0.0f),
					"Your attacks deal more damage. Level 2 boost.");
				break;
			case "D:3":
				card.Configure(opt, "Damage Up", "Level 3",
					"⚔", new Color(0.9f, 0.2f, 0.0f),
					"Your attacks deal more damage. Level 3 boost.");
				break;

			case "AP:1":
				card.Configure(opt, "Extra Projectiles", "Level 1",
					"◈", new Color(0.55f, 0.35f, 1f),
					"Fire 1 additional projectile per shot.");
				break;
			case "AP:2":
				card.Configure(opt, "Extra Projectiles", "Level 2",
					"◈", new Color(0.45f, 0.2f, 0.9f),
					"Fire 2 additional projectiles per shot.");
				break;
			case "AP:3":
				card.Configure(opt, "Extra Projectiles", "Level 3",
					"◈", new Color(0.35f, 0.1f, 0.8f),
					"Fire 3 additional projectiles per shot.");
				break;

			default:
				card.Configure(opt, "Unknown", "",
					"?", new Color(0.5f, 0.5f, 0.5f),
					"Mysterious upgrade.");
				break;
		}
	}

	public void clear()
	{
		_backdrop.Visible    = false;
		_titleLabel.Visible  = false;

		// Only free ChoosingUpgrade cards — _backdrop and _titleLabel live on Options
		// and must never be freed here.
		foreach (Node child in grid.GetChildren())
		{
			if (child is ChoosingUpgrade)
				child.QueueFree();
		}

		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < GetParent<Upgrades>().options.Length; j++)
			{
				if (GetParent<Upgrades>().options[j] == "empty")
				{
					GetParent<Upgrades>().options[j] = RolledOptions[i];
				}
			}
			RolledOptions[i] = "empty";
		}
	}
}
