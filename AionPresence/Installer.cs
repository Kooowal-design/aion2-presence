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

public static class InstallationService
{
    public static string Install(string requestedFolder)
    {
            using (var instance = new Mutex(false, @"Local\Aion2Presence-Standalone-01")) {
                var available = false;
                try { available = instance.WaitOne(0); }
                catch (AbandonedMutexException) { available = true; }
                if (!available) throw new IOException("AION2 Presence nadal działa. Przy zegarku kliknij jego ikonę prawym przyciskiem i wybierz Zakończ, następnie ponów instalację. Zamknięcie okna krzyżykiem tylko ukrywa aplikację.");
                instance.ReleaseMutex();
            }
            var source = Environment.ProcessPath ?? throw new IOException("Nie znaleziono uruchomionego pliku.");
            var folder = Path.GetFullPath(requestedFolder.Trim());
            if (!Path.IsPathFullyQualified(requestedFolder.Trim())) throw new IOException(UiLanguage.Text("Wybierz pełną ścieżkę folderu instalacji."));
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
            return destination;
    }
}
