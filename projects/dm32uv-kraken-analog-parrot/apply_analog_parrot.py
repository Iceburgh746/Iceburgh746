#!/usr/bin/env python3
"""WRCX212 Kraken analog Parrot source overlay, fail-closed and reversible.

Usage: python apply_analog_parrot.py PATH_TO_DM32UV_KRAKEN_SOURCE
Requires overlay/krk_parrot.c beside this script.
This modifies only the supplied working SOURCE COPY, never firmware, radio, or stock BIN.
"""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import shutil
import sys
import re

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

def main():
    if len(sys.argv) != 2:
        raise RuntimeError("Usage: python apply_analog_parrot.py PATH_TO_KRAKEN_SOURCE_COPY")
    repo = Path(sys.argv[1]).expanduser().resolve()
    root = Path(__file__).resolve().parent
    overlay = root / "overlay" / "krk_parrot.c"
    target = repo / "patches" / "kraken_ui" / "krk_parrot.c"
    periodic = repo / "patches" / "kraken_ui" / "krk_periodic.c"
    menus = repo / "patches" / "kraken_ui" / "menu_hosts.json"
    if not (repo / "tools" / "build.sh").exists():
        raise RuntimeError("Not a Kraken source tree: tools/build.sh missing")
    for file in (overlay, target, periodic, menus):
        if not file.is_file():
            raise RuntimeError("Missing source: " + str(file))
    t = target.read_text(encoding="utf-8")
    p = periodic.read_text(encoding="utf-8")
    m = menus.read_text(encoding="utf-8")
    if "krk_parrot_rx_start" not in t or "krk_parrot_armed" not in t:
        raise RuntimeError("Unknown Kraken Parrot baseline. Refusing to overwrite.")
    if "    krk_mon_tick();" not in p or "void krk_hook_bg_tick(void)" not in p:
        raise RuntimeError("Unknown background-hook baseline. Refusing to patch.")
    labels = (
        ('"label": "Parrot On/Off"', '"label": "Analog Parrot"'),
        ('"label": "Check"', '"label": "Audio Check"'),
    )
    for a,b in labels:
        if a not in m:
            raise RuntimeError("Expected Channel Set menu label missing: " + a)
    if "krk_analog_parrot_bg_tick" in p or "WRCX212 Analog Parrot" in t:
        raise RuntimeError("Source appears to be patched already. Aborting, no changes.")
    # Validate current menu JSON before touching any file.
    json.loads(m)
    new_p = p.replace("void krk_vox_tick(void);",
                      "void krk_analog_parrot_bg_tick(void);\\nvoid krk_vox_tick(void);", 1)
    if new_p == p:
        raise RuntimeError("No VOX prototype anchor.")
    new_p = new_p.replace("    krk_mon_tick();",
                          "    krk_analog_parrot_bg_tick();\\n    krk_mon_tick();", 1)
    # Use exact real newlines, not an accidental literal \\n escape sequence.
    new_p = new_p.replace("\\\\n", "\n")
    new_m = m
    for a,b in labels:
        new_m = new_m.replace(a,b,1)
    json.loads(new_m)
    new_t = overlay.read_text(encoding="utf-8")
    essential = ["krk_parrot_rx_start", "krk_parrot_rx_end", "krk_parrot_tick",
                 "krk_analog_parrot_bg_tick", "krk_parrot_arm_screen",
                 "krk_parrot_play_screen", "krk_parrot_check_screen",
                 "krk_parrot_clear_screen", "krk_parrot_status_screen",
                 "krk_parrot_armed", "krk_fm_send_opt"]
    for symbol in essential:
        if symbol not in new_t:
            raise RuntimeError("Overlay missing required symbol: " + symbol)
    # Immutable backups in user's existing SOURCE COPY, not a radio backup.
    backup = repo / ".wrcx212_analog_parrot_backups" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    if backup.exists():
        raise RuntimeError("Backup destination already exists; refusing to overwrite")
    backup.mkdir(parents=True)
    files = [(target,new_t),(periodic,new_p),(menus,new_m)]
    hashes = {}
    for old,_ in files:
        rel = old.relative_to(repo)
        dst = backup / rel
        dst.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(old,dst)
        if digest(old) != digest(dst):
            raise RuntimeError("Backup hash mismatch: "+str(old))
        hashes[str(rel)] = {"original_sha256":digest(old)}
    # All validation complete before mutation.
    try:
        for old, new in files:
            temp = old.with_suffix(old.suffix+".wrcx212.tmp")
            temp.write_text(new,encoding="utf-8",newline="\n")
            temp.replace(old)
            hashes[str(old.relative_to(repo))]["patched_sha256"]=digest(old)
    except Exception:
        for old,_ in files:
            saved = backup / old.relative_to(repo)
            if saved.is_file():
                shutil.copy2(saved,old)
        raise
    report={"status":"SOURCE_PATCH_APPLIED_NOT_BUILT_NOT_FLASHED",
            "backup_directory":str(backup),"source_directory":str(repo),
            "files":hashes,
            "next":"Run tools/gen_manifest.py/build.sh in a VERIFIED Kraken build environment. Do not flash unless build+QEMU+hardware gates pass."}
    reportfile=backup/"WRCX212_patch_report.json"
    reportfile.write_text(json.dumps(report,indent=2),encoding="utf-8")
    print(json.dumps(report,indent=2))
    print("\nSOURCE MODIFIED. NO FIRMWARE IMAGE GENERATED OR FLASHED.\n")

if __name__ == "__main__":
    try:main()
    except Exception as e:
        print("\nBLOCKED (fail-safe):",str(e))
        sys.exit(1)
