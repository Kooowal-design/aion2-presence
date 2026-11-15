using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;

namespace AionPresence;

public static class SelfTests
{
    private static int count;
    private static void Check(bool condition, string label) { if (!condition) throw new Exception(label); count++; }
    public static async Task<string> Run()
    {
        try {
            count += PresenceTests.Verify();
            var language = UiLanguage.Language;
            UiLanguage.Language = "en";
            Check(UiLanguage.Text("Zainstaluj") == "Install", "English installer text");
            Check(UiLanguage.Text("Aktywność: Gathering") == "Activity: Gathering", "Dynamic activity localization");
            var preferencesBefore = File.Exists(Preferences.FileName) ? File.ReadAllText(Preferences.FileName) : null;
            var preview = new MainWindow(preview: true, renderOnly: true) { WindowStartupLocation = System.Windows.WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false };
            try {
                preview.Show(); preview.UpdateLayout();
                Check(preview.HomeText == "Home", "English application navigation");
                Check(preview.UpdateTitle == "Up to date", "Current-version panel in preview");
                preview.SetPreviewUpdate(true);
                Check(preview.UpdateTitle == "Update available", "Available-update panel in preview");
                preview.SetMemberListMode("game");
                Check(preview.ListLabel == "Member list: AION 2", "Window applies fixed game-name mode");
                preview.SetMemberListMode("auto"); preview.ShowActivity = true;
                Check(preview.ListLabel.Contains("Shugo Festival"), "Window applies automatic activity mode");
                preview.ShowActivity = false;
                Check(!preview.ListLabel.Contains("Shugo Festival") && preview.DiscordProfile.Length == 0, "Window respects disabled activity in Auto mode");
                preview.ShowActivity = true;
                preview.ShowName = !preview.ShowName;
                Check(preview.ShowName == preview.DiscordSummary.StartsWith("Lyra"), "Display choice changes the actual Discord summary");
                preview.ShowPage("home"); preview.Width=960; preview.Height=720; preview.UpdateLayout();
                Check(preview.CpValue.Text == "86.32k" && preview.CpValue.ActualHeight > 20, "CP has complete text and a full-height WPF value element");
                preview.SetLanguage("pl");
                Check(preview.HomeText == "Start", "Live language switch restores Polish navigation");
            } finally { preview.Close(); }
            Check((File.Exists(Preferences.FileName) ? File.ReadAllText(Preferences.FileName) : null) == preferencesBefore, "Design preview never changes saved preferences");
            UiLanguage.Language = language;
            SynchronizationContext.SetSynchronizationContext(null); // No UI message loop in command-line tests.
            Check(Program.IsInstaller("Aion2Presence-Setup.exe"), "Installer filename");
            Check(Program.IsInstaller("Aion2Presence-Setup (1).exe"), "Downloaded installer with duplicate suffix");
            Check(!Program.IsInstaller("Aion2Presence.exe"), "Installed app is not an installer");
            Check(Program.IsDesignPreview("Aion2Presence-Preview.exe") && !Program.IsDesignPreview("Aion2Presence.exe"), "Local preview executable defaults to isolated demo mode");
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Check(Updates.Compare("0.2.0-beta.10", "0.2.0-beta.9") > 0, "Numeric beta versions sort correctly");
            Check(Updates.Compare("0.2.0", "0.2.0-beta.99") > 0, "Stable release sorts after its betas");
            Check(!Updates.TryVersion("0.2.0-beta/evil", out _, out _), "Invalid release tags rejected");
            Check(!Updates.AssetUrl("https://github.com/another/repo/releases/download/v0.2.0/Aion2Presence-Setup.exe", "0.2.0", "Aion2Presence-Setup.exe", out _), "Update from another repository rejected");
            Check(!Updates.AssetUrl("http://github.com/Kooowal-design/aion2-presence/releases/download/v0.2.0/Aion2Presence-Setup.exe", "0.2.0", "Aion2Presence-Setup.exe", out _), "Unencrypted update URL rejected");
            object Release(string v) => new { draft = false, tag_name = "v" + v, assets = new[] {
                new { name = "Aion2Presence-Setup.exe", browser_download_url = ReleaseInfo.Repository + "/releases/download/v" + v + "/Aion2Presence-Setup.exe" },
                new { name = "SHA256SUMS.txt", browser_download_url = ReleaseInfo.Repository + "/releases/download/v" + v + "/SHA256SUMS.txt" } } };
            var releaseJson = JsonSerializer.Serialize(new[] { Release("0.2.0-beta.9"), Release("0.2.0-beta.10") });
            Check(Updates.Select(releaseJson, "0.2.0-beta.5")?.Version == "0.2.0-beta.10", "Newest complete beta selected regardless of API order");
            Check(Updates.Select(releaseJson, "0.2.0") is null, "Stable users do not receive beta updates");
            Check(Updates.Select(releaseJson, "0.2.0-beta.10") is null, "No same-version update or downgrade");
            var updateCandidate = Updates.Select(releaseJson, "0.2.0-beta.5")!;
            var downloadRoot = Path.Combine(Path.GetTempPath(), "aion-presence-update-test-" + Guid.NewGuid().ToString("N"));
            try {
                using var downloadMock = new HttpClient(new UpdateTestHandler(false));
                var downloaded = await Updates.Download(updateCandidate, new Progress<int>(), CancellationToken.None, downloadMock, downloadRoot);
                Check(File.ReadAllBytes(downloaded.Path).SequenceEqual(UpdateTestHandler.Payload), "Updater downloads and validates checksum without launching installer");
                using var corruptMock = new HttpClient(new UpdateTestHandler(true));
                try { await Updates.Download(updateCandidate, new Progress<int>(), CancellationToken.None, corruptMock, downloadRoot); throw new Exception("Corrupt update accepted"); }
                catch (IOException) { count++; }
                Check(Directory.GetFiles(downloadRoot, "*.part", SearchOption.AllDirectories).Length == 0, "Failed download leaves no partial installer");
            } finally {
                if (Directory.Exists(downloadRoot)) {
                    foreach (var directory in Directory.GetDirectories(downloadRoot)) {
                        foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
                        Directory.Delete(directory);
                    }
                    Directory.Delete(downloadRoot);
                }
            }
            var diagnostics = new EngineDiagnostics();
            const string gap = "WARN aion_presence_engine::combat::capture_dispatcher: 1587 bytes from port 60728 never reached the capture (on loopback): reading on from the next whole packet";
            diagnostics.Receive(gap, 1000);
            Check(diagnostics.Message(15999) == EngineDiagnostics.CaptureGap, "Capture gap has a readable transient message");
            Check(diagnostics.Message(16000) == "", "Old warning expires without claiming stream recovery");
            diagnostics.Receive(gap, 20000);
            diagnostics.Receive(gap, 30000);
            Check(diagnostics.Message(40000) == EngineDiagnostics.CaptureGap, "Repeated warnings refresh their lifetime");
            diagnostics.Receive("ERROR capture could not start", 31000);
            diagnostics.Receive(gap, 32000);
            Check(diagnostics.Message(60000) == "ERROR capture could not start", "Warnings cannot replace or expire actual errors");
            diagnostics.Reset();
            Check(diagnostics.Message(60000) == "", "New engine session clears old diagnostics");
            UiLanguage.Language = "en";
            Check(UiLanguage.Text(EngineDiagnostics.CaptureGap).StartsWith("Gap in game data"), "Capture warning English translation");
            UiLanguage.Language = language;
            Check(Feedback.BugUrl("0.2.0-beta.2").EndsWith("template=bug_report.yml&version=0.2.0-beta.2"), "Bug form prefilled with app version");
            Check(!Feedback.BugUrl("0.2.0-beta.2").Contains("name="), "Feedback link contains no player identity");
            Check(ReleaseInfo.Resource("License").Contains("GNU GENERAL PUBLIC LICENSE"), "GPL text embedded in executable");
            Check(ReleaseInfo.Resource("ThirdParty").Contains("third-party software notices"), "Dependency notices embedded");
            Check(ReleaseInfo.ApplicationId.Length >= 17 && ReleaseInfo.ApplicationId.All(char.IsAsciiDigit), "Discord application identifier format");
            var c = new CharacterProfile("Kowal", "Gladiator", 1304, 50, 123450, 2050);
            var live = new LiveData(1, now, true, c, "Fighting Garshim", "Kaisinel", true);
            var prefs = new Preferences { MemberListDisplay = "stats" };
            var result = PresenceApp.ActivityFor(live, prefs, 123);
            Check(result["details"]!.GetValue<string>() == "Kowal · Gladiator · CP 123.45k · GS 2050", "Requested summary order");
            Check(result["status_display_type"]!.GetValue<int>() == 2, "Details selected for member list");
            Check(result["timestamps"]!["start"]!.GetValue<long>() == 123, "Session time retained");
            Check(result["state"]!.GetValue<string>() == "Fighting Garshim", "Profile second line contains only activity");
            prefs.CombatPower = false; prefs.Activity = false;
            Check(!PresenceApp.ActivityFor(live, prefs, 123)["details"]!.GetValue<string>().Contains("CP "), "Visibility setting CP");
            Check(!PresenceApp.ActivityFor(live, prefs, 123)["details"]!.GetValue<string>().Contains("Fighting"), "Visibility setting activity");
            Check(PresenceApp.ActivityFor(live, prefs, 123)["state"] is null, "Hidden activity is omitted from profile second line");
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
    private sealed class UpdateTestHandler(bool corrupt) : HttpMessageHandler
    {
        internal static readonly byte[] Payload = "isolated update fixture, never executed"u8.ToArray();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpContent content = request.RequestUri!.AbsolutePath.EndsWith("SHA256SUMS.txt")
                ? new StringContent((corrupt ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(Payload))) + "  Aion2Presence-Setup.exe\n")
                : new ByteArrayContent(Payload);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
