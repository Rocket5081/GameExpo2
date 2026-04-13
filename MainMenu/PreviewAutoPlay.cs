using Godot;


public partial class PreviewAutoPlay : Node3D
{
	public override void _Ready()
	{
		// Defer one frame so instanced GLB children are fully initialised
		CallDeferred(MethodName.PlayIdle);
	}

	private void PlayIdle()
	{
		var player = FindAnimationPlayer(this);
		if (player == null)
		{
			GD.PrintErr($"[PreviewAutoPlay] No AnimationPlayer found under {Name}");
			return;
		}

		string[] animations = player.GetAnimationList();
		if (animations.Length == 0)
		{
			GD.PrintErr($"[PreviewAutoPlay] AnimationPlayer under {Name} has no animations");
			return;
		}

		
		string chosen = null;
		foreach (string anim in animations)
		{
			if ( anim.ToLower().Contains("idle") || anim.ToLower().Contains("special") )
			{
				chosen = anim;
				break;
			}
		}

		
		if (chosen == null)
			chosen = animations[0];

	
		var animResource = player.GetAnimation(chosen);
		if (animResource != null)
			animResource.LoopMode = Animation.LoopModeEnum.Linear;

		player.Play(chosen);
		GD.Print($"[PreviewAutoPlay] Playing '{chosen}' on {Name}");
	}

	private static AnimationPlayer FindAnimationPlayer(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is AnimationPlayer ap)
				return ap;

			var found = FindAnimationPlayer(child);
			if (found != null)
				return found;
		}
		return null;
	}
}
