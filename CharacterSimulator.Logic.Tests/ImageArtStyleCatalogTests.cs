using System;
using CharacterSimulator.Logic.Services;
using Xunit;

namespace CharacterSimulator.Logic.Tests;

public class ImageArtStyleCatalogTests
{
    [Fact]
    public void ApplyPortraitStyle_PrependsArtStyleCueAtBeginning()
    {
        string prompt = "Slender build with silver hair and blue eyes";
        
        // Anime style
        string animePrompt = ImageArtStyleCatalog.ApplyPortraitStyle(prompt, "anime");
        Assert.StartsWith("anime style character portrait", animePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(prompt, animePrompt);

        // Photoreal style
        string photoPrompt = ImageArtStyleCatalog.ApplyPortraitStyle(prompt, "photoreal");
        Assert.StartsWith("photorealistic portrait photo", photoPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(prompt, photoPrompt);

        // Pixel art style
        string pixelPrompt = ImageArtStyleCatalog.ApplyPortraitStyle(prompt, "pixel");
        Assert.StartsWith("pixel art character portrait", pixelPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(prompt, pixelPrompt);
    }

    [Fact]
    public void BuildScenePrompt_PrependsSceneCueAtBeginning()
    {
        string scenePrompt = ImageArtStyleCatalog.BuildScenePrompt(
            "Cyberpunk city neon alley", "Serena", "Character description", "Physical details", "comic");

        Assert.StartsWith("western comic book environment panel", scenePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cyberpunk city neon alley", scenePrompt);
    }
}
