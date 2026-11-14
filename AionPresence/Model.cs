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
    // A2Tools' own class-art keys. The plugin reports the game's localized class name.
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
    public static JsonObject Build(JsonObject original, LiveData live, Preferences p)
    {
        var result = (JsonObject)original.DeepClone();
        var c = live.Character!;
        var cls = ClassName(c, original);
        var activity = Clean(original["state"]?.GetValue<string>());
        var parts = new List<string>();
        if (p.Name) parts.Add(Clean(c.Name));
        if (p.Class && cls.Length > 0) parts.Add(cls);
        if (p.Level && c.Level > 0) parts.Add($"Lv {c.Level}");
        if (p.CombatPower && c.CombatPower > 0) parts.Add($"CP {CP(c.CombatPower)}");
        if (p.GearScore && c.GearScore > 0) parts.Add($"GS {c.GearScore}");
        if (p.Activity && activity.Length > 0) parts.Add(activity);
        result["details"] = Limit(string.Join(" · ", parts.Where(x => x.Length > 0)) is { Length: > 0 } line ? line : "AION 2");
        result["status_display_type"] = 2; // Details in Discord's member list.
        result["type"] = 0;
        // Preserve A2Tools' boss/dungeon/server state, images and timestamps.
        // Full statistics also remain available on the large image's tooltip.
        var full = new List<string> { Clean(c.Name), cls };
        if (c.Level > 0) full.Add($"Lv {c.Level}");
        if (c.CombatPower > 0) full.Add($"CP {c.CombatPower.Value.ToString("N0", CultureInfo.InvariantCulture)}");
        if (c.GearScore > 0) full.Add($"GS {c.GearScore}");
        if (result["assets"] is JsonObject assets) assets["large_text"] = Limit(string.Join(" · ", full.Where(x => x.Length > 0)));
        return result;
    }
}
