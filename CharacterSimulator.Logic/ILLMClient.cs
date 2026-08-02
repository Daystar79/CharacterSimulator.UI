using System.Threading;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic;

public interface ILLMClient
{
    string SendPrompt(Character character, string input, string sceneContext, string goalContext = "");
    
    Task<string> SendPromptAsync(Character character, string input, string sceneContext, string goalContext = "", CancellationToken ct = default);
}
