using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;

namespace AionPresence;

public static class EngineBinary
{
    public static string Extract()
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("AionPresence.Engine");
        if (resource is null) {
            var beside = Path.Combine(AppContext.BaseDirectory, "aion-presence-engine.exe");
            if (File.Exists(beside)) return beside;
            throw new FileNotFoundException("Brak wbudowanego silnika odczytu. Pobierz pełną paczkę wydania.");
        }
        var hash = Convert.ToHexString(SHA256.HashData(resource)).ToLowerInvariant();
        var directory = Path.Combine(Preferences.Folder, "engine", hash[..16]);
        var file = Path.Combine(directory, "aion-presence-engine.exe");
        Directory.CreateDirectory(directory);
        if (File.Exists(file)) {
            using var existing = File.OpenRead(file);
            if (Convert.ToHexString(SHA256.HashData(existing)).Equals(hash, StringComparison.OrdinalIgnoreCase)) return file;
        }
        resource.Position = 0;
        var temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) resource.CopyTo(output);
            File.Move(temp, file, true);
        } finally { if (File.Exists(temp)) File.Delete(temp); }
        return file;
    }
}

public sealed class InstallForm : Form
{
    private readonly Label info;
    private readonly Button install;
    private readonly TextBox installFolder;
    private readonly Button browse;
    public InstallForm()
    {
        Text = "Instalacja AION2 Presence"; ClientSize = new Size(580, 390);
        Icon = AppBrand.LoadIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(14, 19, 29); ForeColor = Color.FromArgb(225, 233, 245); Font = new Font("Segoe UI", 10);
        var title = new Label { Text = "AION2 PRESENCE", Font = new Font("Segoe UI", 19, FontStyle.Bold), AutoSize = true, Location = new Point(24, 24) };
        info = new Label { Text = "Twoja postać i aktywność w AION 2 na Discordzie.\n\nInstalacja dla bieżącego użytkownika Windows\noraz skróty na pulpicie i w menu Start.\n\nWymagane są AION 2, Discord desktop i Npcap.", Location = new Point(26, 76), Size = new Size(480, 130) };
        install = new Button { Text = "Zainstaluj", Location = new Point(406, 328), Size = new Size(148, 36), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(31, 47, 70), ForeColor = ForeColor };
        install.Click += (sender, e) => { if (installed) OnLaunch(sender, e); else Install(); };
        Controls.AddRange([title, info, install]);
        var language = UiLanguage.Selector(this);
        language.Location = new Point(434, 28);
        Controls.Add(language);
        Controls.Add(new Label { Text = "Folder instalacji", Location = new Point(26, 218), Size = new Size(510, 24) });
        installFolder = new TextBox { Text = Preferences.Load().InstallFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AION2 Presence"), Location = new Point(26, 246), Size = new Size(388, 28) };
        browse = new Button { Text = "Przeglądaj…", Location = new Point(426, 244), Size = new Size(128, 32) };
        browse.Click += (_, _) => {
            using var picker = new FolderBrowserDialog { InitialDirectory = installFolder.Text, ShowNewFolderButton = true };
            if (picker.ShowDialog(this) == DialogResult.OK) installFolder.Text = picker.SelectedPath;
        };
        Controls.AddRange([installFolder, browse]);
        UiLanguage.Apply(this);
    }
    private void Install()
    {
        try {
            install.Enabled = false;
            using (var instance = new Mutex(false, @"Local\Aion2Presence-Standalone-01")) {
                var available = false;
                try { available = instance.WaitOne(0); }
                catch (AbandonedMutexException) { available = true; }
                if (!available) throw new IOException("AION2 Presence nadal działa. Przy zegarku kliknij jego ikonę prawym przyciskiem i wybierz Zakończ, następnie ponów instalację. Zamknięcie okna krzyżykiem tylko ukrywa aplikację.");
                instance.ReleaseMutex();
            }
            var source = Environment.ProcessPath ?? throw new IOException("Nie znaleziono uruchomionego pliku.");
            var folder = Path.GetFullPath(installFolder.Text.Trim());
            if (!Path.IsPathFullyQualified(installFolder.Text.Trim())) throw new IOException(UiLanguage.Text("Wybierz pełną ścieżkę folderu instalacji."));
            Directory.CreateDirectory(folder);
            var destination = Path.Combine(folder, "Aion2Presence.exe");
            if (!Path.GetFullPath(source).Equals(destination, StringComparison.OrdinalIgnoreCase)) File.Copy(source, destination, true);
            ReleaseInfo.WriteNotices(folder);
            var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new IOException("Nie można utworzyć skrótu Windows.");
            dynamic shell = Activator.CreateInstance(shellType)!;
            try {
                foreach (var location in new[] { Environment.SpecialFolder.Programs, Environment.SpecialFolder.DesktopDirectory }) {
                    var shortcutFolder = Environment.GetFolderPath(location);
                    Directory.CreateDirectory(shortcutFolder);
                    dynamic shortcut = shell.CreateShortcut(Path.Combine(shortcutFolder, "AION2 Presence.lnk"));
                    try {
                        shortcut.TargetPath = destination; shortcut.WorkingDirectory = folder;
                        shortcut.Description = "AION 2 Discord Presence";
                        shortcut.IconLocation = destination + ",0";
                        shortcut.Save();
                    } finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut); }
                }
            } finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
            using (var startup = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
                if (startup?.GetValue("Aion2Presence") is not null)
                    startup.SetValue("Aion2Presence", $"\"{destination}\" --tray");
            }
            var preferences = Preferences.Load(); preferences.InstallFolder = folder; preferences.Language = UiLanguage.Language; preferences.Save();
            installFolder.Text = destination; installFolder.ReadOnly = true;
            browse.Enabled = false;
            info.Text = "Zainstalowano.\n\nSkróty AION2 Presence są na pulpicie i w menu Start.\nMożesz usunąć pobrany instalator.\n\nKliknij Uruchom, aby rozpocząć.";
            install.Text = "Uruchom"; install.Enabled = true;
            // Change behavior only after installation succeeded.
            launchPath = destination;
            installed = true;
        } catch (Exception e) { info.Text = UiLanguage.Text("Instalacja nie powiodła się:\n") + UiLanguage.Text(e.Message); install.Enabled = true; }
        UiLanguage.Apply(this);
    }
    private string? launchPath;
    private bool installed;
    private void OnLaunch(object? sender, EventArgs e)
    {
        if (installed && launchPath is not null) { Process.Start(new ProcessStartInfo(launchPath) { UseShellExecute = true }); Close(); }
    }
}
