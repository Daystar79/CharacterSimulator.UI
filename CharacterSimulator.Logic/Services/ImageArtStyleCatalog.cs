using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CharacterSimulator.Logic.Services;

/// <summary>
/// Shared art-style presets for portrait + scene image generation.
/// Id is persisted in <see cref="AppSettings.ImageArtStyle"/>.
/// </summary>
public static class ImageArtStyleCatalog
{
    public const string DefaultStyleId = "anime";

    public sealed record ArtStyle(
        string Id,
        string DisplayName,
        string Description,
        /// <summary>Short cue appended/merged into portrait prompts.</summary>
        string PortraitCue,
        /// <summary>Short cue for environment / wide shots.</summary>
        string SceneCue);

    private static readonly ArtStyle[] Styles =
    {
        new(
            "anime",
            "Anime",
            "Clean anime / cel-shaded illustration",
            "anime style character portrait, clean linework, soft cel shading, expressive eyes, high quality illustration",
            "anime style environment art, clean linework, soft cel shading, cinematic lighting, high quality illustration"),
        new(
            "semi_realistic",
            "Semi-realistic",
            "Stylized realism — painterly faces, soft detail",
            "semi-realistic digital portrait, soft painterly detail, natural proportions, high quality character art",
            "semi-realistic digital environment, painterly detail, natural lighting, cinematic composition"),
        new(
            "photoreal",
            "Photoreal",
            "Photo-like realism",
            "photorealistic portrait photo, natural skin texture, sharp focus, cinematic lighting, 85mm lens",
            "photorealistic location photo, natural light, depth of field, cinematic wide shot"),
        new(
            "watercolor",
            "Watercolor",
            "Soft watercolor wash",
            "watercolor portrait illustration, soft washes, paper texture, delicate edges, artistic",
            "watercolor landscape illustration, soft washes, paper texture, atmospheric"),
        new(
            "comic",
            "Comic / Ink",
            "Western comic ink & color",
            "western comic book portrait, bold ink outlines, flat color, dramatic shading",
            "western comic book environment panel, bold ink outlines, dynamic composition"),
        new(
            "oil",
            "Oil painting",
            "Classical oil portrait feel",
            "oil painting portrait, rich brushwork, classical lighting, fine art canvas",
            "oil painting landscape, rich brushwork, classical composition, fine art"),
        new(
            "pixel",
            "Pixel art",
            "Retro pixel sprite style",
            "pixel art character portrait, limited palette, clean pixels, 16-bit aesthetic",
            "pixel art environment scene, limited palette, clean pixels, 16-bit game background"),
        new(
            "3d_render",
            "3D Render",
            "Modern CGI / game render",
            "3D rendered character portrait, subsurface scattering, studio lighting, game-cinematic quality",
            "3D rendered environment, cinematic lighting, game-quality scenery"),
    };

    public static IReadOnlyList<ArtStyle> All => Styles;

    public static ArtStyle GetById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Styles[0];
        return Styles.FirstOrDefault(s => s.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? Styles[0];
    }

    /// <summary>
    /// Merge subject prompt with the selected art style for a character portrait.
    /// Prepends the style cue to the front so AI image generators (Pollinations, SD, Flux)
    /// prioritize the art style directive.
    /// </summary>
    public static string ApplyPortraitStyle(string? prompt, string? artStyleId)
    {
        var style = GetById(artStyleId);
        string p = (prompt ?? "").Trim();
        if (string.IsNullOrEmpty(p))
            return style.PortraitCue;

        if (p.StartsWith(style.PortraitCue, StringComparison.OrdinalIgnoreCase))
            return p;

        return $"{style.PortraitCue}. Subject details: {p}";
    }

    /// <summary>
    /// Scene art: environment + optional character appearance, same art style as portraits.
    /// </summary>
    /// <summary>
    /// Scene art: environment + optional character appearance, same art style as portraits.
    /// </summary>
    public static string BuildScenePrompt(
        string? scenePlaceOrContext,
        string? characterName,
        string? characterDescription,
        string? characterPhysical,
        string? artStyleId)
    {
        var style = GetById(artStyleId);
        string place = string.IsNullOrWhiteSpace(scenePlaceOrContext)
            ? "Quiet interior room, soft ambient light"
            : scenePlaceOrContext.Trim();

        // Cap length for URL-based generators (Pollinations).
        place = Truncate(place, 260);

        var sb = new StringBuilder();
        sb.Append(style.SceneCue);
        sb.Append(". Location and setting: ");
        sb.Append(place);

        string appearance = FirstNonEmpty(characterPhysical, characterDescription);
        if (!string.IsNullOrWhiteSpace(appearance))
        {
            appearance = Truncate(appearance!, 180);
            string who = string.IsNullOrWhiteSpace(characterName) ? "a character" : characterName.Trim();
            sb.Append(". Featuring ");
            sb.Append(who);
            sb.Append(" in scene (");
            sb.Append(appearance);
            sb.Append(")");
        }

        sb.Append(". Wide shot, environmental storytelling, no text, no watermark, no UI.");

        return sb.ToString();
    }

    private static string? FirstNonEmpty(params string?[] parts)
    {
        foreach (var p in parts)
        {
            if (!string.IsNullOrWhiteSpace(p))
                return p.Trim();
        }
        return null;
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max) return text;
        return text[..(max - 1)].TrimEnd() + "…";
    }
}
