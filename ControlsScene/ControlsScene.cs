using Godot;

public partial class ControlsScene : Control
{
    public void OnBackPressed()
    {
        GetTree().ChangeSceneToFile("res://MainMenu/main_menu_lobby.tscn");
    }
}
