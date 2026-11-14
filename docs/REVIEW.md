# Private beta review / Przegląd prywatnej bety

Candidate: **0.2.0-beta.3**. This document does not authorize changing repository visibility.

## Prepared changes

- Capture gaps show a readable bilingual warning for 15 seconds after the last warning. Expiry is not a claim of stream recovery. Actual engine errors remain visible and are not replaced by warnings.

- Maintainer-owned Discord RPC identity, embedded once during building. Users do not create an app, log in through this tool, or provide a token.
- Class images use public Discord CDN URLs; provenance is documented in `NOTICE.md`. Their availability is controlled externally.
- English and Polish README include a three-view Discord illustration using fictional data. Original personal screenshots are not used in that illustration.
- .NET 10 LTS self-contained Windows build; no separate .NET installation needed by users.
- Embedded GPL, attribution and Rust/.NET dependency notices; installer writes them beside the executable. The app and tray menu expose **Licenses & source**.
- Matching Windows ZIP, source ZIP and SHA-256 checksums. Source ZIP includes the pinned parser revision and original Rust crate source archives. The source manifest identifies the exact revision. Building may require network access for dependency restore and Microsoft SDK/runtime packs.
- Npcap remains a separately obtained dependency and is not bundled.
- Experimental Shugo support: only a confirmed active run with the observed content ID `142019` is labelled `Shugo Festival`. A schedule broadcast or server handoff alone is insufficient. The label clears on a run end or map change, or expires at the reported deadline (capped at 20 minutes), with a 10-minute cap if no deadline is provided. No individual competition names or all-venue coverage are claimed.
- Capture handoff between game servers is restored in the headless worker, including the Shugo move announcement.

- Bilingual GitHub bug/feature forms, with links in the app and tray. Only the app version is prefilled; the user reviews and submits the report on GitHub.

## Checks on the target computer

- [ ] Install into a chosen folder; desktop and Start menu shortcuts show Winged A. The window title says `0.2.0-beta.3`.
- [ ] Switch English/Polski, restart and confirm the language is remembered.
- [ ] Open **Licenses & source** and check notices and the source link.
- [ ] Run Discord desktop and AION 2. If the game launcher errors, exit Presence, start the game first, then Presence. Leave ExitLag as configured.
- [ ] Confirm character, class, CP and GS; CP/GS occur only on the first profile text line. Check the list, hover tooltip and profile separately.
- [ ] Check class artwork on the new Discord identity. External image support and the actual appearance need live verification.
- [ ] Gather and craft: activities appear and expire; opening the recipe window alone does not mean crafting.
- [ ] Check a dungeon and recognized boss. For Shugo, report whether the experimental generic label appears; unknown variants may still show `In game`.
- [ ] Exit the game and confirm activity clears. Hide the app and reopen the shortcut to confirm the window returns.
- [ ] Check an update with Windows autostart enabled; autostart should use the new installation.
- [ ] Open Report a bug and Suggest a feature; verify the correct forms open and only the version is prefilled. Do not submit a test report unless you want to keep it.

Automated checks do not replace these live-session checks. Windows binaries are currently unsigned. No assurance of NC approval or account safety is provided; see the README's game-rules section.

## Before making the repository public

Review this candidate first. Changing visibility also exposes existing Git history, commit-author metadata and old releases, not just the current README. No original user screenshots or local settings should be added. Keep `LICENSE`, `NOTICE.md`, dependency license supplements and matching source distribution available.

## Po polsku

Sprawdź nowy instalator, skróty, języki, menu **Licencje i źródła**, nową integrację Discord i ikonki klas. Następnie sprawdź listę osób, podgląd po najechaniu i profil, crafting, zbieranie, dungeon/bossa oraz eksperymentalny ogólny status Shugo. Nazwy poszczególnych konkurencji Shugo nie są jeszcze rozpoznawane. Zweryfikuj wyczyszczenie statusu po zamknięciu gry i autostart po aktualizacji. Repozytorium pozostaje prywatne do Twojej decyzji po przeglądzie.
