Public beta for Windows · English / Polski

## New in 0.2.0-beta.8

- Replaced the optional crafting metadata probe with a full incoming game-stream diagnostic.
- Added guided potion and food tests: recipe window, automatic batch, cancellation and natural completion.
- Added a reception preflight, millisecond ordering, complete frame/stream bytes, local-player ID, per-phase counts, error logs and a single result ZIP.
- Normal Presence activities and gathering detection are unchanged. This release does not claim working workstation-crafting detection.

## ShadowPC diagnostic

Download **Aion2Presence-Crafting-Diagnostic.zip** on ShadowPC, extract it and run **Start-Diagnostic.cmd**.
Exit normal Presence first; keep the game running. Prepare potion and food materials.
The helper explains each step and produces a local **craft-diagnostic-....zip**.

**The diagnostic contains full incoming game data, potentially including character information and chat.**
It never uploads automatically. Share the result privately in the support chat, not in a public GitHub issue.
Limits are 20 minutes / 128 MiB. Normal Presence does not record this trace.

## Polski

Osobna paczka diagnostyczna prowadzi przez mikstury i jedzenie: otwarcie przepisu, automatyczna seria,
anulowanie i naturalny koniec. Zapisuje pelne komunikaty gry i tworzy jeden ZIP do analizy.
Moze zawierac dane postaci i czat; nie wysyla nic automatycznie. Nie publikuj raportu na GitHub.
Zwykla aplikacja, zbieractwo i pozostale aktywnosci dzialaja bez zmian.
Nie trzeba instalowac nowej wersji aplikacji, aby uruchomic osobna diagnostyke.

Npcap, AION 2 and Discord desktop are required for normal Presence on the same computer.
Matching source, notices and SHA-256 checksums are attached. The Windows executable remains unsigned.

