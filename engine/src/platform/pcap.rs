//! Windows Npcap loader adapted from A2Tools, retaining absolute-path DLL loading.
pub const LIBRARIES: &[&str] = &["wpcap.dll"];
pub const MISSING_HELP: &str = "Npcap is required (https://npcap.com).";
pub const READ_TIMES_OUT_WHEN_IDLE: bool = true;
pub const FILTER_CHANGE_KEEPS_QUEUE: bool = false;
pub fn skip_device(_: &str) -> bool { false }
fn system32() -> Option<std::path::PathBuf> {
    use windows::Win32::{System::Com::CoTaskMemFree, UI::Shell::{FOLDERID_System, SHGetKnownFolderPath, KF_FLAG_DEFAULT}};
    unsafe {
        let raw = SHGetKnownFolderPath(&FOLDERID_System, KF_FLAG_DEFAULT, None).ok()?;
        let path = raw.to_string().ok();
        CoTaskMemFree(Some(raw.0 as *const _));
        path.map(std::path::PathBuf::from)
    }
}
pub fn prepare() {
    use std::os::windows::ffi::OsStrExt;
    use windows::{core::PCWSTR, Win32::System::LibraryLoader::SetDllDirectoryW};
    static ONCE: std::sync::Once = std::sync::Once::new();
    ONCE.call_once(|| {
        if let Some(system) = system32() {
            let wide: Vec<u16> = system.join("Npcap").as_os_str().encode_wide().chain(std::iter::once(0)).collect();
            unsafe { let _ = SetDllDirectoryW(PCWSTR(wide.as_ptr())); }
        }
    });
}
pub unsafe fn load(name: &str) -> Result<libloading::Library, libloading::Error> {
    use libloading::os::windows::{Library, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR, LOAD_LIBRARY_SEARCH_SYSTEM32};
    prepare();
    let root = system32().ok_or(libloading::Error::LoadLibraryExWUnknown)?;
    let flags = LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32;
    unsafe { Library::load_with_flags(root.join("Npcap").join(name), flags)
        .or_else(|_|Library::load_with_flags(root.join(name),flags)).map(Into::into) }
}
