# Third-party notices

AION2 Presence reuses open-source code and data from **taengu/A2Tools-DPS-Meter**, licensed under GPL-3.0:

https://github.com/taengu/A2Tools-DPS-Meter

The exact revision is pinned by the `vendor/a2tools` Git submodule. Its license, copyright notices, upstream source and existing third-party notices are retained there. The root `LICENSE` contains the project's GPL license.

Reused components include the packet parser, TCP ordering, Npcap capture pipeline, server/dungeon catalogs and boss selection. The standalone worker intentionally omits Tauri, overlays, packet-log recording and upload sinks. It has a new JSON output loop, presence activity composition and bounded combat-history cleanup. The Windows UI, installer and Discord IPC publisher are provided by this repository.

Discord RPC uses this project's maintainer-owned application identifier, configured in `discord-app.json`. Class artwork is referenced through public Discord CDN image URLs from the upstream presence implementation; changing RPC identity does not require users to register an application or upload assets. These external images remain controlled by their respective owners. AION2 Presence does not require running the upstream application and does not show its name or logo in its product interface or published status.

AION 2 names and game artwork belong to their respective rights holders. This project is not endorsed by NCSOFT or Discord.
