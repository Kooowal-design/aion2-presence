# Shugo detection

The app identifies participation from a known venue map (`21 36`) or an active run (`00 61`). A world schedule (`01 60`) and a server move alone are not participation. The displayed status includes the competition name. This is implemented for the nine competitions below; live verification is still needed for every competition and both factions.

| Competition | Content / Elyos map | Asmodian map |
| --- | --- | --- |
| Up! Up! Up! | 500035 | 510030 |
| Not This Tile? | 500036 | 510031 |
| Jump Jump | 500037 | 510032 |
| Hidden Lugi | 500038 | 510033 |
| Mysterious Track | 500039 | 510034 |
| Wraith Evasion | 500040 | 510035 |
| Goldrin's Treasure | 500041 | 510036 |
| Odyle Flight Frenzy | 500042 | 510037 |
| Defend Shugo Merchants | 500043 | 510038 |

Names and identifiers were checked against [gaming.tools' Shugo Festival content and linked faction maps](https://aion2.gaming.tools/activities/events/shugo-festival), including [Defend content](https://aion2.gaming.tools/activities/dungeon-500043), [Elyos venue](https://aion2.gaming.tools/maps/500043) and [Asmodian venue](https://aion2.gaming.tools/maps/510038). The game content belongs to NCSOFT. No descriptions, artwork or full database are bundled.

The pinned parser's `capture/stream_processor.rs` also documents 500043 as a Shugo run and includes its completion frame in `dungeon_states_read_as_the_game_sends_them`. The same fixture is used in our regression tests. Earlier Presence versions mistakenly used 142019 instead, and could erase a detected event on entry transitions or unrelated run updates.

A known map establishes a maximum 20-minute lease. Active run messages use the supplied deadline, capped at 20 minutes, or a 10-minute lease without a deadline. Completion/exit for the matching event clears it. A change to another map, a local transition, server handoff or game exit clears the old event; arriving on a known Shugo map restores the appropriate competition. Same-map refreshes cannot resurrect an event marked complete. Unknown maps and content do not become Shugo. No raw packets or player logs are recorded for this feature.
