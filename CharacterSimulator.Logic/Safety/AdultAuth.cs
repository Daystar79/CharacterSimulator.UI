using System;

namespace CharacterSimulator.Logic.Safety;

/// <summary>
/// Dual-control safety authority requiring both user adult attestation AND card eligibility before allowing adult content.
/// </summary>
public static class AdultAuth
{
    private static bool _userAdultAttested = false;

    public static bool IsUserAdultAttested => _userAdultAttested;

    public static void SetUserAdultAttested(bool attested)
    {
        _userAdultAttested = attested;
    }

    /// <summary>
    /// Checks if intimate or adult scene paths are authorized for a given character.
    /// Requires BOTH user adult attestation AND character age eligibility.
    /// </summary>
    public static bool IsAdultPathAuthorized(Character character)
    {
        if (!_userAdultAttested) return false;
        return AgeGate.IsAdultEligible(character);
    }
}
