Public beta for Windows · English / Polski

## New in 0.2.0-beta.10

- Added Crafting activity detection, including automatic batches.
- Crafting activates when production starts. Opening the recipe window alone does not activate it.
- Detection is shared across recipes and professions, without per-item configuration.
- Keeps the activity continuous between attempts and clears shortly after the last result or cancellation.
- Excludes Substance Morph. Combat retains priority; map changes clear crafting.
- Fixed a diagnostic interruption caused by Windows readers blocking summary-file replacement.
- Added offline trace replay and regression coverage. Private user captures are not published.

Download **Aion2Presence-Setup.exe**, or select **Download and install** in the app.
Exit the previous copy from its system tray menu before installing manually.

## Polski

Dodano wykrywanie **Crafting**, w tym automatycznych serii.
Status włącza się po rozpoczęciu wytwarzania; samo otwarcie okna przepisów go nie aktywuje.
Wykrywanie jest wspólne dla przepisów i profesji, bez konfiguracji każdego przedmiotu.
Status utrzymuje się pomiędzy próbami serii i znika po ostatnim wyniku lub anulowaniu.
Substance Morph nie uruchamia statusu.

Naprawiono również przerwanie diagnostyki z błędem „Odmowa dostępu” podczas podmiany raportu.
Prywatne komunikaty z diagnostyki nie są publikowane na GitHub.

Pobierz **Aion2Presence-Setup.exe** lub wybierz **Pobierz i zainstaluj** w aplikacji.
Folder instalacji, ustawienia i skróty zostają zachowane.

## Optional full diagnostic

The separate **Aion2Presence-Crafting-Diagnostic.zip** records complete incoming game data
locally and may include character information and chat. No automatic upload occurs.
Normal Presence does not enable recording.

Npcap, AION 2 and Discord desktop are required for normal Presence on the same computer.
Matching source, notices and SHA-256 checksums are attached. The Windows executable remains unsigned.

