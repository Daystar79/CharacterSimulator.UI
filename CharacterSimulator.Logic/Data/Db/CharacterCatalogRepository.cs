using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CharacterSimulator.Logic.Data.Db;

/// <summary>
/// SQLite projection of character card files for fast UI lookups.
/// Full identity SSOT remains on disk under Characters/{card_id}.json.
/// </summary>
public class CharacterCatalogRecord
{
    public string CardId { get; set; } = "";
    public string FileName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string CallName { get; set; } = "";
    public int Age { get; set; }
    public bool CanonAdult { get; set; } = true;
    public string Description { get; set; } = "";
    public string PhysicalShort { get; set; } = "";
    public string AvatarPath { get; set; } = "";
    public string SourceLabel { get; set; } = "";
    public bool IsDerived { get; set; }
    public string FileMtimeUtc { get; set; } = "";
    public string ContentFingerprint { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class CharacterCatalogRepository
{
    private readonly SqliteConnection _conn;

    public CharacterCatalogRepository(SqliteConnection conn)
    {
        _conn = conn;
    }

    public List<CharacterCatalogRecord> ListAll()
    {
        var list = new List<CharacterCatalogRecord>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            SELECT card_id, file_name, display_name, call_name, age, canon_adult,
                   description, physical_short, avatar_path, source_label, is_derived,
                   file_mtime_utc, content_fingerprint, updated_at
            FROM character_catalog
            ORDER BY display_name COLLATE NOCASE, file_name COLLATE NOCASE;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(ReadRecord(reader));
        return list;
    }

    public CharacterCatalogRecord? GetByCardId(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId)) return null;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            SELECT card_id, file_name, display_name, call_name, age, canon_adult,
                   description, physical_short, avatar_path, source_label, is_derived,
                   file_mtime_utc, content_fingerprint, updated_at
            FROM character_catalog
            WHERE card_id = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", cardId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadRecord(reader) : null;
    }

