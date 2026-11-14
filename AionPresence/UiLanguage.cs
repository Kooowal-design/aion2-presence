namespace AionPresence;

public static class UiLanguage
{
    public static string Language { get; set; } = Preferences.Load().Language == "pl" ? "pl" : "en";
    private static readonly (string Pl, string En)[] Messages = [
        ("Instalacja AION2 Presence", "AION2 Presence Setup"),
        ("Twoja postać i aktywność w AION 2 na Discordzie.\n\nInstalacja dla bieżącego użytkownika Windows\noraz skróty na pulpicie i w menu Start.\n\nWymagane są AION 2, Discord desktop i Npcap.", "Your AION 2 character and activity on Discord.\n\nInstalls for the current Windows user, with\nshortcuts on the desktop and in the Start menu.\n\nRequires AION 2, Discord desktop and Npcap."),
        ("Zainstalowano.\n\nSkróty AION2 Presence są na pulpicie i w menu Start.\nMożesz usunąć pobrany instalator.\n\nKliknij Uruchom, aby rozpocząć.", "Installed.\n\nAION2 Presence shortcuts are on your desktop and Start menu.\nYou can delete the downloaded installer.\n\nClick Launch to get started."),
        ("AION2 Presence nadal działa. Przy zegarku kliknij jego ikonę prawym przyciskiem i wybierz Zakończ, następnie ponów instalację. Zamknięcie okna krzyżykiem tylko ukrywa aplikację.", "AION2 Presence is still running. Right-click its system tray icon and select Exit, then retry installation. The window's close button only hides the app."),
        ("AION2 Presence już działa — otwórz go z ikony obok zegarka.", "AION2 Presence is already running. Open it from the system tray icon."),
        ("Brak wbudowanego silnika odczytu. Pobierz pełną paczkę wydania.", "The embedded capture engine is missing. Download the complete release."),
        ("Nie znaleziono uruchomionego pliku.", "Cannot find the running executable."),
        ("Nie można utworzyć skrótu Windows.", "Cannot create the Windows shortcut."),
        ("Instalacja nie powiodła się:\n", "Installation failed:\n"),
        ("Zainstaluj", "Install"), ("Uruchom", "Launch"),
        ("Licencje i źródła", "Licenses & source"), ("Kod źródłowy tego wydania", "Source code for this release"),
        ("Zgłoś błąd", "Report a bug"), ("Zaproponuj funkcję", "Suggest a feature"),
        ("Nie udało się otworzyć przeglądarki.\n", "Could not open the browser.\n"),
        ("Folder instalacji", "Installation folder"), ("Przeglądaj…", "Browse…"),
        ("Wybierz pełną ścieżkę folderu instalacji.", "Choose an absolute installation folder path."),
        ("Twoja postać, statystyki i aktualna aktywność na Discordzie.", "Your character, stats and current activity on Discord."),
        ("Uruchamiam odczyt gry…", "Starting game capture…"),
        ("Czekam na Discorda desktop…", "Waiting for Discord desktop…"),
        ("Czekam na postać…", "Waiting for your character…"),
        ("Postać  —       Klasa  —       CP  —       GS  —", "Character  —       Class  —       CP  —       GS  —"),
        ("Aktywność  —", "Activity  —"), ("Aktywność: ", "Activity: "),
        ("POŁĄCZENIA", "CONNECTIONS"), ("POSTAĆ Z GRY", "GAME CHARACTER"),
        ("PODGLĄD TEKSTU POD NICKIEM", "MEMBER LIST STATUS PREVIEW"),
        ("CO POKAZAĆ NA LIŚCIE", "SHOW IN MEMBER LIST"),
        ("Postać", "Character"), ("Klasa", "Class"), ("Aktywność", "Activity"),
        ("PIERWSZE URUCHOMIENIE\n1. Uruchom Discord desktop i AION 2 na tym samym komputerze.\n2. Pozostaw ExitLag włączony. Odczyt obejmuje też adapter loopback.\n3. Jeśli brak postaci: uruchom aplikację przed wejściem postaci do świata.\n\nWymagany Npcap. Aktywność: boss, dungeon, zbieranie lub crafting.", "GETTING STARTED\n1. Run Discord desktop and AION 2 on the same computer.\n2. You can keep ExitLag enabled. Capture includes loopback traffic.\n3. No character data? Start the app before entering the game world.\n\nNpcap required. Activity: boss, dungeon, gathering or crafting."),
        ("Wstrzymaj", "Pause"), ("Ukryj do zasobnika", "Hide to tray"),
        ("Start z Windows", "Start with Windows"), ("Otwórz", "Open"), ("Zakończ", "Exit"),
        ("Postać rozpoznana · Loopback / tunel · port 13328", "Character detected · Loopback / tunnel · port 13328"),
        ("Podgląd demonstracyjny — bez wysyłania statusu do Discorda", "Demo preview — no activity sent to Discord"),
        ("Karta profilu: ", "Profile card: "),
        ("Odczyt zatrzymany", "Capture paused"), ("Status Discorda wyłączony", "Discord activity cleared"),
        ("Czekam na dane postaci…", "Waiting for character data…"),
        ("Długi tekst może zostać ucięty na liście osób przez Discorda.", "Discord may truncate long text in the member list."),
        ("Uruchamiam…", "Starting…"), ("Czekam na silnik", "Waiting for capture engine"),
        ("Nie udało się uruchomić silnika odczytu.", "Cannot start the capture engine."),
        ("Silnik przesłał nieprawidłowy stan.", "The capture engine returned invalid data."),
        ("Silnik nie odpowiada", "Capture engine is not responding"),
        ("Brak odczytu Npcap — sprawdź komunikat poniżej", "No Npcap capture — check the message below"),
        ("Czekam na AION 2", "Waiting for AION 2"),
        ("Odczyt działa. Czekam na dane postaci z gry…", "Capture is running. Waiting for character data from the game…"),
        ("Postać rozpoznana · Loopback / tunel", "Character detected · Loopback / tunnel"),
        ("Postać rozpoznana · Połączenie gry", "Character detected · Game connection"),
        ("Discord połączony · status gry wyłączony", "Discord connected · game activity cleared"),
        ("Discord potwierdził wyświetlanie szczegółów na liście", "Discord confirmed details display in the member list"),
        ("Discord przyjął status · wygląd listy zależy od klienta", "Discord accepted activity · member list display depends on the client"),
        ("Discord nie odpowiedział — ponawiam", "Discord did not respond — retrying"),
        ("Zatrzymano z błędem", "Stopped with an error"),
        ("Za duża ramka IPC.", "IPC frame is too large."),
        ("Nieprawidłowa długość ramki IPC.", "Invalid IPC frame length."),
        ("Nieprawidłowa ramka JSON.", "Invalid JSON frame."),
        ("Discord odrzucił połączenie.", "Discord rejected the connection."),
        ("Nie znaleziono działającego Discorda desktop.", "Discord desktop is not running."),
        ("Discord zamknął połączenie.", "Discord closed the connection."),
        ("odrzucono status", "activity rejected")
    ];

