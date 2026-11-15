using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace AionPresence;

public partial class InstallWindow : System.Windows.Window, INotifyPropertyChanged
{
    private readonly bool preview;
    private bool installed, busy;
    private string? launchPath, failure;
    private string folder = Preferences.Load().InstallFolder ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AION2 Presence");
    public InstallWindow(bool preview = false)
    {
        this.preview = preview;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AionPresence.Logo")!;
        var logo = new BitmapImage(); logo.BeginInit(); logo.CacheOption = BitmapCacheOption.OnLoad;
        logo.StreamSource = stream; logo.EndInit(); logo.Freeze(); Logo = logo;
        InitializeComponent();
        DataContext = this; Icon = Logo; Title = SetupCaption;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private static string L(string pl, string en) => UiLanguage.Language == "pl" ? pl : en;
    private void Changed() { Title = SetupCaption; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); }
    public ImageSource Logo { get; }
    public string SetupCaption => L("Instalacja AION2 Presence", "AION2 Presence Setup");
    public string Tagline => L("Twoja postać i aktywność na Discordzie.", "Your character and activity on Discord.");
    public string LanguageCode => UiLanguage.Language == "pl" ? "PL" : "EN";
    public string LanguageLabel => L("Zmień język", "Change language");
    public ImageSource LanguageFlag => (ImageSource)FindResource(UiLanguage.Language == "pl" ? "FlagPL" : "FlagUK");
    public string VersionText => "v" + Forms.Application.ProductVersion.Split('+')[0];
    public string Heading => failure is not null ? L("Nie udało się zainstalować", "Installation unsuccessful") :
        installed ? L("Wszystko gotowe", "You're all set") : busy ? L("Instalowanie…", "Installing…") : L("Zainstaluj Presence", "Install Presence");
    public string StatusGlyph => failure is not null ? "!" : installed ? "✓" : "↓";
    public string Accent => failure is not null ? "#FFD18C" : installed ? "#69E6B3" : "#85E6EB";
    public string Description => failure is not null ? UiLanguage.Text(failure) : installed ?
        L("AION2 Presence jest gotowe do uruchomienia.\n\nSkróty znajdziesz na pulpicie i w menu Start.\nPobrany instalator możesz już usunąć.",
          "AION2 Presence is ready to launch.\n\nFind your shortcuts on the desktop and in the Start menu.\nYou can now delete the downloaded installer.") :
        L("Twoja postać, statystyki i aktywność w AION 2 na Discordzie.\n\nInstalacja dla Twojego konta Windows.\nUtworzymy skróty na pulpicie i w menu Start.",
          "Your AION 2 character, stats and activity on Discord.\n\nInstalls for your Windows account.\nAdds shortcuts to the desktop and Start menu.");
    public string FolderLabel => installed ? L("Zainstalowano w", "Installed in") : L("Folder instalacji", "Installation folder");
    public string BrowseText => L("Przeglądaj…", "Browse…");
    public string Footer => installed ? L("Uruchom grę i Discord desktop, a następnie Presence.", "Start the game and Discord desktop, then launch Presence.") :
        L("Wymagane: AION 2, Discord desktop i Npcap.", "Requires AION 2, Discord desktop and Npcap.");
    public string ActionText => installed ? L("Uruchom", "Launch") : busy ? L("Instalowanie…", "Installing…") : L("Zainstaluj", "Install");
    public bool CanAct => !busy;
    public bool CanEdit => !busy && !installed;
    public bool FolderReadOnly => !CanEdit;
    public string Folder { get => folder; set { folder = value; } }
    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        using var picker = new Forms.FolderBrowserDialog { InitialDirectory = Folder, ShowNewFolderButton = true };
        if (picker.ShowDialog(new DialogOwner(new System.Windows.Interop.WindowInteropHelper(this).Handle)) == Forms.DialogResult.OK) {
            Folder = picker.SelectedPath; Changed();
        }
    }
    private sealed class DialogOwner(IntPtr handle) : Forms.IWin32Window { public IntPtr Handle => handle; }
    private async void ActionClick(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (preview) { ShowInstalledPreview(); return; }
        if (installed && launchPath is not null) {
            try { Process.Start(new ProcessStartInfo(launchPath) { UseShellExecute = true }); Close(); }
            catch (Exception error) { System.Windows.MessageBox.Show(this, UiLanguage.Text(error.Message), SetupCaption); }
            return;
        }
        busy = true; failure = null; Changed();
        await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.Background);
        try {
            launchPath = InstallationService.Install(Folder);
            installed = true;
        } catch (Exception error) { failure = error.Message; }
        finally { busy = false; Changed(); }
    }
    private void SetLanguage(string language)
    {
        UiLanguage.Language = language;
        if (!preview) { var prefs = Preferences.Load(); prefs.Language = language; prefs.Save(); }
        Changed();
    }
    private void LanguageClick(object sender, RoutedEventArgs e)
    {
        var button = (System.Windows.Controls.Button)sender;
        button.ContextMenu.PlacementTarget = button; button.ContextMenu.IsOpen = true;
    }
    private void EnglishClick(object sender, RoutedEventArgs e) => SetLanguage("en");
    private void PolishClick(object sender, RoutedEventArgs e) => SetLanguage("pl");
    private void HeaderDrag(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = System.Windows.WindowState.Minimized;
    private void CloseClick(object sender, RoutedEventArgs e) { if (!busy) Close(); }
    internal void ShowInstalledPreview() { installed = true; Changed(); }
    internal void RenderLanguageMenuPreview(string path)
    {
        // Render-only preview uses the same popup as the interactive window.
        var button = FindLanguageButton(this);
        if (button is null) return;
        var menu = button.ContextMenu;
        menu.PlacementTarget = button; menu.IsOpen = true;
        menu.UpdateLayout(); Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var image = new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth), (int)Math.Ceiling(menu.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(menu); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path); encoder.Save(file); menu.IsOpen = false;
    }
    private static System.Windows.Controls.Button? FindLanguageButton(DependencyObject parent)
    {
        for (var i=0; i<VisualTreeHelper.GetChildrenCount(parent); i++) {
            var child = VisualTreeHelper.GetChild(parent,i);
            if (child is System.Windows.Controls.Button button && button.ContextMenu is not null) return button;
            var found = FindLanguageButton(child); if (found is not null) return found;
        }
        return null;
    }
    internal void RenderPreview(string path, double scale = 1)
    {
        UpdateLayout(); Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var image = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth*scale),(int)Math.Ceiling(ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);
        image.Render(this); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file=File.Create(path); encoder.Save(file);
    }
}

