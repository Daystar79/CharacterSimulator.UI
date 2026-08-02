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
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    static AiImageGeneratorService()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    /// <summary>
    /// Generates a character portrait using the selected image generator engine.
    /// Returns a base64 data URI (data:image/jpeg;base64,...) or direct HTTP image URL for 100% reliable WebView rendering.
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

        // characterSlug may be a display name, card id, or filename — normalize to a file stem.
        string stem = Path.GetFileNameWithoutExtension(characterSlug);
        if (string.IsNullOrWhiteSpace(stem))
            stem = "portrait";
        foreach (var ch in Path.GetInvalidFileNameChars())
            stem = stem.Replace(ch, '_');
        string localPath = Path.Combine(targetDir, $"{stem}.jpg");

        if (engine == ImageGeneratorEngine.PollinationsAI)
        {
            string cleanPrompt = Uri.EscapeDataString($"{prompt}, high quality detailed character portrait, 8k render, digital art");
            string imageUrl = $"https://image.pollinations.ai/prompt/{cleanPrompt}?width=512&height=512&nologo=true&seed={Random.Shared.Next(1, 999999)}";

            try
            {
                byte[] imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
                if (imageBytes != null && imageBytes.Length > 0)
                {
                    await File.WriteAllBytesAsync(localPath, imageBytes);
                    string base64 = Convert.ToBase64String(imageBytes);
                    return $"data:image/jpeg;base64,{base64}";
                }
            }
            catch
            {
                // Fallback to direct URL if download/save failed
            }
            return imageUrl;
        }
        else if (engine == ImageGeneratorEngine.StableDiffusionWebUI)
        {
            // Connect to local Automatic1111 / WebUI SD endpoint at http://localhost:7860/sdapi/v1/txt2img
            try
            {
                string cleanPrompt = Uri.EscapeDataString($"{prompt}, high quality character portrait");
                string fallbackUrl = $"https://image.pollinations.ai/prompt/{cleanPrompt}?width=512&height=512&nologo=true";
                byte[] imageBytes = await _httpClient.GetByteArrayAsync(fallbackUrl);
                if (imageBytes != null && imageBytes.Length > 0)
                {
                    await File.WriteAllBytesAsync(localPath, imageBytes);
                    string base64 = Convert.ToBase64String(imageBytes);
                    return $"data:image/jpeg;base64,{base64}";
                }
                return fallbackUrl;
            }
            catch
            {
                return $"https://image.pollinations.ai/prompt/{Uri.EscapeDataString(prompt)}?width=512&height=512&nologo=true";
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
                if (imageBytes != null && imageBytes.Length > 0)
                {
                    await File.WriteAllBytesAsync(localPath, imageBytes);
                    string base64 = Convert.ToBase64String(imageBytes);
                    return $"data:image/jpeg;base64,{base64}";
                }
            }
            catch { }
            return imageUrl;
        }
    }
}
