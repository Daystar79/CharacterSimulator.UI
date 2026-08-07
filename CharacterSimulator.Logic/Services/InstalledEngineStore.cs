using System;
using System.Collections.Generic;
using System.Linq;
using CharacterSimulator.Logic.Data.Db;

namespace CharacterSimulator.Logic.Services;

/// <summary>
/// Static bind for the durable installed-engines cache (same pattern as CharacterPortraitService).
/// Detectors read/write here so Setup / portrait modals get a fast SQLite lookup after the first scan.
/// </summary>
public static class InstalledEngineStore
{
    private static readonly object BindLock = new();
    private static InstalledEngineRepository? _repo;

    public static void Bind(InstalledEngineRepository? repo)
    {
        lock (BindLock)
            _repo = repo;
    }

    public static bool HasStore
    {
        get { lock (BindLock) return _repo != null; }
    }

    private static InstalledEngineRepository? Repo
    {
        get { lock (BindLock) return _repo; }
    }

    public static bool HasRoleplayCache()
    {
        try
        {
            var repo = Repo;
            return repo != null && repo.HasCategory(InstalledEngineRecord.CategoryRoleplay);
        }
        catch
        {
            return false;
        }
    }

    public static bool HasImageCache()
    {
        try
        {
            var repo = Repo;
            return repo != null && repo.HasCategory(InstalledEngineRecord.CategoryImage);
        }
        catch
        {
            return false;
        }
    }

    public static List<DetectedLlmEngine>? TryGetRoleplayCached()
    {
        try
        {
            var repo = Repo;
            if (repo == null) return null;
            var rows = repo.ListByCategory(InstalledEngineRecord.CategoryRoleplay);
            if (rows.Count == 0) return null;

            return rows.Select(r => new DetectedLlmEngine(
                r.EngineId,
                r.DisplayName,
                r.IsAvailable,
                string.IsNullOrEmpty(r.StatusDetail)
                    ? $"Cached scan · {r.ScannedAt:u}"
                    : r.StatusDetail)).ToList();
        }
        catch
        {
            return null;
        }
    }

    public static List<DetectedImageEngine>? TryGetImageCached()
    {
        try
        {
            var repo = Repo;
            if (repo == null) return null;
            var rows = repo.ListByCategory(InstalledEngineRecord.CategoryImage);
            if (rows.Count == 0) return null;

            var list = rows.Select(r =>
            {
                var engineType = !string.IsNullOrWhiteSpace(r.EngineType)
                    ? ImageEngineDetector.ParseEngineId(r.EngineType)
                    : ImageEngineDetector.ParseEngineId(r.EngineId);
                return new DetectedImageEngine(
                    r.EngineId,
                    r.DisplayName,
                    r.IsAvailable,
                    string.IsNullOrEmpty(r.StatusDetail)
                        ? $"Cached scan · {r.ScannedAt:u}"
                        : r.StatusDetail,
                    engineType);
            }).ToList();

            // Always surface Pollinations first if somehow missing from a stale row set.
            if (!list.Any(e => e.Id.Equals(ImageEngineDetector.DefaultEngineId, StringComparison.OrdinalIgnoreCase)))
            {
                list.Insert(0, new DetectedImageEngine(
                    ImageEngineDetector.DefaultEngineId,
                    "✨ Pollinations AI (Default · free web API)",
                    true,
                    "Default portrait engine (ensured)",
                    ImageGeneratorEngine.PollinationsAI));
            }

            return list;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveRoleplay(IEnumerable<DetectedLlmEngine> engines)
    {
        try
        {
            var repo = Repo;
            if (repo == null || engines == null) return;

            var now = DateTime.UtcNow;
            int order = 0;
            var rows = engines.Select(e => new InstalledEngineRecord
            {
                Category = InstalledEngineRecord.CategoryRoleplay,
                EngineId = e.Id,
                DisplayName = e.DisplayName,
                IsAvailable = e.IsAvailable,
                StatusDetail = e.StatusDetail ?? "",
                EngineType = "",
                SortOrder = order++,
                ScannedAt = now
            }).ToList();

            if (rows.Count == 0) return;
            repo.ReplaceCategory(InstalledEngineRecord.CategoryRoleplay, rows);
        }
        catch
        {
            // Cache is optional — never break detection.
        }
    }

    public static void SaveImage(IEnumerable<DetectedImageEngine> engines)
    {
        try
        {
            var repo = Repo;
            if (repo == null || engines == null) return;

            var now = DateTime.UtcNow;
            int order = 0;
            var rows = engines.Select(e => new InstalledEngineRecord
            {
                Category = InstalledEngineRecord.CategoryImage,
                EngineId = e.Id,
                DisplayName = e.DisplayName,
                IsAvailable = e.IsAvailable,
                StatusDetail = e.StatusDetail ?? "",
                EngineType = e.EngineType.ToString(),
                SortOrder = order++,
                ScannedAt = now
            }).ToList();

            if (rows.Count == 0) return;
            repo.ReplaceCategory(InstalledEngineRecord.CategoryImage, rows);
        }
        catch
        {
            // Cache is optional — never break detection.
        }
    }

    public static DateTime? GetLastRoleplayScanUtc()
    {
        try { return Repo?.GetLastScanUtc(InstalledEngineRecord.CategoryRoleplay); }
        catch { return null; }
    }

    public static DateTime? GetLastImageScanUtc()
    {
        try { return Repo?.GetLastScanUtc(InstalledEngineRecord.CategoryImage); }
        catch { return null; }
    }
}
