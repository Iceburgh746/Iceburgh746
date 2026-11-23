#!/usr/bin/env python3
"""WRCX212 RT-950 Pro: merge safely into a user's *own* RMS native CSV export.
Read-only source, no radio/serial access, no firmware writes.
"""
import csv
import hashlib
import json
import os
import sys
from pathlib import Path
from datetime import datetime, timezone

FIELDS = ("Channel_Num,Active,Name,RX,TX,RX_Tone,TX_Tone,TX_Power,"
          "Slot1,Slot2,Slot3,Slot4,Bandwidth,Modulation,BusyLock,Reversed,"
          "PTTID,Clarifier,Scrambler,ScanMonitor,ScanHold").split(",")
# Operator-reported or directory-listed candidates. All N/T, awaiting current access verification.
CANDIDATES = [
    ("ELIZABETH725", 462.725, "131.8", "S", "myGMRS operator report; current activity not proven"),
    ("WOPSON675",   462.675, "123.0", "E", "myGMRS operator report; current activity not proven"),
    ("N_NEWCASTLE", 462.650, "",      "N", "New Castle candidate; confirm tone and access"),
    ("NE_NCAMBRIA", 462.575, "67.0",  "N", "Northern Cambria candidate; check operating status"),
    ("E_WILLIAM",  462.625, "173.8", "E", "Williamsburg candidate; check operating status"),
    ("SW_WHEELING", 462.625, "123.0", "W", "Wheeling WV candidate; check operating status"),
    ("W_NILES",    462.675, "74.4",  "W", "Niles OH candidate; reported testing/reduced operation"),
    ("NW_ERIE",    462.600, "",      "N", "Erie PA candidate; check operating status"),
    ("SE_SPRINGS", 462.550, "",      "S", "Springs PA candidate; check operating status"),
]

def locate_source():
    if len(sys.argv) > 1:
        return Path(sys.argv[1]).expanduser()
    home = Path.home()
    locations = [home / "Downloads", Path(r"C:\Radtel"), Path.cwd()]
    names = ("channels(1).csv", "channels.csv")
    for folder in locations:
        for name in names:
            candidate = folder / name
            if candidate.is_file():
                return candidate
    try:
        from tkinter import Tk, filedialog
        window = Tk()
        window.withdraw()
        window.attributes("-topmost", True)
        selected = filedialog.askopenfilename(
            title="Select YOUR original RMS6 channels CSV export",
            filetypes=[("CSV channel exports", "*.csv"), ("All files", "*.*")]
        )
        window.destroy()
        if selected:
            return Path(selected)
    except Exception as exc:
        print("File picker unavailable:", exc)
    raise RuntimeError("Could not locate original channels CSV. Please select it or pass its path.")

