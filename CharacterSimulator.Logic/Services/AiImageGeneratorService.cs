using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic.Services;

public class AiImageGeneratorService
{
    private static readonly HttpClient _httpClient = new HttpClient();

    /// <summary>
    /// Generates a character portrait using Pollinations.AI free public image endpoint.
    /// </summary>
    /// <param name="prompt">Character appearance prompt</param>
    /// <param name="characterSlug">Character filename / identifier slug</param>
    /// <returns>Local file path of saved portrait image</returns>
    public static async Task<string> GeneratePortraitAsync(string prompt, string characterSlug)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            prompt = $"{characterSlug} character portrait high quality detailed artwork";
        }

        string cleanPrompt = Uri.EscapeDataString($"{prompt}, high quality detailed character portrait, 8k render, digital art");
        string imageUrl = $"https://image.pollinations.ai/prompt/{cleanPrompt}?width=512&height=512&nologo=true&seed={Random.Shared.Next(1, 999999)}";

        string targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Portraits");
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string localPath = Path.Combine(targetDir, $"{characterSlug.Replace(".md", "")}.jpg");

        try
        {
            byte[] imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
            await File.WriteAllBytesAsync(localPath, imageBytes);
            return localPath;
        }
        catch
        {
            return imageUrl; // Fallback to direct web URL
        }
    }
}
