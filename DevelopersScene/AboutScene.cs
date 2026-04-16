using Godot;

public partial class AboutScene : Control
{
    public async void OnBackPressed()
    {
        await SceneTransition.Instance.TransitionTo("res://MainMenu/main_menu_lobby.tscn");
    }
}
