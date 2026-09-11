#!/usr/bin/env python3
"""
WRCX212 UV-K1 Universal MultiBoot slot packer.

Creates an F4HWN v6-compatible 128 KiB slot image from a raw PY32F071 UV-K1
application binary. It does NOT relocate or patch executable code.

Compatibility classes:
  native          F4HWN-compatible firmware (normal round-trip multiboot)
  foreign         raw compatible firmware; factory USB bootloader is the guaranteed recovery path
  return-patched  foreign firmware that already contains an independently verified return hook

The radio-side patch performs the same critical vector checks again before erase.
"""
from __future__ import annotations

import argparse
import json
import struct
import sys
import zlib
from pathlib import Path

APP_BASE = 0x08002800
APP_SIZE = 0x0001D800
APP_END = APP_BASE + APP_SIZE
RAM_BASE = 0x20000000
RAM_SIZE = 16 * 1024
RAM_END = RAM_BASE + RAM_SIZE

SLOT_SIZE = 0x20000
IMAGE_OFFSET = 0x1000
HEADER_SIZE = 64

MAGIC = 0x31424D46  # FMB1
HEADER_VERSION = 1

FLAG_COMMITTED = 1 << 0
FLAG_FOREIGN = 1 << 1
FLAG_RETURN_PATCHED = 1 << 2

KIND_FLAGS = {
    "native": 0,
    "foreign": FLAG_FOREIGN,
    "return-patched": FLAG_FOREIGN | FLAG_RETURN_PATCHED,
}


class ImageError(ValueError):
    pass


def _decode_ascii_field(raw: bytes) -> str:
    return raw.split(b"\0", 1)[0].decode("ascii", "replace")


