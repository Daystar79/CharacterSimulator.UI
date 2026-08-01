using System.Collections.Generic;
using System.Linq;

namespace CharacterSimulator.Logic;

public class RpgOption
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = "Dialogue"; // Dialogue, Somatic, Goal, SkillCheck
    public string CategoryEmoji { get; set; } = "🗣️";
    public string Text { get; set; } = string.Empty;
    public string TargetCharacter { get; set; } = string.Empty;
    public Goal? AssociatedGoal { get; set; }
}

public static class RpgChoiceService
{
    /// <summary>
    /// Player-facing options. Scene may color the location mention;
    /// options do not force a genre (ops/cyberpunk/etc.) onto the characters.
    /// </summary>
    public static List<RpgOption> GenerateOptions(Character speaker, Character target, string sceneContext)
    {
        var options = new List<RpgOption>();
        string place = string.IsNullOrWhiteSpace(sceneContext)
            ? "here"
            : sceneContext.Split(',')[0].Trim();

        options.Add(new RpgOption
        {
            Id = "opt_dialogue_1",
            Category = "Dialogue",
            CategoryEmoji = "🗣️",
            Text = $"Ask {target.Name} how they feel about being in {place}",
            TargetCharacter = target.Name
        });

        options.Add(new RpgOption
        {
            Id = "opt_somatic_1",
            Category = "Somatic Action",
            CategoryEmoji = "🎭",
            Text = $"Quietly match {target.Name}'s pace and watch their natural mannerisms",
            TargetCharacter = target.Name
        });

        var activeGoal = speaker.Goals.FirstOrDefault(g => g.Target == target.Name && g.CooldownRemaining == 0);
        if (activeGoal != null)
        {
            options.Add(new RpgOption
            {
                Id = "opt_goal_1",
                Category = "Goal Strategy",
                CategoryEmoji = "🎯",
                Text = $"Pursue your goal [{activeGoal.Type}] using ({activeGoal.Strategies.FirstOrDefault() ?? "your usual approach"})",
                TargetCharacter = target.Name,
                AssociatedGoal = activeGoal
            });
        }
        else
        {
            options.Add(new RpgOption
            {
                Id = "opt_skill_1",
                Category = "Connection",
                CategoryEmoji = "🎲",
                Text = $"Try to understand {target.Name} on their own terms",
                TargetCharacter = target.Name
            });
        }

        options.Add(new RpgOption
        {
            Id = "opt_identity_1",
            Category = "Identity",
            CategoryEmoji = "🪞",
            Text = $"Invite {target.Name} to speak as themselves — not as the setting expects",
            TargetCharacter = target.Name
        });

        return options;
    }
}
