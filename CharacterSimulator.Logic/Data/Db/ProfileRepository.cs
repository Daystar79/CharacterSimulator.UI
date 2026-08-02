using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace CharacterSimulator.Logic.Data.Db;

public class ProfileRepository
{
    private readonly SqliteConnection _conn;

    public ProfileRepository(SqliteConnection conn)
    {
        _conn = conn;
    }

    public List<UserProfile> GetAllProfiles()
    {
        var list = new List<UserProfile>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, display_name, dob_year, dob_month, dob_day, pin_hash, pin_salt, is_adult_attested, created_at, last_opened_at
            FROM profiles
            ORDER BY last_opened_at DESC;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadProfile(reader));
        }
        return list;
    }

    public UserProfile? GetById(string profileId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, display_name, dob_year, dob_month, dob_day, pin_hash, pin_salt, is_adult_attested, created_at, last_opened_at
            FROM profiles
            WHERE id = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", profileId);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return ReadProfile(reader);
        }
        return null;
    }

    public UserProfile CreateProfile(string displayName, int dobYear, int dobMonth, int dobDay, string? pin = null, bool adultAttested = false)
    {
        string? salt = null;
        string? hash = null;
        if (!string.IsNullOrWhiteSpace(pin))
        {
            salt = GenerateSalt();
            hash = HashPin(pin, salt);
        }

        var profile = new UserProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = displayName,
            DobYear = dobYear,
            DobMonth = dobMonth,
            DobDay = dobDay,
            PinSalt = salt,
            PinHash = hash,
            IsAdultAttested = adultAttested,
            CreatedAt = DateTime.UtcNow,
            LastOpenedAt = DateTime.UtcNow
        };

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO profiles (id, display_name, dob_year, dob_month, dob_day, pin_hash, pin_salt, is_adult_attested, created_at, last_opened_at)
            VALUES (@id, @name, @year, @month, @day, @hash, @salt, @attested, @created, @opened);";
        cmd.Parameters.AddWithValue("@id", profile.Id);
        cmd.Parameters.AddWithValue("@name", profile.DisplayName);
        cmd.Parameters.AddWithValue("@year", profile.DobYear);
        cmd.Parameters.AddWithValue("@month", profile.DobMonth);
        cmd.Parameters.AddWithValue("@day", profile.DobDay);
        cmd.Parameters.AddWithValue("@hash", (object?)profile.PinHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@salt", (object?)profile.PinSalt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@attested", profile.IsAdultAttested ? 1 : 0);
        cmd.Parameters.AddWithValue("@created", profile.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@opened", profile.LastOpenedAt.ToString("o"));

        cmd.ExecuteNonQuery();
        return profile;
    }

    public bool VerifyPin(UserProfile profile, string pin)
    {
        if (string.IsNullOrEmpty(profile.PinHash) || string.IsNullOrEmpty(profile.PinSalt))
            return true; // No PIN required for this profile

        string hash = HashPin(pin, profile.PinSalt);
        return string.Equals(profile.PinHash, hash, StringComparison.Ordinal);
    }

    public void TouchLastOpened(string profileId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE profiles SET last_opened_at = @now WHERE id = @id;";
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@id", profileId);
        cmd.ExecuteNonQuery();
    }

    public void SetAdultAttestation(string profileId, bool attested)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "UPDATE profiles SET is_adult_attested = @attested WHERE id = @id;";
        cmd.Parameters.AddWithValue("@attested", attested ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", profileId);
        cmd.ExecuteNonQuery();
    }

    private static UserProfile ReadProfile(SqliteDataReader reader)
    {
        return new UserProfile
        {
            Id = reader.GetString(0),
            DisplayName = reader.GetString(1),
            DobYear = reader.GetInt32(2),
            DobMonth = reader.GetInt32(3),
            DobDay = reader.GetInt32(4),
            PinHash = reader.IsDBNull(5) ? null : reader.GetString(5),
            PinSalt = reader.IsDBNull(6) ? null : reader.GetString(6),
            IsAdultAttested = reader.GetInt32(7) == 1,
            CreatedAt = DateTime.Parse(reader.GetString(8)),
            LastOpenedAt = DateTime.Parse(reader.GetString(9))
        };
    }

    private static string GenerateSalt()
    {
        byte[] salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        return Convert.ToBase64String(salt);
    }

    private static string HashPin(string pin, string salt)
    {
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            pin,
            Convert.FromBase64String(salt),
            10_000,
            HashAlgorithmName.SHA256,
            32);
        return Convert.ToBase64String(hash);
    }

    public bool DeleteProfile(string profileId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM profiles WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", profileId);
        int rows = cmd.ExecuteNonQuery();
        return rows > 0;
    }
}