def main():
    source = locate_source()
    if not source.is_file():
        raise RuntimeError("Source file does not exist: " + str(source))
    raw = source.read_bytes()
    original_hash = hashlib.sha256(raw).hexdigest().upper()
    with source.open("r", encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f)
        if reader.fieldnames != FIELDS:
            raise RuntimeError(
                "Unsupported source file: native RMS export fields do not match. "
                "No output created.\nActual: " + str(reader.fieldnames))
        original = list(reader)
    if len(original) != 999 or [int(r["Channel_Num"]) for r in original] != list(range(1, 1000)):
        raise RuntimeError("Expected exactly 999 consecutively numbered RMS records. No output created.")
    rows = [dict(r) for r in original]
    empty = [r for r in rows if r["Active"].strip().lower() == "false"
             and not r["Name"].strip() and r["RX"].strip() in ("0.00000", "0")]
    # Channel 999 can be reserved for APRS. Never use or modify it.
    slots = [r for r in empty if 1 <= int(r["Channel_Num"]) <= 998]
    if len(slots) < len(CANDIDATES):
        raise RuntimeError(
            "Only %d genuinely empty slots found below channel 999, but %d are needed. "
            "No existing memory will be overwritten." % (len(slots), len(CANDIDATES)))
    slot_numbers = [int(x["Channel_Num"]) for x in slots[:len(CANDIDATES)]]
    changes = []
    for (name, rx, tone, direction, note), slot in zip(CANDIDATES, slot_numbers):
        dst = rows[slot-1]
        # Safeguard: carry all options from one known empty slot, and explicitly override unsafe defaults.
        dst.update({
            "Active": "True",
            "Name": name,
            "RX": "%.5f" % rx,
            "TX": "%.5f" % rx,
            "RX_Tone": "None",
            "TX_Tone": "None",
            "TX_Power": "N/T",
            "Slot1": "X",    # Proposed extended-monitoring group; RMS group name requires separate setup.
            "Slot2": " ",
            "Slot3": " ",
            "Slot4": " ",
            "Bandwidth": "Wide",
            "Modulation": "FM",
            "BusyLock": "True",
            "Reversed": "False",
            "PTTID": "Off",
            "Clarifier": "0.00",
            "Scrambler": "Off",
            "ScanMonitor": "0",
            "ScanHold": "0.0",
        })
        changes.append({
            "channel": slot, "name": name, "rx_mhz": rx, "tx_power": "N/T",
            "group": "X", "direction": direction, "published_tone_reference": tone,
            "notes": note
        })
    # Full-file round trip validation: all existing channel rows must remain unchanged,
    # and channel 999 must be byte-equivalent as parsed source fields.
    changed = set(slot_numbers)
    for i, (old, new) in enumerate(zip(original, rows), start=1):
        if i not in changed and old != new:
            raise RuntimeError("Preservation check FAILED at channel %d" % i)
    if any(rows[i-1]["TX_Power"] != "N/T" for i in slot_numbers):
        raise RuntimeError("TX inhibit verification FAILED")
    output_dir = Path(r"C:\Radtel\WRCX212_RT950Pro_Channels") if os.name == "nt" else source.parent / "WRCX212_RT950Pro_Channels"
    output_dir.mkdir(parents=True, exist_ok=True)
    newfile = output_dir / "WRCX212_RMS6_MERGED_999_CHANNELS.csv"
    if newfile.resolve() == source.resolve():
        raise RuntimeError("Output would overwrite the original source")
    # Write to a temporary file then replace only our own generated output.
    temp = newfile.with_suffix(".csv.tmp")
    with temp.open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=FIELDS, lineterminator="\r\n")
        writer.writeheader()
        writer.writerows(rows)
    with temp.open("r", encoding="utf-8", newline="") as f:
        roundtrip = list(csv.DictReader(f))
    if roundtrip != rows or len(roundtrip) != 999:
        temp.unlink(missing_ok=True)
        raise RuntimeError("Post-write CSV roundtrip FAILED")
    os.replace(temp, newfile)
    report = {
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "source_path": str(source),
        "original_sha256": original_hash,
        "original_entries": 999,
        "preserved_original_entries": 990,
        "new_entries": len(changes),
        "new_slots": slot_numbers,
        "channel_999_untouched": original[-1] == rows[-1],
        "file_created": str(newfile),
        "new_file_sha256": hashlib.sha256(newfile.read_bytes()).hexdigest().upper(),
        "no_radio_programming_performed": True,
        "rms_native_csv_import_validation": "NOT TESTED IN RMS6",
        "warnings": [
            "RMS6 Channels tab documents CHIRP CSV import, not necessarily reimport of this native export CSV.",
            "Do NOT import/write as if native CSV were a full RMS6 codeplug without checking RMS6 File menu and readback.",
            "Candidate entries are receive-only with TX_Power N/T; confirm the UI honors this before radio write.",
            "Group X membership is proposed; group naming/configuration not exported in channel CSV.",
            "Only nine vacant slots were available; a 67-channel addition cannot fit without restructuring memories.",
            "GMRS 8-14 and NOAA channels in the original export appear transmit-enabled; left unchanged, flagged for review.",
        ],
        "changes": changes,
    }
    report_file = output_dir / "WRCX212_RMS6_MERGE_REPORT.json"
    report_file.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("\nPASS: existing 990 channel records preserved (including channel 999)")
    print("PASS: inserted 9 N/T receive-only candidates into:", slot_numbers)
    print("PASS: 999-row RMS-native-format CSV roundtrip verified")
    print("ORIGINAL SHA256:", original_hash)
    print("FILE:", newfile)
    print("REPORT:", report_file)
    print("\nIMPORTANT: This verifies CSV structure, NOT that RMS6 will load it as a complete codeplug.")
    print("Do NOT write to your radio until import support, safety flags and group assignments are confirmed.")
    print()

if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print("\nBLOCKED (no radio changes):", exc)
        sys.exit(1)
