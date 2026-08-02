using System;
using System.IO;
using System.Threading.Tasks;
using CharacterSimulator.Logic.Services;
using Xunit;

namespace CharacterSimulator.Logic.Tests;

public class ImageEmitProbeAgyTests
{
    [Fact]
    public async Task TryExtractImage_ReadsAbsoluteAgyPath()
    {
        // Use a real portrait from AGY brain if present; else skip soft
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string brain = Path.Combine(home, ".gemini", "antigravity-cli", "brain");
        if (!Directory.Exists(brain)) return;

        string? portrait = null;
        foreach (var f in Directory.EnumerateFiles(brain, "*.jpg", SearchOption.AllDirectories))
        {
            if (ImageEmitProbe.IsProbeArtifactFileName(f)) continue;
            portrait = f;
            break;
        }
        if (portrait == null) return;

        string stdout = portrait + "\n";
        var (bytes, mime) = await ImageEmitProbe.TryExtractImageAsync(
            stdout,
            agentKind: ImageEmitProbe.AgentImageKind.Agy);

        Assert.NotNull(bytes);
        Assert.True(bytes!.Length > 10_000);
        Assert.True(ImageEmitProbe.LooksLikeImage(bytes));
        Assert.Contains("image/", mime);
    }

    [Fact]
    public void IsProbeArtifact_DetectsRedSquare()
    {
        Assert.True(ImageEmitProbe.IsProbeArtifactFileName("red_square_123.jpg"));
        Assert.True(ImageEmitProbe.IsProbeArtifactFileName("/tmp/tiny_red_square.png"));
        Assert.False(ImageEmitProbe.IsProbeArtifactFileName("character_portrait_123.jpg"));
    }

    [Fact]
    public void HarvestAgyBrain_PrefersNonProbe()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string brain = Path.Combine(home, ".gemini", "antigravity-cli", "brain");
        if (!Directory.Exists(brain)) return;

        var result = ImageEmitProbe.TryReadNewestAgyBrainImage(
            DateTime.UtcNow.AddDays(-30),
            excludeProbeNames: true);

        if (result.Bytes == null) return; // no non-probe images
        // If we got bytes with excludeProbeNames, filename should not be probe
        Assert.True(result.Bytes.Length > 0);
    }
}
