using System;
using System.Collections.Generic;
using CharacterSimulator.Logic.State;

namespace CharacterSimulator.Logic.Logs;

/// <summary>
/// Port of Midlayer `logs_io.apply_pressure`.
/// Deterministic transformation write-back for character bias_strength and history.
/// </summary>
public static class PressureApplicator
{
    public static readonly Dictionary<string, int> StrengthDeltaMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["low"] = 0,
        ["medium"] = 5,
        ["high"] = 10,
        ["extreme"] = 15,
    };

    public static DurableLog ApplyPressure(
        DurableLog log,
        string movementId,
        string pressure,
        string strength,
        string notes = "",
        string? deltaOverride = null,
        string? permanence = null,
        string? somaticOverride = null,
        int? biasDelta = null)
    {
        log.EnsureShape();

        string strengthKey = (strength ?? "medium").Trim().ToLowerInvariant();
        int autoDelta = biasDelta ?? (StrengthDeltaMap.TryGetValue(strengthKey, out var val) ? val : 5);

        var snap = log.snapshot;
        int oldBias = ScaleClamps.Clamp0To100(snap.bias_strength);
        int newBias = oldBias;

        if (strengthKey != "low" && autoDelta != 0)
        {
            newBias = ScaleClamps.Clamp0To100(oldBias + autoDelta);
            snap.bias_strength = newBias;
        }

        if (!string.IsNullOrWhiteSpace(somaticOverride))
        {
            snap.default_somatic = somaticOverride;
        }

        snap.as_of = movementId;

        // Low pressure with no delta override is scene-local only (no history row)
        if (strengthKey == "low" && string.IsNullOrEmpty(deltaOverride))
        {
            return log;
        }

        string effectivePermanence = permanence ?? strengthKey switch
        {
            "low" => "temporary",
            "medium" => "medium",
            "high" => "permanent",
            "extreme" => "permanent",
            _ => "medium"
        };

        string deltaText;
        if (!string.IsNullOrEmpty(deltaOverride))
        {
            deltaText = deltaOverride;
        }
        else if (newBias != oldBias)
        {
            deltaText = $"bias_strength {oldBias}→{newBias}";
        }
        else
        {
            deltaText = "no weight shift";
        }

        string capitalStrength = char.ToUpperInvariant(strengthKey[0]) + strengthKey.Substring(1);
        string pressureText = $"{pressure} · {capitalStrength}";

        log.history ??= new List<HistoryEntry>();
        log.history.Add(new HistoryEntry
        {
            movement = movementId,
            pressure = pressureText,
            delta = deltaText,
            permanence = effectivePermanence,
            notes = notes ?? ""
        });

        return log;
    }
}
