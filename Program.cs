using System;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== CharacterSimulator Testing Pipeline ===");
        
        var serena = CharacterLoader.Load("Characters/serena.md");
        var kira = CharacterLoader.Load("Characters/kira.md");
        Console.WriteLine($"Loaded: {serena.Name} and {kira.Name}");

        ILLMClient grokClient = new GrokClient();
        ILLMClient geminiClient = new AgyClient();

        var sceneManager = new SceneManager();
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        var logger = new Logger($"Output/conversation_{timestamp}.log");

        var turnManager = new TurnManager(grokClient, geminiClient, sceneManager, logger);
        turnManager.RunConversation(serena, kira, "Neon alley at night, rainy", maxTurns: 10);

        Console.WriteLine("Conversation complete. Log saved to Output/");
    }
}
