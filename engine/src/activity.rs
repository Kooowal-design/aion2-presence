//! Presence-only state from decoded game frames; raw bytes are never retained.
use a2tools_dps_meter_lib::capture::{loot::{self, Gather}, stream_processor::read_varint};

#[derive(Default)]
pub struct Activity {
    map: Option<i32>,
    action: Option<(&'static str, i64)>,
    festival: Option<(u32, &'static str, i64)>,
    ended_festival: Option<u32>,
    workstation: crate::workstation::Workstation,
}

impl Activity {
    // A party roster can keep naming a dungeon long after the player leaves.
    // Presence requires an actual map load; unknown/transition is not a place.
    pub fn dungeon_id(&self) -> i32 {
        self.map.filter(|id| (600_000..700_000).contains(id)).unwrap_or(0)
    }

    fn leave_location(&mut self) {
        self.map = None;
        self.action = None;
        self.workstation.clear();
        self.festival = None;
    }

    pub fn observe(&mut self, packet: &[u8], now: i64) {
        self.observe_with_local(packet,now,None);
    }
    pub fn observe_with_local(&mut self, packet: &[u8], now: i64, local_actor:Option<i64>) {
        let prefix = read_varint(packet, 0);
        if prefix.length <= 0 { return; }
        let o = prefix.length as usize;
        let Some(op) = packet.get(o..o + 2) else { return; };
        let body = &packet[o + 2..];
        self.workstation.observe_with_local(op,body,now,local_actor);
        if self.workstation.is_active(now) { self.action=None; }
        if server_move_port(packet).is_some()
            || (op == [0x23, 0x36] && body.first() == Some(&0)) {
            self.leave_location();
        }
        if op == [0x21, 0x36] && body.len() >= 8 {
            let map = i32::from_le_bytes(body[4..8].try_into().unwrap());
            if self.map != Some(map) {
                self.action = None;
                self.workstation.clear();
                self.festival = match crate::shugo::game(map) {
                    Some((id, name)) if self.ended_festival != Some(id) => Some((id, name, now + 20 * 60_000)),
                    _ => None,
                };
                if crate::shugo::game(map).is_none() { self.ended_festival = None; }
            }
            self.map = Some(map);
        }
        // A participant run, not the 01 60 world-wide schedule. 142019 was
        // incorrectly used in earlier releases; actual Shugo ids are 500035–43.
        if op == [0x00, 0x61] && body.len() == 21 {
            let content = u32::from_le_bytes(body[..4].try_into().unwrap());
            let state = body[4];
            let started = i64::from_le_bytes(body[5..13].try_into().unwrap());
            let deadline = i64::from_le_bytes(body[13..21].try_into().unwrap());
            if self.dungeon_id() > 0 && self.dungeon_id() == content as i32 && state == 4 && started != 0 {
                self.leave_location();
            }
            if let Some((id, name)) = crate::shugo::game(content as i32) {
                if (state == 1 || state == 2) && started > 0 && (deadline == 0 || deadline > now) {
                    self.ended_festival = None;
                    self.festival = Some((id, name,
                        if deadline > now { deadline.min(now + 20 * 60_000) } else { now + 10 * 60_000 }));
                } else if (state == 3 || state == 4) && started != 0
                    && (self.festival.is_some_and(|(active, _, _)| active == id)
                        || self.map.and_then(crate::shugo::game).is_some_and(|(active, _)| active == id)) {
                    self.festival = None;
                    self.ended_festival = Some(id);
                }
            }
        }
        if let Some(gather) = loot::gather(packet) {
            match gather {
                Gather::Started { .. } => { self.workstation.clear(); self.action = Some(("Gathering", now + 15_000)); }
                Gather::Done { .. } => self.action = Some(("Gathering", now + 8_000)),
                Gather::Refused { .. } => {
                    if self.action.is_some_and(|(kind, _)| kind == "Gathering") { self.action = None; }
                }
            }
        }
        // Substance Morph (8B 8D) is deliberately excluded from workstation state.
    }

    pub fn describe(&self, base: &str, dungeon: i64, now: i64) -> String {
        // Combat takes precedence over recent gathering.
        let festival = self.festival.filter(|(_, _, until)| now < *until)
            .map(|(_, name, _)| format!("Shugo Festival · {name}"));
        if base.contains("Fighting ") {
            return if dungeon <= 0 && festival.is_some() { format!("{} · {base}", festival.unwrap()) }
                else { base.to_string() };
        }
        let place = if dungeon > 0 && base != "In game" { Some(base) }
            else if festival.is_some() { festival.as_deref() }
            else { None };
        let action = if self.workstation.is_active(now) { Some("Crafting") }
            else { self.action.filter(|(_, until)| now < *until).map(|(kind, _)| kind) };
        match (place, action) {
            (Some(place), Some(action)) => format!("{place} · {action}"),
            (Some(place), None) => place.into(),
            (None, Some(action)) => action.into(),
            _ => base.into(),
        }
    }
}

/// The parser core omits the desktop callback. Preserve capture handoff without
/// retaining the address or treating a server change as proof of an event.
pub fn server_move_port(packet: &[u8]) -> Option<u16> {
    let prefix = read_varint(packet, 0);
    let at = usize::try_from(prefix.length).ok().filter(|&n| n > 0)?;
    if packet.get(at..at + 2)? != [0x16, 0x36] { return None; }
    let n = *packet.get(at + 4)? as usize;
    if !(7..=15).contains(&n) { return None; }
    let ip = packet.get(at + 5..at + 5 + n)?;
    if !ip.iter().all(|b| b.is_ascii_digit() || *b == b'.') || ip.iter().filter(|&&b| b == b'.').count() != 3 { return None; }
    let port = u16::from_le_bytes(packet.get(at + 5 + n..at + 7 + n)?.try_into().ok()?);
    (port != 0).then_some(port)
}

#[cfg(test)]
mod tests {
    use super::*;
    fn packet(op: [u8; 2], body: &[u8]) -> Vec<u8> {
        let mut p = vec![(body.len() + 6) as u8]; p.extend(op); p.extend(body); p
    }
    fn map(state: &mut Activity, id: i32) {
        let mut body = vec![0; 8]; body[4..8].copy_from_slice(&id.to_le_bytes());
        state.observe(&packet([0x21, 0x36], &body), 1_000);
    }
    #[test]
    fn gathering_expires_and_refusal_clears_it() {
        let mut s = Activity::default();
        s.observe(&packet([0x31,0x8D], &[0,0,16,1,0,0,0]), 1_000);
        assert_eq!(s.describe("In game", 0, 2_000), "Gathering");
        assert_eq!(s.describe("In game", 0, 16_000), "In game");
        s.observe(&packet([0x31,0x8D], &[4,0x3a,16,0,0,0,0]), 2_000);
        assert_eq!(s.describe("In game", 0, 2_001), "In game");
    }
    #[test]
    fn workstation_crafting_composes_with_combat_and_clears_on_map_change() {
        let mut s=Activity::default();
        let mut details=vec![0,5,123]; details.extend([0;30]);
        s.observe(&packet([0x6f,0x8d],&details),1000);
        s.observe(&packet([0x72,0x8d],&[15,123,4]),1000);
        assert_eq!(s.describe("In game",0,1001),"Crafting");
        assert_eq!(s.describe("Fighting Boss",0,1001),"Fighting Boss");
        s.observe(&packet([0x71,0x8d],&[0,5,123,0]),2000);
        assert_eq!(s.describe("In game",0,4499),"Crafting");
        assert_eq!(s.describe("In game",0,4500),"In game");
        s.observe(&packet([0x6f,0x8d],&details),5000);
        s.observe(&packet([0x72,0x8d],&[15,123,4]),5000);
        map(&mut s,1010);
        assert_eq!(s.describe("In game",0,5001),"In game");
    }
    #[test]
    fn confirmed_local_crafting_without_private_details_still_respects_location_and_gathering() {
        let mut s=Activity::default();
        let start=packet([0x72,0x8d],&[15,123,3]);
        s.observe_with_local(&start,1000,Some(15));
        assert_eq!(s.describe("In game",0,1001),"Crafting");
        assert_eq!(s.describe("Fighting Boss",0,1001),"Fighting Boss");
        s.observe_with_local(&packet([0x31,0x8d],&[0,0,16,1,0,0,0]),2000,Some(15));
        assert_eq!(s.describe("In game",0,2001),"Gathering");
        s.observe_with_local(&start,3000,Some(15));
        assert_eq!(s.describe("In game",0,3001),"Crafting");
        map(&mut s,1010);
        assert_eq!(s.describe("In game",0,3002),"In game");
        s.observe_with_local(&start,4000,Some(16));
        assert_eq!(s.describe("In game",0,4001),"In game");
    }
    #[test]
    fn premium_zero_token_crafting_works_without_any_menu_or_keyboard_signal() {
        let mut s=Activity::default();
        s.observe(&packet([0x6f,0x8d],&[0,5,0]),1000);
        s.observe(&packet([0x72,0x8d],&[15,0,4]),1000);
        assert_eq!(s.describe("In game",0,1001),"Crafting");
        assert_eq!(s.describe("Fighting Boss",0,1001),"Fighting Boss");
        s.observe(&packet([0x71,0x8d],&[0,5,0,0]),2000);
        assert_eq!(s.describe("In game",0,4499),"Crafting");
        assert_eq!(s.describe("In game",0,4500),"In game");
        s.observe_with_local(&packet([0x72,0x8d],&[15,0,4]),5000,Some(15));
        assert_eq!(s.describe("In game",0,5001),"Crafting");
        map(&mut s,1010);
        assert_eq!(s.describe("In game",0,5002),"In game");
    }
    #[test]
    fn morph_results_and_recipe_windows_never_activate_crafting() {
        let mut s = Activity::default();
        s.observe(&packet([0x8F,0x8D], &[0; 41]), 1_000);
        s.observe(&packet([0x8B,0x8D], &[0; 41]), 1_000);
        assert_eq!(s.describe("In game", 0, 1_001), "In game");
        // Upstream's actual Odyle Energy morph-completion fixture.
        let hex = "2f8b8d0000017259e90150057259e90101000000010000000000000000000000000000000000000001000000";
        let morph: Vec<u8> = (0..hex.len()).step_by(2)
            .map(|i| u8::from_str_radix(&hex[i..i+2],16).unwrap()).collect();
        for now in [1_000, 2_000, 3_000] {
            s.observe(&morph, now);
            assert_eq!(s.describe("In game", 0, now + 1), "In game");
        }
        s.observe(&packet([0x31,0x8D], &[0,0,16,1,0,0,0]), 4_000);
        s.observe(&morph, 4_001);
        assert_eq!(s.describe("In game", 0, 4_002), "Gathering",
            "a morph must not replace another confirmed activity");
    }
    #[test]
    fn map_changes_clear_actions_locations_are_not_published_and_bosses_win() {
        let mut s = Activity::default(); map(&mut s,1010);
        s.observe(&packet([0x31,0x8D], &[0,0,16,1,0,0,0]), 1_000);
        assert_eq!(s.describe("In game",0,2_000),"Gathering");
        assert_eq!(s.describe("Temple · Fighting Boss",600021,2_000),"Temple · Fighting Boss");
        map(&mut s,1110); assert_eq!(s.describe("In game",0,2_000),"In game");
        map(&mut s,20); assert_eq!(s.describe("In game",0,2_000),"In game");
        map(&mut s,999999); assert_eq!(s.describe("In game",0,2_000),"In game");
    }
    #[test]
    fn festival_requires_participation_not_a_schedule_or_server_move() {
        let mut s = Activity::default();
        s.observe(&packet([0x01,0x60], &[3,1,2,3,2]),1_000);
        assert_eq!(s.describe("In game",0,2_000),"In game");
        let mut body=vec![]; body.extend(500_043u32.to_le_bytes()); body.push(2);
        body.extend(1_000i64.to_le_bytes()); body.extend(0i64.to_le_bytes());
        s.observe(&packet([0x00,0x61],&body),1_000);
        assert_eq!(s.describe("In game",0,2_000),"Shugo Festival · Defend Shugo Merchants");
        assert_eq!(s.describe("In game",0,601_000),"In game");
        body[4]=3; s.observe(&packet([0x00,0x61],&body),3_000);
        assert_eq!(s.describe("In game",0,3_001),"In game");
    }
    #[test]
    fn all_nine_games_and_both_factions_are_recognized_from_map_loads() {
        for (content, other, name) in crate::shugo::GAMES {
            for id in [content as i32, other] {
                let mut s = Activity::default(); map(&mut s, id);
                assert_eq!(s.describe("In game", 0, 2_000), format!("Shugo Festival · {name}"));
                map(&mut s, 1010);
                assert_eq!(s.describe("In game", 0, 2_000), "In game", "returning clears {id}");
            }
        }
    }
    #[test]
    fn entry_map_restores_event_after_handoff_and_unrelated_runs_do_not_clear_it() {
        let mut s = Activity::default();
        map(&mut s, 1010);
        let mut body = vec![]; body.extend(500043u32.to_le_bytes()); body.push(2);
        body.extend(1_000i64.to_le_bytes()); body.extend(0i64.to_le_bytes());
        s.observe(&packet([0x00, 0x61], &body), 1_000);
        s.observe(&packet([0x23, 0x36], &[0]), 1_001);
        map(&mut s, 500043);
        assert!(s.describe("In game", 0, 2_000).contains("Defend Shugo Merchants"));
        body[..4].copy_from_slice(&600062u32.to_le_bytes());
        s.observe(&packet([0x00, 0x61], &body), 2_001);
        assert!(s.describe("In game", 0, 2_002).contains("Defend Shugo Merchants"));
        assert!(s.describe("Fighting Thieves", 0, 2_003).starts_with("Shugo Festival · Defend Shugo Merchants"));
    }
    #[test]
    fn actual_upstream_completion_fixture_clears_defend_and_old_wrong_id_never_activates_it() {
        let mut s = Activity::default(); map(&mut s, 500043);
        let hex = "1700614ba10700037acd12d0a1010000a26d13d0a1010000";
        let bytes: Vec<u8> = (0..hex.len()).step_by(2).map(|i| u8::from_str_radix(&hex[i..i+2],16).unwrap()).collect();
        s.observe(&bytes, 2_000);
        map(&mut s, 500043);
        assert_eq!(s.describe("In game", 0, 2_001), "In game", "same-map refresh cannot resurrect a finished event");
        map(&mut s, 142019);
        let hex = "170061c32a02000246c2f41ea10100000000000000000000";
        let bytes: Vec<u8> = (0..hex.len()).step_by(2).map(|i| u8::from_str_radix(&hex[i..i+2],16).unwrap()).collect();
        s.observe(&bytes, 2_000);
        assert_eq!(s.describe("In game", 0, 2_001), "In game");
    }
    #[test]
    fn handoff_port_is_read_but_not_reported_as_activity() {
        let mut body=vec![0xef,0x10,14]; body.extend(b"193.202.112.81"); body.extend(13328u16.to_le_bytes()); body.extend([0;8]);
        let p=packet([0x16,0x36],&body);
        assert_eq!(server_move_port(&p),Some(13328));
        assert_eq!(server_move_port(&p[..10]),None);
        let mut s=Activity::default(); s.observe(&p,1_000);
        assert_eq!(s.describe("In game",0,2_000),"In game");
    }

    #[test]
    fn location_requires_map_and_leaving_clears_it_before_the_next_teleport() {
        let mut s = Activity::default();
        assert_eq!(s.dungeon_id(), 0);
        map(&mut s, 600062);
        assert_eq!(s.dungeon_id(), 600062);
        let mut run = vec![];
        run.extend(600062u32.to_le_bytes()); run.push(4);
        run.extend(1_000i64.to_le_bytes()); run.extend(0i64.to_le_bytes());
        s.observe(&packet([0x00, 0x61], &run), 2_000);
        assert_eq!(s.dungeon_id(), 0, "leaving a run needs no later open-world TP");
        run[4] = 2;
        s.observe(&packet([0x00, 0x61], &run), 3_000);
        assert_eq!(s.dungeon_id(), 0, "run/party updates do not prove current location");
        map(&mut s, 600062);
        map(&mut s, 1010);
        assert_eq!(s.dungeon_id(), 0);
        map(&mut s, 999999);
        assert_eq!(s.dungeon_id(), 0, "unknown maps never inherit a dungeon");
    }

    #[test]
    fn local_transition_and_server_handoff_invalidate_old_dungeon() {
        let mut s = Activity::default(); map(&mut s, 600062);
        s.observe(&packet([0x23, 0x36], &[7, 0, 0]), 1_000);
        assert_eq!(s.dungeon_id(), 600062, "another entity moving is not our teleport");
        s.observe(&packet([0x23, 0x36], &[0, 0, 0]), 2_000);
        assert_eq!(s.dungeon_id(), 0);
        map(&mut s, 600062);
        assert_eq!(s.dungeon_id(), 600062, "a confirmed internal teleport restores location");
        let mut body = vec![0xef, 0x10, 14];
        body.extend(b"193.202.112.81"); body.extend(13328u16.to_le_bytes());
        s.observe(&packet([0x16, 0x36], &body), 3_000);
        assert_eq!(s.dungeon_id(), 0);
    }
}
