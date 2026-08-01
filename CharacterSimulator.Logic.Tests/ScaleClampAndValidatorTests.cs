using Xunit;
using CharacterSimulator.Logic.State;

namespace CharacterSimulator.Logic.Tests;

public class ScaleClampAndValidatorTests
{
    [Fact]
    public void ScaleClamps_ClampsValuesCorrectly()
    {
        Assert.Equal(0, ScaleClamps.Clamp0To100(-15));
        Assert.Equal(100, ScaleClamps.Clamp0To100(150));
        Assert.Equal(42, ScaleClamps.Clamp0To100(42));
    }

    [Fact]
    public void Validator_ValidatesRequiredKeysAndRanges()
    {
        var snapshot = new PsychosomaticStateSnapshot
        {
            CharacterId = "test_slug",
            AutonomicState = new AutonomicState
            {
                Arousal = 120, // out of range
                Stress = 50,
                Fatigue = 10,
                Pain = 0,
                PrimarySomaticZones = new System.Collections.Generic.List<string> { "Z1_Cranial_Ocular", "InvalidZone" }
            },
            SubconsciousBias = new SubconsciousBias
            {
                BiasState = "INVALID_BIAS_STATE"
            }
        };

        var result = PsychosomaticStateValidator.Validate(snapshot);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("arousal: 120 out of range"));
        Assert.Contains(result.Errors, e => e.Contains("invalid zone 'InvalidZone'"));
        Assert.Contains(result.Errors, e => e.Contains("invalid value 'INVALID_BIAS_STATE'"));
    }

    [Fact]
    public void ClampInPlace_FixesOutRangeValues()
    {
        var snapshot = new PsychosomaticStateSnapshot
        {
            AutonomicState = new AutonomicState { Arousal = 200, Stress = -50 }
        };

        PsychosomaticStateValidator.ClampInPlace(snapshot);

        Assert.Equal(100, snapshot.AutonomicState.Arousal);
        Assert.Equal(0, snapshot.AutonomicState.Stress);
    }
}
