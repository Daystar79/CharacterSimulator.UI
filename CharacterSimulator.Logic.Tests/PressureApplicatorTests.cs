using Xunit;
using CharacterSimulator.Logic.Logs;

namespace CharacterSimulator.Logic.Tests;

public class PressureApplicatorTests
{
    [Fact]
    public void ApplyPressure_Low_NoHistoryAndNoBiasShift()
    {
        var log = new DurableLog();
        log.snapshot.bias_strength = 50;

        PressureApplicator.ApplyPressure(log, "movement_1", "Low Tension", "low");

        Assert.Equal(50, log.snapshot.bias_strength);
        Assert.Empty(log.history);
        Assert.Equal("movement_1", log.snapshot.as_of);
    }

    [Fact]
    public void ApplyPressure_MediumHighExtreme_AppliesCorrectDeltasAndClamps()
    {
        var log = new DurableLog();
        log.snapshot.bias_strength = 50;

        // Medium: +5 -> 55
        PressureApplicator.ApplyPressure(log, "mov_1", "Confrontation", "medium");
        Assert.Equal(55, log.snapshot.bias_strength);
        Assert.Single(log.history);
        Assert.Equal("medium", log.history[0].permanence);

        // High: +10 -> 65
        PressureApplicator.ApplyPressure(log, "mov_2", "Crisis", "high");
        Assert.Equal(65, log.snapshot.bias_strength);
        Assert.Equal(2, log.history.Count);
        Assert.Equal("permanent", log.history[1].permanence);

        // Extreme: +15 -> 80
        PressureApplicator.ApplyPressure(log, "mov_3", "Breakthrough", "extreme");
        Assert.Equal(80, log.snapshot.bias_strength);

        // Clamp test: +15 again from 95 -> 100 max
        log.snapshot.bias_strength = 95;
        PressureApplicator.ApplyPressure(log, "mov_4", "Overload", "extreme");
        Assert.Equal(100, log.snapshot.bias_strength);
    }
}
