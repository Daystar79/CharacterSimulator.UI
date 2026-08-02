using System;
using System.Collections.Generic;
using System.IO;
using CharacterSimulator.Logic.Data.Db;
using Microsoft.Data.Sqlite;

namespace CharacterSimulator.Logic.Services;

public class ProfileService
{
    private static readonly object SyncLock = new();
    private static ProfileService? _instance;

    public static ProfileService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (SyncLock)
                {
                    _instance ??= new ProfileService();
                }
            }
            return _instance;
        }
    }

    private readonly SqliteConnection _conn;
    private readonly ProfileRepository _profileRepo;
    private readonly SessionRepository _sessionRepo;
    private readonly CharacterProgressRepository _progressRepo;

    public UserProfile? ActiveProfile { get; private set; }

    public ProfileService(string? dbPath = null)
    {
        _conn = AppDbInitializer.CreateConnection(dbPath);
        AppDbInitializer.InitializeDatabase(_conn);

        _profileRepo = new ProfileRepository(_conn);
        _sessionRepo = new SessionRepository(_conn);
        _progressRepo = new CharacterProgressRepository(_conn);

        EnsureDefaultProfileExists();
    }

    private void EnsureDefaultProfileExists()
    {
        var profiles = _profileRepo.GetAllProfiles();
        if (profiles.Count == 0)
        {
            // Seed default player profile (adult by default)
            ActiveProfile = _profileRepo.CreateProfile("Player 1", 1995, 1, 1, pin: null, adultAttested: false);
        }
        else
        {
            ActiveProfile = profiles[0];
        }
    }

    public List<UserProfile> GetAllProfiles() => _profileRepo.GetAllProfiles();

    public UserProfile CreateProfile(string name, int dobYear, int dobMonth, int dobDay, string? pin = null, bool adultAttested = false)
    {
        var profile = _profileRepo.CreateProfile(name, dobYear, dobMonth, dobDay, pin, adultAttested);
        ActiveProfile = profile;
        return profile;
    }

    public bool SwitchProfile(string profileId, string? pin = null)
    {
        var profile = _profileRepo.GetById(profileId);
        if (profile == null) return false;

        if (!_profileRepo.VerifyPin(profile, pin ?? "")) return false;

        ActiveProfile = profile;
        _profileRepo.TouchLastOpened(profileId);
        return true;
    }

    public bool DeleteProfile(string profileId)
    {
        bool success = _profileRepo.DeleteProfile(profileId);
        if (success && ActiveProfile?.Id == profileId)
        {
            var remaining = _profileRepo.GetAllProfiles();
            ActiveProfile = remaining.Count > 0 ? remaining[0] : null;
        }
        return success;
    }

    public ProfileRepository Profiles => _profileRepo;
    public SessionRepository Sessions => _sessionRepo;
    public CharacterProgressRepository Progress => _progressRepo;
}
