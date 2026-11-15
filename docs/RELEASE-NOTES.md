Public beta for Windows · English / Polski

## New in 0.2.0-beta.15

- Fixed Crafting detection for production through the premium crafting window, regardless of its keyboard binding or how the window is opened.
- The recorded premium batch uses the same production messages as station crafting, with an attempt/recipe token of zero. The previous decoder incorrectly rejected zero; starts and matching results now accept it.
- Positive actor IDs, local-player attribution, matching private/start pairs, automatic-batch continuity, result cleanup and combat priority remain in place. Merely opening a menu does not activate Crafting. Substance Morph remains excluded.
- Regression checks replay the complete premium batch recording and the earlier station recording. Synthetic tests cover zero-token batches, all five profession values, bystander rejection and timeout/map cleanup. Private recordings are not included in the source or release.

The fix has been validated against recorded gameplay. Live confirmation of the installed update is still welcome; the recordings do not cover every profession or recipe.

Download **Aion2Presence-Setup.exe**, or choose **Download and install** in the app. For a manual update, exit the previous copy from its system tray menu first. Settings and the installation folder are retained.

**Polski:** Poprawiono wykrywanie produkcji przez okno craftingu premium. Nie zależy ono od skrótu Alt+G ani przypisania klawiszy. W zapisie gry pole używane do dopasowania próby ma wartość 0, którą wcześniejszy parser błędnie odrzucał. Teraz obsługuje start i wyniki takiej produkcji, zachowując dotychczasowe sprawdzanie postaci, ciągłości serii i zakończenia. Samo otwarcie okna nie włącza statusu. Poprawkę sprawdzono na zapisanej serii premium oraz wcześniejszym zapisie ze stołu; pozostaje potwierdzenie po instalacji. Nie potrzeba kolejnego nagrania do sprawdzenia tego wydania.

Npcap, AION 2 and Discord desktop are required on the same computer. The executable remains unsigned. Matching source, notices and SHA-256 checksums are attached.
