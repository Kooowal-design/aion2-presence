//! Explicit, local-only discovery mode. No packet contents or player data are saved.
use std::{collections::BTreeMap, path::PathBuf, sync::Arc, time::Instant};
use serde_json::json;
use a2tools_dps_meter_lib::capture::stream_processor::read_varint;

#[derive(Default)]
struct Metadata {
    rows: BTreeMap<(u64, String, [u8; 2], usize), u64>,
    dropped: u64,
}
impl Metadata {
    fn observe(&mut self, packet: &[u8], second: u64, phase: &str) {
        let prefix = read_varint(packet, 0);
        let Ok(at) = usize::try_from(prefix.length) else { return };
        if at == 0 { return; }
        let Some(op) = packet.get(at..at + 2) else { return };
        let key = (second, phase.to_owned(), [op[0],op[1]], packet.len()-at-2);
        if let Some(n) = self.rows.get_mut(&key) { *n += 1; }
        else if self.rows.len() < 50_000 { self.rows.insert(key,1); }
        else { self.dropped += 1; }
    }
}
fn phase(text: &str) -> &'static str {
    match text.trim() {
        "idle" => "idle", "crafting" => "crafting", "stopped" => "stopped",
        "done" => "done", _ => "unmarked",
    }
}
pub async fn run(output: PathBuf, marker: PathBuf) -> Result<(), Box<dyn std::error::Error>> {
    use crate::capture::{combat_port_detector::CombatPortDetector, pcap_capturer::PcapCapturer};
    use crate::combat::capture_dispatcher::CaptureDispatcher;
    use a2tools_dps_meter_lib::{combat::{data_storage::DataStorage,ping_tracker::PingTracker},
        i18n::lookup::{NpcLookup,SkillLookup}};
    use std::io::{Seek,Write};
    let mut file = std::fs::OpenOptions::new().write(true).create_new(true).open(output)?;
    let counts = Arc::new(parking_lot::Mutex::new(Metadata::default()));
    let label = Arc::new(parking_lot::Mutex::new("idle"));
    let tap_counts = counts.clone(); let tap_label = label.clone();
    let start = Instant::now();
    a2tools_dps_meter_lib::capture::frame_tap::set(Some(Arc::new(move |packet| {
        if let Some(port) = crate::activity::server_move_port(packet) {
            crate::capture::combat_port_detector::announce_port(port);
        }
        tap_counts.lock().observe(packet,start.elapsed().as_secs(),*tap_label.lock());
    })));
    let detector = Arc::new(CombatPortDetector::new());
    let storage = Arc::new(DataStorage::new());
    let (tx,rx) = tokio::sync::mpsc::channel(4096);
    let capturer = PcapCapturer::new(tx,detector.clone());
    let dispatcher = CaptureDispatcher::new(storage.clone(),Arc::new(SkillLookup::new()),
        Arc::new(NpcLookup::new()),detector.clone(),Arc::new(PingTracker::new()));
    let task = tokio::spawn(async move { dispatcher.run(rx).await; });
    capturer.start();
    let mut ticker = tokio::time::interval(std::time::Duration::from_secs(1));
    loop {
        ticker.tick().await;
        let current = phase(&std::fs::read_to_string(&marker).unwrap_or_default());
        *label.lock() = current;
        let elapsed = start.elapsed().as_secs();
        let data = {
            let counts = counts.lock();
            let rows: Vec<_> = counts.rows.iter().map(|((sec,phase,op,len),count)|
                json!({"second":sec,"phase":phase,"opcode":format!("{:02X} {:02X}",op[0],op[1]),
                    "bodyBytes":len,"count":count})).collect();
            json!({"schema":1,"kind":"craft-metadata-only","elapsedSeconds":elapsed,
                "captureRunning":capturer.is_running(),"portDetected":detector.current_port().is_some(),
                "payloadsRecorded":false,"dropped":counts.dropped,"rows":rows})
        };
        file.rewind()?; file.set_len(0)?;
        serde_json::to_writer_pretty(&mut file,&data)?; file.flush()?;
        if current == "done" || elapsed >= 180 { break; }
        storage.flush_combat_only();
    }
    capturer.stop(); task.abort();
    a2tools_dps_meter_lib::capture::frame_tap::set(None);
    Ok(())
}
#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn metadata_excludes_payload_and_groups_by_phase() {
        let mut m=Metadata::default();
        m.observe(&[8,0x8b,0x8d,1,2,3],1,"idle");
        m.observe(&[8,0x8b,0x8d,9,8,7],1,"idle");
        m.observe(&[8,0x8b,0x8d,9,8,7],2,"crafting");
        assert_eq!(m.rows[&(1,"idle".into(),[0x8b,0x8d],3)],2);
        assert_eq!(m.rows.len(),2);
        assert_eq!(phase("player name"),"unmarked");
    }
    #[test]
    fn invalid_frames_ignored_and_storage_bounded() {
        let mut m=Metadata::default();
        m.observe(&[],0,"idle"); m.observe(&[1],0,"idle");
        assert!(m.rows.is_empty());
        for sec in 0..=50_000 { m.observe(&[4,0x8b,0x8d],sec,"idle"); }
        assert_eq!(m.rows.len(),50_000); assert_eq!(m.dropped,1);
    }
}
