namespace CharacterSimulator.GUI.Components.Workspace;

/// <summary>System / diagnostics log row for the dialogue workspace.</summary>
public sealed class LogEntryModel
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Message { get; set; } = "";
    public string LevelClass { get; set; } = "log-info";
}

/// <summary>Chat / RP feed bubble for the dialogue workspace.</summary>
public sealed class ChatMessageModel
{
    public string SpeakerName { get; set; } = "";
    public string TargetName { get; set; } = "";
    public string Dialogue { get; set; } = "";
    public string SomaticText { get; set; } = "";
    public string BondDeltaText { get; set; } = "";
    public string SpeakerEmotionEmoji { get; set; } = "💬";
    public string SpeakerColor { get; set; } = "#38BDF8";
    public bool IsLeft { get; set; } = true;
    public bool IsSystem { get; set; } = false;
}
