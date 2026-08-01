namespace CharacterSimulator.Logic;

public class SceneManager
{
    private string _currentScene = string.Empty;

    public void SetScene(string scene)
    {
        _currentScene = scene;
    }

    public string GetScene() => _currentScene;
}
