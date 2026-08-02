using System;
using System.Collections.Generic;
using System.Linq;

namespace CharacterSimulator.Logic;

/// <summary>
/// Genre describes the environmental / narrative tone of the scene only.
/// It must never rewrite character identity (voice, history, appearance).
/// </summary>
public sealed class SceneGenre
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    /// <summary>Guidance for the LLM about setting tone — not character personality.</summary>
    public string EnvironmentTone { get; init; } = string.Empty;
    public IReadOnlyList<string> ScenePresets { get; init; } = Array.Empty<string>();
}

public static class SceneGenreCatalog
{
    public const string DefaultGenreId = "contemporary";

    private static readonly IReadOnlyList<SceneGenre> Genres = new List<SceneGenre>
    {
        new()
        {
            Id = "contemporary",
            DisplayName = "Contemporary",
            Description = "Modern everyday life; neutral urban or domestic spaces.",
            EnvironmentTone = "Present-day realistic setting. Ordinary rooms, streets, weather, and social norms.",
            ScenePresets = new[]
            {
                "Quiet city apartment living room, late afternoon light through half-closed blinds",
                "Corner café table by the window, soft rain on the glass, two cups cooling",
                "Shared studio kitchen at night, only the under-cabinet lights on",
            }
        },
        new()
        {
            Id = "sanctuary",
            DisplayName = "Sanctuary / Intimate",
            Description = "Warm private refuge; soft light, closeness, low pressure.",
            EnvironmentTone = "Private sanctuary space — soft light, textiles, tea, unhurried closeness. Setting invites rest without forcing any character to change who they are.",
            ScenePresets = new[]
            {
                "Low lantern light, cashmere throws, steeping jasmine tea; a quiet nest of cushions",
                "Private atelier at dusk; silk and ambient music; the door locked against the day",
                "Bare feet on warm wood floors; open windows; rain somewhere far off",
            }
        },
        new()
        {
            Id = "cyberpunk",
            DisplayName = "Cyberpunk",
            Description = "Neon rain, megacity edges, tech-noir atmosphere.",
            EnvironmentTone = "Cyberpunk environment only: neon, rain, dense city infrastructure, corporate towers. Characters remain themselves — they do not become stock street-samurai unless that is already on their card.",
            ScenePresets = new[]
            {
                "Neon alley at night, rainy cyberpunk district, puddles reflecting signage",
                "Upper-tier transit platform overlooking a glowing megacity grid",
                "After-hours data lounge under violet strip lights, rain on the glass roof",
            }
        },
        new()
        {
            Id = "fantasy",
            DisplayName = "Fantasy",
            Description = "Mythic places, old magic, pre-industrial wonder.",
            EnvironmentTone = "Secondary-world fantasy locale: stone, lanterns, forests, courts, or temples. Magic may exist in the place; characters keep their own identities and speech.",
            ScenePresets = new[]
            {
                "Moonlit cloister garden, pale blossoms, a still fountain at the center",
                "Waystation inn common room, hearth fire, travelers half-asleep on benches",
                "Cliffside shrine overlooking a sea of clouds at first light",
            }
        },
        new()
        {
            Id = "scifi",
            DisplayName = "Science Fiction",
            Description = "Ships, stations, near/far future tech spaces.",
            EnvironmentTone = "Science-fiction location: hull metal, viewports, artificial gravity, distant stars. Technology is environmental; it does not rewrite character voice.",
            ScenePresets = new[]
            {
                "Observation deck of a quiet orbital station, Earthrise filling the viewport",
                "Dim corridor of a long-haul freighter between watch rotations",
                "Greenhouse module under artificial daylight, condensation on the panes",
            }
        },
        new()
        {
            Id = "noir",
            DisplayName = "Noir / Mystery",
            Description = "Shadow, secrets, moral fog, rain-slick streets.",
            EnvironmentTone = "Noir atmosphere: low-key lighting, secrets, moral ambiguity in the environment. Characters keep their established personality and speech patterns.",
            ScenePresets = new[]
            {
                "Rain-slick street outside a half-lit office building, one window still glowing",
                "Back booth of a quiet bar, jukebox low, a dossier on the table",
                "Archive room after hours, dust, a single green desk lamp",
            }
        },
        new()
        {
            Id = "horror",
            DisplayName = "Horror / Unease",
            Description = "Dread in the place, not a forced personality rewrite.",
            EnvironmentTone = "Horror or uncanny environment: wrong quiet, liminal rooms, unease in architecture and sound. Characters react as themselves; do not turn them into generic screamers.",
            ScenePresets = new[]
            {
                "Empty hallway of an old house, wallpaper peeling, a light that won't stay on",
                "Fogbound pier at 3 a.m., water knocking the pilings out of rhythm",
                "Basement storage under a theater, costumes hanging like people",
            }
        },
        new()
        {
            Id = "historical",
            DisplayName = "Historical",
            Description = "Period rooms and social texture without forcing dialect unless on-card.",
            EnvironmentTone = "Historical period setting (architecture, clothing norms of the place, technology level). Characters keep their card voice unless the card itself is period-native.",
            ScenePresets = new[]
            {
                "Candlelit drawing room, heavy drapes, rain against leaded glass",
                "Harbor warehouse at dawn, salt air, crates and rope",
                "Garden path of a country estate, late summer insects in the hedges",
            }
        },
        new()
        {
            Id = "slice",
            DisplayName = "Slice of Life",
            Description = "Gentle daily beats; low stakes, high texture.",
            EnvironmentTone = "Slice-of-life setting: ordinary comfort, small tasks, soft conflict. No forced plot machinery.",
            ScenePresets = new[]
            {
                "Sunday kitchen, coffee brewing, someone scrolling a phone at the counter",
                "Laundromat bench, dryers humming, afternoon light on linoleum",
                "Bookstore aisle, quiet, a shared look over the same shelf",
            }
        },
        new()
        {
            Id = "romance",
            DisplayName = "Romance",
            Description = "Charged closeness in the place; consent and card voice still rule.",
            EnvironmentTone = "Romantic framing in the environment: soft light, proximity, privacy. Characters pursue closeness only as their identity allows.",
            ScenePresets = new[]
            {
                "Balcony at blue hour, city lights below, one shared blanket",
                "Small restaurant after most tables have cleared, dessert still unfinished",
                "Walk home along a quiet waterfront, winter coats, unhurried pace",
            }
        },
        new()
        {
            Id = "custom",
            DisplayName = "Custom",
            Description = "No preset tone — use only the freeform scene text.",
            EnvironmentTone = "Use only the stated scene location. Do not invent a genre overlay.",
            ScenePresets = new[]
            {
                "Describe the place, time, and sensory detail…",
            }
        },
    };

