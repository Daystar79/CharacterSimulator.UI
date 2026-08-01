using System;
using System.Collections.Generic;

namespace CharacterSimulator.Logic;

public class TurnStepEventArgs : EventArgs
{
    public int TurnIndex { get; set; }
    public string SpeakerName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string Dialogue { get; set; } = string.Empty;
    public List<string> SomaticZones { get; set; } = new List<string>();
    public int BondDelta { get; set; }
    public int CurrentBond { get; set; }
    public string SpeakerEmotion { get; set; } = "Neutral";
    public string SpeakerEmotionEmoji { get; set; } = "😐";
    public string? ActiveGoalType { get; set; }
    public string GoalStatus { get; set; } = "None";
    public string SceneContext { get; set; } = string.Empty;
    public string RawAgentOutput { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string? ImagePrompt { get; set; }
}

public class GoalEvaluationEventArgs : EventArgs
{
    public string CharacterName { get; set; } = string.Empty;
    public string GoalType { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
}