    public CharacterCatalogRecord? GetByFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            SELECT card_id, file_name, display_name, call_name, age, canon_adult,
                   description, physical_short, avatar_path, source_label, is_derived,
                   file_mtime_utc, content_fingerprint, updated_at
            FROM character_catalog
            WHERE file_name = @fn LIMIT 1;";
        cmd.Parameters.AddWithValue("@fn", Path.GetFileName(fileName));

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadRecord(reader) : null;
    }

    public void Upsert(CharacterCatalogRecord record)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO character_catalog (
                card_id, file_name, display_name, call_name, age, canon_adult,
                description, physical_short, avatar_path, source_label, is_derived,
                file_mtime_utc, content_fingerprint, updated_at)
            VALUES (
                @id, @fn, @name, @call, @age, @adult,
                @desc, @phys, @avatar, @src, @derived,
                @mtime, @fp, @updated)
            ON CONFLICT(card_id) DO UPDATE SET
                file_name = excluded.file_name,
                display_name = excluded.display_name,
                call_name = excluded.call_name,
                age = excluded.age,
                canon_adult = excluded.canon_adult,
                description = excluded.description,
                physical_short = excluded.physical_short,
                avatar_path = excluded.avatar_path,
                source_label = excluded.source_label,
                is_derived = excluded.is_derived,
                file_mtime_utc = excluded.file_mtime_utc,
                content_fingerprint = excluded.content_fingerprint,
                updated_at = excluded.updated_at;";

        cmd.Parameters.AddWithValue("@id", record.CardId);
        cmd.Parameters.AddWithValue("@fn", record.FileName);
        cmd.Parameters.AddWithValue("@name", record.DisplayName);
        cmd.Parameters.AddWithValue("@call", (object?)record.CallName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@age", record.Age);
        cmd.Parameters.AddWithValue("@adult", record.CanonAdult ? 1 : 0);
        cmd.Parameters.AddWithValue("@desc", (object?)record.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@phys", (object?)record.PhysicalShort ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@avatar", (object?)record.AvatarPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@src", (object?)record.SourceLabel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@derived", record.IsDerived ? 1 : 0);
        cmd.Parameters.AddWithValue("@mtime", (object?)record.FileMtimeUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@fp", (object?)record.ContentFingerprint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public bool Delete(string cardId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM character_catalog WHERE card_id = @id;";
        cmd.Parameters.AddWithValue("@id", cardId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public int Count()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM character_catalog;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Sync index with Characters directory: upsert new/changed files, drop orphans.
    /// Only opens JSON when fingerprint (mtime+size) differs or row is missing.
    /// </summary>
    /// <returns>Number of rows written (insert/update).</returns>
    public int ReconcileFromDisk(string charactersDirectory)
    {
        if (string.IsNullOrWhiteSpace(charactersDirectory) || !Directory.Exists(charactersDirectory))
            return 0;

        var diskFiles = Directory.GetFiles(charactersDirectory)
            .Where(CharacterCatalog.IsLoadableCardFile)
            .ToList();

        var diskById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in diskFiles)
        {
            string fileName = Path.GetFileName(path);
            string cardId = CharacterCatalog.GetCardId(fileName);
            if (!string.IsNullOrEmpty(cardId))
                diskById[cardId] = path;
        }

        var existing = ListAll().ToDictionary(r => r.CardId, StringComparer.OrdinalIgnoreCase);
        int written = 0;

        // Remove orphans
        foreach (var id in existing.Keys.ToList())
        {
            if (!diskById.ContainsKey(id))
                Delete(id);
        }

        // Upsert new / changed
        foreach (var (cardId, path) in diskById)
        {
            string fingerprint = ComputeFileFingerprint(path);
            if (existing.TryGetValue(cardId, out var row) &&
                string.Equals(row.ContentFingerprint, fingerprint, StringComparison.Ordinal))
            {
                continue; // unchanged
            }

            var record = BuildRecordFromCardFile(path);
            if (record == null) continue;

            // Preserve source_label if file re-scan doesn't carry one
            if (string.IsNullOrWhiteSpace(record.SourceLabel) &&
                existing.TryGetValue(cardId, out var prev) &&
                !string.IsNullOrWhiteSpace(prev.SourceLabel))
            {
                record.SourceLabel = prev.SourceLabel;
            }

            Upsert(record);
            written++;
        }

        return written;
    }

    /// <summary>
    /// Index a single card file (e.g. after DeriveCard save).
    /// </summary>
    public CharacterCatalogRecord? UpsertFromFile(string cardPath, string? sourceLabel = null, bool? isDerived = null)
    {
        var record = BuildRecordFromCardFile(cardPath);
        if (record == null) return null;

        if (!string.IsNullOrWhiteSpace(sourceLabel))
            record.SourceLabel = sourceLabel.Trim();
        if (isDerived.HasValue)
            record.IsDerived = isDerived.Value;

        Upsert(record);
        return record;
    }

    public static CharacterCatalogRecord? BuildRecordFromCardFile(string cardPath)
    {
        if (!File.Exists(cardPath) || !CharacterCatalog.IsLoadableCardFile(cardPath))
            return null;

        string fileName = Path.GetFileName(cardPath);
        string cardId = CharacterCatalog.GetCardId(fileName);
        string charDir = Path.GetDirectoryName(cardPath) ?? "";

        string displayName = "";
        string callName = "";
        int age = 0;
        bool canonAdult = true;
        string description = "";
        string physical = "";
        bool isDerived = false;
        string sourceLabel = "";

        try
        {
            string content = File.ReadAllText(cardPath);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("name", out var n))
                displayName = n.GetString()?.Trim() ?? "";
            if (root.TryGetProperty("call_name", out var cn))
                callName = cn.GetString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(callName))
                displayName = callName;
            if (root.TryGetProperty("age", out var a) && a.ValueKind == JsonValueKind.Number)
                age = a.GetInt32();
            if (root.TryGetProperty("canon_adult", out var ca))
                canonAdult = ca.ValueKind != JsonValueKind.False;
            if (root.TryGetProperty("cultural_bias", out var cb))
                description = cb.GetString()?.Trim() ?? "";
            if (root.TryGetProperty("physical", out var p) && p.ValueKind == JsonValueKind.String)
                physical = p.GetString()?.Trim() ?? "";
            if (root.TryGetProperty("derived", out var d) && d.ValueKind == JsonValueKind.True)
                isDerived = true;
            if (root.TryGetProperty("source_label", out var sl))
                sourceLabel = sl.GetString()?.Trim() ?? "";
        }
        catch
        {
            // Unreadable JSON still gets an index row so selectors don't hide the file.
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = cardId.Length > 8
                ? $"Unnamed ({cardId[..8]})"
                : (string.IsNullOrEmpty(cardId) ? "Unnamed Character" : $"Unnamed ({cardId})");
        }

        if (string.IsNullOrWhiteSpace(description))
            description = Truncate(physical, 200);

        string physicalShort = Truncate(physical, 160);
        string avatar = ResolveAvatarForIndex(charDir, cardId, displayName);

        var fi = new FileInfo(cardPath);
        return new CharacterCatalogRecord
        {
            CardId = cardId,
            FileName = fileName,
            DisplayName = displayName,
            CallName = callName,
            Age = age,
            CanonAdult = age >= 18 && canonAdult,
            Description = description,
            PhysicalShort = physicalShort,
            AvatarPath = avatar,
            SourceLabel = sourceLabel,
            IsDerived = isDerived,
            FileMtimeUtc = fi.LastWriteTimeUtc.ToString("o"),
            ContentFingerprint = ComputeFileFingerprint(cardPath),
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static string ComputeFileFingerprint(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            // Cheap stable fingerprint: mtime ticks + length (avoid hashing every refresh)
            return $"{fi.LastWriteTimeUtc.Ticks:x}-{fi.Length:x}";
        }
        catch
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        text = text.Trim();
        if (text.Length <= max) return text;
        return text[..(max - 1)].TrimEnd() + "…";
    }

    private static string ResolveAvatarForIndex(string charDir, string cardId, string displayName)
    {
        // Delegate to catalog helper by building a minimal path check
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        var stems = new List<string>();
        if (!string.IsNullOrWhiteSpace(cardId)) stems.Add(cardId);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            stems.Add(displayName);
            stems.Add(displayName.ToLowerInvariant());
            stems.Add(displayName.Replace(' ', '_').ToLowerInvariant());
        }

        string[] dirs =
        {
            charDir,
            Path.Combine(appDir, "Assets", "Portraits"),
            Path.Combine(Directory.GetCurrentDirectory(), "CharacterSimulator.GUI", "Assets", "Portraits"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Portraits"),
        };
        string[] exts = { ".png", ".jpg", ".jpeg", ".webp" };

        foreach (var stem in stems.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var dir in dirs)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                foreach (var ext in exts)
                {
                    string candidate = Path.Combine(dir, stem + ext);
                    if (File.Exists(candidate)) return candidate;
                }
            }
        }

        return "";
    }

    private static CharacterCatalogRecord ReadRecord(SqliteDataReader reader)
    {
        return new CharacterCatalogRecord
        {
            CardId = reader.GetString(0),
            FileName = reader.GetString(1),
            DisplayName = reader.GetString(2),
            CallName = reader.IsDBNull(3) ? "" : reader.GetString(3),
            Age = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            CanonAdult = !reader.IsDBNull(5) && reader.GetInt32(5) != 0,
            Description = reader.IsDBNull(6) ? "" : reader.GetString(6),
            PhysicalShort = reader.IsDBNull(7) ? "" : reader.GetString(7),
            AvatarPath = reader.IsDBNull(8) ? "" : reader.GetString(8),
            SourceLabel = reader.IsDBNull(9) ? "" : reader.GetString(9),
            IsDerived = !reader.IsDBNull(10) && reader.GetInt32(10) != 0,
            FileMtimeUtc = reader.IsDBNull(11) ? "" : reader.GetString(11),
            ContentFingerprint = reader.IsDBNull(12) ? "" : reader.GetString(12),
            UpdatedAt = reader.IsDBNull(13)
                ? DateTime.UtcNow
                : (DateTime.TryParse(reader.GetString(13), out var dt) ? dt : DateTime.UtcNow)
        };
    }
}
