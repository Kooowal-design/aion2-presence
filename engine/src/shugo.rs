//! Verified content and Elyos/Asmodian venue ids; see docs/SHUGO.md.
pub const GAMES: [(u32, i32, &str); 9] = [
    (500035, 510030, "Up! Up! Up!"),
    (500036, 510031, "Not This Tile?"),
    (500037, 510032, "Jump Jump"),
    (500038, 510033, "Hidden Lugi"),
    (500039, 510034, "Mysterious Track"),
    (500040, 510035, "Wraith Evasion"),
    (500041, 510036, "Goldrin's Treasure"),
    (500042, 510037, "Odyle Flight Frenzy"),
    (500043, 510038, "Defend Shugo Merchants"),
];

pub fn game(id: i32) -> Option<(u32, &'static str)> {
    GAMES.iter().find(|(content, other, _)| *content as i32 == id || *other == id)
        .map(|(content, _, name)| (*content, *name))
}
