using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CharacterSimulator.Logic.Safety;

public class HardBanAuditResult
{
    public bool IsClean => Violations.Count == 0;
    public List<string> Violations { get; } = new();
    public string SanitizedDialogue { get; set; } = string.Empty;
}

public static class HardBanFilter
{
    public static List<string> ExtractHardBans(Character character)
    {
        var result = new List<string>();
        if (character == null) return result;

        if (character.Attributes.TryGetValue("hard_bans", out var bansStr) && !string.IsNullOrWhiteSpace(bansStr))
        {
            var parts = bansStr.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                string trimmed = p.Trim().Trim('"', '\'');
                if (!string.IsNullOrWhiteSpace(trimmed)) result.Add(trimmed);
            }
        }

        return result;
    }

    public static HardBanAuditResult AuditAndSanitize(string dialogue, Character character)
    {
        var result = new HardBanAuditResult { SanitizedDialogue = dialogue ?? string.Empty };
        if (string.IsNullOrWhiteSpace(dialogue)) return result;

        var hardBans = ExtractHardBans(character);
        if (hardBans.Count == 0) return result;

        string currentText = dialogue;
        foreach (var ban in hardBans)
        {
            string pattern = @"\b" + Regex.Escape(ban) + @"\b";
            if (Regex.IsMatch(currentText, pattern, RegexOptions.IgnoreCase))
            {
                result.Violations.Add(ban);
                currentText = Regex.Replace(currentText, pattern, "[REDACTED_BAN]", RegexOptions.IgnoreCase);
            }
        }

        result.SanitizedDialogue = currentText;
        return result;
    }
}
