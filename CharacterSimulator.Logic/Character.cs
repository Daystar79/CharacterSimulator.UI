using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CharacterSimulator.Logic;

public class Goal
{
    public string Type { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public int Intensity { get; set; }
    public List<string> Strategies { get; set; } = new List<string>();
    public string SuccessCondition { get; set; } = string.Empty;
    public string FailureCondition { get; set; } = string.Empty;
    public int Cooldown { get; set; }
    public int Priority { get; set; }
    public int CooldownRemaining { get; set; }
    public int Attempts { get; set; }
}

public class SessionVariant
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string OpeningBeat { get; set; } = string.Empty;
}

public class Character
{
    public string Name { get; set; } = string.Empty;
    public string CardPath { get; set; } = string.Empty;
    public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    public string Bio { get; set; } = string.Empty;
    public string CurrentState { get; set; } = "DORMANT";
    public string CognitiveBias { get; set; } = string.Empty;
    public string CognitiveGift { get; set; } = string.Empty;
    public string BiasState { get; set; } = "DORMANT"; // DORMANT, DEFENSIVE_ACTIVE, GENERATIVE_ACTIVE
    public int BiasStrength { get; set; } = 50;
    public string CulturalBias { get; set; } = string.Empty;
    public string VoiceSyntacticalEngine { get; set; } = string.Empty;
    public string ConversationalStance { get; set; } = string.Empty;
    public string VerbalDefense { get; set; } = string.Empty;
    public string GenerativeStance { get; set; } = string.Empty;
    public List<string> LatentAnchors { get; set; } = new List<string>();
    public List<SessionVariant> SessionVariants { get; set; } = new List<SessionVariant>();
    public string? LogPath { get; set; }
    public string ActiveFocus { get; set; } = string.Empty;
    public Dictionary<string, int> RelationalBaselines { get; set; } = new Dictionary<string, int>();
    public List<string> Memories { get; set; } = new List<string>();
    public List<string> ActiveSkills { get; set; } = new List<string>();
    public int Stress { get; set; } = 0;
    public int Arousal { get; set; } = 0;
    public int Fatigue { get; set; } = 0;
    public int Pain { get; set; } = 0;
    public string Emotion { get; set; } = "Neutral";
    public string EmotionEmoji { get; set; } = "😐";
    public string Impulse { get; set; } = string.Empty;
    public int Bond { get; set; }
    public int Health { get; set; } = 100;
    public int Energy { get; set; } = 100;
    public List<string> Inventory { get; set; } = new List<string>();
    public List<string> SomaticZones { get; set; } = new List<string>();
    public List<Goal> Goals { get; set; } = new List<Goal>();
    public Dictionary<string, int> ResistanceCount { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> SuspicionLevel { get; set; } = new Dictionary<string, int>();
    public string? AvatarPath { get; set; }

    public bool CanonAdult { get; set; } = true;
    public int Age { get; set; } = 25;
    public Logs.DurableLog? DurableLog { get; set; }
    public State.PsychosomaticStateSnapshot? LiveState { get; set; }

    public void UpdateEmotionFromSomatic(List<string> somaticZones, string dialogue)
    {
        string text = (string.Join(" ", somaticZones) + " " + dialogue).ToLowerInvariant();

        if (text.Contains("smirk") || text.Contains("smile") || text.Contains("chuckle"))
        {
            Emotion = "Smirking";
            EmotionEmoji = "😏";
        }
        else if (text.Contains("narrow") || text.Contains("glance") || text.Contains("suspicio") || text.Contains("crosses arms"))
        {
            Emotion = "Suspicious";
            EmotionEmoji = "🤔";
        }
        else if (text.Contains("sigh") || text.Contains("blush") || text.Contains("hesitat") || text.Contains("taps fingers"))
        {
            Emotion = "Flustered";
            EmotionEmoji = "😳";
        }
        else if (text.Contains("glare") || text.Contains("angry") || text.Contains("frown") || text.Contains("threat"))
        {
            Emotion = "Angry";
            EmotionEmoji = "😠";
        }
        else if (text.Contains("focus") || text.Contains("aim") || text.Contains("nods"))
        {
            Emotion = "Focused";
            EmotionEmoji = "🎯";
        }
        else
        {
            Emotion = "Neutral";
            EmotionEmoji = "😐";
        }
    }

    public bool IsGoalActive(Goal goal, string targetName)
    {
        if (goal.CooldownRemaining > 0) return false;
        if (goal.Target != targetName) return false;
        return true;
    }

    public bool EvaluateSuccess(Goal goal, Character targetCharacter)
    {
        if (string.IsNullOrEmpty(goal.SuccessCondition)) return false;
        if (goal.SuccessCondition.Contains("bond >="))
        {
            var match = Regex.Match(goal.SuccessCondition, @"bond >= (\d+)");
            if (match.Success)
            {
                int threshold = int.Parse(match.Groups[1].Value);
                if (this.Bond < threshold) return false;
            }
        }
        return true;
    }

    public bool EvaluateFailure(Goal goal, Character targetCharacter)
    {
        if (string.IsNullOrEmpty(goal.FailureCondition)) return false;
        if (goal.FailureCondition.Contains("bond <"))
        {
            var match = Regex.Match(goal.FailureCondition, @"bond < (\d+)");
            if (match.Success)
            {
                int threshold = int.Parse(match.Groups[1].Value);
                if (this.Bond < threshold) return true;
            }
        }
        if (goal.FailureCondition.Contains("target_resisted >="))
        {
            var match = Regex.Match(goal.FailureCondition, @"target_resisted >= (\d+)");
            if (match.Success)
            {
                int threshold = int.Parse(match.Groups[1].Value);
                if (this.ResistanceCount.ContainsKey(targetCharacter.Name) &&
                    this.ResistanceCount[targetCharacter.Name] >= threshold)
                    return true;
            }
        }
        return false;
    }
}
