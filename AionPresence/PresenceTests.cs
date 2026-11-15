using System.Text.Json;
using System.Text.Json.Nodes;

namespace AionPresence;

// Pure formatting tests: no windows, capture engine, Discord connection or saved settings.
internal static class PresenceTests
{
    public static int Verify()
    {
        var count = 0;
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); count++; }
        var live = new LiveData(1, 1000, true, new("Lyra", "Spiritmaster", 1304, 45, 86320, 1520), "Crafting");
        JsonObject Make(Preferences? p = null, string? activity = null) => PresenceApp.ActivityFor(activity is null ? live : live with { Activity = activity }, p ?? new(), 123);
        var active = Make();
        Check(active["status_display_type"]!.GetValue<int>() == 1 && PresenceFormat.MemberListText(active) == "Crafting", "Auto switches the member list to activity only");
        Check(active["details"]!.GetValue<string>() == "Lyra · Spiritmaster · CP 86.32k · GS 1520", "Full selected profile stats survive automatic switching");
        Check(active["state"]!.GetValue<string>() == "Crafting", "Profile activity is separate from statistics");
        Check(active["timestamps"]!["start"]!.GetValue<long>() == 123, "Changing display preserves session time");
        foreach (var idle in new[] { "In game", " in GAME ", "AION 2", "", "  " }) {
            var p = Make(activity: idle);
            Check(p["status_display_type"]!.GetValue<int>() == 2 && PresenceFormat.MemberListText(p).Contains("CP 86.32k"), "Auto restores statistics while idle");
            Check(p["assets"]?["small_image"] is null && p["state"]!.GetValue<string>() == "AION 2", "Idle has a correctly spaced name and no badge");
        }
        foreach (var item in new[] { ("Crafting", "crafting"), ("Gathering", "gathering"), ("Shattered Arkanis", "dungeon"), ("Fighting Boss", "boss"), ("Shattered Arkanis · Fighting Boss", "boss"), ("Shugo Festival · Defend Shugo Merchants", "shugo") }) {
            var p = Make(activity:item.Item1);
            Check(PresenceFormat.MemberListText(p) == item.Item1 && p["assets"]!["small_image"]!.GetValue<string>().EndsWith("/" + item.Item2 + ".png"), "Recognized activity selects its own original badge");
        }
        foreach (var mode in new[] { "auto", "stats", "activity", "game" }) {
            var p = Make(new() { MemberListDisplay = mode, Activity = false });
            Check(p["state"] is null && p["assets"]?["small_image"] is null, "Disabling activity removes profile text and badge in every mode");
            Check(p["status_display_type"]!.GetValue<int>() == (mode == "game" ? 0 : 2), "Disabled activity cannot trigger automatic switching");
        }
        Check(Make(new() { MemberListDisplay = "stats" })["status_display_type"]!.GetValue<int>() == 2, "Stats mode remains on selected statistics during activity");
        Check(PresenceFormat.MemberListText(Make(new() { MemberListDisplay = "activity" }, "In game")) == "AION 2", "Activity-only falls back to AION 2");
        var game = Make(new() { MemberListDisplay = "game", Level = true });
        Check(game["status_display_type"]!.GetValue<int>() == 0 && PresenceFormat.MemberListText(game) == "AION 2", "Fixed game-name mode uses Discord Name");
        Check(game["details"]!.GetValue<string>().Contains("CP 86.32k") && game["details"]!.GetValue<string>().Contains("Lv 45") && game["state"]!.GetValue<string>() == "Crafting", "Fixed name keeps selected profile details and activity");
        foreach (var item in new[] { ("Name", "Lyra"), ("Class", "Spiritmaster"), ("CombatPower", "CP "), ("GearScore", "GS "), ("Level", "Lv ") }) {
            var settings = new Preferences { Level = true };
            typeof(Preferences).GetProperty(item.Item1)!.SetValue(settings, false);
            var p = Make(settings);
            Check(!p["details"]!.GetValue<string>().Contains(item.Item2) && !p["assets"]!["large_text"]!.GetValue<string>().Contains(item.Item2), "Hidden field is absent from profile text and image tooltip: " + item.Item1);
        }
        var none = Make(new() { Name = false, Class = false, CombatPower = false, GearScore = false, Level = false, Activity = false });
        Check(PresenceFormat.MemberListText(none) == "AION 2", "No selected fields falls back to AION 2");
        Check(PresenceFormat.MemberListText(Make(new() { Name = false, Class = false, CombatPower = false, GearScore = false, Level = false, MemberListDisplay = "activity" }, "In game")) == "AION 2", "All character fields off does not expose In game on the list");
        var legacy = JsonSerializer.Deserialize<Preferences>("{\"Name\":false,\"GearScore\":false}", Preferences.Json)!;
        Check(!legacy.Name && !legacy.GearScore && legacy.Class && legacy.MemberListDisplay == "auto", "Old settings keep all field choices and gain Auto mode");
        var saved = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(new Preferences { MemberListDisplay = "game", Level = true }, Preferences.Json), Preferences.Json)!;
        Check(saved.MemberListDisplay == "game" && saved.Level, "Mode and fields survive settings serialization");
        Check(PresenceFormat.MemberListText(Make(new() { MemberListDisplay = "unknown-future-value" })) == "Crafting", "Unknown display modes fall back safely to Auto");
        foreach (var cls in new[] { "Gladiator", "Templar", "Ranger", "Assassin", "Sorcerer", "Spiritmaster", "Cleric", "Chanter", "Brawler" }) {
            var p = PresenceApp.ActivityFor(live with { Character = live.Character! with { Class = cls } }, new(), 123);
            Check(p["assets"]!["large_image"]!.GetValue<string>().StartsWith("https://raw.githubusercontent.com/Kooowal-design/aion2-presence/") && p["assets"]!["large_image"]!.GetValue<string>().EndsWith("/" + cls.ToLowerInvariant() + ".png"), "Class uses this project's artwork: " + cls);
        }
        return count;
    }
}