def inspect_image(data: bytes) -> dict:
    if not data:
        raise ImageError("image is empty")
    if len(data) > APP_SIZE:
        raise ImageError(
            f"image is {len(data)} bytes; UV-K1 PY32F071 application limit is {APP_SIZE} bytes"
        )
    if len(data) < 8:
        raise ImageError("image is too short to contain an ARM vector table")

    initial_sp, reset_vector = struct.unpack_from("<II", data, 0)
    reset_addr = reset_vector & ~1

    errors: list[str] = []
    warnings: list[str] = []

    if not (RAM_BASE <= initial_sp <= RAM_END):
        errors.append(
            f"initial SP 0x{initial_sp:08X} is outside 16 KiB SRAM "
            f"(0x{RAM_BASE:08X}-0x{RAM_END:08X})"
        )
    elif initial_sp & 0x7:
        warnings.append(
            f"initial SP 0x{initial_sp:08X} is not 8-byte aligned"
        )

    if (reset_vector & 1) == 0:
        errors.append(
            f"reset vector 0x{reset_vector:08X} is not a Thumb entry"
        )

    image_end = APP_BASE + len(data)
    if not (APP_BASE <= reset_addr < image_end):
        errors.append(
            f"reset handler 0x{reset_addr:08X} is not inside the supplied image "
            f"(0x{APP_BASE:08X}-0x{image_end - 1:08X})"
        )

    vector_words = min(len(data) // 4, 48)
    suspicious = 0
    for i in range(2, vector_words):
        v = struct.unpack_from("<I", data, i * 4)[0]
        if v in (0, 0xFFFFFFFF):
            continue
        addr = v & ~1
        if (v & 1) == 0 or not (APP_BASE <= addr < APP_END):
            suspicious += 1
    if suspicious:
        warnings.append(
            f"{suspicious} early vector-table entries are unusual; review before flashing"
        )

    return {
        "size": len(data),
        "crc32": zlib.crc32(data) & 0xFFFFFFFF,
        "initial_sp": initial_sp,
        "reset_vector": reset_vector,
        "reset_handler": reset_addr,
        "errors": errors,
        "warnings": warnings,
        "compatible": not errors,
    }


def fixed_ascii(text: str, length: int, field: str) -> bytes:
    try:
        raw = text.encode("ascii")
    except UnicodeEncodeError as exc:
        raise ImageError(f"{field} must be ASCII") from exc
    if len(raw) >= length:
        raw = raw[: length - 1]
    return raw + b"\0" * (length - len(raw))


def build_header(data: bytes, name: str, version: str, kind: str) -> bytes:
    if kind not in KIND_FLAGS:
        raise ImageError(f"unknown kind: {kind}")
    info = inspect_image(data)
    if not info["compatible"]:
        raise ImageError("; ".join(info["errors"]))

    flags = FLAG_COMMITTED | KIND_FLAGS[kind]
    reserved = bytearray(16)
    reserved[:8] = b"WRCXU1\0\0"

    header = struct.pack(
        "<IHHII16s16s16s",
        MAGIC,
        HEADER_VERSION,
        flags,
        len(data),
        info["crc32"],
        fixed_ascii(name, 16, "name"),
        fixed_ascii(version, 16, "version"),
        bytes(reserved),
    )
    assert len(header) == HEADER_SIZE
    return header


def build_slot(data: bytes, name: str, version: str, kind: str) -> bytes:
    header = build_header(data, name, version, kind)
    if IMAGE_OFFSET + len(data) > SLOT_SIZE:
        raise ImageError("image does not fit slot")
    slot = bytearray(b"\xFF" * SLOT_SIZE)
    slot[:HEADER_SIZE] = header
    slot[IMAGE_OFFSET : IMAGE_OFFSET + len(data)] = data
    return bytes(slot)


def parse_slot(slot: bytes) -> dict:
    if len(slot) != SLOT_SIZE:
        raise ImageError(f"slot package must be exactly {SLOT_SIZE} bytes")
    fields = struct.unpack_from("<IHHII16s16s16s", slot, 0)
    magic, hdrver, flags, size, crc, name, version, reserved = fields
    if magic != MAGIC:
        raise ImageError("not an FMB1 slot package")
    if size == 0 or size > APP_SIZE or IMAGE_OFFSET + size > len(slot):
        raise ImageError("invalid image size in slot header")
    image = slot[IMAGE_OFFSET : IMAGE_OFFSET + size]
    actual = zlib.crc32(image) & 0xFFFFFFFF
    return {
        "magic": "FMB1",
        "header_version": hdrver,
        "flags": flags,
        "committed": bool(flags & FLAG_COMMITTED),
        "foreign": bool(flags & FLAG_FOREIGN),
        "return_patched": bool(flags & FLAG_RETURN_PATCHED),
        "size": size,
        "crc32": crc,
        "actual_crc32": actual,
        "crc_ok": actual == crc,
        "name": _decode_ascii_field(name),
        "version": _decode_ascii_field(version),
        "reserved_signature": _decode_ascii_field(reserved),
        "image": image,
        "inspection": inspect_image(image),
    }


def main() -> int:
    ap = argparse.ArgumentParser(
        description="Validate a UV-K1 PY32F071 BIN and create an F4HWN v6 Multiboot slot package."
    )
    ap.add_argument("input", type=Path, help="raw UV-K1 application .bin")
    ap.add_argument("-o", "--output", type=Path, help="output .fmb file")
    ap.add_argument("--name", default="FOREIGN UVK1", help="slot display name (15 ASCII chars max)")
    ap.add_argument("--version", default="WRCX-U1", help="slot version (15 ASCII chars max)")
    ap.add_argument(
        "--kind",
        choices=tuple(KIND_FLAGS),
        default="foreign",
        help="compatibility class; default: foreign",
    )
    ap.add_argument("--inspect-only", action="store_true")
    ap.add_argument("--json", action="store_true", help="print machine-readable report")
    ns = ap.parse_args()

    try:
        data = ns.input.read_bytes()
        report = inspect_image(data)
    except (OSError, ImageError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2

    printable = {k: v for k, v in report.items()}
    printable["crc32"] = f"0x{report['crc32']:08X}"
    printable["initial_sp"] = f"0x{report['initial_sp']:08X}"
    printable["reset_vector"] = f"0x{report['reset_vector']:08X}"
    printable["reset_handler"] = f"0x{report['reset_handler']:08X}"

    if ns.json:
        print(json.dumps(printable, indent=2))
    else:
        print(f"Size:          {report['size']} / {APP_SIZE} bytes")
        print(f"CRC32:         0x{report['crc32']:08X}")
        print(f"Initial SP:    0x{report['initial_sp']:08X}")
        print(f"Reset vector:  0x{report['reset_vector']:08X}")
        print(f"Reset handler: 0x{report['reset_handler']:08X}")
        for warning in report["warnings"]:
            print(f"WARNING: {warning}")
        for error in report["errors"]:
            print(f"ERROR: {error}")
        print("Compatibility:", "PASS" if report["compatible"] else "REJECT")

    if ns.inspect_only:
        return 0 if report["compatible"] else 3
    if not report["compatible"]:
        return 3

    out = ns.output or ns.input.with_suffix(".fmb")
    try:
        slot = build_slot(data, ns.name, ns.version, ns.kind)
        out.write_bytes(slot)
    except (OSError, ImageError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 4

    if not ns.json:
        print(f"Wrote: {out}")
        if ns.kind == "foreign":
            print(
                "RECOVERY NOTE: raw foreign firmware does not contain the F4HWN menu. "
                "Factory USB bootloader recovery remains available."
            )
        elif ns.kind == "return-patched":
            print(
                "Return-patched flag set. Only use this if the image's return hook "
                "has been independently verified on the exact firmware build."
            )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