    public static IReadOnlyList<SceneGenre> All => Genres;

    public static IReadOnlyList<string> DisplayNames => Genres.Select(g => g.DisplayName).ToList();

    public static SceneGenre GetById(string? id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
                return Genres.First(g => g.Id == DefaultGenreId);
            return Genres.FirstOrDefault(g => g.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                ?? Genres.FirstOrDefault(g => g.DisplayName.Equals(id, StringComparison.OrdinalIgnoreCase))
                ?? Genres.First(g => g.Id == DefaultGenreId);
        }
        catch
        {
            return Genres[0]; // never throw into UI startup
        }
    }

    public static SceneGenre GetByDisplayName(string? displayName) => GetById(displayName);

    public static string DefaultSceneFor(string? genreId)
    {
        var g = GetById(genreId);
        return g.ScenePresets.Count > 0 ? g.ScenePresets[0] : "";
    }

    /// <summary>
    /// Composes the scene context string for prompts: location + genre as setting tone only.
    /// </summary>
    public static string ComposeSceneContext(string? genreId, string? scenePrompt)
    {
        var genre = GetById(genreId);
        string place = string.IsNullOrWhiteSpace(scenePrompt)
            ? DefaultSceneFor(genre.Id)
            : scenePrompt.Trim();

        if (genre.Id == "custom")
            return place;

        return $@"Location: {place}
Genre (environment only): {genre.DisplayName}
Environmental tone: {genre.EnvironmentTone}
Important: Genre describes the place and mood of the setting. It does not change who any character is.";
    }
}
