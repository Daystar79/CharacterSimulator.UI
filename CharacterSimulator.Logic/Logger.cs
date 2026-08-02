using System;
using System.Collections.Generic;
using System.IO;

namespace CharacterSimulator.Logic;

public class Logger
{
    private readonly string _logPath;
    private readonly object _fileLock = new object();

    public Logger(string logPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            _logPath = logPath;
            File.WriteAllText(_logPath, $"[LOG START] {DateTime.Now}\n\n");
        }
        catch (Exception ex)
        {
            // If we can't initialize the logger, use a fallback path
            _logPath = Path.Combine(Path.GetTempPath(), "character_simulator_fallback.log");
            try
            {
                File.WriteAllText(_logPath, $"[LOG START FALLBACK] {DateTime.Now} - Original path failed: {ex.Message}\n\n");
            }
            catch { /* If even fallback fails, give up */ }
        }
    }

    private void SafeAppend(string text)
    {
        if (string.IsNullOrEmpty(_logPath)) return;
        try
        {
            lock (_fileLock)
            {
                File.AppendAllText(_logPath, text);
            }
        }
        catch { /* Silently ignore file I/O errors */ }
    }

    public void LogScene(string scene) => SafeAppend($"[SCENE] {scene}\n");

    public void LogTurn(string character, string dialogue, List<string> somaticZones, int bond, string? goalType = null, string? goalStatus = null)
    {
        var line = $"[{character}] (Bond: {bond}, Somatic: {string.Join(", ", somaticZones)}) {dialogue}";
        if (goalType != null) line += $" [Goal: {goalType} - {goalStatus}]";
        line += "\n";
        SafeAppend(line);
    }

    public void LogGoalSuccess(string character, string goalType, string target) =>
        SafeAppend($"[GOAL SUCCESS] {character} achieved {goalType} with {target}!\n");

    public void LogGoalFailure(string character, string goalType, string target) =>
        SafeAppend($"[GOAL FAILURE] {character} failed {goalType} with {target}.\n");
}
