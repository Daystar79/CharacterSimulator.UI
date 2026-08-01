using Xunit;
using CharacterSimulator.Logic;
using CharacterSimulator.Logic.Safety;

namespace CharacterSimulator.Logic.Tests;

public class AgeGateTests
{
    [Fact]
    public void AgeGate_BlocksUnderageOrNonCanonAdult()
    {
        var minorChar = new Character
        {
            Name = "Minor Character",
            CanonAdult = true,
            Age = 16
        };

        var nonAdultCardChar = new Character
        {
            Name = "Non Adult Card",
            CanonAdult = false,
            Age = 25
        };

        var adultChar = new Character
        {
            Name = "Adult Character",
            CanonAdult = true,
            Age = 22
        };

        Assert.False(AgeGate.IsAdultEligible(minorChar));
        Assert.False(AgeGate.IsAdultEligible(nonAdultCardChar));
        Assert.True(AgeGate.IsAdultEligible(adultChar));

        AdultAuth.SetUserAdultAttested(false);
        Assert.False(AdultAuth.IsAdultPathAuthorized(adultChar));

        AdultAuth.SetUserAdultAttested(true);
        Assert.True(AdultAuth.IsAdultPathAuthorized(adultChar));
        Assert.False(AdultAuth.IsAdultPathAuthorized(minorChar));
    }
}
