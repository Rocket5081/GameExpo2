using Godot;
using System;

public partial class Enemy : CharacterBody3D
{
	[Export] public int damage;
	[Export] public int hp;
	[Export] public int maxHP;
	[Export] public NetID myId;

	[Export] public AnimationPlayer myAnimation;

	protected NavigationAgent3D navAgent;

	private Node3D         _healthBarRoot;
	private MeshInstance3D _barBg;
	private MeshInstance3D _barFill;

	private float       _damageCooldown   = 0f;
	private const float DamageCooldownTime = 0.5f;
	private Player      _contactPlayer    = null;

	// Track who last shot us so Die() can award the kill
	private Player _lastShooter = null;

	public override void _Ready()
	{
		AddToGroup("enemy");
		base._Ready();

		BuildHealthBar();

		navAgent = GetNode<NavigationAgent3D>("NavigationAgent3D");
        navAgent.PathPostprocessing = NavigationPathQueryParameters3D.PathPostProcessing.Edgecentered;

		if (GenericCore.Instance != null && GenericCore.Instance.IsServer)
			SetupContactArea();

		GetParent().GetParent<MainGame>().Enms.Add(this);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		if (_damageCooldown > 0f)
			_damageCooldown -= (float)delta;

		if (GenericCore.Instance != null && GenericCore.Instance.IsServer
			&& _contactPlayer != null && _damageCooldown <= 0f)
		{
			_contactPlayer.hp = Mathf.Max(_contactPlayer.hp - damage, 0);
			_damageCooldown   = DamageCooldownTime;

			// Contact damage counts as "damage dealt" for score purposes
			_contactPlayer.NotifyDamageDealt(damage);
		}

		UpdateHealthBar();
	}

	private void BuildHealthBar()
	{
		_healthBarRoot                 = new Node3D();
		_healthBarRoot.Position        = new Vector3(0f, 6f, 0f);
		_healthBarRoot.RotationDegrees = new Vector3(0f, 180f, 0f);
		AddChild(_healthBarRoot);

		float barW = 1.6f;
		float barH = 0.22f;

		var bgMesh = new BoxMesh { Size = new Vector3(barW + 0.06f, barH + 0.06f, 0.01f) };
		var bgMat  = new StandardMaterial3D
		{
			AlbedoColor   = new Color(0.15f, 0.15f, 0.15f),
			ShadingMode   = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode      = BaseMaterial3D.CullModeEnum.Disabled,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
		};
		bgMesh.Material = bgMat;

		_barBg            = new MeshInstance3D { Mesh = bgMesh };
		_barBg.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		_healthBarRoot.AddChild(_barBg);

		var fillMesh = new BoxMesh { Size = new Vector3(barW, barH, 0.02f) };
		var fillMat  = new StandardMaterial3D
		{
			AlbedoColor   = new Color(0.1f, 1f, 0.1f),
			ShadingMode   = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode      = BaseMaterial3D.CullModeEnum.Disabled,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
		};
		fillMesh.Material = fillMat;

		_barFill            = new MeshInstance3D { Mesh = fillMesh };
		_barFill.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		_barFill.Position   = new Vector3(0f, 0f, 0.02f);
		_healthBarRoot.AddChild(_barFill);
	}

	private void UpdateHealthBar()
	{
		if (_barFill == null || maxHP <= 0) return;

		float ratio = Mathf.Clamp((float)hp / maxHP, 0f, 1f);
		float barW  = 1.6f;

		_barFill.Scale    = new Vector3(ratio, 1f, 1f);
		_barFill.Position = new Vector3((ratio - 1f) * barW * 0.5f, 0f, 0.02f);

		if (_barFill.Mesh is BoxMesh bm && bm.Material is StandardMaterial3D mat)
			mat.AlbedoColor = new Color(1f - ratio, ratio * 0.9f, 0.05f);
	}

	private void SetupContactArea()
	{
		var area            = new Area3D();
		area.CollisionLayer = 8;
		area.CollisionMask  = 2;
		area.Monitoring     = true;

		var shape   = new CollisionShape3D();
		shape.Shape = new SphereShape3D { Radius = 1.4f };
		area.AddChild(shape);
		AddChild(area);

		area.BodyEntered += OnPlayerBodyEntered;
		area.BodyExited  += OnPlayerBodyExited;
	}

	private void OnPlayerBodyEntered(Node3D body)
	{
		if (body is not Player player) return;
		_contactPlayer = player;
	}

	private void OnPlayerBodyExited(Node3D body)
	{
		if (body is Player)
			_contactPlayer = null;
	}

	// shooter = the Player whose bullet hit us (may be null for contact damage)
	public virtual void TakeDamage(int amount, Player shooter = null)
	{
		if (shooter != null)
		{
			_lastShooter = shooter;
			shooter.NotifyDamageDealt(amount);
		}

		hp -= amount;
		if (hp <= 0)
			Die();
	}

	protected virtual void Die()
	{
		_lastShooter?.NotifyKill();
		QueueFree();
	}

	public void OnHitByBullet(int amount, Player shooter = null)
	{
		TakeDamage(amount, shooter);
		myAnimation?.Play("Hurt");
	}
}