    public static string Text(string text)
    {
        // Match complete messages before prefixes such as "Character".
        foreach (var pair in Messages.OrderByDescending(p => Math.Max(p.Pl.Length, p.En.Length))) {
            if (text == pair.Pl || text == pair.En) return Language == "pl" ? pair.Pl : pair.En;
        }
        foreach (var pair in Messages.Where(p => p.Pl.EndsWith(' ') || p.Pl.EndsWith('\n') || p.Pl.StartsWith("Postać rozpoznana · ")).OrderByDescending(p => p.Pl.Length)) {
            if (text.StartsWith(pair.Pl, StringComparison.Ordinal)) return (Language == "pl" ? pair.Pl : pair.En) + text[pair.Pl.Length..];
            if (text.StartsWith(pair.En, StringComparison.Ordinal)) return (Language == "pl" ? pair.Pl : pair.En) + text[pair.En.Length..];
        }
        return text;
    }

    public static void Apply(Control control)
    {
        if (control is not ComboBox) control.Text = Text(control.Text);
        foreach (Control child in control.Controls) Apply(child);
    }

    public static Button Selector(Control form, Action? changed = null, bool persist = true)
    {
        var selector = new Button {
            Text = Language == "pl" ? "Polski ▾" : "English ▾", Width = 120, Height = 28,
            ForeColor = Color.FromArgb(225, 233, 245), BackColor = Color.FromArgb(31, 47, 70), FlatStyle = FlatStyle.Flat
        };
        var menu = new ContextMenuStrip();
        void Select(string language) {
            Language = language;
            selector.Text = Language == "pl" ? "Polski ▾" : "English ▾";
            if (persist) { var prefs = Preferences.Load(); prefs.Language = Language; prefs.Save(); }
            changed?.Invoke();
            Apply(form);
        }
        menu.Items.Add("English", null, (_, _) => Select("en"));
        menu.Items.Add("Polski", null, (_, _) => Select("pl"));
        selector.Click += (_, _) => menu.Show(selector, new Point(0, selector.Height));
        selector.Disposed += (_, _) => menu.Dispose();
        return selector;
    }
}
