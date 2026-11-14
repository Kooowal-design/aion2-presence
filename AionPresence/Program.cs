namespace AionPresence;

internal static class Program
{
    internal static bool IsInstaller(string path) => Path.GetFileNameWithoutExtension(path)
        .StartsWith("Aion2Presence-Setup", StringComparison.OrdinalIgnoreCase);
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && args[0] == "--write-notices") {
            ReleaseInfo.WriteNotices(Path.GetFullPath(args[1]));
            return;
        }
        if (args.Contains("--install") || IsInstaller(Environment.ProcessPath ?? "")) {
            Application.Run(new InstallForm());
            return;
        }
        if (args.Contains("--self-test")) {
            var result = SelfTests.Run().GetAwaiter().GetResult();
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test-result.txt"), result);
            Environment.Exit(result.StartsWith("PASS") ? 0 : 1);
            return;
        }
        if (args.Length >= 2 && (args[0] == "--render-preview" || args[0] == "--render-setup-preview")) {
            UiLanguage.Language = args.Contains("--polish") ? "pl" : "en";
            using Form form = args[0] == "--render-preview" ? new MainForm(true) : new InstallForm();
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-20000, -20000);
            form.Show(); Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(args[1]);
            return;
        }
        using var showWindow = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Aion2Presence-Show-01", out var newSignal);
        using var mutex = new Mutex(true, @"Local\Aion2Presence-Standalone-01", out var first);
        if (!first) {
            if (!newSignal) showWindow.Set();
            else MessageBox.Show(UiLanguage.Text("AION2 Presence już działa — otwórz go z ikony obok zegarka."), "AION2 Presence");
            return;
        }
        Application.Run(new MainForm(false, args.Contains("--tray"), showWindow));
    }
}
