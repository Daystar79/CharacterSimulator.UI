public interface ILLMClient
{
    string SendPrompt(Character character, string input, string sceneContext, string goalContext = "");
}
