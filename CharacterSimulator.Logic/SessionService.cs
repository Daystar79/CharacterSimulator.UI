using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CharacterSimulator.Logic;

public class RoleplaySessionData
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public DateTime SavedAt { get; set; } = DateTime.Now;
    public string SceneContext { get; set; } = string.Empty;
    public Character CharacterA { get; set; } = new();
    public Character CharacterB { get; set; } = new();
    public List<TurnStepEventArgs> History { get; set; } = new();
}

public static class SessionService
{
    public static void SaveSession(string path, RoleplaySessionData sessionData)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static RoleplaySessionData? LoadSession(string path)
    {
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<RoleplaySessionData>(json);
    }
}
