using System;
using System.IO;
using System.Text;
using CharacterSimulator.Logic.Data.Db;
using CharacterSimulator.Logic.Services;
using Xunit;

namespace CharacterSimulator.Logic.Tests;

public class CharacterPortraitStoreTests
{
    [Fact]
    public void PortraitRepository_UpsertAndGetDataUri_RoundTrips()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"test_portrait_{Guid.NewGuid():N}.db");
        try
        {
            using var conn = AppDbInitializer.CreateConnection(tempDb);
            AppDbInitializer.InitializeDatabase(conn);
            var repo = new CharacterPortraitRepository(conn);

            // Minimal fake JPEG header + payload (not a real image, storage only)
            byte[] bytes = Encoding.UTF8.GetBytes("fake-jpeg-bytes-for-test");
            Assert.False(repo.Exists("cardabc"));

            repo.UpsertBytes("cardabc", bytes, "image/jpeg", prompt: "silver hair", engine: "Mock");
            Assert.True(repo.Exists("cardabc"));

            var rec = repo.Get("cardabc");
            Assert.NotNull(rec);
            Assert.Equal(bytes, rec!.ImageBlob);
            Assert.Equal("silver hair", rec.Prompt);

            string? uri = repo.GetDataUri("cardabc");
            Assert.NotNull(uri);
            Assert.StartsWith("data:image/jpeg;base64,", uri);
            Assert.Contains(Convert.ToBase64String(bytes), uri!);
        }
        finally
        {
            try { File.Delete(tempDb); } catch { }
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task CharacterPortraitService_SaveAndEnsure_UsesStoreWithoutNetworkWhenPresent()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"test_portrait_svc_{Guid.NewGuid():N}.db");
        try
        {
            using var conn = AppDbInitializer.CreateConnection(tempDb);
            AppDbInitializer.InitializeDatabase(conn);
            var portraits = new CharacterPortraitRepository(conn);
            var catalog = new CharacterCatalogRepository(conn);
            CharacterPortraitService.Bind(portraits, catalog);

            byte[] bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0x00, 0x01, 0x02 };
            string uri = CharacterPortraitService.SavePortrait("id001", bytes, "image/jpeg", "test prompt", "Test");
            Assert.StartsWith("data:image/jpeg;base64,", uri);
            Assert.True(CharacterPortraitService.HasPortrait("id001"));

            // Ensure must NOT call network — returns stored
            var ensured = await CharacterPortraitService.EnsurePortraitAsync(
                "id001", "should not generate", generateIfMissing: true);
            Assert.Equal(uri, ensured);
        }
        finally
        {
            CharacterPortraitService.Bind(null);
            try { File.Delete(tempDb); } catch { }
        }
    }
}
