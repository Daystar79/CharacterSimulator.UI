using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public class TurnManager
{
    private readonly ILLMClient _clientA;
    private readonly ILLMClient _clientB;
    private readonly SceneManager _sceneManager;
    private readonly Logger _logger;

    public TurnManager(ILLMClient clientA, ILLMClient clientB, SceneManager sceneManager, Logger logger)
    {
        _clientA = clientA;
        _clientB = clientB;
        _sceneManager = sceneManager;
        _logger = logger;
    }

    public void RunConversation(Character charA, Character charB, string scene, int maxTurns)
    {
        _sceneManager.SetScene(scene);
        _logger.LogScene(scene);

        charA.ResistanceCount[charB.Name] = 0;
        charB.ResistanceCount[charA.Name] = 0;

        string lastInputB = "";

        for (int turn = 0; turn < maxTurns; turn++)
        {
            // Client A Turn
            Goal activeGoalA = GetActiveGoal(charA, charB.Name);
            string goalContextA = activeGoalA != null ?
                $"Your current goal: {activeGoalA.Type} {activeGoalA.Target} (Intensity: {activeGoalA.Intensity}). Strategies: {string.Join(", ", activeGoalA.Strategies)}." :
                "";

            string promptA = _clientA.SendPrompt(charA, lastInputB, scene, goalContextA);
            var (dialogueA, somaticA, bondDeltaA, goalStatusA) = ParseResponse(promptA);

            charA.SomaticZones = somaticA;
            charA.Bond += bondDeltaA;
            _logger.LogTurn(charA.Name, dialogueA, somaticA, charA.Bond, activeGoalA?.Type, goalStatusA);

            if (activeGoalA != null)
            {
                if (charA.EvaluateSuccess(activeGoalA, charB))
                {
                    _logger.LogGoalSuccess(charA.Name, activeGoalA.Type, charB.Name);
                    charA.Goals.Remove(activeGoalA);
                }
                else if (charA.EvaluateFailure(activeGoalA, charB))
                {
                    _logger.LogGoalFailure(charA.Name, activeGoalA.Type, charB.Name);
                    activeGoalA.CooldownRemaining = activeGoalA.Cooldown;
                    activeGoalA.Attempts++;
                }
            }

            // Client B Turn
            Goal activeGoalB = GetActiveGoal(charB, charA.Name);
            string goalContextB = activeGoalB != null ?
                $"Your current goal: {activeGoalB.Type} {activeGoalB.Target} (Intensity: {activeGoalB.Intensity}). Strategies: {string.Join(", ", activeGoalB.Strategies)}." :
                "";

            string promptB = _clientB.SendPrompt(charB, dialogueA, scene, goalContextB);
            var (dialogueB, somaticB, bondDeltaB, goalStatusB) = ParseResponse(promptB);

            charB.SomaticZones = somaticB;
            charB.Bond += bondDeltaB;
            _logger.LogTurn(charB.Name, dialogueB, somaticB, charB.Bond, activeGoalB?.Type, goalStatusB);

            if (activeGoalB != null)
            {
                if (charB.EvaluateSuccess(activeGoalB, charA))
                {
                    _logger.LogGoalSuccess(charB.Name, activeGoalB.Type, charA.Name);
                    charB.Goals.Remove(activeGoalB);
                }
                else if (charB.EvaluateFailure(activeGoalB, charA))
                {
                    _logger.LogGoalFailure(charB.Name, activeGoalB.Type, charA.Name);
                    activeGoalB.CooldownRemaining = activeGoalB.Cooldown;
                    activeGoalB.Attempts++;
                }
            }

            foreach (var goal in charA.Goals) if (goal.CooldownRemaining > 0) goal.CooldownRemaining--;
            foreach (var goal in charB.Goals) if (goal.CooldownRemaining > 0) goal.CooldownRemaining--;

            lastInputB = dialogueB;
        }
    }

    private Goal GetActiveGoal(Character character, string targetName)
    {
        return character.Goals
            .Where(g => g.Target == targetName && g.CooldownRemaining == 0)
            .OrderByDescending(g => g.Priority)
            .ThenByDescending(g => g.Intensity)
            .FirstOrDefault();
    }

    private (string Dialogue, List<string> SomaticZones, int BondDelta, string GoalStatus) ParseResponse(string response)
    {
        var somaticMatch = Regex.Match(response, @"\[Somatic: (.*?)\]");
        var somaticZones = somaticMatch.Success ?
            somaticMatch.Groups[1].Value.Split(',').Select(s => s.Trim()).ToList() :
            new List<string>();

        var dialogue = Regex.Replace(response, @"\[Somatic:.*?\]", "").Trim();
        dialogue = Regex.Replace(dialogue, @"\[Goal:.*?\]", "").Trim();

        var bondDelta = 0;
        var bondMatch = Regex.Match(response, @"bond (\+|-)(\d+)");
        if (bondMatch.Success)
        {
            bondDelta = int.Parse(bondMatch.Groups[2].Value) * (bondMatch.Groups[1].Value == "+" ? 1 : -1);
        }

        var goalStatus = "None";
        var goalMatch = Regex.Match(response, @"\[Goal: (.*?)\]");
        if (goalMatch.Success)
        {
            goalStatus = goalMatch.Groups[1].Value;
        }

        return (dialogue, somaticZones, bondDelta, goalStatus);
    }
}
