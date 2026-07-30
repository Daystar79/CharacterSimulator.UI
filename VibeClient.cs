using System;
using System.Diagnostics;

public class VibeClient : ILLMClient
{
    public string SendPrompt(Character character, string input, string sceneContext, string goalContext = "")
    {
        string prompt = BuildPrompt(character, input, sceneContext, goalContext);
        return ExecuteVibeCLI(prompt);
    }

    private string BuildPrompt(Character character, string input, string sceneContext, string goalContext)
    {
        string targetName = string.IsNullOrEmpty(input) ? "the other character" : "They";
        return $@"You are {character.Name}, {character.Attributes.GetValueOrDefault("hair", "")}, {character.Attributes.GetValueOrDefault("eyes", "")}, wearing {character.Attributes.GetValueOrDefault("clothing", "")}.
Current scene: {sceneContext}
Your current state: {character.CurrentState}. Bond with {targetName}: {character.Bond}.
Last somatic zones: {string.Join(", ", character.SomaticZones)}.{goalContext}
{targetName} just said: ""{input}""
Generate your response as {character.Name}. Include:
1. A somatic reaction (1-2 zones, e.g., ""adjusts her jacket, sighs"").
2. Dialogue (1-2 sentences max).
3. Any changes to your state (e.g., bond +5, new somatic zones).
4. If you resisted or advanced a goal, note it (e.g., ""[Resisted: Seduce]"" or ""[Advanced: Seduce]"").
Response format:[Somatic: {{zones}}] {{dialogue}} [Goal: {{status}}]";
    }

    private string ExecuteVibeCLI(string prompt)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "vibe",
                    Arguments = $"--no-stream \"{prompt.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output.Trim();
        }
        catch (Exception ex)
        {
            return $"[ERROR: Vibe CLI failed - {ex.Message}]";
        }
    }
}
