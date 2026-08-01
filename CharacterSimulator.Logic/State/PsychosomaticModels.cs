using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CharacterSimulator.Logic.State;

public static class SomaticZoneEnum
{
    public const string Z1_Cranial_Ocular = "Z1_Cranial_Ocular";
    public const string Z2_Vocal_Cervical = "Z2_Vocal_Cervical";
    public const string Z3_Thoracic_Respiratory = "Z3_Thoracic_Respiratory";
    public const string Z4_Abdominal_Visceral = "Z4_Abdominal_Visceral";
    public const string Z5_Pelvic_Kinesthetic = "Z5_Pelvic_Kinesthetic";
    public const string Z6_Peripheral_Grounding = "Z6_Peripheral_Grounding";

    public static readonly HashSet<string> AllowedZones = new(StringComparer.OrdinalIgnoreCase)
    {
        Z1_Cranial_Ocular,
        Z2_Vocal_Cervical,
        Z3_Thoracic_Respiratory,
        Z4_Abdominal_Visceral,
        Z5_Pelvic_Kinesthetic,
        Z6_Peripheral_Grounding
    };
}

public static class BiasStateEnum
{
    public const string Dormant = "DORMANT";
    public const string DefensiveActive = "DEFENSIVE_ACTIVE";
    public const string GenerativeActive = "GENERATIVE_ACTIVE";

    public static readonly HashSet<string> AllowedStates = new(StringComparer.OrdinalIgnoreCase)
    {
        Dormant,
        DefensiveActive,
        GenerativeActive
    };
}

public static class StatusDynamicEnum
{
    public const string Dominant = "dominant";
    public const string Yielding = "yielding";
    new public const string Equals = "equals";
    public const string Deference = "deference";

    public static readonly HashSet<string> AllowedDynamics = new(StringComparer.OrdinalIgnoreCase)
    {
        Dominant,
        Yielding,
        Equals,
        Deference
    };
}

public class AutonomicState
{
    [JsonPropertyName("arousal")]
    public int Arousal { get; set; }

    [JsonPropertyName("stress")]
    public int Stress { get; set; }

    [JsonPropertyName("fatigue")]
    public int Fatigue { get; set; }

    [JsonPropertyName("pain")]
    public int Pain { get; set; }

    [JsonPropertyName("primary_somatic_zones")]
    public List<string> PrimarySomaticZones { get; set; } = new();

    [JsonPropertyName("autonomic_surge")]
    public bool AutonomicSurge { get; set; }
}

public class AffectiveState
{
    [JsonPropertyName("primary_emotion")]
    public string PrimaryEmotion { get; set; } = string.Empty;

    [JsonPropertyName("secondary_emotion")]
    public string SecondaryEmotion { get; set; } = string.Empty;

    [JsonPropertyName("emotional_intensity")]
    public int EmotionalIntensity { get; set; }

    [JsonPropertyName("impulse")]
    public string Impulse { get; set; } = string.Empty;
}

public class SubconsciousBias
{
    [JsonPropertyName("active_wound")]
    public string ActiveWound { get; set; } = string.Empty;

    [JsonPropertyName("active_gift")]
    public string ActiveGift { get; set; } = string.Empty;

    [JsonPropertyName("bias_state")]
    public string BiasState { get; set; } = BiasStateEnum.Dormant;

    [JsonPropertyName("perceptual_warp")]
    public string PerceptualWarp { get; set; } = string.Empty;
}

public class PerceivedReciprocity
{
    [JsonPropertyName("perceived_liking")]
    public int PerceivedLiking { get; set; }

    [JsonPropertyName("perceived_threat")]
    public int PerceivedThreat { get; set; }
}

public class RelationalVector
{
    [JsonPropertyName("emotional_safety")]
    public int EmotionalSafety { get; set; }

    [JsonPropertyName("attraction_physical")]
    public int AttractionPhysical { get; set; }

    [JsonPropertyName("attraction_emotional")]
    public int AttractionEmotional { get; set; }

    [JsonPropertyName("respect_competence")]
    public int RespectCompetence { get; set; }

    [JsonPropertyName("status_dynamic")]
    public string StatusDynamic { get; set; } = StatusDynamicEnum.Equals;

    [JsonPropertyName("resentment_friction")]
    public int ResentmentFriction { get; set; }

    [JsonPropertyName("perceived_reciprocity")]
    public PerceivedReciprocity PerceivedReciprocity { get; set; } = new();

    [JsonPropertyName("relational_anchors")]
    public List<string> RelationalAnchors { get; set; } = new();
}

public class CompetingDrive
{
    [JsonPropertyName("drive_name")]
    public string DriveName { get; set; } = string.Empty;

    [JsonPropertyName("salience")]
    public int Salience { get; set; }
}

public class PriorityArbitration
{
    [JsonPropertyName("winning_drive")]
    public string WinningDrive { get; set; } = string.Empty;

    [JsonPropertyName("salience_score")]
    public int SalienceScore { get; set; }

    [JsonPropertyName("competing_drives")]
    public List<CompetingDrive> CompetingDrives { get; set; } = new();
}

public class OutputVector
{
    [JsonPropertyName("feels")]
    public string Feels { get; set; } = string.Empty;

    [JsonPropertyName("thinks")]
    public string Thinks { get; set; } = string.Empty;

    [JsonPropertyName("says")]
    public string Says { get; set; } = string.Empty;

    [JsonPropertyName("does")]
    public string Does { get; set; } = string.Empty;
}

public class PsychosomaticStateSnapshot
{
    [JsonPropertyName("character_id")]
    public string CharacterId { get; set; } = string.Empty;

    [JsonPropertyName("timestamp_or_movement")]
    public string TimestampOrMovement { get; set; } = string.Empty;

    [JsonPropertyName("autonomic_state")]
    public AutonomicState AutonomicState { get; set; } = new();

    [JsonPropertyName("affective_state")]
    public AffectiveState AffectiveState { get; set; } = new();

    [JsonPropertyName("subconscious_bias")]
    public SubconsciousBias SubconsciousBias { get; set; } = new();

    [JsonPropertyName("relational_vectors")]
    public Dictionary<string, RelationalVector> RelationalVectors { get; set; } = new();

    [JsonPropertyName("priority_arbitration")]
    public PriorityArbitration PriorityArbitration { get; set; } = new();

    [JsonPropertyName("output_vector")]
    public OutputVector? OutputVector { get; set; }
}
