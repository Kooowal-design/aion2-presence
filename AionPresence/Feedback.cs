using System.Diagnostics;

namespace AionPresence;

public static class Feedback
{
    public static string BugUrl(string version) => ReleaseInfo.Repository + "/issues/new?template=bug_report.yml&version=" + Uri.EscapeDataString(version);
    public static string FeatureUrl => ReleaseInfo.Repository + "/issues/new?template=feature_request.yml";
    public static void OpenBug() => Open(BugUrl(Application.ProductVersion.Split('+')[0]));
    public static void OpenFeature() => Open(FeatureUrl);
    private static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception e) { MessageBox.Show(UiLanguage.Text("Nie udało się otworzyć przeglądarki.\n") + e.Message, "AION2 Presence"); }
    }
}
