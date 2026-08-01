using Avalonia.Media.Imaging;

namespace CharacterSimulator.GUI;

public class DialogueMessageModel
{
    public int TurnIndex { get; set; }
    public string SpeakerName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string Dialogue { get; set; } = string.Empty;
    public string SomaticText { get; set; } = string.Empty;
    public string BondDeltaText { get; set; } = string.Empty;
    public string GoalStatusText { get; set; } = string.Empty;
    public string SpeakerEmotion { get; set; } = "Neutral";
    public string SpeakerEmotionEmoji { get; set; } = "😐";
    public string SpeakerColor { get; set; } = "#00D2FF";
    public string SpeakerBg { get; set; } = "#1E293B";
    public bool IsLeft { get; set; } = true;
    public string? ImagePrompt { get; set; }
    public Bitmap? SpeakerBitmap { get; set; }
}

public class GoalViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class InventoryItemViewModel
{
    public string Name { get; set; } = string.Empty;
}
