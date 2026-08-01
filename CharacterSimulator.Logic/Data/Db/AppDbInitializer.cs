using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace CharacterSimulator.Logic.Data.Db;

public static class AppDbInitializer
{
    public static string GetDatabasePath(string? customDir = null)
    {
        string baseDir = customDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles");
        if (!Directory.Exists(baseDir))
        {
            Directory.CreateDirectory(baseDir);
        }
        return Path.Combine(baseDir, "app_data.db");
    }

    public static SqliteConnection CreateConnection(string? dbPath = null)
    {
        string path = dbPath ?? GetDatabasePath();
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        var conn = new SqliteConnection(builder.ConnectionString);
        conn.Open();

        // Enable Foreign Keys & Write-Ahead Logging for speed + reliability
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
            cmd.ExecuteNonQuery();
        }

        return conn;
    }

    public static void InitializeDatabase(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS schema_info (
                version INTEGER PRIMARY KEY,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS profiles (
                id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                dob_year INTEGER NOT NULL,
                dob_month INTEGER NOT NULL,
                dob_day INTEGER NOT NULL,
                pin_hash TEXT,
                pin_salt TEXT,
                is_adult_attested INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                last_opened_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                profile_id TEXT NOT NULL,
                title TEXT NOT NULL,
                scene TEXT NOT NULL,
                genre TEXT NOT NULL,
                mode TEXT NOT NULL,
                status TEXT NOT NULL,
                started_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                FOREIGN KEY (profile_id) REFERENCES profiles(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS session_participants (
                session_id TEXT NOT NULL,
                character_slug TEXT NOT NULL,
                slot_order INTEGER NOT NULL,
                PRIMARY KEY (session_id, character_slug),
                FOREIGN KEY (session_id) REFERENCES sessions(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS session_turns (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                turn_index INTEGER NOT NULL,
                speaker TEXT NOT NULL,
                target TEXT NOT NULL,
                dialogue TEXT NOT NULL,
                somatic_json TEXT,
                bond_delta INTEGER NOT NULL DEFAULT 0,
                current_bond INTEGER NOT NULL DEFAULT 0,
                speaker_emotion TEXT,
                meta_json TEXT,
                created_at TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES sessions(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS character_progress (
                profile_id TEXT NOT NULL,
                character_slug TEXT NOT NULL,
                bias_strength INTEGER NOT NULL DEFAULT 0,
                active_focus TEXT,
                bias_state TEXT,
                snapshot_json TEXT,
                updated_at TEXT NOT NULL,
                PRIMARY KEY (profile_id, character_slug),
                FOREIGN KEY (profile_id) REFERENCES profiles(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS character_history (
                id TEXT PRIMARY KEY,
                profile_id TEXT NOT NULL,
                character_slug TEXT NOT NULL,
                movement_id TEXT,
                pressure TEXT,
                delta TEXT,
                permanence TEXT,
                notes TEXT,
                created_at TEXT NOT NULL,
                FOREIGN KEY (profile_id) REFERENCES profiles(id) ON DELETE CASCADE
            );

            INSERT OR IGNORE INTO schema_info (version, updated_at) VALUES (1, datetime('now'));
        ";
        cmd.ExecuteNonQuery();
    }
}
