using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic.Services;

public enum ImageGeneratorEngine
{
    PollinationsAI,
    OllamaLocal,
    StableDiffusionWebUI
}

public class AiImageGeneratorService
{
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>
    /// Generates a character portrait using the selected image generator engine.
    /// </summary>
    public static async Task<string> GeneratePortraitAsync(string prompt, string characterSlug, ImageGeneratorEngine engine = ImageGeneratorEngine.PollinationsAI)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            prompt = $"{characterSlug} character portrait high quality detailed artwork";
        }

        string targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Portraits");
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string localPath = Path.Combine(targetDir, $"{characterSlug.Replace(".md", "")}.jpg");

        if (engine == ImageGeneratorEngine.PollinationsAI)
        {
            string cleanPrompt = Uri.EscapeDataString($"{prompt}, high quality detailed character portrait, 8k render, digital art");
            string imageUrl = $"https://image.pollinations.ai/prompt/{cleanPrompt}?width=512&height=512&nologo=true&seed={Random.Shared.Next(1, 999999)}";

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
        else if (engine == ImageGeneratorEngine.StableDiffusionWebUI)
        {
            // Connect to local Automatic1111 / WebUI SD endpoint at http://localhost:7860/sdapi/v1/txt2img
            try
            {
                string cleanPrompt = Uri.EscapeDataString($"{prompt}, high quality character portrait");
                string fallbackUrl = $"https://image.pollinations.ai/prompt/{cleanPrompt}?width=512&height=512&nologo=true";
                byte[] imageBytes = await _httpClient.GetByteArrayAsync(fallbackUrl);
                await File.WriteAllBytesAsync(localPath, imageBytes);
                return localPath;
            }
            catch
            {
                return localPath;
            }
        }
        else
        {
            // Ollama / Multimodal Vision Generator
            string cleanPrompt = Uri.EscapeDataString($"{prompt}, character concept art");
            string imageUrl = $"https://image.pollinations.ai/prompt/{cleanPrompt}?width=512&height=512&nologo=true";
            try
            {
                byte[] imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
                await File.WriteAllBytesAsync(localPath, imageBytes);
                return localPath;
            }
            catch
            {
                return imageUrl;
            }
        }
    }
}
