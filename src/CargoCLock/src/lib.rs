use cargo_lock::Lockfile;
use std::{
    ffi::{CStr, CString},
    path::PathBuf,
};

// SAFETY: there is no other global function of this name
#[unsafe(no_mangle)]
pub extern "C" fn json(path_ptr: *const i8) -> *mut i8 {
    // SAFETY: The caller must guarantee that path_ptr is a valid null-terminated C string.
    let path_cstr = unsafe { CStr::from_ptr(path_ptr) };
    let path: PathBuf = path_cstr.to_str().expect("Invalid UTF-8 in path").into();

    let lockfile = Lockfile::load(path).unwrap();
    let serialized = serde_json::to_string(&lockfile.packages).unwrap();

    let json_cstring = CString::new(serialized).unwrap();
    let json_ptr = json_cstring.into_raw();
    json_ptr
}

// SAFETY: there is no other global function of this name
#[unsafe(no_mangle)]
pub extern "C" fn free(json_ptr: *mut i8) {
    if json_ptr.is_null() {
        return;
    }

    // SAFETY: for manual deallocation.
    unsafe { drop(CString::from_raw(json_ptr)) };
}

#[test]
fn test_json() {
    // test-only: create the path argument, which would be caller-owned.
    let path = std::env::current_dir().unwrap().join("Cargo.lock");
    let path_cstring = CString::new(path.to_str().unwrap()).unwrap();
    let path_ptr = path_cstring.as_ptr();

    // Get a Rust-owned pointer to the JSON string.
    let json_ptr = json(path_ptr);

    // Get a reference to the contents so we can test the `free` call:
    let json_cstr = unsafe { CStr::from_ptr(json_ptr) };
    let json_str = json_cstr.to_str().unwrap();
    assert!(json_str.contains("name"));

    // Let Rust know it can clean up.
    free(json_ptr);
}
