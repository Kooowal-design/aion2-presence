# Beta release checks / Przegląd bety

Candidate: **0.2.0-beta.5**.

## Prepared changes

- Dungeon names require a captured map load. Party roster updates cannot restore an old location. Run departure, local teleport and server handoff invalidate the previous location; open-world/unknown maps show no dungeon. Verify exiting Shattered Arkanis before using another teleport, including a normal open-world mob kill.

- Capture gaps show a readable bilingual warning for 15 seconds after the last warning. Expiry is not a claim of stream recovery. Actual engine errors remain visible and are not replaced by warnings.

- Maintainer-owned Discord RPC identity, embedded once during building. Users do not create an app, log in through this tool, or provide a token.
- Class images use public Discord CDN URLs; provenance is documented in `NOTICE.md`. Their availability is controlled externally.
- English and Polish README include a three-view Discord illustration using fictional data. Original personal screenshots are not used in that illustration.
- .NET 10 LTS self-contained Windows build; no separate .NET installation needed by users.
- Embedded GPL, attribution and Rust/.NET dependency notices; installer writes them beside the executable. The app and tray menu expose **Licenses & source**.
- Matching Windows ZIP, source ZIP and SHA-256 checksums. Source ZIP includes the pinned parser revision and original Rust crate source archives. The source manifest identifies the exact revision. Building may require network access for dependency restore and Microsoft SDK/runtime packs.
- Npcap remains a separately obtained dependency and is not bundled.
- Shugo: nine competition names, 18 faction maps, corrected content ids, map-based entry detection and matching run completion. See docs/SHUGO.md. Map/run sequences are tested; live checks remain necessary.
- Capture handoff between game servers is restored in the headless worker, including the Shugo move announcement.

- Bilingual GitHub bug/feature forms, with links in the app and tray. Only the app version is prefilled; the user reviews and submits the report on GitHub.

## Checks on the target computer

- [ ] Install into a chosen folder; desktop and Start menu shortcuts show Winged A. The window title says `0.2.0-beta.5`.
- [ ] Switch English/Polski, restart and confirm the language is remembered.
- [ ] Open **Licenses & source** and check notices and the source link.
- [ ] Run Discord desktop and AION 2. If the game launcher errors, exit Presence, start the game first, then Presence. Leave ExitLag as configured.
- [ ] Confirm character, class, CP and GS; CP/GS occur only on the first profile text line. Check the list, hover tooltip and profile separately.
- [ ] Check class artwork on the new Discord identity. External image support and the actual appearance need live verification.
- [ ] Gathering appears and expires. Substance Morph must not activate an activity.
- [ ] Check a dungeon and recognized boss. For Shugo, verify the correct competition name after entry and its removal after completion/return.
- [ ] Exit the game and confirm activity clears. Hide the app and reopen the shortcut to confirm the window returns.
- [ ] Check an update with Windows autostart enabled; autostart should use the new installation.
- [ ] Open Report a bug and Suggest a feature; verify the correct forms open and only the version is prefilled. Do not submit a test report unless you want to keep it.

Automated checks do not replace these live-session checks. Windows binaries are currently unsigned. No assurance of NC approval or account safety is provided; see the README's game-rules section.

## Release privacy and source

The publication repository uses a clean history with GitHub noreply identities. Keep the old archive private. Do not add original user screenshots or local settings. Keep `LICENSE`, `NOTICE.md`, dependency license supplements and matching source distribution available.

## Po polsku

Sprawdź nowy instalator, skróty, języki, menu **Licencje i źródła**, nową integrację Discord i ikonki klas. Następnie sprawdź listę osób, podgląd po najechaniu i profil, zbieranie, dungeon/bossa oraz eksperymentalny status Shugo z nazwą konkurencji dla obu frakcji. Zweryfikuj wyczyszczenie statusu po zamknięciu gry i autostart po aktualizacji. Repozytorium jest publiczne; zweryfikuj to wydanie w rzeczywistej sesji.

- [ ] Updates: startup/manual checks, available version notification, verified download, shutdown before installer, preserved folder/settings; reject corrupt downloads. Check offline behavior. Old versions require a manual update to beta.5 first.
