//! Standalone capture worker. No Tauri, A2Tools process, overlays or uploads.
//! Raw game-stream recording requires the separate explicit --craft-probe mode.
//! Parser + capture pipeline reused under GPL-3.0 from taengu/A2Tools-DPS-Meter.
use std::sync::Arc;
use std::time::{SystemTime, UNIX_EPOCH};
use std::io::Write;
use serde_json::{Value, json};
use a2tools_dps_meter_lib::{combat::data_storage::DataStorage, i18n::lookup::{NpcLookup, SkillLookup}};
mod activity;
mod shugo;
mod craft_probe;
mod workstation;

pub use a2tools_dps_meter_lib::{clock, i18n};
mod capture {
    pub use a2tools_dps_meter_lib::capture::{captured_payload, stream_assembler, stream_processor, tcp_order};
    pub mod combat_port_detector { include!("../../vendor/a2tools/src-tauri/src/capture/combat_port_detector.rs"); }
    pub mod pcap_capturer { include!("../../vendor/a2tools/src-tauri/src/capture/pcap_capturer.rs"); }
}
mod combat {
    pub use a2tools_dps_meter_lib::combat::{data_storage, ping_tracker};
    pub mod capture_dispatcher { include!("../../vendor/a2tools/src-tauri/src/combat/capture_dispatcher.rs"); }
}
mod platform {
    #[path = "../../../vendor/a2tools/src-tauri/src/platform/win32/window_detector.rs"]
    pub mod window_detector;
    pub mod pcap;
}
// Only explicit --craft-probe enables a local diagnostic sink.
mod logging { pub mod logger { pub fn log_packet(cap: &crate::capture::captured_payload::CapturedPayload) { crate::craft_probe::record_stream(cap); } } }
mod share { pub mod ring { pub fn record(_: &crate::capture::captured_payload::CapturedPayload) {} } }

fn now_ms() -> i64 { SystemTime::now().duration_since(UNIX_EPOCH).unwrap_or_default().as_millis() as i64 }
fn trusted_crafting_actor(storage:&DataStorage)->Option<i64> {
    // Loot-derived guesses can belong to a bystander; only game/self identity
    // can attribute a public crafting start to the local character.
    if storage.local_identity_from_self_record() { storage.local_player_id() } else { None }
}
fn english_class(class: a2tools_dps_meter_lib::entity::job_class::JobClass) -> &'static str {
    use a2tools_dps_meter_lib::entity::job_class::JobClass::*;
    match class { Gladiator=>"Gladiator", Templar=>"Templar", Ranger=>"Ranger", Assassin=>"Assassin", Sorcerer=>"Sorcerer", Cleric=>"Cleric", Elementalist=>"Spiritmaster", Chanter=>"Chanter", Fighter=>"Brawler" }
}
fn snapshot(storage: &DataStorage, npcs: &NpcLookup, dungeons: &Value, servers: &Value, running: bool, port: Option<u16>, device: Option<String>, capture_ok: bool, dungeon: i32) -> Value {
    let p = storage.local_profile();
    // Location is confirmed by presence map frames, never a stale party roster.
    let mut activity = String::new();
    let target = storage.current_target();
    if let Some(code) = storage.mob_code(target) {
        if npcs.is_boss(code) || npcs.is_training_dummy(code) {
            if let Some(fight) = storage.get_target_snapshot_light(target) {
                if now_ms() - fight.last_damage_time <= 20_000 {
                    let name = npcs.get_npc_name(code);
                    if !name.is_empty() { activity = format!("Fighting {name}"); }
                }
            }
        }
    }
    if dungeon > 0 {
        if let Some(name) = dungeons.get(dungeon.to_string()).and_then(|v| v.get("name")).and_then(Value::as_str) {
            let group = dungeon - dungeon % 10;
            let siblings = dungeons.as_object().map_or(0, |table| table.keys().filter_map(|key|key.parse::<i32>().ok()).filter(|id| id-id%10==group).count());
            let difficulty = if siblings >= 9 { "" } else { match dungeon % 10 { 1=>"Expedition", 2=>"Conquest Normal", 3=>"Conquest Hard", _=>"" } };
            let place = if difficulty.is_empty() { name.to_string() } else { format!("{name} ({difficulty})") };
            activity = if activity.is_empty() { place } else { format!("{place} · {activity}") };
        }
    }
    // Gathering and event composition are added by Activity below.
    if activity.is_empty() { activity = "In game".into(); }
    let server = servers.get("servers").and_then(|s| s.get(p.server_id.to_string())).and_then(|s| s.get("en")).and_then(Value::as_str).unwrap_or("");
    json!({ "schema":1, "updatedAt":now_ms(), "running":running, "captureOk":capture_ok, "port":port, "device":device,
        "activity":activity, "dungeonId":dungeon, "server":server,
        "character": p.name.as_ref().filter(|n|!n.trim().is_empty()).map(|name| json!({"name":name,"class":p.class.map(english_class),"serverId":p.server_id,"level":p.level,"combatPower":p.combat_power,"gearScore":p.gear_score})) })
}

