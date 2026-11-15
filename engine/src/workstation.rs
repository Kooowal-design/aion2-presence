//! Workstation lifecycle inferred from a Global potion/cooking capture.
//! No item-name list: attempt tokens and actors are protocol varints.
//! Premium-window production uses token 0 in the observed Global capture.
//! 6F 8D: private per-attempt details (00 05 <recipe> <variable minigame data>)
//! 72 8D: visible start (<actor> <recipe> <profession>)
//! 71 8D: private result (<outcome> 05 <recipe> 00)
//! 74 8D: visible result (<actor> <outcome> 05 <recipe> <profession>)
//! A public start is accepted only for a game-confirmed local actor, or as an
//! immediate matching pair with private details when identity is unavailable.
use a2tools_dps_meter_lib::capture::stream_processor::read_varint;
const ATTEMPT_LEASE: i64 = 60_000;
const BETWEEN_ATTEMPTS: i64 = 2_500;

#[derive(Default)]
pub struct Workstation {
    pending: Option<(u32,i64)>,
    active: Option<(u32,u32,u8,i64)>,
}
fn var(body: &[u8], at: &mut usize) -> Option<u32> {
    let v=read_varint(body,*at);
    if v.length<=0 || v.value<0 { return None; }
    *at=at.checked_add(v.length as usize)?;
    Some(v.value as u32)
}
fn start(body: &[u8]) -> Option<(u32,u32,u8)> {
    let mut at=0;
    let actor=var(body,&mut at).filter(|id|*id>0)?; let recipe=var(body,&mut at)?;
    let profession=*body.get(at)?;
    (at+1==body.len() && (1..=5).contains(&profession)).then_some((actor,recipe,profession))
}
fn private_result(body: &[u8]) -> Option<u32> {
    if *body.first()?>2 || body.get(1)!=Some(&5) { return None; }
    let mut at=2; let recipe=var(body,&mut at)?;
    (body.get(at)==Some(&0) && at+1==body.len()).then_some(recipe)
}
fn public_result(body: &[u8]) -> Option<(u32,u32,u8)> {
    let mut at=0; let actor=var(body,&mut at).filter(|id|*id>0)?;
    if *body.get(at)?>2 || body.get(at+1)!=Some(&5) { return None; }
    at+=2; let recipe=var(body,&mut at)?; let profession=*body.get(at)?;
    (at+1==body.len()).then_some((actor,recipe,profession))
}
impl Workstation {
    pub fn clear(&mut self) { self.pending=None; self.active=None; }
    pub fn is_active(&self, now:i64) -> bool { self.active.is_some_and(|(_,_,_,until)|now<until) }
    pub fn observe(&mut self, op:&[u8], body:&[u8], now:i64) {
        self.observe_with_local(op,body,now,None);
    }
    pub fn observe_with_local(&mut self, op:&[u8], body:&[u8], now:i64, local_actor:Option<i64>) {
        let local_actor=local_actor.and_then(|id|u32::try_from(id).ok()).filter(|id|*id>0);
        if let Some(local)=local_actor {
            if self.active.is_some_and(|(actor,_,_,_)|actor!=local) { self.active=None; }
        }
        // Details and start are adjacent in all 19 observed attempts.
        let pending=self.pending.take();
        match op {
            // Recipe/minigame payload length is variable. The old 24-byte floor
            // was not a protocol requirement; a complete recipe is sufficient
            // here because a separate matching start must still follow it.
            [0x6f,0x8d] if body.starts_with(&[0,5]) => {
                let mut at=2;
                if let Some(recipe)=var(body,&mut at) { self.pending=Some((recipe,now)); }
            }
            [0x72,0x8d] => {
                if let Some((actor,recipe,profession))=start(body) {
                    let own_start=local_actor==Some(actor);
                    let private_pair=local_actor.is_none() && pending.is_some_and(|(private_recipe,at)|
                        private_recipe==recipe && (0..=2_000).contains(&(now-at)));
                    if own_start || private_pair {
                        self.active=Some((actor,recipe,profession,now.saturating_add(ATTEMPT_LEASE)));
                    }
                }
            }
            [0x71,0x8d] => {
                if let (Some((actor,recipe,profession,until)),Some(result))=(self.active,private_result(body)) {
                    if now<until && result==recipe {
                        self.active=Some((actor,recipe,profession,now.saturating_add(BETWEEN_ATTEMPTS)));
                    }
                }
            }
            [0x74,0x8d] => {
                if let (Some((actor,recipe,profession,until)),Some(result))=(self.active,public_result(body)) {
                    if now<until && result==(actor,recipe,profession) {
                        self.active=Some((actor,recipe,profession,now.saturating_add(BETWEEN_ATTEMPTS)));
                    }
                }
            }
            _ => {}
        }
    }
}
/// Offline analysis of a local diagnostic: output only activity transitions,
/// never the private frame payloads, player names or chat from the input.
pub fn replay(path: &std::path::Path) -> Result<serde_json::Value,Box<dyn std::error::Error>> {
    use std::io::BufRead;
    let mut state=Workstation::default();
    let mut transitions=vec![]; let mut previous=false; let mut frames=0u64;
    for line in std::io::BufReader::new(std::fs::File::open(path)?).lines() {
        let row:serde_json::Value=serde_json::from_str(&line?)?;
        if row["kind"]!="frame" { continue; }
        let hex=row["frameHex"].as_str().ok_or("frameHex missing")?;
        if hex.len()%2!=0 || !hex.is_ascii() { return Err("invalid frame hex".into()); }
        let packet:Vec<u8>=(0..hex.len()).step_by(2).map(|at|u8::from_str_radix(&hex[at..at+2],16)).collect::<Result<_,_>>()?;
        let prefix=read_varint(&packet,0);
        let at=usize::try_from(prefix.length).ok().filter(|at|*at>0).ok_or("invalid prefix")?;
        let op=packet.get(at..at+2).ok_or("missing opcode")?;
        let now=row["elapsedMs"].as_i64().ok_or("missing time")?;
        state.observe(op,&packet[at+2..],now); frames+=1;
        let active=state.is_active(now);
        if active!=previous {
            transitions.push(serde_json::json!({"elapsedMs":now,"phase":row["phase"],"crafting":active}));
            previous=active;
        }
    }
    Ok(serde_json::json!({"framesReplayed":frames,"transitions":transitions}))
}
#[cfg(test)]
mod tests {
    use super::*;
    fn vi(mut n:u32)->Vec<u8> {
        let mut b=vec![]; while n>=128 { b.push((n as u8 & 127)|128); n>>=7; } b.push(n as u8); b
    }
    fn details(recipe:u32)->Vec<u8> { let mut b=vec![0,5]; b.extend(vi(recipe)); b.extend([0;30]); b }
    fn begins(actor:u32,recipe:u32,profession:u8)->Vec<u8> { let mut b=vi(actor); b.extend(vi(recipe)); b.push(profession); b }
    fn ends(recipe:u32,outcome:u8)->Vec<u8> { let mut b=vec![outcome,5]; b.extend(vi(recipe)); b.push(0); b }
    #[test]
    fn both_professions_and_arbitrary_recipe_ids_follow_one_lifecycle() {
        for (recipe,profession) in [(35_783,4),(31_351,5),(123_456,4)] {
            let mut s=Workstation::default();
            s.observe(&[0x6f,0x8d],&details(recipe),1_000);
            s.observe(&[0x72,0x8d],&begins(12345,recipe,profession),1_001);
            assert!(s.is_active(10_000));
            s.observe(&[0x71,0x8d],&ends(recipe,0),11_000);
            assert!(s.is_active(12_500)); // auto-repeat gap
            s.observe(&[0x6f,0x8d],&details(recipe),12_600);
            s.observe(&[0x72,0x8d],&begins(12345,recipe,profession),12_600);
            assert!(s.is_active(20_000));
            s.observe(&[0x71,0x8d],&ends(recipe,2),21_000);
            assert!(!s.is_active(23_500)); // cancel, failure or natural last result
        }
    }
    #[test]
    fn another_players_public_events_window_and_morph_do_not_activate() {
        let mut s=Workstation::default();
        for op in [[0x72,0x8d],[0x74,0x8d],[0x81,0x8d],[0x8b,0x8d]] {
            s.observe(&op,&begins(12345,35783,4),1000);
            assert!(!s.is_active(1001));
        }
        s.observe(&[0x6f,0x8d],&details(35783),2000);
        s.observe(&[0x72,0x8d],&begins(99,999,4),2000);
        s.observe(&[0x72,0x8d],&begins(12345,35783,4),2001);
        assert!(!s.is_active(2002));
        s.observe(&[0x6f,0x8d],&details(35783),3000);
        s.observe(&[0,0x36],&[],3001);
        s.observe(&[0x72,0x8d],&begins(12345,35783,4),3001);
        assert!(!s.is_active(3002));
    }
    #[test]
    fn lost_results_expire_and_bad_packets_never_extend_the_lease() {
        let mut s=Workstation::default();
        s.observe(&[0x6f,0x8d],&details(35783),1000);
        s.observe(&[0x72,0x8d],&begins(12345,35783,4),1000);
        s.observe(&[0x71,0x8d],&ends(999,0),2000);
        s.observe(&[0x74,0x8d],&[0xff;20],2000);
        assert!(s.is_active(60_999)); assert!(!s.is_active(61_000));
        s.clear(); assert!(!s.is_active(1001));
        for n in 0..10 {
            s.observe(&[0x6f,0x8d],&[0;9][..n.min(9)],5000);
            s.observe(&[0x72,0x8d],&[0xff;9][..n.min(9)],5000);
            assert!(!s.is_active(5001));
        }
    }
    #[test]
    fn short_private_details_need_a_complete_recipe_and_matching_start() {
        for profession in 1..=5 {
            for length in 3..24 {
                let mut body=vec![0,5,123]; body.resize(length,0);
                let mut s=Workstation::default();
                s.observe(&[0x6f,0x8d],&body,1000);
                assert!(!s.is_active(1001),"details alone must not activate");
                s.observe(&[0x72,0x8d],&begins(15,123,profession),1001);
                assert!(s.is_active(1002),"short payload, profession {profession}, length {length}");
            }
        }
        for body in [vec![0,5],vec![0,5,0x80],vec![0,4,123]] {
            let mut s=Workstation::default();
            s.observe(&[0x6f,0x8d],&body,1000);
            s.observe(&[0x72,0x8d],&begins(15,123,4),1001);
            assert!(!s.is_active(1002));
        }
    }
    #[test]
    fn premium_zero_token_production_repeats_and_finishes_without_menu_events() {
        let mut s=Workstation::default();
        // Anonymized actor and observed relative timing/lengths. The complete
        // private payload is not needed or retained in this regression fixture.
        for (at,length,outcome,duration) in [(1000,118,1,15116),(17637,68,0,5637),(24683,58,0,3684)] {
            let mut body=vec![0,5,0];body.resize(length,0);
            s.observe(&[0x6f,0x8d],&body,at);
            s.observe(&[0x72,0x8d],&begins(15,0,4),at);
            assert!(s.is_active(at+duration-1));
            s.observe(&[0x71,0x8d],&ends(0,outcome),at+duration);
            let mut result=vi(15);result.extend([outcome,5,0,4]);
            s.observe(&[0x74,0x8d],&result,at+duration);
            assert!(s.is_active(at+duration+2499));
        }
        assert!(!s.is_active(30867));
    }
    #[test]
    fn zero_token_keeps_actor_attribution_and_requires_a_start() {
        for profession in 1..=5 {
            let mut s=Workstation::default();
            s.observe(&[0x72,0x8d],&begins(15,0,profession),1000);
            s.observe(&[0x71,0x8d],&ends(0,0),1001);
            assert!(!s.is_active(1002),"unknown public start/result must not activate");
            s.observe_with_local(&[0x6f,0x8d],&details(0),2000,Some(15));
            s.observe_with_local(&[0x72,0x8d],&begins(16,0,profession),2000,Some(15));
            assert!(!s.is_active(2001),"private pair must not override known local actor");
            s.observe_with_local(&[0x72,0x8d],&begins(15,0,profession),3000,Some(15));
            assert!(s.is_active(3001));
            let mut result=vi(16);result.extend([0,5,0,profession]);
            s.observe_with_local(&[0x74,0x8d],&result,4000,Some(15));
            assert!(s.is_active(7000),"another actor's result cannot end local production");
            result=vi(15);result.extend([0,5,0,profession]);
            s.observe_with_local(&[0x74,0x8d],&result,8000,Some(15));
            assert!(s.is_active(10499));assert!(!s.is_active(10500));
            s.clear();
            s.observe(&[0x6f,0x8d],&details(0),11000);
            s.observe(&[0x72,0x8d],&begins(0,0,profession),11000);
            assert!(!s.is_active(11001),"actor 0 remains invalid");
        }
    }
    #[test]
    fn game_confirmed_local_start_does_not_require_private_minigame_details() {
        for profession in 1..=5 {
            let mut s=Workstation::default();
            s.observe_with_local(&[0x72,0x8d],&begins(16,98765,profession),1000,Some(15));
            assert!(!s.is_active(1001),"bystander must not activate");
            s.observe_with_local(&[0x72,0x8d],&begins(15,98765,profession),1001,Some(15));
            assert!(s.is_active(1002));
            s.observe_with_local(&[0x71,0x8d],&ends(98765,0),2000,Some(15));
            assert!(s.is_active(4499));assert!(!s.is_active(4500));
        }
    }
    #[test]
    fn known_local_identity_overrides_a_coincidental_private_pair() {
        let mut s=Workstation::default();
        s.observe_with_local(&[0x6f,0x8d],&details(123),1000,Some(15));
        s.observe_with_local(&[0x72,0x8d],&begins(16,123,4),1001,Some(15));
        assert!(!s.is_active(1002));
        s.observe_with_local(&[0x72,0x8d],&begins(15,456,4),1003,Some(15));
        assert!(s.is_active(1004));
    }
    #[test]
    fn identity_changes_and_malformed_local_starts_cannot_keep_old_crafting() {
        let mut s=Workstation::default();
        s.observe_with_local(&[0x72,0x8d],&begins(15,123,4),1000,Some(15));
        assert!(s.is_active(1001));
        s.observe_with_local(&[0,0x36],&[],1002,Some(16));
        assert!(!s.is_active(1003));
        for body in [vec![15],vec![15,0x80],vec![15,123,0],vec![15,123,6],vec![15,123,4,0]] {
            s.observe_with_local(&[0x72,0x8d],&body,2000,Some(15));
            assert!(!s.is_active(2001));
        }
        for op in [[0x8f,0x8d],[0x8b,0x8d]] {
            s.observe_with_local(&op,&begins(15,123,4),3000,Some(15));
            assert!(!s.is_active(3001),"window and morph must remain excluded");
        }
    }
}

