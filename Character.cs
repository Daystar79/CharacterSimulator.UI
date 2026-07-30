using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class Goal
{
    public string Type { get; set; }
    public string Target { get; set; }
    public int Intensity { get; set; }
    public List<string> Strategies { get; set; } = new List<string>();
    public string SuccessCondition { get; set; }
    public string FailureCondition { get; set; }
    public int Cooldown { get; set; }
    public int Priority { get; set; }
    public int CooldownRemaining { get; set; }
    public int Attempts { get; set; }
}

public class Character
{
    public string Name { get; set; }
    public string CardPath { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    public string CurrentState { get; set; } = "DORMANT";
    public int Bond { get; set; }
    public List<string> SomaticZones { get; set; } = new List<string>();
    public List<Goal> Goals { get; set; } = new List<Goal>();
    public Dictionary<string, int> ResistanceCount { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> SuspicionLevel { get; set; } = new Dictionary<string, int>();

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
