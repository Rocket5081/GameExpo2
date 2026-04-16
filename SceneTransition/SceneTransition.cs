using Godot;
using System.Threading.Tasks;

public partial class SceneTransition : CanvasLayer
{
	public static SceneTransition Instance { get; private set; }

	[Export] public float WipeDuration = 0.85f;

	[Export]
	public AudioStream TransitionSFX
	{
		get => _sfx?.Stream;
		set { if (_sfx != null) _sfx.Stream = value; }
	}

	private AudioStreamPlayer _sfx;
	private RuneFlood         _flood;

	public override void _Ready()
	{
		Instance = this;
		_sfx     = GetNode<AudioStreamPlayer>("SFX");
		_flood   = GetNode<RuneFlood>("RuneFlood");
		_flood.Visible = false;
	}

	//Flood runes in left→right, covering the screen. Awaitable.
	public async Task WipeIn()
	{
		_flood.Progress = 0f;
		_flood.Visible  = true;

		if (_sfx.Stream != null) _sfx.Play();

		var tween = CreateTween();
		tween.SetEase(Tween.EaseType.In);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.TweenMethod(Callable.From<float>(SetProgress), 0f, 1f, (double)WipeDuration);
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	// Flood runes out left→right, revealing the screen. Awaitable.
	public async Task WipeOut()
	{
		var tween = CreateTween();
		tween.SetEase(Tween.EaseType.Out);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.TweenMethod(Callable.From<float>(SetProgress), 1f, 0f, (double)WipeDuration);
		await ToSignal(tween, Tween.SignalName.Finished);

		_flood.Visible = false;
	}

	// Full scene-change helper
	public async Task TransitionTo(string scenePath)
	{
		await WipeIn();
		GetTree().ChangeSceneToFile(scenePath);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await WipeOut();
	}

	// ── Internal ──────────────────────────────────────────────────────────────
	private void SetProgress(float p) => _flood.Progress = p;
}
