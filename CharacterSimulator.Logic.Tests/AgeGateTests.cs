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

    [Fact]
    public void CalculateAge_HandlesMonthEndDaysCorrectly()
    {
        // Born Oct 31, 2008
        var profile = new Data.Db.UserProfile
        {
            DobYear = 2008,
            DobMonth = 10,
            DobDay = 31
        };

        // On Oct 29, 2026 (2 days before 18th birthday) -> should be 17
        var beforeBirthday = new System.DateTime(2026, 10, 29);
        Assert.Equal(17, profile.CalculateAge(beforeBirthday));
        Assert.False(profile.IsAdultEligible(beforeBirthday));

        // On Oct 31, 2026 (on 18th birthday) -> should be 18
        var onBirthday = new System.DateTime(2026, 10, 31);
        Assert.Equal(18, profile.CalculateAge(onBirthday));
        Assert.True(profile.IsAdultEligible(onBirthday));

        // On Nov 1, 2026 (after 18th birthday) -> should be 18
        var afterBirthday = new System.DateTime(2026, 11, 1);
        Assert.Equal(18, profile.CalculateAge(afterBirthday));
        Assert.True(profile.IsAdultEligible(afterBirthday));
    }
}
