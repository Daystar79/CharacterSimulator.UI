using System;
using CharacterSimulator.Logic.Services;
using Xunit;

namespace CharacterSimulator.Logic.Tests;

public class ImageArtStyleCatalogTests
{
    [Fact]
    public void ApplyPortraitStyle_LeadsWithAppearanceThenArtStyle()
    {
        string prompt = "Slender build with silver hair and blue eyes";

        // Appearance-first so Pollinations/Flux weight body details over style fluff
        string animePrompt = ImageArtStyleCatalog.ApplyPortraitStyle(prompt, "anime");
        Assert.StartsWith("solo character portrait of this exact person:", animePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(prompt, animePrompt);
        Assert.Contains("anime style character portrait", animePrompt, StringComparison.OrdinalIgnoreCase);

        string photoPrompt = ImageArtStyleCatalog.ApplyPortraitStyle(prompt, "photoreal");
        Assert.Contains(prompt, photoPrompt);
        Assert.Contains("photorealistic portrait photo", photoPrompt, StringComparison.OrdinalIgnoreCase);

        string pixelPrompt = ImageArtStyleCatalog.ApplyPortraitStyle(prompt, "pixel");
        Assert.Contains(prompt, pixelPrompt);
        Assert.Contains("pixel art character portrait", pixelPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildScenePrompt_LeadsWithCharacterInLocation()
    {
        string scenePrompt = ImageArtStyleCatalog.BuildScenePrompt(
            "Cyberpunk city neon alley", "Serena", null, "Physical details", "comic");

        Assert.StartsWith("Cinematic full-body shot of Serena", scenePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Physical details", scenePrompt);
        Assert.Contains("Cyberpunk city neon alley", scenePrompt);
        Assert.Contains("western comic book environment panel", scenePrompt, StringComparison.OrdinalIgnoreCase);
    }
}
