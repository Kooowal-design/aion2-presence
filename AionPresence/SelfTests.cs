using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AionPresence;

public static class SelfTests
{
    private static int count;
    private static void Check(bool condition, string label) { if (!condition) throw new Exception(label); count++; }
    public static async Task<string> Run()
    {
        try {
            var language = UiLanguage.Language;
            UiLanguage.Language = "en";
            Check(UiLanguage.Text("Zainstaluj") == "Install", "English installer text");
            Check(UiLanguage.Text("Aktywność: Gathering") == "Activity: Gathering", "Dynamic activity localization");
            using (var preview = new MainForm(true)) {
                Check(preview.Controls.Cast<Control>().SelectMany(Flatten).Any(c => c.Text == "CONNECTIONS"), "English application controls");
                UiLanguage.Language = "pl"; UiLanguage.Apply(preview);
                Check(preview.Controls.Cast<Control>().SelectMany(Flatten).Any(c => c.Text == "POŁĄCZENIA"), "Live language switch restores Polish");
            }
            UiLanguage.Language = language;
            SynchronizationContext.SetSynchronizationContext(null); // No UI message loop in command-line tests.
            Check(Program.IsInstaller("Aion2Presence-Setup.exe"), "Installer filename");
            Check(Program.IsInstaller("Aion2Presence-Setup (1).exe"), "Downloaded installer with duplicate suffix");
            Check(!Program.IsInstaller("Aion2Presence.exe"), "Installed app is not an installer");
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Check(Feedback.BugUrl("0.2.0-beta.2").EndsWith("template=bug_report.yml&version=0.2.0-beta.2"), "Bug form prefilled with app version");
            Check(!Feedback.BugUrl("0.2.0-beta.2").Contains("name="), "Feedback link contains no player identity");
            Check(ReleaseInfo.Resource("License").Contains("GNU GENERAL PUBLIC LICENSE"), "GPL text embedded in executable");
            Check(ReleaseInfo.Resource("ThirdParty").Contains("third-party software notices"), "Dependency notices embedded");
            Check(ReleaseInfo.ApplicationId.Length >= 17 && ReleaseInfo.ApplicationId.All(char.IsAsciiDigit), "Discord application identifier format");
            var c = new CharacterProfile("Kowal", "Gladiator", 1304, 50, 123450, 2050);
            var live = new LiveData(1, now, true, c, "Fighting Garshim", "Kaisinel", true);
            var prefs = new Preferences();
            var result = PresenceApp.ActivityFor(live, prefs, 123);
            Check(result["details"]!.GetValue<string>() == "Kowal · Gladiator · CP 123.45k · GS 2050 · Fighting Garshim", "Requested summary order");
            Check(result["status_display_type"]!.GetValue<int>() == 2, "Details selected for member list");
            Check(result["timestamps"]!["start"]!.GetValue<long>() == 123, "Session time retained");
            Check(result["state"]!.GetValue<string>() == "Lv 50 · Fighting Garshim", "Profile second line contains only level and activity");
            prefs.CombatPower = false; prefs.Activity = false;
            Check(!PresenceApp.ActivityFor(live, prefs, 123)["details"]!.GetValue<string>().Contains("CP "), "Visibility setting CP");
            Check(!PresenceApp.ActivityFor(live, prefs, 123)["details"]!.GetValue<string>().Contains("Fighting"), "Visibility setting activity");
            Check(PresenceApp.ActivityFor(live, prefs, 123)["state"]!.GetValue<string>() == "Lv 50", "Hidden activity is omitted from profile second line");
            Check(PresenceApp.ActivityFor(live with { Character = c with { Level = null } }, new(), 123)["state"]!.GetValue<string>() == "Fighting Garshim", "Unknown level is omitted from profile second line");
            Check(!live.Fresh(now + 31000), "Expired source not accepted");
            Check(!live.Fresh(now - 6000), "Future source not accepted");
            Check(PresenceFormat.CP(1234500) == "1.23M", "Compact CP million");
            var longName = live with { Character = c with { Name = new string('x', 127) + "😀" } };
            var longText = PresenceApp.ActivityFor(longName, new(), 1)["details"]!.GetValue<string>();
            Check(longText.Length <= 128 && !char.IsHighSurrogate(longText[^2]), "Unicode-safe length limit");
            var missing = live with { Character = c with { CombatPower = null, GearScore = null } };
            var missingText = PresenceApp.ActivityFor(missing, new(), 1)["details"]!.GetValue<string>();
            Check(!missingText.Contains("CP ") && !missingText.Contains("GS "), "Unknown statistics omitted");
            var json = JsonSerializer.Serialize(live);
            Check(JsonSerializer.Deserialize<LiveData>(json, Preferences.Json)?.Character?.Name == "Kowal", "Engine JSON schema");
            using (var embedded = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("AionPresence.Engine")) {
                Check(embedded is not null && embedded.Length > 100_000, "Standalone engine is embedded in this executable");
                var path = EngineBinary.Extract();
                Check(File.Exists(path), "Embedded engine extracts without A2Tools");
            }
            using var deadline = new CancellationTokenSource(10000);
            var ct = deadline.Token;
            // Isolated pipe names: no game or live Discord account is touched by tests.
            var prefix = "aion-presence-test-" + Guid.NewGuid().ToString("N") + "-";
            using var server = new NamedPipeServerStream(prefix + "0", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            var mock = Task.Run(async () => {
                await server.WaitForConnectionAsync(ct);
                var hello = await IpcFrame.Read(server, ct);
                Check(hello.Op == 0 && hello.Body["client_id"]!.GetValue<string>() == PresenceApp.ApplicationId, "Real IPC handshake protocol");
                await IpcFrame.Write(server, 1, new JsonObject { ["evt"] = "READY", ["data"] = new JsonObject() }, ct);
                var command = await IpcFrame.Read(server, ct);
                Check(command.Body["args"]!["activity"]!["status_display_type"]!.GetValue<int>() == 2, "IPC carries member-list option");
                await IpcFrame.Write(server, 3, new JsonObject { ["ping"] = 1 }, ct);
                var pong = await IpcFrame.Read(server, ct);
                Check(pong.Op == 4, "IPC ping/pong");
                await IpcFrame.Write(server, 1, new JsonObject { ["cmd"] = "SET_ACTIVITY", ["nonce"] = command.Body["nonce"]!.DeepClone(), ["data"] = result.DeepClone() }, ct);
                var clear = await IpcFrame.Read(server, ct);
                Check(clear.Body["args"]!["activity"] is null, "Explicit status clear");
                await IpcFrame.Write(server, 1, new JsonObject { ["nonce"] = clear.Body["nonce"]!.DeepClone(), ["data"] = null }, ct);
            }, ct);
            await using (var connection = await DiscordPipe.Connect("", ct, prefix)) {
                var reply = await connection.Set(PresenceApp.Command(result), ct);
                Check(reply["data"]!["status_display_type"]!.GetValue<int>() == 2, "Discord response acknowledged");
                await connection.Set(PresenceApp.Command(null), ct);
            }
            await mock;
            var malformed = new MemoryStream(new byte[8]);
            try { await IpcFrame.Read(malformed, ct); throw new Exception("Malformed frame accepted"); }
            catch (IOException) { count++; }
            var header = new byte[8]; BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), IpcFrame.MaxBytes + 1);
            try { await IpcFrame.Read(new MemoryStream(header), ct); throw new Exception("Oversized frame accepted"); }
            catch (IOException) { count++; }
            return $"PASS: {count} checks. Isolated IPC; no live Discord status was published.";
        } catch (Exception e) { return "FAIL: " + e; }
    }
    private static IEnumerable<Control> Flatten(Control root) => new[] { root }.Concat(root.Controls.Cast<Control>().SelectMany(Flatten));
}
