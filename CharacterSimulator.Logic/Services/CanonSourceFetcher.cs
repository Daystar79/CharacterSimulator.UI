using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic.Services;

/// <summary>
/// Fetches documented public-canon text for DeriveCard SSOT.
/// Prefers Wikipedia plain-text extracts; user paste is always accepted as higher priority.
/// </summary>
public static class CanonSourceFetcher
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(25)
        };
        // Wikipedia requires a descriptive User-Agent
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "CharacterSimulator/1.0 (local desktop; derive-card; educational)");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public sealed class CanonFetchResult
    {
        public bool Success { get; init; }
        public string Title { get; init; } = "";
        public string SourceUrl { get; init; } = "";
        public string SourceLabel { get; init; } = "";
        public string Text { get; init; } = "";
        public string? Error { get; init; }
        public bool FromUserPaste { get; init; }
    }

    /// <summary>
    /// Resolve SSOT text: user paste wins; otherwise attempt Wikipedia by character name.
    /// </summary>
    public static async Task<CanonFetchResult> ResolveAsync(
        string characterName,
        string? userPaste = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(userPaste))
        {
            return new CanonFetchResult
            {
                Success = true,
                Title = characterName.Trim(),
                SourceLabel = "User-supplied paste",
                SourceUrl = "",
                Text = userPaste.Trim(),
                FromUserPaste = true
            };
        }

        if (string.IsNullOrWhiteSpace(characterName))
        {
            return new CanonFetchResult
            {
                Success = false,
                Error = "Character name is required when no source paste is provided."
            };
        }

        try
        {
            var wiki = await FetchWikipediaExtractAsync(characterName.Trim(), ct).ConfigureAwait(false);
            if (wiki != null)
                return wiki;

            return new CanonFetchResult
            {
                Success = false,
                Title = characterName.Trim(),
                Error = $"No usable Wikipedia extract found for '{characterName.Trim()}'. " +
                        "Paste a wiki/official profile excerpt as SSOT and try again."
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new CanonFetchResult
            {
                Success = false,
                Title = characterName.Trim(),
                Error = $"Canon fetch failed: {ex.Message}. Paste source text as fallback."
            };
        }
    }

    private static async Task<CanonFetchResult?> FetchWikipediaExtractAsync(string title, CancellationToken ct)
    {
        // 1) Direct title extract (with redirects)
        var direct = await QueryWikipediaExtractAsync(title, ct).ConfigureAwait(false);
        if (direct != null && direct.Text.Length >= 120)
            return direct;

        // 2) Search for best match, then extract
        string? searchTitle = await SearchWikipediaTitleAsync(title, ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(searchTitle) &&
            !searchTitle.Equals(title, StringComparison.OrdinalIgnoreCase))
        {
            var viaSearch = await QueryWikipediaExtractAsync(searchTitle, ct).ConfigureAwait(false);
            if (viaSearch != null && viaSearch.Text.Length >= 120)
                return viaSearch;
        }

        return direct is { Text.Length: >= 40 } ? direct : null;
    }

    private static async Task<CanonFetchResult?> QueryWikipediaExtractAsync(string title, CancellationToken ct)
    {
        string url =
            "https://en.wikipedia.org/w/api.php" +
            "?action=query&prop=extracts&explaintext=true&exsectionformat=plain" +
            "&redirects=1&format=json&formatversion=2" +
            "&titles=" + Uri.EscapeDataString(title);

        using var resp = await Http.GetAsync(url, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;

        string body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("query", out var query)) return null;
        if (!query.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var page in pages.EnumerateArray())
        {
            if (page.TryGetProperty("missing", out _)) continue;
            if (!page.TryGetProperty("extract", out var extractEl)) continue;
            string? extract = extractEl.GetString();
            if (string.IsNullOrWhiteSpace(extract)) continue;

            string pageTitle = page.TryGetProperty("title", out var t) ? (t.GetString() ?? title) : title;
            // Cap size so CLI prompt argv stays manageable
            string text = extract.Trim();
            if (text.Length > 12000)
                text = text[..12000] + "\n…[truncated for prompt size]";

            string pageUrl = "https://en.wikipedia.org/wiki/" + Uri.EscapeDataString(pageTitle.Replace(' ', '_'));

            return new CanonFetchResult
            {
                Success = true,
                Title = pageTitle,
                SourceLabel = $"Wikipedia: {pageTitle}",
                SourceUrl = pageUrl,
                Text = text,
                FromUserPaste = false
            };
        }

        return null;
    }

    private static async Task<string?> SearchWikipediaTitleAsync(string query, CancellationToken ct)
    {
        string url =
            "https://en.wikipedia.org/w/api.php" +
            "?action=query&list=search&srlimit=5&format=json&formatversion=2" +
            "&srsearch=" + Uri.EscapeDataString(query);

        using var resp = await Http.GetAsync(url, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;

        string body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("query", out var q)) return null;
        if (!q.TryGetProperty("search", out var search) || search.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var hit in search.EnumerateArray())
        {
            if (hit.TryGetProperty("title", out var titleEl))
            {
                string? t = titleEl.GetString();
                if (!string.IsNullOrWhiteSpace(t))
                    return t;
            }
        }

        return null;
    }
}
