using Xunit;
using CharacterSimulator.Logic.Hygiene;

namespace CharacterSimulator.Logic.Tests;

public class SystemLeakLinterTests
{
    [Fact]
    public void SystemLeakLinter_DetectsAndRedactsKnownLeaks()
    {
        string leakingDialogue = "I feel like I am in Focus Lock and entering Realm VIII while managing my Debt Ledger.";

        var result = SystemLeakLinter.Audit(leakingDialogue);

        Assert.True(result.HasCriticalLeaks);
        Assert.Equal(3, result.Findings.Count);
        Assert.Contains(result.Findings, f => f.Match == "Focus Lock");
        Assert.Contains(result.Findings, f => f.Match == "Realm VIII");
        Assert.Contains(result.Findings, f => f.Match == "Debt Ledger");
        Assert.DoesNotContain("Focus Lock", result.SanitizedDialogue);
        Assert.DoesNotContain("Realm VIII", result.SanitizedDialogue);
        Assert.DoesNotContain("Debt Ledger", result.SanitizedDialogue);
    }
}
