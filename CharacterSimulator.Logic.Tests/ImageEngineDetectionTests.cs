using System.Collections.Generic;
using CharacterSimulator.Logic.Services;
using Xunit;

namespace CharacterSimulator.Logic.Tests;

public class ImageEngineDetectionTests
{
    [Fact]
    public void ParseEngineId_DefaultsUnknownAndLegacyVisionToPollinations()
    {
        Assert.Equal(ImageGeneratorEngine.PollinationsAI, ImageEngineDetector.ParseEngineId(null));
        Assert.Equal(ImageGeneratorEngine.PollinationsAI, ImageEngineDetector.ParseEngineId(""));
        Assert.Equal(ImageGeneratorEngine.PollinationsAI, ImageEngineDetector.ParseEngineId("OllamaLocal"));
        Assert.Equal(ImageGeneratorEngine.PollinationsAI, ImageEngineDetector.ParseEngineId("Claude"));
        Assert.Equal(ImageGeneratorEngine.PollinationsAI, ImageEngineDetector.ParseEngineId("PollinationsAI"));
        Assert.Equal(ImageGeneratorEngine.StableDiffusionWebUI, ImageEngineDetector.ParseEngineId("StableDiffusionWebUI"));
        Assert.Equal(ImageGeneratorEngine.AgentAgy, ImageEngineDetector.ParseEngineId("AgentAgy"));
        Assert.Equal(ImageGeneratorEngine.AgentGrok, ImageEngineDetector.ParseEngineId("AgentGrok"));
    }

    [Fact]
    public void CoalesceToAvailable_FallsBackToPollinations()
    {
        var available = new List<DetectedImageEngine>
        {
            new("PollinationsAI", "P", true, "", ImageGeneratorEngine.PollinationsAI),
            new("StableDiffusionWebUI", "SD", true, "", ImageGeneratorEngine.StableDiffusionWebUI)
        };

        Assert.Equal("PollinationsAI", ImageEngineDetector.CoalesceToAvailable(null, available));
        Assert.Equal("StableDiffusionWebUI", ImageEngineDetector.CoalesceToAvailable("StableDiffusionWebUI", available));
        Assert.Equal("PollinationsAI", ImageEngineDetector.CoalesceToAvailable("AgentGrok", available));
        Assert.Equal("PollinationsAI", ImageEngineDetector.CoalesceToAvailable("OllamaLocal", available));
    }

    [Fact]
    public void LooksLikeImage_RecognizesPngAndJpegMagic()
    {
        byte[] png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
        byte[] jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x00 };
        byte[] text = System.Text.Encoding.UTF8.GetBytes("not an image");

        Assert.True(ImageEmitProbe.LooksLikeImage(png));
        Assert.True(ImageEmitProbe.LooksLikeImage(jpeg));
        Assert.False(ImageEmitProbe.LooksLikeImage(text));
        Assert.Equal("image/png", ImageEmitProbe.DetectMime(png));
        Assert.Equal("image/jpeg", ImageEmitProbe.DetectMime(jpeg));
    }

    [Fact]
    public async System.Threading.Tasks.Task DetectAvailableImageEngines_AlwaysIncludesPollinationsFirst()
    {
        // Skip agent probes so the test is offline-fast and deterministic
        var engines = await ImageEngineDetector.DetectAvailableImageEnginesAsync(
            probeAgents: false, forceReprobe: false);

        Assert.NotEmpty(engines);
        Assert.Equal(ImageEngineDetector.DefaultEngineId, engines[0].Id);
        Assert.True(engines[0].IsAvailable);
        Assert.Equal(ImageGeneratorEngine.PollinationsAI, engines[0].EngineType);
    }
}
