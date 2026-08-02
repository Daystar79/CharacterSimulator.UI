using System.Threading;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic;

public interface ILLMClient
{
    string SendPrompt(Character character, string input, string sceneContext, string goalContext = "");
    
    Task<string> SendPromptAsync(Character character, string input, string sceneContext, string goalContext = "", CancellationToken ct = default);

    /// <summary>
    /// Free-form completion (no roleplay character prompt assembly).
    /// Used by host tools such as DeriveCard.
    /// </summary>
    Task<string> CompleteRawAsync(string prompt, CancellationToken ct = default);
}
