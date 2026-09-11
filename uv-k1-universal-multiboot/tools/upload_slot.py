#!/usr/bin/env python3
"""
Upload an FMB1 firmware-slot package to F4HWN v6 Multiboot over a programming/USB serial port.

Safety model:
- slot 0 is never writable by this tool
- the device's 0x0514 session timestamp is established first
- slot erase uses 0x0722
- image bytes are written first with 0x0724
- the COMMITTED 64-byte FMB1 header is written LAST
- 0x0726 full-image CRC validation must pass

This tool does not flash internal MCU application memory. Selecting/restoring the
slot is still performed on the radio's Multiboot screen.
"""
from __future__ import annotations

import argparse
import secrets
import struct
import sys
import time
from pathlib import Path
import zlib

try:
    import serial
    from serial.tools import list_ports
except ImportError as exc:
    print("ERROR: pyserial is required: python -m pip install pyserial", file=sys.stderr)
    raise SystemExit(2) from exc

SLOT_SIZE = 0x20000
IMAGE_OFFSET = 0x1000
APP_SIZE = 0x1D800
MAGIC = 0x31424D46
FLAG_COMMITTED = 1 << 0

OBF = bytes([
    0x16, 0x6C, 0x14, 0xE6, 0x2E, 0x91, 0x0D, 0x40,
    0x21, 0x35, 0xD5, 0x40, 0x13, 0x03, 0xE9, 0x80,
])

STATUS = {
    0: "OK",
    1: "bad magic / empty",
    2: "unsupported header version",
    3: "not committed",
    4: "bad size",
    5: "CRC mismatch",
    6: "SPI flash error",
    7: "bad slot",
    8: "authorization/session mismatch",
    9: "RAM restore loader error",
    10: "bad vector table",
    11: "wrong target/reset address",
}


def crc16_ccitt(data: bytes) -> int:
    crc = 0
    for b in data:
        crc ^= b << 8
        for _ in range(8):
            crc = ((crc << 1) ^ 0x1021) & 0xFFFF if (crc & 0x8000) else (crc << 1) & 0xFFFF
    return crc


def xor_obf(data: bytes) -> bytes:
    return bytes(b ^ OBF[i % len(OBF)] for i, b in enumerate(data))


def make_request(command_id: int, payload: bytes = b"") -> bytes:
    body = struct.pack("<HH", command_id, len(payload)) + payload
    crc = struct.pack("<H", crc16_ccitt(body))
    enc = xor_obf(body + crc)
    return b"\xAB\xCD" + struct.pack("<H", len(body)) + enc + b"\xDC\xBA"


def read_exact(ser: serial.Serial, n: int, timeout: float) -> bytes:
    end = time.monotonic() + timeout
    out = bytearray()
    while len(out) < n and time.monotonic() < end:
        chunk = ser.read(n - len(out))
        if chunk:
            out += chunk
    if len(out) != n:
        raise TimeoutError(f"serial timeout: needed {n} bytes, got {len(out)}")
    return bytes(out)


def read_reply(ser: serial.Serial, timeout: float = 4.0) -> tuple[int, bytes]:
    end = time.monotonic() + timeout
    state = 0
    while time.monotonic() < end:
        b = ser.read(1)
        if not b:
            continue
        if state == 0:
            state = 1 if b == b"\xAB" else 0
        else:
            if b == b"\xCD":
                break
            state = 1 if b == b"\xAB" else 0
    else:
        raise TimeoutError("reply sync 0xABCD not received")

    size = struct.unpack("<H", read_exact(ser, 2, timeout))[0]
    tail = read_exact(ser, size + 4, timeout)
    enc_body = tail[:size]
    marker = tail[size + 2:size + 4]
    if marker != b"\xDC\xBA":
        raise RuntimeError(f"bad reply footer: {marker.hex()}")
    body = xor_obf(enc_body)
    if len(body) < 4:
        raise RuntimeError("short nested reply")
    reply_id, payload_size = struct.unpack_from("<HH", body, 0)
    if payload_size > len(body) - 4:
        raise RuntimeError("nested reply size is inconsistent")
    return reply_id, body[4:4 + payload_size]


