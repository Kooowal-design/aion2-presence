Public beta for Windows · English / Polski

Download **Aion2Presence-Setup.exe** to install. Exit the previous copy from its system tray menu before updating. Npcap, AION 2 and Discord desktop are required on the same computer.

This release changes dungeon location tracking:

- Dungeon names require an observed map load. Party information alone no longer identifies your current location.
- Leaving a dungeon run, a local teleport or a server handoff invalidates the previous location until a new map load confirms it.
- Open-world and unknown maps do not inherit the previous dungeon name.
- If started mid-session, the app may show `In game` until the next map load. Live verification of the reported exit issue is still needed.

The Windows executable is unsigned; Windows may show an unknown-publisher or SmartScreen warning. Matching source, license notices and SHA-256 checksums are attached. See the README for supported activities, setup and limitations. Use the app or system tray menu to report bugs or suggest features.

**Polski:** Poprawione śledzenie dungeona: nazwa wymaga potwierdzenia mapy, a wyjście z runu, teleport i zmiana serwera unieważniają poprzednią lokalizację. Sama informacja o grupie nie wystarcza już do pokazania dungeona. Przy uruchomieniu w trakcie sesji może być potrzebne kolejne wczytanie mapy. Poprawkę trzeba jeszcze sprawdzić przy rzeczywistym wyjściu z instancji.
