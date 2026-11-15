using System.ComponentModel;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using WpfMessageBox = System.Windows.MessageBox;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using Brush = System.Windows.Media.Brush;

namespace AionPresence;

public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
{
    private readonly Preferences prefs = Preferences.Load();
    private readonly bool preview, renderOnly;
    private readonly bool startTray;
    private readonly EventWaitHandle? showWindow;
    private readonly CancellationTokenSource stop = new();
    private readonly DispatcherTimer timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(800) };
    private PresenceApp? app;
    private Forms.NotifyIcon? tray;
    private string selectedPage = "home";
    private bool paused, quitting, closing, updateBusy;
    private AppUpdate? update;
    private string updateMessage = "Sprawdź aktualizacje";
    private DateTimeOffset nextCheck;
    private string? notifiedVersion;
    private int percent;
    private string characterName = "—", characterClass = "—", level = "—", cp = "—", gs = "—", activity = "";
    private string summary = "", profile = "", captureDetails = "", discordDetails = "", error = "";
    private static readonly Brush Green = FrozenBrush(65, 221, 155), Cyan = FrozenBrush(71, 209, 230), Muted = FrozenBrush(157, 185, 205), Gold = FrozenBrush(245, 214, 143), Ink = FrozenBrush(238, 245, 251);

    public MainWindow(bool preview = false, bool startTray = false, EventWaitHandle? showWindow = null, bool renderOnly = false)
    {
        this.preview = preview; this.startTray = startTray; this.showWindow = showWindow; this.renderOnly = renderOnly;
        Logo = LoadLogo();
        InitializeComponent();
        Icon = Logo; DataContext = this;
        Title = "AION2 Presence · " + VersionText + (preview ? " · Local preview" : "");
        if (preview) { RefreshDemo(); SetPreviewUpdate(false); }
        Loaded += OnLoaded; Closing += OnClosing;
        Closed += (_, _) => { timer.Stop(); stop.Cancel(); tray?.Dispose(); stop.Dispose(); };
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private static Brush FrozenBrush(byte r, byte g, byte b) { var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b)); brush.Freeze(); return brush; }
    private static BitmapSource LoadLogo()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AionPresence.Logo")!;
        var logo = new BitmapImage(); logo.BeginInit(); logo.CacheOption = BitmapCacheOption.OnLoad; logo.StreamSource = stream; logo.EndInit(); logo.Freeze(); return logo;
    }
    private static string L(string text) => UiLanguage.Text(text);
    private void Changed()
    {
        if (Dispatcher.HasShutdownStarted) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(Changed); return; }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
    public BitmapSource Logo { get; }
    public bool IsPreview => preview;
    public string VersionText => "v" + Forms.Application.ProductVersion.Split('+')[0];
    public string LanguageCode => UiLanguage.Language == "pl" ? "PL" : "EN";
    public string HomeText => L("Start");
    public string HelpText => IsHelp ? L("Start") : L("Pomoc");
    public string PreviewLabel => L("PODGLĄD LOKALNY\nFikcyjne dane");
    public string PreviewStateText => L("Stan aktualizacji");
    public string CharacterText => L("Postać");
    public string ClassText => L("Klasa");
    public string ActivityOptionText => L("Aktywność");
    public string ActivityHeading => L("AKTUALNA AKTYWNOŚĆ");
    public string DisplayHeading => L("CO POKAZAĆ NA LIŚCIE");
    public string DiscordPreviewHeading => L("PODGLĄD DISCORDA");
    public string HelpHeading => L("PIERWSZE URUCHOMIENIE");
    public string ConnectionsHeading => L("POŁĄCZENIA");
    public string ReportText => L("Zgłoś błąd");
    public string SuggestText => L("Zaproponuj funkcję");
    public string LicensesText => L("Licencje i źródła");
    public string StartupText => L("Start z Windows");
    public string HideText => L("Ukryj do zasobnika");
    public string PauseText => L(paused ? "Uruchom" : "Wstrzymaj");
    public string HelpDescription => L("Uruchom AION 2 i Discord desktop na tym samym komputerze.\nDo odczytu wymagany jest Npcap. ExitLag może pozostać włączony.\n\nJeśli gra zgłasza błąd przy starcie, włącz najpierw grę, potem Presence.\nBrak danych postaci? Poczekaj na wejście do świata lub teleport.\nNazwy dungeonów wymagają odebrania komunikatu mapy.");
    public bool IsHome => selectedPage == "home";
    public bool IsHelp => selectedPage == "help";
    internal string SelectedPage => selectedPage;
    public string CharacterNameText => characterName;
    public string CharacterClassText => characterClass;
    public string LevelText => level;
    public string CpText => cp;
    public string GsText => gs;
    public string ActivityText => activity.Length > 0 ? activity : L("Czekam na dane postaci…");
    public string DiscordSummary => summary.Length > 0 ? summary : L("Czekam na dane postaci…");
    public string DiscordProfile => profile;
    public string CaptureDetails => L(captureDetails);
    public string DiscordDetails => L(discordDetails);
    public string ErrorText => L(error);
    public bool HasError => error.Length > 0;
    public string CaptureText => L(paused ? "Odczyt zatrzymany" : preview ? "Gra wykryta" : app?.SourceFresh != true ? "Czekam na silnik" : app.Data?.Running == true ? app.Data.CaptureOk ? "Gra wykryta" : "Odczyt niedostępny" : "Czekam na AION 2");
    public string DiscordText => L(preview ? "Podgląd lokalny" : app?.DiscordConnected == true ? "Discord połączony" : "Czekam na Discorda desktop…");
    public Brush CaptureBrush => !paused && (preview || app?.SourceFresh == true && app.Data?.Running == true && app.Data.CaptureOk) ? Green : Muted;
    public Brush DiscordBrush => preview ? Cyan : app?.DiscordConnected == true ? Green : Muted;
    public bool ShowName { get => prefs.Name; set { prefs.Name = value; SettingsChanged(); } }
    public bool ShowClass { get => prefs.Class; set { prefs.Class = value; SettingsChanged(); } }
    public bool ShowCp { get => prefs.CombatPower; set { prefs.CombatPower = value; SettingsChanged(); } }
    public bool ShowGs { get => prefs.GearScore; set { prefs.GearScore = value; SettingsChanged(); } }
    public bool ShowActivity { get => prefs.Activity; set { prefs.Activity = value; SettingsChanged(); } }
    public bool ShowLevel { get => prefs.Level; set { prefs.Level = value; SettingsChanged(); } }
    public bool AutoStart {
        get => prefs.AutoStart;
        set {
            if (value == prefs.AutoStart) return;
            try {
                if (!preview) {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                    if (value) key.SetValue("Aion2Presence", $"\"{Environment.ProcessPath}\" --tray"); else key.DeleteValue("Aion2Presence", false);
                }
                prefs.AutoStart = value; SettingsChanged();
            } catch (Exception e) { error = e.Message; Changed(); }
        }
    }
    private void SettingsChanged()
    {
        if (preview) RefreshDemo();
        else try { prefs.Language = UiLanguage.Language; prefs.Save(); } catch (IOException e) { error = e.Message; }
        Changed();
    }
    public bool IsDownloading => updateBusy && updateMessage == "Pobieram aktualizację…";
    public bool IsGoldUpdate => update is not null || IsDownloading;
    public bool IsCurrent => update is null && !updateBusy && updateMessage.StartsWith("Masz najnowszą", StringComparison.Ordinal);
    public bool UpdateEnabled => !updateBusy;
    public double DownloadPercent => percent;
    public Brush UpdateAccent => IsGoldUpdate ? Gold : IsCurrent ? Green : Cyan;
    public Brush UpdateTitleBrush => IsGoldUpdate ? Gold : Ink;
    public string UpdateTitle => L(IsDownloading ? "Pobieram aktualizację…" : updateBusy ? "Sprawdzam aktualizacje…" : update is not null ? "Dostępna aktualizacja" : IsCurrent ? "Aktualna wersja" : updateMessage.StartsWith("Nie udało", StringComparison.Ordinal) ? "Nie można sprawdzić wersji" : "Aktualizacje aplikacji");
    public string UpdateDetail => IsDownloading ? L("Sprawdzę plik przed uruchomieniem instalatora.") : update is not null ? "v" + update.Version + "  ·  " + L("Zainstalowana: ") + VersionText : IsCurrent ? L("Korzystasz z najnowszej wersji.") + "  ·  " + VersionText : L("Bieżąca wersja: ") + VersionText;
    public string UpdateActionText => L(IsDownloading ? "Pobieram aktualizację…" : updateBusy ? "Sprawdzam aktualizacje…" : update is not null ? "Pobierz i zainstaluj" : IsCurrent ? "Sprawdź ponownie" : "Sprawdź aktualizacje") + (IsDownloading ? " " + percent + "%" : "");
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!renderOnly) {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            var scale = source.CompositionTarget.TransformToDevice;
            var work = Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
            var w = work.Width / scale.M11 - 20; var h = work.Height / scale.M22 - 20;
            MinWidth = Math.Min(MinWidth, w); MinHeight = Math.Min(MinHeight, h);
            MaxHeight = work.Height / scale.M22; Width = Math.Min(Width, w); Height = Math.Min(Height, h);
        }
        if (preview) return;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(L("Otwórz"), null, (_, _) => Restore());
        menu.Items.Add(L("Sprawdź aktualizacje"), null, async (_, _) => { Restore(); await CheckUpdates(); });
        menu.Items.Add(L("Zgłoś błąd"), null, (_, _) => Feedback.OpenBug());
        menu.Items.Add(L("Zaproponuj funkcję"), null, (_, _) => Feedback.OpenFeature());
        menu.Items.Add(L("Licencje i źródła"), null, (_, _) => ReleaseInfo.ShowLicenses(new DialogOwner(new WindowInteropHelper(this).Handle)));
        menu.Items.Add(L("Zakończ"), null, (_, _) => RequestQuit());
        tray = new Forms.NotifyIcon { Icon = AppBrand.LoadIcon(16), Text = "AION2 Presence", Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => Restore(); tray.BalloonTipClicked += (_, _) => Restore();
        app = new PresenceApp(() => prefs); app.Start();
        timer.Tick += (_, _) => { if (showWindow?.WaitOne(0) == true) Restore(); RefreshLive(); if (!updateBusy && DateTimeOffset.UtcNow >= nextCheck) _ = CheckUpdates(); };
        timer.Start(); if (startTray) Hide(); await CheckUpdates();
    }
    private void SetCharacter(LiveData? data)
    {
        var c = data?.Character;
        characterName = c?.Name ?? "—"; characterClass = c?.Class ?? "—"; level = c?.Level?.ToString() ?? "—";
        cp = c?.CombatPower > 0 ? PresenceFormat.CP(c.CombatPower) : "—"; gs = c?.GearScore > 0 ? c.GearScore.ToString()! : "—"; activity = data?.Activity ?? "";
    }
    private void RefreshDemo()
    {
        var data = new LiveData(1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), true, new CharacterProfile("Lyra", "Cleric", 1304, 45, 86320, 1520), "Shugo Festival · Defend Shugo Merchants", "", true);
        SetCharacter(data); var payload = PresenceApp.ActivityFor(data, prefs, 1);
        summary = payload["details"]?.GetValue<string>() ?? ""; profile = payload["state"]?.GetValue<string>() ?? "";
        captureDetails = "Podgląd lokalny — fikcyjne dane, bez odczytu gry."; discordDetails = "Status nie jest wysyłany na Discorda.";
    }
    private void RefreshLive()
    {
        if (app is null) return;
        SetCharacter(app.SourceFresh ? app.Data : null); summary = app.Preview; profile = app.ProfilePreview;
        captureDetails = app.CaptureStatus; discordDetails = app.Status; error = app.LastError; Changed();
    }
    internal void ShowPage(string page) { selectedPage = page; Changed(); }
    internal void SetPreviewUpdate(bool available)
    {
        if (!preview) return;
        update = available ? new AppUpdate("0.2.0-beta.7", new Uri(ReleaseInfo.Repository), new Uri(ReleaseInfo.Repository)) : null;
        updateMessage = "Masz najnowszą wersję — sprawdź ponownie"; Changed();
    }
    internal void SetLanguage(string language)
    {
        UiLanguage.Language = language; prefs.Language = language;
        if (!preview) try { prefs.Save(); } catch (IOException e) { error = e.Message; }
        if (tray?.ContextMenuStrip is { } menu) foreach (Forms.ToolStripItem item in menu.Items) item.Text = L(item.Text ?? "");
        Changed();
    }
    private async Task CheckUpdates()
    {
        if (preview || updateBusy || quitting) return;
        updateBusy = true; nextCheck = DateTimeOffset.UtcNow.AddHours(6); updateMessage = "Sprawdzam aktualizacje…"; Changed();
        try {
            update = await Updates.Check(Forms.Application.ProductVersion.Split('+')[0], stop.Token);
            updateMessage = "Masz najnowszą wersję — sprawdź ponownie";
            if (update is not null && notifiedVersion != update.Version) { notifiedVersion = update.Version; tray?.ShowBalloonTip(7000, "AION2 Presence", L("Dostępna nowa wersja: ") + update.Version, Forms.ToolTipIcon.Info); }
        } catch (OperationCanceledException) { if (!quitting) updateMessage = "Nie udało się sprawdzić aktualizacji — spróbuj ponownie"; }
        catch (Exception e) when (e is HttpRequestException or IOException or System.Text.Json.JsonException or InvalidOperationException or FormatException or KeyNotFoundException) { updateMessage = "Nie udało się sprawdzić aktualizacji — spróbuj ponownie"; }
        finally { updateBusy = false; Changed(); }
    }
    private async void UpdateClick(object sender, RoutedEventArgs e)
    {
        if (preview) { SetPreviewUpdate(update is null); return; }
        if (updateBusy) return; if (update is null) { await CheckUpdates(); return; }
        var candidate = update;
        if (WpfMessageBox.Show(this, L("Pobrać aktualizację? Po sprawdzeniu pliku aplikacja zamknie się i otworzy instalator. Ustawienia zostaną zachowane."), "AION2 Presence · " + candidate.Version, MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
        updateBusy = true; percent = 0; updateMessage = "Pobieram aktualizację…"; Changed();
        try { var downloaded = await Updates.Download(candidate, new Progress<int>(p => { percent = p; Changed(); }), stop.Token); Updates.InstallOnExit(downloaded); RequestQuit(); }
        catch (OperationCanceledException) { if (!quitting) UpdateFailed(); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException) { UpdateFailed(); }
        finally { updateBusy = false; Changed(); }
    }
    private void UpdateFailed() => WpfMessageBox.Show(this, L("Aktualizacja nie powiodła się. Spróbuj ponownie lub pobierz instalator z GitHub Releases."), "AION2 Presence");
    private async void PauseClick(object sender, RoutedEventArgs e)
    {
        if (preview) { paused = !paused; Changed(); return; }
        if (app is not null) { var current = app; app = null; paused = true; await current.DisposeAsync(); }
        else { app = new PresenceApp(() => prefs); app.Start(); paused = false; }
        Changed();
    }
    private void Restore() { Show(); if (WindowState == System.Windows.WindowState.Minimized) WindowState = System.Windows.WindowState.Normal; Activate(); }
    internal void RequestQuit() { quitting = true; Close(); }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (preview || closing) return;
        if (!quitting) { e.Cancel = true; Hide(); return; }
        e.Cancel = true; IsEnabled = false; timer.Stop(); stop.Cancel();
        if (app is not null) { var current = app; app = null; await current.DisposeAsync(); }
        tray?.Dispose(); closing = true; Close();
    }
    private sealed class DialogOwner(IntPtr handle) : Forms.IWin32Window { public IntPtr Handle => handle; }
    private void HomeClick(object sender, RoutedEventArgs e) => ShowPage("home");
    private void HelpClick(object sender, RoutedEventArgs e) => ShowPage(IsHelp ? "home" : "help");
    private void PreviewStateClick(object sender, RoutedEventArgs e) => SetPreviewUpdate(update is null);
    private void ReportClick(object sender, RoutedEventArgs e) => Feedback.OpenBug();
    private void SuggestClick(object sender, RoutedEventArgs e) => Feedback.OpenFeature();
    private void LicensesClick(object sender, RoutedEventArgs e) => ReleaseInfo.ShowLicenses(new DialogOwner(new WindowInteropHelper(this).Handle));
    private void HideClick(object sender, RoutedEventArgs e) { if (preview) WindowState = System.Windows.WindowState.Minimized; else Hide(); }
    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = System.Windows.WindowState.Minimized;
    private void MaximizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState == System.Windows.WindowState.Maximized ? System.Windows.WindowState.Normal : System.Windows.WindowState.Maximized;
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void HeaderDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) MaximizeClick(sender, e);
        else if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }
    private void LanguageClick(object sender, RoutedEventArgs e) { var button = (System.Windows.Controls.Button)sender; button.ContextMenu.PlacementTarget = button; button.ContextMenu.IsOpen = true; }
    private void EnglishClick(object sender, RoutedEventArgs e) => SetLanguage("en");
    private void PolishClick(object sender, RoutedEventArgs e) => SetLanguage("pl");
    internal void RenderPreview(string path, double scale = 1)
    {
        UpdateLayout(); Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var image = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth * scale), (int)Math.Ceiling(ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        image.Render(this); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(path); encoder.Save(file);
    }
}
