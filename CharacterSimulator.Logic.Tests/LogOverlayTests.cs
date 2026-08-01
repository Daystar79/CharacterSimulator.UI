using System.Collections.Generic;
using Xunit;
using CharacterSimulator.Logic;
using CharacterSimulator.Logic.Logs;

namespace CharacterSimulator.Logic.Tests;

public class LogOverlayTests
{
    [Fact]
    public void ApplyOverlay_LogOverlayWinsOverCardDefaults()
    {
        var character = new Character
        {
            Name = "Test Character",
            CurrentState = "DORMANT",
            ActiveFocus = "Origin",
            BiasStrength = 30,
            ActiveSkills = new List<string> { "BaseSkill" },
            Memories = new List<string> { "BaseMemory" },
            RelationalBaselines = new Dictionary<string, int> { ["Partner"] = 20 }
        };

        var log = new DurableLog
        {
            snapshot = new DurableLogSnapshot
            {
                active_focus = "VIII — Integration",
                bias_strength = 75,
                default_somatic = "chest tightens"
            },
            skills = new SkillSet { active = new List<string> { "OverlaySkill" } },
            memories = new MemorySet { detailed = new List<string> { "OverlayMemory" } },
            relational_baselines = new Dictionary<string, int> { ["Partner"] = 80 }
        };

        DurableLogStore.ApplyOverlay(character, log);

        Assert.Equal("VIII — Integration", character.ActiveFocus);
        Assert.Equal("VIII — Integration", character.CurrentState);
        Assert.Equal(75, character.BiasStrength);
        Assert.Contains("OverlaySkill", character.ActiveSkills);
        Assert.Contains("OverlayMemory", character.Memories);
        Assert.Equal(80, character.RelationalBaselines["Partner"]);
        Assert.Contains("chest tightens", character.SomaticZones);
    }
}
