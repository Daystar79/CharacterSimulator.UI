public class SceneManager
{
    private string _currentScene;
    public void SetScene(string scene) => _currentScene = scene;
    public string GetScene() => _currentScene;
}
