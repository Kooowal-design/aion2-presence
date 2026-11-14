//! Presence-only state from decoded game frames; raw bytes are never retained.
//! Craft validation follows the GPL upstream StreamProcessor::craft_done.
use a2tools_dps_meter_lib::capture::{loot::{self, Gather}, stream_processor::read_varint};

#[derive(Default)]
pub struct Activity {
    map: Option<i32>,
    action: Option<(&'static str, i64)>,
    festival_until: Option<i64>,
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
        self.festival_until = None;
    }

    pub fn observe(&mut self, packet: &[u8], now: i64) {
        let prefix = read_varint(packet, 0);
        if prefix.length <= 0 { return; }
        let o = prefix.length as usize;
        let Some(op) = packet.get(o..o + 2) else { return; };
        let body = &packet[o + 2..];
        if server_move_port(packet).is_some()
            || (op == [0x23, 0x36] && body.first() == Some(&0)) {
            self.leave_location();
        }
        if op == [0x21, 0x36] && body.len() >= 8 {
            let map = i32::from_le_bytes(body[4..8].try_into().unwrap());
            if self.map != Some(map) { self.action = None; self.festival_until = None; }
            self.map = Some(map);
        }
        // A confirmed participant run, not the 01 60 world-wide schedule.
        // 142019 is the Shugo run observed by the pinned upstream parser.
        if op == [0x00, 0x61] && body.len() == 21 {
            let content = u32::from_le_bytes(body[..4].try_into().unwrap());
            let state = body[4];
            let started = i64::from_le_bytes(body[5..13].try_into().unwrap());
            let deadline = i64::from_le_bytes(body[13..21].try_into().unwrap());
            if self.dungeon_id() > 0 && self.dungeon_id() == content as i32 && state == 4 && started != 0 {
                self.leave_location();
            }
            self.festival_until = if content == 142_019 && (state == 1 || state == 2) && started > 0
                && (deadline == 0 || deadline > now) {
                Some(if deadline > now { deadline.min(now + 20 * 60_000) } else { now + 10 * 60_000 })
            } else { None };
        }
        if let Some(gather) = loot::gather(packet) {
            match gather {
                Gather::Started { .. } => self.action = Some(("Gathering", now + 15_000)),
                Gather::Done { .. } => self.action = Some(("Gathering", now + 8_000)),
                Gather::Refused { .. } => {
                    if self.action.is_some_and(|(kind, _)| kind == "Gathering") { self.action = None; }
                }
            }
        }
        // A complete craft result, not the recipe list sent when opening a window.
        if op == [0x8B, 0x8D] && body.len() == 41 && body[..3] == [0, 0, 1] {
            let number = |at: usize| u32::from_le_bytes(body[at..at + 4].try_into().unwrap());
            let recipe = number(3);
            if recipe == number(9) && (1..=99_999_999).contains(&recipe)
                && number(33).saturating_add(number(37)) <= 10_000 {
                self.action = Some(("Crafting", now + 20_000));
            }
        }
    }

    pub fn describe(&self, base: &str, dungeon: i64, now: i64) -> String {
        // Combat takes precedence over a recent completed craft or gather.
        if base.contains("Fighting ") { return base.to_string(); }
        let place = if dungeon > 0 && base != "In game" { Some(base) }
            else if self.festival_until.is_some_and(|until| now < until) { Some("Shugo Festival") }
            else { None };
        let action = self.action.filter(|(_, until)| now < *until).map(|(kind, _)| kind);
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
    fn recipe_window_never_means_crafting_and_malformed_results_are_ignored() {
        let mut s = Activity::default();
        s.observe(&packet([0x8F,0x8D], &[0; 41]), 1_000);
        s.observe(&packet([0x8B,0x8D], &[0; 41]), 1_000);
        assert_eq!(s.describe("In game", 0, 1_001), "In game");
        let mut body = vec![0;41]; body[2]=1;
        body[3..7].copy_from_slice(&32_070_001u32.to_le_bytes());
        body[9..13].copy_from_slice(&32_070_001u32.to_le_bytes()); body[33]=1;
        s.observe(&packet([0x8B,0x8D], &body), 1_000);
        assert_eq!(s.describe("In game", 0, 2_000), "Crafting");
        assert_eq!(s.describe("In game", 0, 21_000), "In game");
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
        let mut body=vec![]; body.extend(142_019u32.to_le_bytes()); body.push(2);
        body.extend(1_000i64.to_le_bytes()); body.extend(0i64.to_le_bytes());
        s.observe(&packet([0x00,0x61],&body),1_000);
        assert_eq!(s.describe("In game",0,2_000),"Shugo Festival");
        assert_eq!(s.describe("In game",0,601_000),"In game");
        body[4]=3; s.observe(&packet([0x00,0x61],&body),3_000);
        assert_eq!(s.describe("In game",0,3_001),"In game");
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
