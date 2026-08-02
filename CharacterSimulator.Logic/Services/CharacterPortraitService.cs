using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CharacterSimulator.Logic.Data.Db;

namespace CharacterSimulator.Logic.Services;

/// <summary>
/// Portrait lookup: SQLite BLOB by card_id. On character load, return stored image
/// or generate once and upsert.
/// </summary>
public static class CharacterPortraitService
{
    private static readonly object BindLock = new();
    private static CharacterPortraitRepository? _portraits;
    private static CharacterCatalogRepository? _catalog;

    /// <summary>In-flight generates so double-select doesn't spam the image API.</summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Bind(CharacterPortraitRepository? portraits, CharacterCatalogRepository? catalog = null)
    {
        lock (BindLock)
        {
            _portraits = portraits;
            if (catalog != null)
                _catalog = catalog;
        }
    }

    public static bool HasStore
    {
        get { lock (BindLock) return _portraits != null; }
    }

    public static bool HasPortrait(string cardId)
    {
        CharacterPortraitRepository? repo;
        lock (BindLock) repo = _portraits;
        return repo != null && !string.IsNullOrWhiteSpace(cardId) && repo.Exists(cardId);
    }

    /// <summary>
    /// Resolve display URI for a card: DB BLOB → data URI; else null if missing.
    /// </summary>
    public static string? TryGetStoredDataUri(string cardId)
    {
        CharacterPortraitRepository? repo;
        lock (BindLock) repo = _portraits;
        if (repo == null || string.IsNullOrWhiteSpace(cardId)) return null;
        return repo.GetDataUri(cardId);
    }

    /// <summary>
    /// Save generated (or imported) portrait bytes for a card; updates catalog marker.
    /// </summary>
    public static string SavePortrait(
        string cardId,
        byte[] imageBytes,
        string mimeType = "image/jpeg",
        string? prompt = null,
        string? engine = null)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            throw new ArgumentException("cardId required", nameof(cardId));
        if (imageBytes == null || imageBytes.Length == 0)
            throw new ArgumentException("image bytes required", nameof(imageBytes));

        CharacterPortraitRepository? repo;
        CharacterCatalogRepository? catalog;
        lock (BindLock)
        {
            repo = _portraits;
            catalog = _catalog;
        }

        if (repo == null)
            throw new InvalidOperationException("Portrait store not bound (ProfileService not initialized).");

        repo.UpsertBytes(cardId, imageBytes, mimeType, prompt, engine);
        CharacterPortraitRepository.WriteCacheFile(cardId, imageBytes,
            mimeType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg");

        // Catalog phone book: marker so ListCards knows a portrait exists
        try
        {
            var row = catalog?.GetByCardId(cardId);
            if (row != null)
            {
                row.AvatarPath = CharacterPortraitRepository.AvatarMarker(cardId);
                catalog!.Upsert(row);
            }
        }
        catch { /* catalog optional */ }

        return repo.GetDataUri(cardId) ?? "";
    }

    /// <summary>
    /// On character load: if portrait in SQLite, return it; otherwise generate, store, return.
    /// Returns empty string if generation fails or cardId invalid.
    /// </summary>
    public static async Task<string> EnsurePortraitAsync(
        string cardId,
        string appearancePrompt,
        ImageGeneratorEngine engine = ImageGeneratorEngine.PollinationsAI,
        bool generateIfMissing = true,
        CancellationToken ct = default,
        string? modelId = null)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return "";

        CharacterPortraitRepository? repo;
        lock (BindLock) repo = _portraits;

        // Fast path: already in DB
        if (repo != null)
        {
            string? existing = repo.GetDataUri(cardId);
            if (!string.IsNullOrEmpty(existing))
                return existing;
        }

        if (!generateIfMissing)
            return "";

        var gate = Locks.GetOrAdd(cardId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Re-check after lock (another load may have finished)
            if (repo != null)
            {
                string? existing = repo.GetDataUri(cardId);
                if (!string.IsNullOrEmpty(existing))
                    return existing;
            }

            string? artStyleId = null;
            try { artStyleId = AppConfigService.LoadSettings()?.ImageArtStyle; }
            catch { /* optional */ }

            var result = await AiImageGeneratorService.GeneratePortraitDetailedAsync(
                appearancePrompt,
                cardId,
                engine,
                ct,
                modelId,
                allowPollinationsFallback: true,
                artStyleId: artStyleId).ConfigureAwait(false);

            if (result.ImageBytes != null && result.ImageBytes.Length > 0 && repo != null)
            {
                return SavePortrait(
                    cardId,
                    result.ImageBytes,
                    result.MimeType,
                    appearancePrompt,
                    engine.ToString());
            }

            // Fallback: remote URL only (not persisted as BLOB)
            return result.DisplayUri ?? "";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CharacterPortraitService] Ensure failed for {cardId}: {ex.Message}");
            return "";
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Persist a data-URI or raw bytes from the manual Generate modal.
    /// </summary>
    public static string? SaveFromDataUriOrUrl(
        string cardId,
        string dataUriOrUrl,
        string? prompt = null,
        string? engine = null)
    {
        if (string.IsNullOrWhiteSpace(cardId) || string.IsNullOrWhiteSpace(dataUriOrUrl))
            return null;

        if (dataUriOrUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            // data:image/jpeg;base64,....
            int comma = dataUriOrUrl.IndexOf(',');
            if (comma < 0) return null;
            string header = dataUriOrUrl[..comma];
            string b64 = dataUriOrUrl[(comma + 1)..];
            string mime = "image/jpeg";
            int mimeStart = header.IndexOf(':');
            int mimeEnd = header.IndexOf(';');
            if (mimeStart >= 0 && mimeEnd > mimeStart)
                mime = header[(mimeStart + 1)..mimeEnd];

            try
            {
                byte[] bytes = Convert.FromBase64String(b64);
                return SavePortrait(cardId, bytes, mime, prompt, engine);
            }
            catch
            {
                return null;
            }
        }

        // Non-data URI: leave as remote src; no BLOB without download
        return dataUriOrUrl;
    }
}
