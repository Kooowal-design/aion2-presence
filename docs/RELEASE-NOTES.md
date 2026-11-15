Public beta for Windows · English / Polski

## New in 0.2.0-beta.10

- Added experimental workstation Crafting activity based on complete Global potion and cooking traces.
- Uses private per-attempt details plus a matching actor/recipe start, without an item-name list.
- Keeps automatic batches continuous across result/start gaps; clears shortly after the last result or cancellation.
- Excludes Substance Morph and public-only crafting events. Combat retains priority; map changes clear crafting.
- Fixed a diagnostic interruption caused by Windows readers blocking atomic summary-file replacement. The helper now allows delete sharing, and the engine retries transient access/sharing failures.
- Added offline trace replay and regression coverage. Private user captures are not included in the repository.

Potion cancellation/natural completion and multiple cooking attempts were replayed.
Cooking cancellation/final completion and other professions still require live validation.
A missing result expires after 60 seconds. See docs/CRAFTING.md for evidence and limitations.

Download **Aion2Presence-Setup.exe**, or select **Download and install** in the app.
Exit the previous copy from its system tray menu before installing manually.

## Polski

Dodano eksperymentalne wykrywanie craftingu przy stanowisku na podstawie pełnego zapisu
mikstur i jedzenia. Nie wymaga osobnej konfiguracji dla każdego przepisu.
Status utrzymuje się pomiędzy próbami automatycznej serii i znika po ostatnim wyniku
lub anulowaniu. Substance Morph nie uruchamia statusu.

Naprawiono również przerwanie diagnostyki z błędem „Odmowa dostępu” podczas podmiany raportu.
Zapis został odtworzony offline; prywatne komunikaty nie są publikowane na GitHub.
Cooking nie miał osobno zarejestrowanego anulowania i naturalnego końca, a pozostałe
profesje wymagają jeszcze sprawdzenia w grze.

Pobierz **Aion2Presence-Setup.exe** lub wybierz **Pobierz i zainstaluj** w aplikacji.
Folder instalacji, ustawienia i skróty zostają zachowane.

## Optional full diagnostic

The separate **Aion2Presence-Crafting-Diagnostic.zip** records complete incoming game data
locally and may include character information and chat. No automatic upload occurs.
Normal Presence does not enable recording.

Npcap, AION 2 and Discord desktop are required for normal Presence on the same computer.
Matching source, notices and SHA-256 checksums are attached. The Windows executable remains unsigned.

