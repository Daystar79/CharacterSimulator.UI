using Xunit;
using CharacterSimulator.Logic.Services;

namespace CharacterSimulator.Logic.Tests;

public class ThemeCatalogTests
{
    [Fact]
    public void ThemeCatalog_ContainsExpectedPresets()
    {
        Assert.NotEmpty(ThemeCatalog.All);
        Assert.Contains(ThemeCatalog.All, t => t.Id == "midnight");
        Assert.Contains(ThemeCatalog.All, t => t.Id == "cyberpunk");
        Assert.Contains(ThemeCatalog.All, t => t.Id == "matrix");
        Assert.Contains(ThemeCatalog.All, t => t.Id == "amber");
        Assert.Contains(ThemeCatalog.All, t => t.Id == "obsidian");
    }

    [Theory]
    [InlineData("cyberpunk", "Cyberpunk Synthwave")]
    [InlineData("matrix", "Emerald Matrix")]
    [InlineData("amber", "Solarized Amber")]
    [InlineData("obsidian", "Obsidian OLED")]
    [InlineData("invalid_id", "Midnight Slate")]
    [InlineData(null, "Midnight Slate")]
    public void ThemeCatalog_GetById_ReturnsExpectedOrFallback(string? id, string expectedDisplayName)
    {
        var preset = ThemeCatalog.GetById(id);
        Assert.NotNull(preset);
        Assert.Equal(expectedDisplayName, preset.DisplayName);
    }
}
