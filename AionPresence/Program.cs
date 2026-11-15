namespace AionPresence;

internal static class Program
{
    internal static bool IsInstaller(string path) => Path.GetFileNameWithoutExtension(path)
        .StartsWith("Aion2Presence-Setup", StringComparison.OrdinalIgnoreCase);
    internal static bool IsDesignPreview(string path) => Path.GetFileNameWithoutExtension(path)
        .StartsWith("Aion2Presence-Preview", StringComparison.OrdinalIgnoreCase);
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
            if (args[0] == "--render-preview") {
                var main = new MainWindow(preview: true, renderOnly: true) { WindowStartupLocation = System.Windows.WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false };
                main.SetPreviewUpdate(args.Contains("--update-available"));
                if (args.Contains("--help-page")) main.ShowPage("help");
                if (args.Contains("--small-preview")) { main.Width = 960; main.Height = 720; }
                main.Show(); main.RenderPreview(args[1], args.Contains("--scale-150") ? 1.5 : 1); main.Close(); return;
            }
            using Form form = new InstallForm();
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-20000, -20000);
            form.Show(); Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(args[1]);
            return;
        }
        if (args.Contains("--ui-preview") || (IsDesignPreview(Environment.ProcessPath ?? "") && !args.Contains("--live-preview"))) {
            UiLanguage.Language = args.Contains("--polish") ? "pl" : "en";
            var preview = new MainWindow(preview: true);
            preview.SetPreviewUpdate(args.Contains("--update-available"));
            new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose }.Run(preview);
            return;
        }
        using var showWindow = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Aion2Presence-Show-01", out var newSignal);
        using var mutex = new Mutex(true, @"Local\Aion2Presence-Standalone-01", out var first);
        if (!first) {
            if (!newSignal) showWindow.Set();
            else MessageBox.Show(UiLanguage.Text("AION2 Presence już działa — otwórz go z ikony obok zegarka."), "AION2 Presence");
            return;
        }
        var window = new MainWindow(startTray: args.Contains("--tray"), showWindow: showWindow);
        var desktop = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose };
        desktop.SessionEnding += (_, _) => window.RequestQuit();
        desktop.Run(window);
        // The installer must see the old instance as fully stopped.
        mutex.ReleaseMutex();
        Updates.LaunchPending();
    }
}
