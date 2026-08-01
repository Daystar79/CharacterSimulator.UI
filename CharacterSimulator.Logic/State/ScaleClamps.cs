using System;

namespace CharacterSimulator.Logic.State;

/// <summary>
/// Enforces integer scale boundaries [0, 100] across all host bookkeeping & state math.
/// Never trusts unclamped free-form numbers from LLMs or raw input.
/// </summary>
public static class ScaleClamps
{
    public const int MinScale = 0;
    public const int MaxScale = 100;

    /// <summary>
    /// Clamps an integer value to the range [0, 100].
    /// </summary>
    public static int Clamp0To100(int value)
    {
        return Math.Clamp(value, MinScale, MaxScale);
    }

    /// <summary>
    /// Validates whether a value is within the acceptable [0, 100] range.
    /// </summary>
    public static bool IsValidScale(int value)
    {
        return value >= MinScale && value <= MaxScale;
    }
}
