"""WRCX212 UV Laboratory offline diagnostics and APRS AX.25 test bench.
Read-only: this program never writes to a connected radio or transmits RF.
Python 3.10+; standard library only.
"""
from __future__ import annotations
import argparse
import datetime
import json
import os
from pathlib import Path
import shutil
import sys
import unittest

ROOT = Path(os.environ.get("WRCX212_HOME", r"C:\WRCX212-LAB"))
REPO = ROOT / "Firmware" / "Quansheng" / "uv-k1-k5v3-firmware-custom"

def inventory():
    binaries = ("git", "java", "javac", "python", "adb", "docker", "node", "npm", "cmake")
    apps = REPO / "App" / "apps"
    return {
        "timestamp_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "workspace": str(ROOT),
        "tools": {name: shutil.which(name) for name in binaries},
        "firmware_source_exists": REPO.is_dir(),
        "app_build_script_exists": (REPO / "compile-app.sh").is_file(),
        "firmware_build_script_exists": (REPO / "compile-firmware.sh").is_file(),
        "aprs_app_dirs": sorted(p.name for p in apps.iterdir()
            if p.is_dir() and ("aprs" in p.name.lower() or "kiss" in p.name.lower()))
            if apps.is_dir() else []
    }

def address(call: str, ssid: int = 0, last: bool = False) -> bytes:
    call = call.upper()
    if not 1 <= len(call) <= 6 or not call.isalnum() or not 0 <= ssid <= 15:
        raise ValueError("Invalid callsign or SSID")
    return bytes(ord(ch) << 1 for ch in call.ljust(6)) + bytes([0x60 | (ssid << 1) | int(last)])

def crc16_x25(data: bytes) -> int:
    crc = 0xffff
    for b in data:
        crc ^= b
        for _ in range(8):
            crc = (crc >> 1) ^ (0x8408 if crc & 1 else 0)
    return crc ^ 0xffff

def ui_frame(src: str, dest: str, text: str) -> bytes:
    payload = address(dest) + address(src, last=True) + b"\x03\xf0" + text.encode("ascii")
    return payload + crc16_x25(payload).to_bytes(2, "little")

def decode_ui(frame: bytes) -> dict:
    if len(frame) < 18 or crc16_x25(frame) != 0x0f47:
        # X.25 residue when a completed frame including transmitted FCS is checked
        raise ValueError("Short frame or invalid FCS")
    def decode_addr(b):
        return "".join(chr(ch >> 1) for ch in b[:6]).strip() + (
            ("-" + str((b[6] >> 1) & 15)) if (b[6] >> 1) & 15 else "")
    index = 0
    addrs = []
    while True:
        if index + 7 > len(frame) - 4 or len(addrs) >= 10:
            raise ValueError("Invalid AX.25 address chain")
        part = frame[index:index+7]
        addrs.append(decode_addr(part))
        index += 7
        if part[6] & 1:
            break
    if frame[index:index+2] != b"\x03\xf0":
        raise ValueError("Not an unnumbered-information APRS frame")
    return {"destination": addrs[0], "source": addrs[1],
            "path": addrs[2:], "information": frame[index+2:-2].decode("ascii", "replace")}

class SelfTests(unittest.TestCase):
    def test_crc_vector(self):
        self.assertEqual(crc16_x25(b"123456789"), 0x906e)
    def test_frame_roundtrip(self):
        f = ui_frame("N0CALL-1".split("-")[0], "APRS", "!0000.00N/00000.00W-Test")
        decoded = decode_ui(f)
        self.assertEqual(decoded["source"], "N0CALL")
        self.assertIn("Test", decoded["information"])
    def test_corruption(self):
        corrupted = bytearray(ui_frame("N0CALL", "APRS", ">Test"))
        corrupted[-3] ^= 1
        with self.assertRaises(ValueError):
            decode_ui(bytes(corrupted))

def main():
    parser = argparse.ArgumentParser(description="WRCX212 offline UV Laboratory")
    parser.add_argument("action", choices=["inventory", "test", "decode"])
    parser.add_argument("--hex", help="Complete AX.25 frame bytes including FCS, hexadecimal")
    args = parser.parse_args()
    if args.action == "inventory":
        print(json.dumps(inventory(), indent=2))
    elif args.action == "decode":
        if not args.hex:
            parser.error("--hex is required for decode")
        print(json.dumps(decode_ui(bytes.fromhex(args.hex)), indent=2))
    else:
        suite = unittest.defaultTestLoader.loadTestsFromTestCase(SelfTests)
        result = unittest.TextTestRunner(verbosity=2).run(suite)
        sys.exit(0 if result.wasSuccessful() else 1)

if __name__ == "__main__":
    main()
