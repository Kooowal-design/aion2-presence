AION2 Presence - ALT+G crafting capture

Polski:
1. Rozpakuj ZIP na Shadow PC i uruchom Start-AltG.cmd.
2. Gra musi dzialac, Npcap musi byc zainstalowany. ExitLag zostaje wlaczony.
3. Zamknij zwykle Presence z menu przy zegarze.
4. Narzedzie prowadzi przez ALT+G i jedna serie 3 sztuk dowolnego przepisu.
   Nie wykonuj testu przy stole. Nie trzeba sprawdzac innych receptur.
5. Dolacz wynikowy altg-diagnostic-....zip w prywatnym czacie.

English:
Extract on the same Windows/Shadow PC as the running game and run Start-AltG.cmd.
Npcap is required; ExitLag can stay enabled. Exit normal Presence from its tray.
The Polish prompts guide one ALT+G batch of exactly 3 items; no station test.
Attach the resulting altg-diagnostic-....zip in the private support conversation.

The helper downloads the existing beta.14 diagnostic engine from this project's
GitHub Release and verifies its pinned SHA-256 before starting it. It does not
install/update Presence or control the game. Opening a menu is an analysis marker,
not the proposed detection mechanism. This package is NOT a crafting fix.

Only an explicit run starts recording. Incoming game data can include chat and
character information. Nothing is uploaded automatically. Do not post the output
publicly. Limits inherited from the engine: 20 minutes / 128 MiB trace.
The engine and its dependency notices/source are supplied in the downloaded
release archive. Helper source: GPL-3.0-or-later, as in the repository LICENSE.
