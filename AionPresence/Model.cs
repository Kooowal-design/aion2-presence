using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AionPresence;

public sealed record CharacterProfile(string Name, string? Class, int? ServerId, int? Level, long? CombatPower, int? GearScore);
public sealed record LiveData(int Schema, long UpdatedAt, bool Running, CharacterProfile? Character,
    string Activity = "", string Server = "", bool CaptureOk = false, int? Port = null, string? Device = null)
{
    public bool Fresh(long now) => Schema == 1 && UpdatedAt <= now + 5000 && now - UpdatedAt <= 30_000;
}

public sealed class Preferences
{
    public string Language { get; set; } = "en";
    public string? InstallFolder { get; set; }
    public bool Name { get; set; } = true;
    public bool Class { get; set; } = true;
    public bool CombatPower { get; set; } = true;
    public bool GearScore { get; set; } = true;
    public bool Activity { get; set; } = true;
    public bool Level { get; set; }
    public string MemberListDisplay { get; set; } = "auto";
    public bool StartMinimized { get; set; }
    public bool AutoStart { get; set; }
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aion2Presence");
    public static string FileName => Path.Combine(Folder, "settings.json");
    public static Preferences Load()
    {
        try { return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FileName), Json) ?? new(); }
        catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Folder);
        var temp = FileName + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
        File.Move(temp, FileName, true);
    }
}

public static class PresenceFormat
{
    // Normalize the game's localized class names to the project's artwork keys.
    private static readonly Dictionary<string, string> Classes = new() {
        ["검성"] = "Gladiator", ["수호성"] = "Templar", ["궁성"] = "Ranger", ["살성"] = "Assassin",
        ["마도성"] = "Sorcerer", ["정령성"] = "Spiritmaster", ["치유성"] = "Cleric", ["호법성"] = "Chanter",
        ["권성"] = "Brawler", ["Fighter"] = "Brawler", ["Elementalist"] = "Spiritmaster"
    };
    public static string Clean(string? text) => string.Concat((text ?? "").Where(c => !char.IsControl(c))).Trim();
    public static string Limit(string text, int max = 128)
    {
        if (text.Length <= max) return text;
        var end = max - 1;
        if (char.IsHighSurrogate(text[end - 1])) end--;
        return text[..end] + "…";
    }
    public static string CP(long? value) => value switch {
        >= 1_000_000 => (value.Value / 1_000_000d).ToString("0.##", CultureInfo.InvariantCulture) + "M",
        >= 1000 => (value.Value / 1000d).ToString("0.##", CultureInfo.InvariantCulture) + "k",
        > 0 => value.Value.ToString(CultureInfo.InvariantCulture), _ => ""
    };
    public static string ClassName(CharacterProfile c, JsonObject original)
    {
        var image = original["assets"]?["large_image"]?.GetValue<string>();
        if (image is not null && new[] { "gladiator", "templar", "ranger", "assassin", "sorcerer", "spiritmaster", "cleric", "chanter", "brawler" }.Contains(image))
            return char.ToUpperInvariant(image[0]) + image[1..];
        var name = Clean(c.Class);
        return Classes.GetValueOrDefault(name, name);
    }
    public static bool HasActivity(string? activity) => Clean(activity) is { Length: > 0 } text
        && !text.Equals("In game", StringComparison.OrdinalIgnoreCase)
        && !text.Equals("AION 2", StringComparison.OrdinalIgnoreCase);
    public static string? ActivityIcon(string? activity)
    {
        if (!HasActivity(activity)) return null;
        var text = Clean(activity);
        if (text.Contains("Fighting ", StringComparison.OrdinalIgnoreCase)) return "boss";
        if (text.Contains("Crafting", StringComparison.OrdinalIgnoreCase)) return "crafting";
        if (text.Contains("Gathering", StringComparison.OrdinalIgnoreCase)) return "gathering";
        if (text.StartsWith("Shugo Festival", StringComparison.OrdinalIgnoreCase)) return "shugo";
        return "dungeon";
    }
    public static string MemberListText(JsonObject activity) =>
        activity["status_display_type"]?.GetValue<int>() switch {
            0 => "AION 2",
            1 => activity["state"]?.GetValue<string>() ?? "AION 2",
            _ => activity["details"]?.GetValue<string>() ?? "AION 2"
        };
    public static JsonObject Build(JsonObject original, LiveData live, Preferences p)
    {
        var result = (JsonObject)original.DeepClone();
        var c = live.Character!;
        var cls = ClassName(c, original);
        var activity = Clean(original["state"]?.GetValue<string>());
        var parts = new List<string>();
        if (p.Name) parts.Add(Clean(c.Name));
        if (p.Class && cls.Length > 0) parts.Add(cls);
        if (p.CombatPower && c.CombatPower > 0) parts.Add($"CP {CP(c.CombatPower)}");
        if (p.GearScore && c.GearScore > 0) parts.Add($"GS {c.GearScore}");
        if (p.Level && c.Level > 0) parts.Add($"Lv {c.Level}");
        result["details"] = Limit(string.Join(" · ", parts.Where(x => x.Length > 0)) is { Length: > 0 } line ? line : "AION 2");
        if (p.Activity) result["state"] = Limit(HasActivity(activity) ? activity : "AION 2");
        else result.Remove("state");
        result["status_display_type"] = p.MemberListDisplay switch {
            "game" => 0,
            "activity" when p.Activity => 1,
            "stats" => 2,
            _ => p.Activity && HasActivity(activity) ? 1 : 2
        };
        result["type"] = 0;
        // Hidden fields stay hidden in the artwork tooltip as well.
        if (result["assets"] is JsonObject assets) assets["large_text"] = result["details"]!.DeepClone();
        return result;
    }
}
