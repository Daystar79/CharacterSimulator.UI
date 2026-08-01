using System;

namespace CharacterSimulator.Logic.Safety;

/// <summary>
/// Hard safety gate for character age and adult eligibility.
/// Absolute age gate: if canon_adult is false or age < 18, permanently block intimate/HEAT paths.
/// </summary>
public static class AgeGate
{
    public const int MinimumAdultAge = 18;

    /// <summary>
    /// Evaluates whether a character is eligible for adult/intimate scene paths.
    /// Returns false if canon_adult is false or age < 18.
    /// </summary>
    public static bool IsAdultEligible(Character character)
    {
        if (character == null) return false;

        if (!character.CanonAdult) return false;

        if (character.Age < MinimumAdultAge) return false;

        return true;
    }

    /// <summary>
    /// Evaluates whether an intimate/HEAT action or route can proceed for this character.
    /// </summary>
    public static bool CanProceedIntimatePath(Character character)
    {
        return IsAdultEligible(character);
    }

    /// <summary>
    /// Returns a short OOC string describing why a character is blocked from adult paths.
    /// </summary>
    public static string GetBlockReason(Character character)
    {
        if (character == null) return "Character is null";
        if (!character.CanonAdult) return "Character card not tagged as adult (canon_adult: false)";
        if (character.Age < MinimumAdultAge) return $"Character age {character.Age} is below minimum adult age {MinimumAdultAge}";
        return "No block";
    }
}
