using Godot;

public partial class AboutScene : Control
{
    public void OnBackPressed()
    {
        // Walk up the tree to find the embedded MainMenuLobby and hide this
        // overlay without doing a full scene reload (which would kill the network).
        Node node = GetParent();
        while (node != null)
        {
            if (node is MainMenuLobby lobby)
            {
                lobby.ShowMainPanel();
                return;
            }
            node = node.GetParent();
        }

        // Fallback: only used if this scene is opened standalone (not as an overlay).
        _ = SceneTransition.Instance.TransitionTo("res://MainMenu/main_menu_lobby.tscn");
    }
}
