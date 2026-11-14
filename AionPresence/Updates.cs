using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace AionPresence;

public sealed record AppUpdate(string Version, Uri Installer, Uri Checksums);
internal sealed record PreparedUpdate(string Path, string Hash);

public static class Updates
{
    public const string Api = "https://api.github.com/repos/Kooowal-design/aion2-presence/releases?per_page=30";
    private static readonly HttpClient Client = CreateClient();
    private static (string Path, string Hash)? pending;
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Aion2Presence-Updater");
        return client;
    }
    public static bool TryVersion(string text, out Version number, out string[] pre)
    {
        var parts = text.TrimStart('v').Split('+')[0].Split('-', 2);
        pre = parts.Length > 1 ? parts[1].Split('.') : [];
        number = new Version(0, 0, 0);
        return parts[0].Split('.').Length == 3 && Version.TryParse(parts[0], out number!)
            && (parts.Length == 1 || pre.All(p => p.Length > 0 && p.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')));
    }
    public static int Compare(string left, string right)
    {
        if (!TryVersion(left, out var a, out var ap) || !TryVersion(right, out var b, out var bp)) throw new FormatException("Invalid release version");
        var result = a.CompareTo(b); if (result != 0) return result;
        if (ap.Length == 0 || bp.Length == 0) return ap.Length == bp.Length ? 0 : ap.Length == 0 ? 1 : -1;
        for (var i = 0; i < Math.Min(ap.Length, bp.Length); i++) {
            var an = ulong.TryParse(ap[i], out var av); var bn = ulong.TryParse(bp[i], out var bv);
            result = an && bn ? av.CompareTo(bv) : an != bn ? an ? -1 : 1 : string.CompareOrdinal(ap[i], bp[i]);
            if (result != 0) return result;
        }
        return ap.Length.CompareTo(bp.Length);
    }
    public static bool AssetUrl(string text, string version, string file, out Uri uri)
    {
        uri = null!;
        return Uri.TryCreate(text, UriKind.Absolute, out uri!) && uri.Scheme == "https" && uri.Host == "github.com"
            && uri.Port == 443 && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0
            && uri.AbsolutePath == $"/Kooowal-design/aion2-presence/releases/download/v{version}/{file}";
    }
    public static AppUpdate? Select(string json, string current)
    {
        using var doc = JsonDocument.Parse(json);
        AppUpdate? newest = null;
        var includeBeta = current.Contains('-');
        foreach (var release in doc.RootElement.EnumerateArray()) {
            if (release.GetProperty("draft").GetBoolean()) continue;
            var version = release.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "";
            if (!TryVersion(version, out _, out var pre) || (!includeBeta && pre.Length > 0)
                || Compare(version, current) <= 0 || (newest is not null && Compare(version, newest.Version) <= 0)) continue;
            string? installer = null, checksums = null;
            foreach (var asset in release.GetProperty("assets").EnumerateArray()) {
                var name = asset.GetProperty("name").GetString(); var url = asset.GetProperty("browser_download_url").GetString();
                if (name == "Aion2Presence-Setup.exe") installer = url;
                if (name == "SHA256SUMS.txt") checksums = url;
            }
            if (AssetUrl(installer ?? "", version, "Aion2Presence-Setup.exe", out var setup)
                && AssetUrl(checksums ?? "", version, "SHA256SUMS.txt", out var sums)) newest = new(version, setup, sums);
        }
        return newest;
    }
    public static async Task<AppUpdate?> Check(string current, CancellationToken ct)
    {
        // No character data, settings, account identifiers or logs are sent.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        using var response = await Client.GetAsync(Api, deadline.Token); response.EnsureSuccessStatusCode();
        return Select(await response.Content.ReadAsStringAsync(deadline.Token), current);
    }
    public static string ExpectedHash(string sums)
    {
        if (sums.Length > 65536) throw new IOException("Invalid checksum file");
        foreach (var line in sums.Split('\n')) {
            var fields = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 2 && fields[1] == "Aion2Presence-Setup.exe" && fields[0].Length == 64 && fields[0].All(Uri.IsHexDigit)) return fields[0];
        }
        throw new IOException("Missing installer checksum");
    }
    internal static async Task<PreparedUpdate> Download(AppUpdate update, IProgress<int> progress, CancellationToken ct, HttpClient? testClient = null, string? testRoot = null)
    {
        var client = testClient ?? Client;
        if (!AssetUrl(update.Installer.AbsoluteUri, update.Version, "Aion2Presence-Setup.exe", out _)
            || !AssetUrl(update.Checksums.AbsoluteUri, update.Version, "SHA256SUMS.txt", out _)) throw new IOException("Invalid update source");
        var expected = ExpectedHash(await client.GetStringAsync(update.Checksums, ct));
        var folder = Path.Combine(testRoot ?? Path.Combine(Preferences.Folder, "updates"), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var part = Path.Combine(folder, "download.part"); var setup = Path.Combine(folder, "Aion2Presence-Setup.exe");
        try {
            using var response = await client.GetAsync(update.Installer, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            const long limit = 150 * 1024 * 1024;
            var size = response.Content.Headers.ContentLength;
            if (size > limit) throw new IOException("Update is too large");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var input = await response.Content.ReadAsStreamAsync(ct))
            await using (var output = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true)) {
                var buffer = new byte[65536]; long total = 0; int count;
                while ((count = await input.ReadAsync(buffer, ct)) > 0) {
                    total += count; if (total > limit) throw new IOException("Update is too large");
                    await output.WriteAsync(buffer.AsMemory(0, count), ct); hash.AppendData(buffer, 0, count);
                    if (size > 0) progress.Report((int)Math.Min(99, total * 100 / size.Value));
                }
            }
            if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Installer checksum mismatch");
            File.Move(part, setup);
            progress.Report(100);
            return new(setup, expected);
        } catch { if (File.Exists(part)) File.Delete(part); throw; }
    }
    internal static void InstallOnExit(PreparedUpdate update) => pending = (update.Path, update.Hash);
    public static void LaunchPending()
    {
        if (pending is not { } update) return;
        try {
            using (var file = File.OpenRead(update.Path))
                if (!Convert.ToHexString(SHA256.HashData(file)).Equals(update.Hash, StringComparison.OrdinalIgnoreCase)) throw new IOException("Installer checksum mismatch");
            Process.Start(new ProcessStartInfo(update.Path) { UseShellExecute = true, Arguments = "--install" });
        } catch { MessageBox.Show(UiLanguage.Text("Nie udało się uruchomić aktualizacji. Pobierz instalator z GitHub Releases."), "AION2 Presence"); }
    }
}
