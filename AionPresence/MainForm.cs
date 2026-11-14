using System.Diagnostics;
using System.Net.Http;
using Microsoft.Win32;

namespace AionPresence;

public sealed class MainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(14, 19, 29), PanelBg = Color.FromArgb(23, 31, 46);
    private static readonly Color TextColor = Color.FromArgb(225, 233, 245), Muted = Color.FromArgb(151, 168, 192), Accent = Color.FromArgb(139, 181, 255);
    private readonly Preferences prefs = Preferences.Load();
    private PresenceApp? app;
    private readonly NotifyIcon? tray;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 800 };
    private readonly Label capture = Label("Uruchamiam odczyt gry…", 11, Muted);
    private readonly Label discord = Label("Czekam na Discorda desktop…", 11, Muted);
    private readonly Label listPreview = Label("Czekam na postać…", 12, TextColor);
    private readonly Label profilePreview = Label("", 10, Muted);
    private readonly Label values = Label("Postać  —       Klasa  —       CP  —       GS  —", 11, TextColor);
    private readonly Label activity = Label("Aktywność  —", 11, Muted);
    private readonly Label error = Label("", 9, Color.FromArgb(239, 183, 130));
    private readonly Button toggle = new() { Text = "Wstrzymaj", Width = 125, Height = 36 };
    private readonly LinkLabel updateLink = new() { AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
    private readonly CancellationTokenSource updateStop = new();
    private AppUpdate? availableUpdate;
    private bool updateBusy;
    private DateTimeOffset nextUpdateCheck;
    private string updateMessage = "Sprawdź aktualizacje";
    private string? notifiedVersion;
    private bool quitting;
    private bool closing;
    private readonly bool previewMode;
    public MainForm(bool preview = false, bool startTray = false, EventWaitHandle? showWindow = null)
    {
        previewMode = preview;
        Text = "AION2 Presence · " + Application.ProductVersion.Split('+')[0];
        ClientSize = new Size(900, 824); MinimumSize = new Size(800, 862);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg; ForeColor = TextColor; Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = AppBrand.LoadIcon();
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 10, Padding = new Padding(26), BackColor = Bg };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        Controls.Add(root);
        var header = new Panel { Dock = DockStyle.Fill };
        var title = Label("AION2 PRESENCE", 22, TextColor, FontStyle.Bold); title.Location = new Point(0, 0); title.Size = new Size(520, 38);
        var subtitle = Label("Twoja postać, statystyki i aktualna aktywność na Discordzie.", 10, Muted); subtitle.Location = new Point(1, 42); subtitle.Size = new Size(740, 24);
        header.Controls.AddRange([title, subtitle]); root.Controls.Add(header);
        var language = UiLanguage.Selector(this, () => {
            prefs.Language = UiLanguage.Language;
            RefreshUpdateLink();
            if (tray?.ContextMenuStrip is { } menu)
                foreach (ToolStripItem item in menu.Items) item.Text = UiLanguage.Text(item.Text ?? "");
        }, !preview);
        language.Location = new Point(680, 4);
        language.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        header.Controls.Add(language);
        var statuses = Card("POŁĄCZENIA", capture, discord); root.Controls.Add(statuses);
        root.Controls.Add(Card("POSTAĆ Z GRY", values, activity));
        var presence = Card("PODGLĄD TEKSTU POD NICKIEM", listPreview, profilePreview);
        root.Controls.Add(presence);
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = PanelBg, Padding = new Padding(14, 10, 12, 8), WrapContents = true };
        var optionsTitle = Label("CO POKAZAĆ NA LIŚCIE", 9, Accent, FontStyle.Bold); optionsTitle.Width = 780; optionsTitle.Height = 25; options.Controls.Add(optionsTitle);
        AddOption(options, "Postać", prefs.Name, v => prefs.Name = v);
        AddOption(options, "Klasa", prefs.Class, v => prefs.Class = v);
        AddOption(options, "CP", prefs.CombatPower, v => prefs.CombatPower = v);
        AddOption(options, "GS", prefs.GearScore, v => prefs.GearScore = v);
        AddOption(options, "Aktywność", prefs.Activity, v => prefs.Activity = v);
        AddOption(options, "Level", prefs.Level, v => prefs.Level = v);
        root.Controls.Add(options);
        var help = Label("PIERWSZE URUCHOMIENIE\n1. Uruchom Discord desktop i AION 2 na tym samym komputerze.\n2. Pozostaw ExitLag włączony. Odczyt obejmuje też adapter loopback.\n3. Jeśli brak postaci: uruchom aplikację przed wejściem postaci do świata.\n\nWymagany Npcap. Aktywność: boss, dungeon, zbieranie lub crafting.", 10, Muted);
        help.Dock = DockStyle.Fill; help.Padding = new Padding(2, 12, 0, 0); root.Controls.Add(help);
        error.Dock = DockStyle.Fill; error.MaximumSize = new Size(840, 0); error.AutoSize = false; root.Controls.Add(error);
        var updateRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        updateLink.LinkColor = Accent;
        updateLink.LinkClicked += async (_, _) => await UpdateClicked();
        updateRow.Controls.Add(updateLink); root.Controls.Add(updateRow); RefreshUpdateLink();
        var feedback = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        var report = new LinkLabel { Text = "Zgłoś błąd", AutoSize = true, LinkColor = Accent, Margin = new Padding(0, 4, 20, 0) };
        report.LinkClicked += (_, _) => Feedback.OpenBug(); feedback.Controls.Add(report);
        var suggest = new LinkLabel { Text = "Zaproponuj funkcję", AutoSize = true, LinkColor = Accent, Margin = new Padding(0, 4, 20, 0) };
        suggest.LinkClicked += (_, _) => Feedback.OpenFeature(); feedback.Controls.Add(suggest);
        root.Controls.Add(feedback);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        StyleButton(toggle); toggle.Click += async (_, _) => await Toggle(); bottom.Controls.Add(toggle);
        var hide = new Button { Text = "Ukryj do zasobnika", Width = 160, Height = 36 }; StyleButton(hide); hide.Click += (_, _) => Hide(); bottom.Controls.Add(hide);
        var autostart = new CheckBox { Text = "Start z Windows", Checked = prefs.AutoStart, AutoSize = true, Margin = new Padding(15, 9, 20, 0), ForeColor = Muted };
        autostart.CheckedChanged += (_, _) => {
            try {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (autostart.Checked) key.SetValue("Aion2Presence", $"\"{Environment.ProcessPath}\" --tray");
                else key.DeleteValue("Aion2Presence", false);
                prefs.AutoStart = autostart.Checked; SavePrefs();
            } catch (Exception e) { error.Text = e.Message; }
        };
        bottom.Controls.Add(autostart); root.Controls.Add(bottom);
        var legal = new LinkLabel { Text = UiLanguage.Text("Licencje i źródła"), AutoSize = true, LinkColor = Accent, Margin = new Padding(8, 9, 12, 0) };
        legal.LinkClicked += (_, _) => ReleaseInfo.ShowLicenses(this); bottom.Controls.Add(legal);
        if (!preview) {
            var menu = new ContextMenuStrip();
            menu.Items.Add(UiLanguage.Text("Otwórz"), null, (_, _) => Restore());
            menu.Items.Add(UiLanguage.Text("Sprawdź aktualizacje"), null, async (_, _) => { Restore(); await CheckForUpdates(); });
            menu.Items.Add(UiLanguage.Text("Zgłoś błąd"), null, (_, _) => Feedback.OpenBug());
            menu.Items.Add(UiLanguage.Text("Zaproponuj funkcję"), null, (_, _) => Feedback.OpenFeature());
            menu.Items.Add(UiLanguage.Text("Licencje i źródła"), null, (_, _) => ReleaseInfo.ShowLicenses(this));
            menu.Items.Add(UiLanguage.Text("Zakończ"), null, (_, _) => { quitting = true; Close(); });
            tray = new NotifyIcon { Icon = AppBrand.LoadIcon(16), Text = "AION2 Presence", Visible = true, ContextMenuStrip = menu };
            tray.DoubleClick += (_, _) => Restore();
            tray.BalloonTipClicked += (_, _) => Restore();
            timer.Tick += (_, _) => { if (showWindow?.WaitOne(0) == true) Restore(); RefreshState(); if (!updateBusy && DateTimeOffset.UtcNow >= nextUpdateCheck) _ = CheckForUpdates(); }; timer.Start();
            Shown += async (_, _) => { StartApp(); if (startTray) Hide(); await CheckForUpdates(); };
        } else {
            capture.Text = "Postać rozpoznana · Loopback / tunel · port 13328";
            discord.Text = "Podgląd demonstracyjny — bez wysyłania statusu do Discorda";
            values.Text = "Kowal     ·     Gladiator     ·     CP 123.45k     ·     GS 2050";
            activity.Text = "Aktywność: Fighting Ritualist Garshim";
            listPreview.Text = "Kowal · Gladiator · CP 123.45k · GS 2050 · Fighting Ritualist Garshim";
            profilePreview.Text = "Karta profilu: Lv 50 · Fighting Ritualist Garshim";
        }
        FormClosing += OnClosing;
        UiLanguage.Apply(this);
    }
    private static Label Label(string text, float size, Color color, FontStyle style = FontStyle.Regular) =>
        new() { Text = text, Font = new Font("Segoe UI", size, style), ForeColor = color, AutoSize = false, BackColor = Color.Transparent, AutoEllipsis = true };
    private static Panel Card(string title, params Label[] lines)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = PanelBg, Margin = new Padding(0, 0, 0, 9), Padding = new Padding(14) };
        var heading = Label(title, 9, Accent, FontStyle.Bold); heading.SetBounds(14, 10, 780, 20); heading.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; panel.Controls.Add(heading);
        var y = 34;
        foreach (var line in lines) { line.SetBounds(14, y, 780, 27); line.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; panel.Controls.Add(line); y += 29; }
        return panel;
    }
    private void AddOption(FlowLayoutPanel parent, string text, bool value, Action<bool> change)
    {
        var check = new CheckBox { Text = text, Checked = value, AutoSize = true, Margin = new Padding(0, 4, 23, 6), ForeColor = TextColor };
        check.CheckedChanged += (_, _) => { change(check.Checked); SavePrefs(); };
        parent.Controls.Add(check);
    }
    private static void StyleButton(Button b) { b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Color.FromArgb(55, 76, 110); b.BackColor = Color.FromArgb(31, 47, 70); b.ForeColor = TextColor; b.Cursor = Cursors.Hand; }
    private void SavePrefs() { if (!previewMode) { try { prefs.Language = UiLanguage.Language; prefs.Save(); } catch (Exception e) { error.Text = e.Message; } } }
    private void Restore() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    private void StartApp() { app = new PresenceApp(() => prefs); app.Start(); toggle.Text = "Wstrzymaj"; }
    private void RefreshUpdateLink()
    {
        updateLink.Enabled = !updateBusy;
        updateLink.Text = availableUpdate is not null && !updateBusy
            ? UiLanguage.Text("Nowa wersja: ") + availableUpdate.Version + " — " + UiLanguage.Text("Pobierz i zainstaluj")
            : UiLanguage.Text(updateMessage);
    }
    private async Task CheckForUpdates()
    {
        if (previewMode || updateBusy || quitting) return;
        updateBusy = true; nextUpdateCheck = DateTimeOffset.UtcNow.AddHours(6);
        updateMessage = "Sprawdzam aktualizacje…"; RefreshUpdateLink();
        try {
            availableUpdate = await Updates.Check(Application.ProductVersion.Split('+')[0], updateStop.Token);
            updateMessage = "Masz najnowszą wersję — sprawdź ponownie";
            if (availableUpdate is not null && notifiedVersion != availableUpdate.Version) {
                notifiedVersion = availableUpdate.Version;
                tray?.ShowBalloonTip(7000, "AION2 Presence", UiLanguage.Text("Dostępna nowa wersja: ") + availableUpdate.Version, ToolTipIcon.Info);
            }
        } catch (OperationCanceledException) { if (!quitting) updateMessage = "Nie udało się sprawdzić aktualizacji — spróbuj ponownie"; }
        catch (Exception e) when (e is HttpRequestException or IOException or System.Text.Json.JsonException or InvalidOperationException or FormatException or KeyNotFoundException) {
            updateMessage = "Nie udało się sprawdzić aktualizacji — spróbuj ponownie";
        } finally { updateBusy = false; if (!IsDisposed) RefreshUpdateLink(); }
    }
    private async Task UpdateClicked()
    {
        if (updateBusy) return;
        if (availableUpdate is null) { await CheckForUpdates(); return; }
        var candidate = availableUpdate;
        if (MessageBox.Show(this, UiLanguage.Text("Pobrać aktualizację? Po sprawdzeniu pliku aplikacja zamknie się i otworzy instalator. Ustawienia zostaną zachowane."), "AION2 Presence · " + candidate.Version, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
        updateBusy = true; updateMessage = "Pobieram aktualizację…"; RefreshUpdateLink();
        try {
            var downloaded = await Updates.Download(candidate, new Progress<int>(percent => {
                if (!IsDisposed) updateLink.Text = UiLanguage.Text("Pobieram aktualizację…") + " " + percent + "%";
            }), updateStop.Token);
            Updates.InstallOnExit(downloaded);
            quitting = true; Close();
        } catch (OperationCanceledException) {
            if (!quitting) MessageBox.Show(this, UiLanguage.Text("Aktualizacja nie powiodła się. Spróbuj ponownie lub pobierz instalator z GitHub Releases."), "AION2 Presence");
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException) {
            MessageBox.Show(this, UiLanguage.Text("Aktualizacja nie powiodła się. Spróbuj ponownie lub pobierz instalator z GitHub Releases."), "AION2 Presence");
        } finally { updateBusy = false; if (!IsDisposed) RefreshUpdateLink(); }
    }
    private async Task Toggle()
    {
        toggle.Enabled = false;
        if (app is not null) { var current = app; app = null; await current.DisposeAsync(); capture.Text = "Odczyt zatrzymany"; discord.Text = "Status Discorda wyłączony"; toggle.Text = "Uruchom"; }
        else StartApp();
        toggle.Enabled = true;
    }
    private void RefreshState()
    {
        if (app is null) { UiLanguage.Apply(this); return; }
        capture.Text = UiLanguage.Text(app.CaptureStatus); discord.Text = UiLanguage.Text(app.Status);
        listPreview.Text = app.Preview.Length > 0 ? app.Preview : "Czekam na dane postaci…";
        profilePreview.Text = app.ProfilePreview.Length > 0 ? "Karta profilu: " + app.ProfilePreview : "Długi tekst może zostać ucięty na liście osób przez Discorda.";
        var d = app.Data;
        if (d?.Character is { } c) {
            values.Text = $"{c.Name}     ·     {c.Class ?? "?"}     ·     CP {(c.CombatPower > 0 ? PresenceFormat.CP(c.CombatPower) : "—")}     ·     GS {(c.GearScore > 0 ? c.GearScore.ToString() : "—")}";
            activity.Text = $"Aktywność: {d.Activity}";
        } else { values.Text = "Postać  —       Klasa  —       CP  —       GS  —"; activity.Text = "Aktywność  —"; }
        error.Text = UiLanguage.Text(app.LastError);
        UiLanguage.Apply(this);
    }
    private async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (previewMode || closing) return;
        if (!quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
        e.Cancel = true; quitting = true; Enabled = false; timer.Stop();
        updateStop.Cancel();
        if (app is not null) { var current = app; app = null; await current.DisposeAsync(); }
        tray?.Dispose(); closing = true; Close();
    }
    protected override void Dispose(bool disposing) { if (disposing) { timer.Dispose(); tray?.Dispose(); updateStop.Dispose(); } base.Dispose(disposing); }
}
