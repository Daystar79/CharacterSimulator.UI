using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static class CharacterLoader
{
    public static Character Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Character file not found: {path}");

        var fileContent = File.ReadAllText(path);
        var yamlStart = fileContent.IndexOf("---");
        var yamlEnd = fileContent.IndexOf("---", yamlStart + 3);

        if (yamlStart == -1 || yamlEnd == -1)
            throw new FormatException("Character file must have YAML frontmatter.");

        var yamlContent = fileContent.Substring(yamlStart + 3, yamlEnd - yamlStart - 3);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var yamlObject = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
        var character = new Character
        {
            Name = yamlObject.ContainsKey("name") ? yamlObject["name"].ToString() : Path.GetFileNameWithoutExtension(path),
            CardPath = path,
            CurrentState = yamlObject.ContainsKey("current_state") ? yamlObject["current_state"].ToString() : "DORMANT",
            Bond = yamlObject.ContainsKey("bond") ? Convert.ToInt32(yamlObject["bond"]) : 0
        };

        if (yamlObject.ContainsKey("physical"))
        {
            var physical = (Dictionary<string, object>)yamlObject["physical"];
            foreach (var kvp in physical)
                character.Attributes[kvp.Key] = kvp.Value.ToString();
        }

        if (yamlObject.ContainsKey("somatic_zones"))
        {
            var zones = (List<object>)yamlObject["somatic_zones"];
            foreach (var zone in zones) character.SomaticZones.Add(zone.ToString());
        }

        if (yamlObject.ContainsKey("goals"))
        {
            var goalsList = (List<object>)yamlObject["goals"];
            foreach (var goalObj in goalsList)
            {
                var goalDict = (Dictionary<string, object>)goalObj;
                var goal = new Goal
                {
                    Type = goalDict["type"].ToString(),
                    Target = goalDict["target"].ToString(),
                    Intensity = goalDict.ContainsKey("intensity") ? Convert.ToInt32(goalDict["intensity"]) : 5,
                    SuccessCondition = goalDict.ContainsKey("success_condition") ? goalDict["success_condition"].ToString() : "",
                    FailureCondition = goalDict.ContainsKey("failure_condition") ? goalDict["failure_condition"].ToString() : "",
                    Cooldown = goalDict.ContainsKey("cooldown") ? Convert.ToInt32(goalDict["cooldown"]) : 3,
                    Priority = goalDict.ContainsKey("priority") ? Convert.ToInt32(goalDict["priority"]) : 3
                };

                if (goalDict.ContainsKey("strategy"))
                {
                    var strategies = (List<object>)goalDict["strategy"];
                    foreach (var s in strategies) goal.Strategies.Add(s.ToString());
                }

                character.Goals.Add(goal);
            }
        }

        return character;
    }
}
