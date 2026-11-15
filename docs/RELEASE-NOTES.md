Public beta for Windows · English / Polski

## New in 0.2.0-beta.14

- Removed the unnecessary 24-byte minimum for private crafting details. A complete recipe plus a matching start is still required on the fallback path.
- A crafting start can now be recognized directly when its actor matches the local character confirmed by the game's self-identity data. It no longer depends on receiving adjacent private minigame details in that case.
- Public starts for other or unidentified actors, recipe windows and Substance Morph do not activate Crafting.
- Existing recipe/result matching, automatic-batch continuity, timeout and transition cleanup remain in place.
- Regression tests cover short detail frames, all five profession values, identity attribution and malformed events. The existing private capture is replayed without recording new gameplay.

This addresses restrictive detection conditions found while investigating reports about Soulstone, Manastone, crafting scrolls, Magic Crystal and a bow. Live verification of those recipes after the update is still needed; this release does not claim they have all been individually confirmed.

Download **Aion2Presence-Setup.exe**, or choose **Download and install** in the app. For a manual update, exit the previous copy from its system tray menu first. Settings and the installation folder are retained.

**Polski:** Usunięto zbędny próg długości komunikatu craftingu i dodano rozpoznawanie rozpoczęcia wytwarzania po potwierdzonej przez grę tożsamości własnej postaci. Nie wymaga wtedy prywatnego komunikatu minigry bezpośrednio przed startem. Zdarzenia innych graczy, samo okno receptur i Substance Morph nie włączają statusu. Poprawkę trzeba jeszcze sprawdzić w grze na zgłoszonych recepturach. Nie jest potrzebna nowa diagnostyka do zainstalowania i sprawdzenia tego wydania.

Npcap, AION 2 and Discord desktop are required on the same computer. The executable remains unsigned. Matching source, notices and SHA-256 checksums are attached.