using Godot;

public partial class AboutScene : Control
{
    public void OnBackPressed()
    {
        GetTree().ChangeSceneToFile("res://MainMenu/main_menu_lobby.tscn");
    }
}
