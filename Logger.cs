using System;
using System.Collections.Generic;
using System.IO;

public class Logger
{
    private readonly string _logPath;

    public Logger(string logPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath));
        _logPath = logPath;
        File.WriteAllText(_logPath, $"[LOG START] {DateTime.Now}\n\n");
    }

    public void LogScene(string scene) => File.AppendAllText(_logPath, $"[SCENE] {scene}\n");

    public void LogTurn(string character, string dialogue, List<string> somaticZones, int bond, string goalType = null, string goalStatus = null)
    {
        File.AppendAllText(_logPath, $"[{character}] (Bond: {bond}, Somatic: {string.Join(", ", somaticZones)}) {dialogue}");
        if (goalType != null) File.AppendAllText(_logPath, $" [Goal: {goalType} - {goalStatus}]");
        File.AppendAllText(_logPath, "\n");
    }

    public void LogGoalSuccess(string character, string goalType, string target) =>
        File.AppendAllText(_logPath, $"[GOAL SUCCESS] {character} achieved {goalType} with {target}!\n");

    public void LogGoalFailure(string character, string goalType, string target) =>
        File.AppendAllText(_logPath, $"[GOAL FAILURE] {character} failed {goalType} with {target}.\n");
}