def transact(
    ser: serial.Serial,
    command_id: int,
    payload: bytes,
    expected_reply: int,
    timeout: float = 4.0,
) -> bytes:
    ser.reset_input_buffer()
    ser.write(make_request(command_id, payload))
    ser.flush()
    reply_id, reply_payload = read_reply(ser, timeout)
    if reply_id != expected_reply:
        raise RuntimeError(
            f"unexpected reply 0x{reply_id:04X}; expected 0x{expected_reply:04X}"
        )
    return reply_payload


def load_package(path: Path) -> tuple[bytes, bytes, dict]:
    raw = path.read_bytes()
    if len(raw) != SLOT_SIZE:
        raise ValueError(f"package must be exactly {SLOT_SIZE} bytes")
    magic, hdrver, flags, size, crc = struct.unpack_from("<IHHII", raw, 0)
    if magic != MAGIC:
        raise ValueError("package is not FMB1")
    if hdrver > 1:
        raise ValueError(f"unsupported FMB1 header version {hdrver}")
    if not (flags & FLAG_COMMITTED):
        raise ValueError("package header is not COMMITTED")
    if size == 0 or size > APP_SIZE:
        raise ValueError(f"invalid image size {size}")
    image = raw[IMAGE_OFFSET:IMAGE_OFFSET + size]
    actual = zlib.crc32(image) & 0xFFFFFFFF
    if actual != crc:
        raise ValueError(
            f"package CRC mismatch: header 0x{crc:08X}, actual 0x{actual:08X}"
        )
    name = raw[16:32].split(b"\0", 1)[0].decode("ascii", "replace")
    version = raw[32:48].split(b"\0", 1)[0].decode("ascii", "replace")
    return raw[:64], image, {
        "header_version": hdrver,
        "flags": flags,
        "size": size,
        "crc32": crc,
        "name": name,
        "version": version,
    }


def handshake(ser: serial.Serial, timestamp: int) -> str:
    payload = struct.pack("<I", timestamp)
    reply = transact(ser, 0x0514, payload, 0x0515, timeout=5.0)
    return reply[:16].split(b"\0", 1)[0].decode("ascii", "replace")


def erase_slot(ser: serial.Serial, slot: int, timestamp: int) -> None:
    payload = bytes([slot, 0]) + struct.pack("<I", timestamp)
    reply = transact(ser, 0x0722, payload, 0x0723, timeout=25.0)
    if len(reply) < 2:
        raise RuntimeError("short erase reply")
    rslot, status = reply[0], reply[1]
    if rslot != slot or status != 0:
        raise RuntimeError(
            f"erase failed for slot {slot}: {STATUS.get(status, f'status {status}')}"
        )


def write_block(
    ser: serial.Serial,
    slot: int,
    offset: int,
    data: bytes,
    timestamp: int,
) -> None:
    if len(data) > 220:
        raise ValueError("write block too large")
    payload = (
        bytes([slot, 0])
        + struct.pack("<I", offset)
        + struct.pack("<H", len(data))
        + struct.pack("<I", timestamp)
        + data
    )
    reply = transact(ser, 0x0724, payload, 0x0725, timeout=8.0)
    if len(reply) < 2:
        raise RuntimeError("short write reply")
    rslot, status = reply[0], reply[1]
    if rslot != slot or status != 0:
        raise RuntimeError(
            f"write failed at 0x{offset:05X}: {STATUS.get(status, f'status {status}')}"
        )


