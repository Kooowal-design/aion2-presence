using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AionPresence;

public sealed class PresenceApp : IAsyncDisposable
{
    // Maintainer-owned Discord identity, embedded at build time.
    // This is an application identifier, never an account token or secret.
    public static string ApplicationId => ReleaseInfo.ApplicationId;
    private readonly Func<Preferences> preferences;
    private readonly CancellationTokenSource stop = new();
    private Process? engine;
    private Task? runner;
    private Task? reader;
    private Task? errors;
    private LiveData? data;
    private long sessionSince;
    private string? identity;
    private string lastError = "";
    private long lastDataAt;
    public string Status { get; private set; } = "Uruchamiam…";
    public string CaptureStatus { get; private set; } = "Czekam na silnik";
    public string Preview { get; private set; } = "";
    public string ProfilePreview { get; private set; } = "";
    public string LastError => lastError;
    public bool Accepted { get; private set; }
    public LiveData? Data => Volatile.Read(ref data);
    public PresenceApp(Func<Preferences> preferences) { this.preferences = preferences; }
    public void Start() { runner ??= Run(stop.Token); }
    public static JsonObject Command(JsonObject? activity, long? pid = null) => new() {
        ["cmd"] = "SET_ACTIVITY", ["args"] = new JsonObject {
            ["pid"] = pid ?? Environment.ProcessId, ["activity"] = activity?.DeepClone() },
        ["nonce"] = Guid.NewGuid().ToString("N")
    };
    public static JsonObject ActivityFor(LiveData d, Preferences preferences, long since)
    {
        var c = d.Character!;
        var cls = PresenceFormat.ClassName(c, new JsonObject());
        var key = cls.ToLowerInvariant();
        var state = d.Activity;
        var artwork = new JsonObject();
        if (key.Length > 0) artwork["large_image"] = ReleaseInfo.ImageFor(key);
        var original = new JsonObject {
            ["state"] = PresenceFormat.Limit(PresenceFormat.Clean(state)),
            ["assets"] = artwork,
            ["timestamps"] = new JsonObject { ["start"] = since }
        };
        var result = PresenceFormat.Build(original, d, preferences);
        // Keep the profile's second line focused on level and activity.
        var full = new List<string>();
        if (c.Level > 0) full.Add($"Lv {c.Level}");
        if (preferences.Activity && !string.IsNullOrWhiteSpace(d.Activity)) full.Add(PresenceFormat.Clean(d.Activity));
        result["state"] = PresenceFormat.Limit(full.Count > 0 ? string.Join(" · ", full) : "AION 2");
        return result;
    }
    private async Task StartEngine(CancellationToken ct)
    {
        var path = EngineBinary.Extract();
        var start = new ProcessStartInfo(path) {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8,
            WorkingDirectory = AppContext.BaseDirectory
        };
        engine = Process.Start(start) ?? throw new IOException("Nie udało się uruchomić silnika odczytu.");
        var current = engine;
        reader = Task.Run(async () => {
            while (!ct.IsCancellationRequested) {
                var line = await current.StandardOutput.ReadLineAsync(ct);
                if (line is null) break;
                if (line.Length > 32768) continue;
                try {
                    var next = JsonSerializer.Deserialize<LiveData>(line, Preferences.Json);
                    if (next is { Schema: 1 }) { Volatile.Write(ref data, next); Interlocked.Exchange(ref lastDataAt, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); }
                } catch (JsonException) { lastError = "Silnik przesłał nieprawidłowy stan."; }
            }
        }, ct);
        errors = Task.Run(async () => {
            while (!ct.IsCancellationRequested) {
                var line = await current.StandardError.ReadLineAsync(ct);
                if (line is null) break;
                if (line.Length > 0) lastError = PresenceFormat.Limit(line, 500);
            }
        }, ct);
        await Task.CompletedTask;
    }
    private async Task Run(CancellationToken ct)
    {
        DiscordPipe? discord = null;
        string? sent = null;
        var lastSent = DateTimeOffset.MinValue;
        var retryDiscord = DateTimeOffset.MinValue;
        var retryEngine = DateTimeOffset.MinValue;
        try {
            while (!ct.IsCancellationRequested) {
                var now = DateTimeOffset.UtcNow;
                if ((engine is null || engine.HasExited) && now >= retryEngine) {
                    if (reader is not null) { try { await reader; } catch (OperationCanceledException) { } }
                    if (errors is not null) { try { await errors; } catch (OperationCanceledException) { } }
                    engine?.Dispose(); engine = null;
                    Volatile.Write(ref data, null);
                    try { await StartEngine(ct); } catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception) { lastError = e.Message; }
                    retryEngine = now.AddSeconds(10);
                }
                var d = Data;
                var fresh = d?.Fresh(now.ToUnixTimeMilliseconds()) == true && now.ToUnixTimeMilliseconds() - Interlocked.Read(ref lastDataAt) < 8000;
                CaptureStatus = !fresh ? "Silnik nie odpowiada" : !d!.CaptureOk ? "Brak odczytu Npcap — sprawdź komunikat poniżej" :
                    !d.Running ? "Czekam na AION 2" : d.Character is null ? "Odczyt działa. Czekam na dane postaci z gry…" :
                    $"Postać rozpoznana · {(d.Device?.Contains("loopback", StringComparison.OrdinalIgnoreCase) == true ? "Loopback / tunel" : "Połączenie gry")}{(d.Port is not null ? " · port " + d.Port : "")}";
                var canShow = fresh && d!.Running && d.Character is { Name.Length: > 0 };
                JsonObject? payload = null;
                if (canShow) {
                    var currentIdentity = $"{d!.Character!.Name}:{d.Character.ServerId}";
                    if (identity != currentIdentity) { sessionSince = now.ToUnixTimeSeconds(); identity = currentIdentity; }
                    payload = ActivityFor(d, preferences(), sessionSince);
                    Preview = payload["details"]?.GetValue<string>() ?? "";
                    ProfilePreview = payload["state"]?.GetValue<string>() ?? "";
                } else { identity = null; Preview = ""; ProfilePreview = ""; }
                if (discord is null && now >= retryDiscord) {
                    try { discord = await DiscordPipe.Connect("", ct); sent = null; }
                    catch (IOException e) { Status = e.Message; retryDiscord = now.AddSeconds(10); }
                }
                var json = payload?.ToJsonString() ?? "null";
                if (discord is not null && json != sent && ((now - lastSent).TotalSeconds >= 5 || payload is null)) {
                    try {
                        var response = await discord.Set(Command(payload), ct);
                        sent = json; lastSent = now; Accepted = payload is not null;
                        var echoed = response["data"]?["status_display_type"]?.GetValue<int>() == 2;
                        Status = payload is null ? "Discord połączony · status gry wyłączony" : echoed ?
                            "Discord potwierdził wyświetlanie szczegółów na liście" :
                            "Discord przyjął status · wygląd listy zależy od klienta";
                    }
                    catch (Exception e) when (e is IOException or OperationCanceledException or InvalidOperationException) {
                        if (ct.IsCancellationRequested) break;
                        Status = e is OperationCanceledException ? "Discord nie odpowiedział — ponawiam" : e.Message;
                        await discord.DisposeAsync(); discord = null; Accepted = false; retryDiscord = now.AddSeconds(5);
                    }
                }
                await Task.Delay(1000, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { lastError = e.Message; Status = "Zatrzymano z błędem"; }
        finally {
            Accepted = false;
            if (discord is not null) {
                try { using var timeout = new CancellationTokenSource(1500); await discord.Set(Command(null), timeout.Token); } catch { }
                await discord.DisposeAsync();
            }
            if (engine is not null) {
                try { if (!engine.HasExited) engine.Kill(true); await engine.WaitForExitAsync(); } catch { }
            }
            if (reader is not null) { try { await reader; } catch (OperationCanceledException) { } catch (IOException) { } }
            if (errors is not null) { try { await errors; } catch (OperationCanceledException) { } catch (IOException) { } }
            engine?.Dispose();
        }
    }
    public async ValueTask DisposeAsync() { stop.Cancel(); if (runner is not null) await runner; stop.Dispose(); }
}
