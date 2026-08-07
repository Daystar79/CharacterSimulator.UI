using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace CharacterSimulator.Logic.Data.Db;

public class CharacterPortraitRecord
{
    public string CardId { get; set; } = "";
    public string MimeType { get; set; } = "image/jpeg";
    public byte[] ImageBlob { get; set; } = Array.Empty<byte>();
    public string Prompt { get; set; } = "";
    public string Engine { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>WebView-friendly data URI for &lt;img src&gt;.</summary>
    public string ToDataUri()
    {
        if (ImageBlob == null || ImageBlob.Length == 0) return "";
        string mime = string.IsNullOrWhiteSpace(MimeType) ? "image/jpeg" : MimeType;
        return $"data:{mime};base64,{Convert.ToBase64String(ImageBlob)}";
    }
}

/// <summary>
/// SQLite lookup for character portraits keyed by opaque card_id.
/// </summary>
public class CharacterPortraitRepository
{
    public const string DbAvatarMarkerPrefix = "db:portrait:";

    private readonly SqliteConnection _conn;

    public CharacterPortraitRepository(SqliteConnection conn)
    {
        _conn = conn;
    }

    public static string AvatarMarker(string cardId) => DbAvatarMarkerPrefix + cardId;

    public static bool IsDbAvatarMarker(string? pathOrMarker) =>
        !string.IsNullOrEmpty(pathOrMarker) &&
        pathOrMarker.StartsWith(DbAvatarMarkerPrefix, StringComparison.OrdinalIgnoreCase);

    public static string? CardIdFromMarker(string? pathOrMarker)
    {
        if (!IsDbAvatarMarker(pathOrMarker)) return null;
        return pathOrMarker![DbAvatarMarkerPrefix.Length..];
    }

    public bool Exists(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId)) return false;
        lock (_conn)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM character_portraits WHERE card_id = @id LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", cardId);
            return cmd.ExecuteScalar() != null;
        }
    }

    public CharacterPortraitRecord? Get(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId)) return null;
        lock (_conn)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
                SELECT card_id, mime_type, image_blob, prompt, engine, updated_at
                FROM character_portraits
                WHERE card_id = @id LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", cardId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return new CharacterPortraitRecord
            {
                CardId = reader.GetString(0),
                MimeType = reader.IsDBNull(1) ? "image/jpeg" : reader.GetString(1),
                ImageBlob = reader.IsDBNull(2) ? Array.Empty<byte>() : (byte[])reader[2],
                Prompt = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Engine = reader.IsDBNull(4) ? "" : reader.GetString(4),
                UpdatedAt = reader.IsDBNull(5)
                    ? DateTime.UtcNow
                    : (DateTime.TryParse(reader.GetString(5), out var dt) ? dt : DateTime.UtcNow)
            };
        }
    }

    public string? GetDataUri(string cardId)
    {
        var rec = Get(cardId);
        if (rec == null || rec.ImageBlob.Length == 0) return null;
        return rec.ToDataUri();
    }

    public void Upsert(CharacterPortraitRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.CardId))
            throw new ArgumentException("card_id required", nameof(record));
        if (record.ImageBlob == null || record.ImageBlob.Length == 0)
            throw new ArgumentException("image_blob required", nameof(record));

        lock (_conn)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO character_portraits (card_id, mime_type, image_blob, prompt, engine, updated_at)
                VALUES (@id, @mime, @blob, @prompt, @engine, @updated)
                ON CONFLICT(card_id) DO UPDATE SET
                    mime_type = excluded.mime_type,
                    image_blob = excluded.image_blob,
                    prompt = excluded.prompt,
                    engine = excluded.engine,
                    updated_at = excluded.updated_at;";

            cmd.Parameters.AddWithValue("@id", record.CardId);
            cmd.Parameters.AddWithValue("@mime", string.IsNullOrWhiteSpace(record.MimeType) ? "image/jpeg" : record.MimeType);
            cmd.Parameters.AddWithValue("@blob", record.ImageBlob);
            cmd.Parameters.AddWithValue("@prompt", (object?)record.Prompt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@engine", (object?)record.Engine ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
    }

    public void UpsertBytes(string cardId, byte[] imageBytes, string mimeType = "image/jpeg",
        string? prompt = null, string? engine = null)
    {
        Upsert(new CharacterPortraitRecord
        {
            CardId = cardId,
            ImageBlob = imageBytes,
            MimeType = mimeType,
            Prompt = prompt ?? "",
            Engine = engine ?? ""
        });
    }

    public bool Delete(string cardId)
    {
        lock (_conn)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM character_portraits WHERE card_id = @id;";
            cmd.Parameters.AddWithValue("@id", cardId);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    /// <summary>Optional: also write a cache file under Assets/Portraits for tools that expect a path.</summary>
    public static string? WriteCacheFile(string cardId, byte[] imageBytes, string extension = ".jpg")
    {
        try
        {
            string targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Portraits");
            Directory.CreateDirectory(targetDir);
            string path = Path.Combine(targetDir, cardId + extension);
            File.WriteAllBytes(path, imageBytes);
            return path;
        }
        catch
        {
            return null;
        }
    }
}