#[tokio::main]
async fn main() {
    tracing_subscriber::fmt().with_writer(std::io::stderr).with_env_filter("warn").without_time().with_ansi(false).init();
    let args: Vec<String> = std::env::args().collect();
    if args.len()==3 && args[1]=="--replay-crafting" {
        match workstation::replay(std::path::Path::new(&args[2])) {
            Ok(result)=>println!("{result}"),
            Err(error)=>{eprintln!("Replay failed: {error}");std::process::exit(1);}
        }
        return;
    }
    if args.get(1).is_some_and(|a| a == "--craft-probe") {
        if args.len() != 4 {
            eprintln!("Usage: aion-presence-engine --craft-probe OUTPUT.json PHASE.txt");
            std::process::exit(2);
        }
        if let Err(error) = craft_probe::run(args[2].clone().into(),args[3].clone().into()).await {
            eprintln!("Craft diagnostic failed: {error}"); std::process::exit(1);
        }
        return;
    }
    if std::env::args().any(|a|a=="--probe") {
        match capture::pcap_capturer::list_device_labels() {
            Ok(devices)=>println!("{}",json!({"npcap":true,"devices":devices})),
            Err(error)=>{ println!("{}",json!({"npcap":false,"error":error})); std::process::exit(1); }
        }
        return;
    }
    let storage = Arc::new(DataStorage::new());
    let activities = Arc::new(parking_lot::Mutex::new(activity::Activity::default()));
    let activity_tap = activities.clone();
    let activity_storage = storage.clone();
    a2tools_dps_meter_lib::capture::frame_tap::set(Some(Arc::new(move |packet| {
        if let Some(port) = activity::server_move_port(packet) {
            capture::combat_port_detector::announce_port(port);
        }
        activity_tap.lock().observe_with_local(packet, now_ms(), trusted_crafting_actor(&activity_storage));
    })));
    let skills = Arc::new(SkillLookup::new());
    let npcs = Arc::new(NpcLookup::new());
    skills.load_from_json(include_str!("../../vendor/a2tools/src/data/i18n/skills/en.json"));
    npcs.load_from_json(include_str!("../../vendor/a2tools/src/data/i18n/npcs/en.json"));
    let dungeons: Value = serde_json::from_str(include_str!("../../vendor/a2tools/src/data/i18n/dungeons/en.json")).expect("dungeon catalog");
    let servers: Value = serde_json::from_str(include_str!("../../vendor/a2tools/src/data/servers.json")).expect("server catalog");
    let detector = Arc::new(capture::combat_port_detector::CombatPortDetector::new());
    let ping = Arc::new(combat::ping_tracker::PingTracker::new());
    let (tx,rx) = tokio::sync::mpsc::channel(4096);
    let capturer = capture::pcap_capturer::PcapCapturer::new(tx, detector.clone());
    let mut target_selector = a2tools_dps_meter_lib::combat::dps_calculator::DpsCalculator::new(storage.clone(), skills.clone(), npcs.clone(), ping.clone());
    let dispatcher = combat::capture_dispatcher::CaptureDispatcher::new(storage.clone(), skills, npcs.clone(), detector.clone(), ping);
    tokio::spawn(async move { dispatcher.run(rx).await; });
    capturer.start();
    let mut ticker = tokio::time::interval(std::time::Duration::from_secs(2));
    let mut was_running = false;
    let mut last_cleanup = now_ms();
    loop {
        ticker.tick().await;
        let running = platform::window_detector::find_aion2_window();
        if !running && was_running {
            *activities.lock() = activity::Activity::default();
            storage.flush();
            storage.set_local_character_name(None);
            storage.set_local_player_id(None);
            detector.reset();
        }
        was_running = running;
        // The upstream app normally runs this in its meter refresh. The headless
        // worker must also select the boss before the presence can name it.
        let _ = target_selector.get_dps();
        let mut data = snapshot(&storage, &npcs, &dungeons, &servers, running, detector.current_port(), detector.current_device(), capturer.is_running(), activities.lock().dungeon_id());
        let activity = activities.lock().describe(data["activity"].as_str().unwrap_or("In game"), data["dungeonId"].as_i64().unwrap_or(0), now_ms());
        data["activity"] = json!(activity);
        let output = serde_json::to_string(&data).expect("snapshot JSON");
        if writeln!(std::io::stdout(), "{output}").is_err() { break; }
        let _ = std::io::stdout().flush();
        // Presence needs a short combat window, never a growing fight history.
        if now_ms() - last_cleanup > 60_000 {
            storage.flush_combat_only();
            last_cleanup = now_ms();
        }
    }
    capturer.stop();
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn snapshot_keeps_own_stats_and_uses_known_activity_only() {
        use a2tools_dps_meter_lib::entity::job_class::JobClass;
        let storage = DataStorage::new();
        storage.set_local_identity_from_game(42, Some("Kowal".into()));
        storage.note_self_profile("Kowal", Some(JobClass::Gladiator), Some(50));
        storage.note_self_combat_power("Kowal", 123450);
        storage.note_self_gear_score("Kowal", 2050);
        let data = snapshot(&storage, &NpcLookup::new(), &json!({}), &json!({}), true, None, None, true, 0);
        assert_eq!(data["character"]["name"], "Kowal");
        assert_eq!(data["character"]["class"], "Gladiator");
        assert_eq!(data["character"]["combatPower"], 123450);
        assert_eq!(data["character"]["gearScore"], 2050);
        assert_eq!(data["activity"], "In game");
    }
    #[test]
    fn unknown_identity_never_fabricates_a_character() {
        let data = snapshot(&DataStorage::new(), &NpcLookup::new(), &json!({}), &json!({}), true, None, None, true, 0);
        assert!(data["character"].is_null());
    }
    #[test]
    fn public_crafting_attribution_requires_game_confirmed_identity() {
        let storage=DataStorage::new();
        storage.set_local_player_id(Some(15));
        assert_eq!(trusted_crafting_actor(&storage),None,"an unconfirmed actor id is insufficient");
        storage.set_local_identity_from_game(15,Some("TestCharacter".into()));
        assert_eq!(trusted_crafting_actor(&storage),Some(15));
        storage.set_local_player_id(None);
        assert_eq!(trusted_crafting_actor(&storage),None);
    }
    #[test]
    fn stale_party_dungeon_is_never_published_as_current_location() {
        let storage = DataStorage::new();
        storage.set_current_dungeon(600062);
        let catalog = json!({"600062":{"name":"Shattered Arkanis"}});
        let data = snapshot(&storage, &NpcLookup::new(), &catalog, &json!({}), true, None, None, true, 0);
        assert_eq!(data["activity"], "In game");
        assert_eq!(data["dungeonId"], 0);
        assert_eq!(storage.current_dungeon_id(), 600062, "the stale parser fallback still exists but is not used by presence");
        let confirmed = snapshot(&storage, &NpcLookup::new(), &catalog, &json!({}), true, None, None, true, 600062);
        assert_eq!(confirmed["activity"], "Shattered Arkanis (Conquest Normal)");
    }
    #[test]
    fn captured_self_record_reaches_presence_through_stream_parser() {
        use a2tools_dps_meter_lib::capture::{stream_processor::StreamProcessor, framing::length_value};
        // A2Tools' public, shortened protocol fixture (self identity + gear score).
        let hex = "3336ed745e91c12837064e616963686118051e000000011c0000007f0100007f0100001c000000d002040000000000";
        let record: Vec<u8> = (0..hex.len()).step_by(2).map(|i|u8::from_str_radix(&hex[i..i+2],16).unwrap()).collect();
        let mut len = length_value(record.len());
        let mut frame = Vec::new();
        while len >= 128 { frame.push((len as u8) | 128); len >>= 7; }
        frame.push(len as u8); frame.extend(record);
        let storage = Arc::new(DataStorage::new());
        let npcs = Arc::new(NpcLookup::new());
        let mut parser = StreamProcessor::new(storage.clone(), Arc::new(SkillLookup::new()), npcs.clone());
        assert_eq!(parser.consume_stream(&frame),frame.len());
        let data = snapshot(&storage, &npcs, &json!({}), &json!({}), true, None, None, true, 0);
        assert_eq!(data["character"]["name"],"Naicha");
        assert_eq!(data["character"]["class"],"Cleric");
        assert_eq!(data["character"]["gearScore"],383);
        assert_eq!(data["character"]["level"],28);
    }
    #[test]
    fn fighting_activity_works_without_a_meter_window() {
        use a2tools_dps_meter_lib::{combat::{dps_calculator::DpsCalculator,ping_tracker::PingTracker}, entity::damage_packet::ParsedDamagePacket};
        let storage=Arc::new(DataStorage::new());
        storage.set_local_identity_from_game(42,Some("Kowal".into()));
        let npcs=Arc::new(NpcLookup::new());
        npcs.load_from_json(r#"{"777":{"name":"Test Boss","isBoss":true}}"#);
        storage.append_mob(50000,777);
        let mut hit=ParsedDamagePacket::new();
        hit.set_actor_id(42); hit.set_target_id(50000); hit.set_damage(100); hit.set_skill_code(11010000);
        storage.append_damage(hit);
        let mut selector=DpsCalculator::new(storage.clone(),Arc::new(SkillLookup::new()),npcs.clone(),Arc::new(PingTracker::new()));
        let _=selector.get_dps();
        let data=snapshot(&storage,&npcs,&json!({}),&json!({}),true,None,None,true,0);
        assert_eq!(data["activity"],"Fighting Test Boss");
        storage.set_current_dungeon(600012);
        let data=snapshot(&storage,&npcs,&json!({"600012":{"name":"Fire Temple"}}),&json!({}),true,None,None,true,600012);
        assert_eq!(data["activity"],"Fire Temple (Conquest Normal) · Fighting Test Boss");
    }
}
