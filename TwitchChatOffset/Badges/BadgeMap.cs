using TwitchChatOffset.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TwitchChatOffset.Badges;

public class BadgeConfigException(string message) : Exception(message)
{
    public class FileNotFound(string path) : BadgeConfigException($"Badge config file not found: {path}");

    public class InvalidValue(string key, object? value)
        : BadgeConfigException($"Badge config entry for '{key}' must map to a string; got: {value}");
}

public class BadgeMap
{
    private const string WildcardKey = "*";

    private static readonly Dictionary<string, string> DefaultEntries = new()
    {
        ["broadcaster"] = "📺",
        ["moderator"] = "🛡️",
        ["vip"] = "💎",
        ["turbo"] = "🚀",
        ["subscriber"] = "⭐",
        ["founder"] = "💜",
        ["premium"] = "🎁",
        ["partner"] = "✔️",
        ["staff"] = "🛠️",
        ["admin"] = "🔑",
        ["global_mod"] = "🌐",
        ["bits"] = "💰",
        ["bits-leader"] = "🏆",
        ["bits-charity"] = "🎗️",
        ["sub-gift-leader"] = "🎀",
        ["sub-gift-chelper"] = "🎁",
        ["predictions"] = "🔮",
        ["glhf-pledge"] = "❤️",
        ["confirmed-bot"] = "🤖"
    };

    public static BadgeMap Default { get; } = new(new Dictionary<string, string>(DefaultEntries));

    public static BadgeMap Empty => new([]);

    public static string DefaultConfigJson => JsonConvert.SerializeObject(DefaultEntries, Formatting.Indented);

    public static void WriteDefaultConfig(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, DefaultConfigJson + Environment.NewLine);
        PrintLine($"Wrote default badge config to '{path}'");
    }

    private readonly Dictionary<string, string> _map;
    private readonly string? _wildcard;

    private BadgeMap(Dictionary<string, string> map)
    {
        _map = map;
        _wildcard = map.TryGetValue(WildcardKey, out string? wildcard) ? wildcard : null;
    }

    public static BadgeMap Load(string? configPath)
    {
        Dictionary<string, string> map = new(DefaultEntries);
        if (string.IsNullOrEmpty(configPath) || configPath == string.Empty)
            return new BadgeMap(map);

        string jsonString;
        try
        {
            jsonString = File.ReadAllText(configPath);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new BadgeConfigException.FileNotFound(configPath);
        }
        JObject config = JsonUtils.Deserialize(jsonString);
        foreach (KeyValuePair<string, JToken?> entry in config)
        {
            string? emoji = entry.Value is JValue { Value: string value } ? value : null;
            if (emoji == null)
                throw new BadgeConfigException.InvalidValue(entry.Key, entry.Value?.Type switch
                {
                    JTokenType.Null => null,
                    _ => entry.Value is JValue jvalue ? jvalue.Value : entry.Value
                });
            map[entry.Key] = emoji;
        }
        return new BadgeMap(map);
    }

    public string GetBadgesText(JToken message)
    {
        if (message.DN("user_badges") is not JArray badges || badges.Count == 0)
            return string.Empty;
        StringBuilder builder = new();
        foreach (JToken badge in badges)
        {
            string? id = badge.DN("_id")?.AsN<string>()?.Value;
            if (id == null)
                continue;
            string? version = badge.DN("version")?.AsN<string>()?.Value;
            if (id.Length > 0 && !string.IsNullOrEmpty(version)
                && _map.TryGetValue(string.Concat(id, ":", version), out string? versionedEmoji))
            {
                if (versionedEmoji.Length > 0)
                    builder.Append(versionedEmoji);
                continue;
            }
            if (_map.TryGetValue(id, out string? emoji))
            {
                if (emoji.Length > 0)
                    builder.Append(emoji);
                continue;
            }
            if (!string.IsNullOrEmpty(_wildcard))
                builder.Append(_wildcard);
        }
        return builder.ToString();
    }
}