def validate_slot(ser: serial.Serial, slot: int) -> int:
    reply = transact(ser, 0x0726, bytes([slot]), 0x0727, timeout=15.0)
    if len(reply) < 6:
        raise RuntimeError("short validate reply")
    crc, rslot, status = struct.unpack_from("<IBB", reply, 0)
    if rslot != slot:
        raise RuntimeError(f"validate echoed slot {rslot}, expected {slot}")
    if status != 0:
        raise RuntimeError(
            f"device validation failed: {STATUS.get(status, f'status {status}')} "
            f"(device CRC 0x{crc:08X})"
        )
    return crc


def list_serial_ports() -> None:
    ports = list(list_ports.comports())
    if not ports:
        print("No serial ports found.")
        return
    for p in ports:
        print(f"{p.device:12} {p.description or ''} {p.hwid or ''}")


def main() -> int:
    ap = argparse.ArgumentParser(description="Upload an FMB1 package to F4HWN v6 slot 1..4.")
    ap.add_argument("package", type=Path, nargs="?")
    ap.add_argument("--port", help="COM port, e.g. COM5")
    ap.add_argument("--slot", type=int, choices=range(1, 5), metavar="1..4")
    ap.add_argument("--baud", type=int, default=38400)
    ap.add_argument("--chunk", type=int, default=192, choices=range(32, 221))
    ap.add_argument("--list-ports", action="store_true")
    ap.add_argument("--yes", action="store_true", help="skip destructive erase confirmation")
    ap.add_argument("--dry-run", action="store_true")
    ns = ap.parse_args()

    if ns.list_ports:
        list_serial_ports()
        return 0
    if not ns.package or not ns.port or ns.slot is None:
        ap.error("package, --port and --slot are required unless --list-ports is used")

    try:
        header, image, meta = load_package(ns.package)
    except (OSError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2

    print(f"Package: {ns.package}")
    print(f"Slot:    {ns.slot}")
    print(f"Name:    {meta['name']}")
    print(f"Version: {meta['version']}")
    print(f"Size:    {meta['size']} bytes")
    print(f"CRC32:   0x{meta['crc32']:08X}")

    if ns.dry_run:
        print("Dry run complete; no radio writes performed.")
        return 0

    if not ns.yes:
        answer = input(f"Erase and replace F4HWN Multiboot slot {ns.slot}? Type YES: ")
        if answer.strip() != "YES":
            print("Cancelled.")
            return 1

    timestamp = secrets.randbits(32)
    try:
        with serial.Serial(ns.port, ns.baud, timeout=0.15, write_timeout=4.0) as ser:
            time.sleep(0.15)
            version = handshake(ser, timestamp)
            print(f"Radio firmware: {version or '(version not reported)'}")
            print("Erasing slot...")
            erase_slot(ser, ns.slot, timestamp)

            total = len(image)
            print("Writing image...")
            for pos in range(0, total, ns.chunk):
                block = image[pos:pos + ns.chunk]
                write_block(ser, ns.slot, IMAGE_OFFSET + pos, block, timestamp)
                done = min(pos + len(block), total)
                pct = (done * 100) // total
                print(f"\r  {done:6}/{total} bytes  {pct:3}%", end="", flush=True)
            print()

            print("Committing header...")
            for pos in range(0, len(header), ns.chunk):
                write_block(ser, ns.slot, pos, header[pos:pos + ns.chunk], timestamp)

            print("Validating slot CRC on radio...")
            device_crc = validate_slot(ser, ns.slot)
            if device_crc != meta["crc32"]:
                raise RuntimeError(
                    f"device CRC 0x{device_crc:08X} != package CRC 0x{meta['crc32']:08X}"
                )

    except (serial.SerialException, TimeoutError, RuntimeError, OSError, ValueError) as exc:
        print(f"\nERROR: {exc}", file=sys.stderr)
        print(
            "The slot header is written last, so an interrupted upload should remain "
            "uncommitted and be refused by Multiboot.",
            file=sys.stderr,
        )
        return 3

    print(f"SUCCESS: slot {ns.slot} validated, CRC32 0x{meta['crc32']:08X}.")
    print("The internal MCU application has NOT been changed yet.")
    print("Select the slot from the radio's Multiboot menu when ready.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
