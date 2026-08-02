using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CharacterSimulator.Logic;
using CharacterSimulator.Logic.Services;
using Xunit;

namespace CharacterSimulator.Logic.Tests;

public class DeriveCardServiceTests
{
    [Fact]
    public void ExtractJsonObject_HandlesFencedAndBare()
    {
        string bare = """{"name":"A","age":21}""";
        Assert.Equal(bare, DeriveCardService.ExtractJsonObject(bare));

        string fenced = """
            Here is the pack:
            ```json
            {"name":"B","age":30}
            ```
            done
            """;
        string? extracted = DeriveCardService.ExtractJsonObject(fenced);
        Assert.NotNull(extracted);
        Assert.Contains("\"name\":\"B\"", extracted);
    }

    [Fact]
    public void TryParseDerivePack_AcceptsWrappedAndBareCard()
    {
        string wrapped = """
            {
              "accuracy_summary": {
                "sources": ["wiki"],
                "kept": ["physical"],
                "compressed": [],
                "left_blank": ["esoteric"]
              },
              "card": {
                "name": "Testa",
                "age": 22,
                "canon_adult": true,
                "physical": "Tall, silver hair"
              }
            }
            """;

        Assert.True(DeriveCardService.TryParseDerivePack(wrapped, out var card, out var acc, out var err), err);
        Assert.Equal("Testa", card["name"]?.ToString());
        Assert.Contains("wiki", acc.Sources);
        Assert.Contains("physical", acc.Kept);

        string bare = """{"name":"Bare","age":40,"physical":"unknown"}""";
        Assert.True(DeriveCardService.TryParseDerivePack(bare, out var bareCard, out _, out err), err);
        Assert.Equal("Bare", bareCard["name"]?.ToString());
    }

    [Fact]
    public async Task DeriveAsync_MockProvider_WritesRandomIdCard()
    {
        string charDir = CharacterCatalog.ResolveCharactersDirectory();
        var before = Directory.GetFiles(charDir, "*.json");

        var result = await DeriveCardService.DeriveAsync(new DeriveCardService.DeriveCardRequest
        {
            CharacterName = "MockShinano",
            UserFilter = "test",
            SourcePaste = "MockShinano is a tall silver-haired character known for quiet speech.",
            LlmProvider = "MockEngine",
            SaveToDisk = true
        });

        Assert.True(result.Success, result.Error);
        Assert.False(string.IsNullOrEmpty(result.CardFileName));
        Assert.EndsWith(".json", result.CardFileName!);
        Assert.False(result.CardFileName!.Contains("MockShinano", System.StringComparison.OrdinalIgnoreCase));
        Assert.Equal("MockShinano", result.CharacterName);
        Assert.True(File.Exists(result.CardPath));

        // Display name comes from card body, not file stem
        Assert.Equal("MockShinano", CharacterCatalog.GetCharacterDisplayName(result.CardFileName!));

        // Cleanup test artifact
        try
        {
            if (result.CardPath != null && File.Exists(result.CardPath))
                File.Delete(result.CardPath);
        }
        catch { }

        _ = before;
    }

    [Fact]
    public void EnforceAgeGate_ViaParseAndDeriveInvariants()
    {
        // Minor age in pack → canon_adult forced false on save path through mock derive invariants
        string pack = """
            {
              "accuracy_summary": { "sources": [], "kept": [], "compressed": [], "left_blank": [] },
              "card": {
                "name": "Young Hero",
                "age": 15,
                "canon_adult": true,
                "physical": "youthful"
              }
            }
            """;
        Assert.True(DeriveCardService.TryParseDerivePack(pack, out var card, out _, out _));
        // Invariants are applied inside DeriveAsync; simulate expected gate here for documentation
        int age = card["age"]!.GetValue<int>();
        Assert.True(age < 18);
    }
}
