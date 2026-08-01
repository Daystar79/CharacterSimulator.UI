using System;
using System.Collections.Generic;

namespace CharacterSimulator.Logic.Logs;

public class DurableLogSnapshot
{
    public string active_focus { get; set; } = string.Empty;
    public Dictionary<string, object> latent_weights { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int bias_strength { get; set; } = 60;
    public string default_somatic { get; set; } = string.Empty;
    public int flexibility { get; set; } = 40;
    public string as_of { get; set; } = "build";
}

public class SkillSet
{
    public List<string> active { get; set; } = new();
    public List<string> latent { get; set; } = new();
}

public class MemorySet
{
    public List<string> detailed { get; set; } = new();
    public List<string> footnote { get; set; } = new();
}

public class TemporaryEffect
{
    public string id { get; set; } = string.Empty;
    public int? remaining_movements { get; set; }
    public string description { get; set; } = string.Empty;
}

public class HistoryEntry
{
    public string movement { get; set; } = string.Empty;
    public string pressure { get; set; } = string.Empty;
    public string delta { get; set; } = string.Empty;
    public string permanence { get; set; } = string.Empty;
    public string notes { get; set; } = string.Empty;
}

public class DurableLog
{
    public int schema_version { get; set; } = 2;
    public int revision { get; set; } = 1;
    public string updated_at { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    public string? last_commit_id { get; set; }
    public string character_id { get; set; } = string.Empty;
    public DurableLogSnapshot snapshot { get; set; } = new();
    public SkillSet skills { get; set; } = new();
    public MemorySet memories { get; set; } = new();
    public Dictionary<string, int> relational_baselines { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<TemporaryEffect> temporary_effects { get; set; } = new();
    public List<HistoryEntry> history { get; set; } = new();

    public void EnsureShape()
    {
        snapshot ??= new DurableLogSnapshot();
        skills ??= new SkillSet();
        skills.active ??= new List<string>();
        skills.latent ??= new List<string>();
        memories ??= new MemorySet();
        memories.detailed ??= new List<string>();
        memories.footnote ??= new List<string>();
        relational_baselines ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        temporary_effects ??= new List<TemporaryEffect>();
        history ??= new List<HistoryEntry>();
    }
}
