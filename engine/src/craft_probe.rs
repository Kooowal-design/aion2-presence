//! Explicit opt-in capture. Normal Presence never opens these files.
use std::{collections::BTreeMap, io::{BufWriter, Write}, path::PathBuf, sync::{Arc, OnceLock}, time::Instant};
use serde_json::{json, Value};
use a2tools_dps_meter_lib::capture::{captured_payload::CapturedPayload, stream_processor::read_varint};
const MAX_BYTES: u64 = 128 * 1024 * 1024;
const MAX_SECONDS: u64 = 1200;
type Shared = Arc<parking_lot::Mutex<Recorder>>;
static ACTIVE: OnceLock<parking_lot::Mutex<Option<Shared>>> = OnceLock::new();

struct Recorder {
    file: BufWriter<std::fs::File>,
    start: Instant,
    phase: String,
    bytes: u64,
    frames: u64,
    chunks: u64,
    phases: BTreeMap<String,u64>,
    connections: BTreeMap<(u16,u16),usize>,
    error: Option<String>,
}
fn hex(bytes: &[u8]) -> String {
    use std::fmt::Write;
    let mut out = String::with_capacity(bytes.len()*2);
    for byte in bytes { let _ = write!(out,"{byte:02x}"); }
    out
}
impl Recorder {
    fn event(&mut self, mut value: Value) {
        if self.error.is_some() { return; }
        value["elapsedMs"] = json!(self.start.elapsed().as_millis() as u64);
        value["phase"] = json!(self.phase);
        let result = serde_json::to_vec(&value);
        match result {
            Ok(mut line) => {
                line.push(b'\n');
                if self.bytes + line.len() as u64 > MAX_BYTES {
                    self.error = Some("trace byte limit reached; capture incomplete".into());
                } else if let Err(error) = self.file.write_all(&line) {
                    self.error = Some(format!("trace write failed: {error}"));
                } else { self.bytes += line.len() as u64; }
            }
            Err(error) => self.error = Some(error.to_string()),
        }
    }
    fn frame(&mut self, packet: &[u8]) {
        if self.error.is_some() { return; }
        let prefix = read_varint(packet,0);
        let op = usize::try_from(prefix.length).ok().filter(|at| *at>0)
            .and_then(|at|packet.get(at..at+2));
        self.event(json!({"kind":"frame","sequence":self.frames,
            "opcode":op.map(hex),"frameHex":hex(packet)}));
        if self.error.is_none() {
            self.frames += 1;
            *self.phases.entry(self.phase.clone()).or_default() += 1;
        }
    }
    fn chunk(&mut self, cap: &CapturedPayload) {
        if self.error.is_some() { return; }
        let next = self.connections.len();
        let connection = *self.connections.entry((cap.src_port,cap.dst_port)).or_insert(next);
        self.event(json!({"kind":"stream","sequence":self.chunks,"connection":connection,
            "capturedAtMs":cap.captured_at_ms,"streamHex":hex(&cap.data)}));
        if self.error.is_none() { self.chunks += 1; }
    }
    fn flush(&mut self) {
        if let Err(error) = self.file.flush() {
            self.error = Some(format!("trace flush failed: {error}"));
        }
    }
}
/// Called after direction filtering/TCP ordering, before stream assembly.
pub fn record_stream(cap: &CapturedPayload) {
    if let Some(active) = ACTIVE.get() {
        if let Some(recorder) = active.lock().as_ref() { recorder.lock().chunk(cap); }
    }
}
fn phase(text: &str) -> &str {
    match text.trim() {
        "idle" | "potions-window" | "potions-batch" | "potions-cancel" |
        "potions-finish" | "potions-finished" | "food-window" | "food-batch" |
        "food-cancel" | "food-finish" | "food-finished" | "morph" | "done" => text.trim(),
        _ => "unmarked",
    }
}
fn checkpoint(path: &std::path::Path, value: &Value) -> std::io::Result<()> {
    let tmp = path.with_extension("tmp");
    std::fs::write(&tmp, serde_json::to_vec_pretty(value)?)?;
    std::fs::rename(tmp,path)
}
pub async fn run(output: PathBuf, marker: PathBuf) -> Result<(), Box<dyn std::error::Error>> {
    use crate::capture::{combat_port_detector::CombatPortDetector,pcap_capturer::PcapCapturer};
    use crate::combat::capture_dispatcher::CaptureDispatcher;
    use a2tools_dps_meter_lib::{combat::{data_storage::DataStorage,ping_tracker::PingTracker},
        i18n::lookup::{NpcLookup,SkillLookup}};
    // Refuse to overwrite an earlier test.
    let report = std::fs::OpenOptions::new().write(true).create_new(true).open(&output)?;
    drop(report);
    let trace_path = output.with_file_name("craft-trace.jsonl");
    let trace = std::fs::OpenOptions::new().write(true).create_new(true).open(trace_path)?;
    let recorder = Arc::new(parking_lot::Mutex::new(Recorder {
        file:BufWriter::new(trace),start:Instant::now(),phase:"idle".into(),bytes:0,
        frames:0,chunks:0,phases:BTreeMap::new(),connections:BTreeMap::new(),error:None,
    }));
    *ACTIVE.get_or_init(Default::default).lock() = Some(recorder.clone());
    let tap = recorder.clone();
    a2tools_dps_meter_lib::capture::frame_tap::set(Some(Arc::new(move |packet| {
        if let Some(port)=crate::activity::server_move_port(packet) {
            crate::capture::combat_port_detector::announce_port(port);
        }
        tap.lock().frame(packet);
    })));
    let detector = Arc::new(CombatPortDetector::new());
    let storage = Arc::new(DataStorage::new());
    let (tx,rx) = tokio::sync::mpsc::channel(16384);
    let capturer = PcapCapturer::new(tx,detector.clone());
    let dispatcher = CaptureDispatcher::new(storage.clone(),Arc::new(SkillLookup::new()),
        Arc::new(NpcLookup::new()),detector.clone(),Arc::new(PingTracker::new()));
    let task = tokio::spawn(async move { dispatcher.run(rx).await; });
    capturer.start();
    let mut ticker = tokio::time::interval(std::time::Duration::from_millis(100));
    let mut last_checkpoint = Instant::now()-std::time::Duration::from_secs(2);
    let mut result = Ok(());
    loop {
        ticker.tick().await;
        let current = std::fs::read_to_string(&marker).ok();
        let mut rec = recorder.lock();
        if let Some(current) = current {
            let current = phase(&current);
            if current != rec.phase {
                rec.phase = current.into();
                rec.event(json!({"kind":"phase"}));
            }
        }
        let elapsed = rec.start.elapsed().as_secs();
        let reason = if rec.error.is_some() { "recording-error" }
            else if rec.phase=="done" { "user-finished" }
            else if elapsed>=MAX_SECONDS { "time-limit" }
            else if task.is_finished() || !capturer.is_running() { "capture-stopped" }
            else { "running" };
        let stop = reason!="running";
        if stop || last_checkpoint.elapsed().as_secs()>=1 {
            rec.flush();
            let data = json!({"schema":3,"kind":"craft-full-server-trace","elapsedSeconds":elapsed,
                "captureRunning":capturer.is_running(),"portDetected":detector.current_port().is_some(),
                "phase":rec.phase,"frames":rec.frames,"streamChunks":rec.chunks,"traceBytes":rec.bytes,
                "framesByPhase":rec.phases,"localPlayerId":storage.local_player_id(),
                "error":rec.error,"stopReason":reason,"complete":reason=="user-finished" && rec.error.is_none(),
                "payloadsRecorded":true,"direction":"server-to-client",
                "scope":"all decoded frames and ordered server stream chunks admitted by the game capture pipeline; no opcode allowlist",
                "limits":{"bytes":MAX_BYTES,"seconds":MAX_SECONDS},
                "traceFile":"craft-trace.jsonl"});
            if let Err(error)=checkpoint(&output,&data) { result=Err(error.into()); break; }
            last_checkpoint=Instant::now();
        }
        if stop { break; }
        drop(rec);
        storage.flush_combat_only();
    }
    capturer.stop();
    task.abort();
    let _ = task.await;
    a2tools_dps_meter_lib::capture::frame_tap::set(None);
    *ACTIVE.get().unwrap().lock()=None;
    recorder.lock().flush();
    result
}
#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn full_trace_preserves_unknown_and_variable_length_frames() {
        let path=std::env::temp_dir().join(format!("craft-probe-{}.jsonl",std::process::id()));
        let mut rec=Recorder { file:BufWriter::new(std::fs::File::create(&path).unwrap()),
            start:Instant::now(),phase:"food-batch".into(),bytes:0,frames:0,chunks:0,
            phases:BTreeMap::new(),connections:BTreeMap::new(),error:None };
        let frames=vec![vec![8,0x71,0x8d,1,0,0,0],vec![5,0xff,0x42,0,255],
            [vec![0x80,1,0x6f,0x8d],vec![0x99;118]].concat()];
        for frame in &frames { rec.frame(frame); }
        rec.flush();
        let lines=std::fs::read_to_string(&path).unwrap();
        let values:Vec<Value>=lines.lines().map(|line|serde_json::from_str(line).unwrap()).collect();
        for (i,frame) in frames.iter().enumerate() {
            assert_eq!(values[i]["frameHex"],hex(frame));
            assert_eq!(values[i]["sequence"],i);
            assert_eq!(values[i]["phase"],"food-batch");
        }
        assert_eq!(rec.phases["food-batch"],3);
        rec.bytes=MAX_BYTES;
        rec.frame(&[1,2,3]);
        assert!(rec.error.is_some()); assert_eq!(rec.frames,3);
        drop(rec); std::fs::remove_file(path).unwrap();
    }
    #[test]
    fn markers_do_not_accept_free_text() {
        assert_eq!(phase("potions-cancel\n"),"potions-cancel");
        assert_eq!(phase("food-finished"),"food-finished");
        assert_eq!(phase("someone's name"),"unmarked");
    }
    #[test]
    fn checkpoint_replaces_valid_json() {
        let path=std::env::temp_dir().join(format!("craft-checkpoint-{}.json",std::process::id()));
        checkpoint(&path,&json!({"frames":1000})).unwrap();
        checkpoint(&path,&json!({"frames":1})).unwrap();
        assert_eq!(std::fs::read_to_string(&path).unwrap(),"{\n  \"frames\": 1\n}");
        std::fs::remove_file(path).unwrap();
    }
}

