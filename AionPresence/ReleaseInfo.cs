using System.Reflection;
using System.Text.Json;

namespace AionPresence;

public static class ReleaseInfo
{
    public const string Repository = "https://github.com/Kooowal-design/aion2-presence";
    public static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AionPresence." + name)
            ?? throw new IOException("Missing release resource: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    public static readonly JsonDocument Identity = JsonDocument.Parse(Resource("DiscordIdentity"));
    public static string ApplicationId => Identity.RootElement.GetProperty("applicationId").GetString()!;
    public static bool ProjectOwned => Identity.RootElement.GetProperty("projectOwned").GetBoolean();
    public static string ImageFor(string key) => Identity.RootElement.TryGetProperty("imageUrls", out var images)
        && images.TryGetProperty(key, out var image) ? image.GetString()! : key;
    public static string SourceUrl {
        get {
            using var info = JsonDocument.Parse(Resource("ReleaseInfo"));
            var revision = info.RootElement.GetProperty("revision").GetString();
            return revision is { Length: 40 } && revision.All(Uri.IsHexDigit) ? Repository + "/tree/" + revision : Repository;
        }
    }
    public static void WriteNotices(string folder)
    {
        File.WriteAllText(Path.Combine(folder, "LICENSE.txt"), Resource("License"));
        File.WriteAllText(Path.Combine(folder, "NOTICE.md"), Resource("Notice"));
        File.WriteAllText(Path.Combine(folder, "THIRD-PARTY-NOTICES.txt"), Resource("ThirdParty"));
        File.WriteAllText(Path.Combine(folder, "SOURCE.txt"), SourceUrl + "\nGPL-3.0; corresponding source archive is provided with the matching release.\n");
        File.WriteAllText(Path.Combine(folder, "release-info.json"), Resource("ReleaseInfo"));
    }
    public static void ShowLicenses(IWin32Window? owner = null)
    {
        using var form = new Form { Text = UiLanguage.Text("Licencje i źródła"), Width = 760, Height = 600, StartPosition = FormStartPosition.CenterParent, Icon = AppBrand.LoadIcon() };
        var tabs = new TabControl { Dock = DockStyle.Fill };
        foreach (var entry in new[] { ("NOTICE", "Notice"), ("GPL-3.0", "License"), ("Third-party", "ThirdParty") }) {
            var tab = new TabPage(entry.Item1);
            tab.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Text = Resource(entry.Item2).ReplaceLineEndings(Environment.NewLine) });
            tabs.TabPages.Add(tab);
        }
        var source = new LinkLabel { Text = UiLanguage.Text("Kod źródłowy tego wydania"), Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(10) };
        source.LinkClicked += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(SourceUrl) { UseShellExecute = true });
        form.Controls.Add(tabs); form.Controls.Add(source);
        form.ShowDialog(owner);
    }
}
