# WRCX212 UV-K1 Universal MultiBoot U1

Source-level extension for **F4HWN v6.0.0** on the Quansheng **UV-K1 / UV-K5 V3 (PY32F071)** that lets the F4HWN Multiboot external-flash slots carry compatible non-F4HWN UV-K1 application images.

## Ready-to-flash U1 base firmware

The compiled Fusion build is now stored permanently in this repository:

`releases/U1/WRCX212_UVK1_F4HWN_6.0.0_Universal_MultiBoot_U1.bin`

Build verification:

- Size: **107,032 bytes** of the 120,832-byte application region.
- Free application flash: **13,800 bytes**.
- Application base: `0x08002800`.
- Initial stack pointer: `0x20004000`.
- Reset vector: `0x08002D49` (Thumb).
- Reset handler: `0x08002D48`.
- SHA-256: `73fbb3f1f8d247340ed23111dec2a59bc2db2381047b2e985bd2c78a3ed099f7`.
- ARM build, linker, Multiboot RAM-stub isolation, size and vector checks passed.
- This exact build has **not yet been tested on a physical radio**, so keep a known-good recovery BIN and factory flashing method available for the first hardware test.

`releases/U1/BUILD_MANIFEST.json` and `releases/U1/SHA256SUMS.txt` contain the machine-readable verification information.

## What U1 does

- Keeps the F4HWN v6.0.0 five-slot / config-bank architecture.
- Keeps slot 0 protected as the firmware-managed Main backup.
- Uses the existing FMB1 128 KiB slot format and CRC32.
- Adds a `FOREIGN` slot flag without changing the 64-byte header size.
- Adds radio-side validation of a foreign image's ARM vector table before any internal flash erase:
  - image size <= 118 KiB;
  - initial stack pointer is inside the PY32F071 16 KiB SRAM window;
  - reset vector has the Thumb bit set;
  - reset handler points inside the supplied application image beginning at `0x08002800`.
- Provides a PC-side BIN inspector/slot packer.
- Provides a PC-side uploader that uses F4HWN's authenticated `0x0722/0x0724/0x0726` slot API.
- Writes the slot header **last**, so an interrupted upload remains uncommitted and Multiboot refuses it.

## Critical architecture limitation

F4HWN v6 Multiboot is an **application-level** selector. Restoring a slot replaces the complete application region at `0x08002800..0x0801FFFF`.

Therefore a totally unrelated precompiled firmware does **not** automatically contain the F4HWN Multiboot user interface after it is booted.

U1 supports three practical classes:

| Class | Stored in slot | Boots | Return to F4HWN Multiboot |
|---|---:|---:|---|
| F4HWN/native | Yes | Yes | Yes |
| Foreign source build that itself includes/ports the Multiboot code | Yes | Yes | Yes |
| Raw compatible foreign BIN | Yes | Yes | **Factory USB bootloader recovery / reflash F4HWN required** |

A universal seamless round-trip for *arbitrary* unmodified binaries would require persistent selector code below `0x08002800` (i.e. replacing or extending the factory bootloader). U1 deliberately **does not modify the factory bootloader**.

That restriction is intentional: preserving the factory recovery path is more important than pretending every binary can be made safely chain-loadable.

## Files

- `releases/U1/WRCX212_UVK1_F4HWN_6.0.0_Universal_MultiBoot_U1.bin` — compiled Fusion U1 base firmware.
- `releases/U1/BUILD_MANIFEST.json` — verified build metadata.
- `releases/U1/SHA256SUMS.txt` — firmware checksum.
- `tools/apply_f4hwn_u1.py` — deterministic source patcher used by the verified build workflow.
- `tools/make_slot.py` — validates a raw BIN and creates a 128 KiB `.fmb` slot package.
- `tools/upload_slot.py` — uploads the package to user slot 1..4 over the F4HWN serial/VCP protocol.
- `requirements.txt` — PC dependency (`pyserial`).
- `patches/f4hwn-v6.0.0-universal-multiboot.patch` — exact generated source diff from the successful build.
- `docs/PROTOCOL.md` — slot format and host command notes.

## Rebuild the patched F4HWN base firmware

Apply the generated patch to the upstream **v6.0.0** tree:

```bash
git checkout v6.0.0
git apply patches/f4hwn-v6.0.0-universal-multiboot.patch
./compile-firmware.sh Fusion
```

The repository's GitHub Actions build uses `tools/apply_f4hwn_u1.py` from a clean official v6.0.0 checkout and publishes the verified result.

Before experimenting with a foreign slot, flash and test the patched F4HWN build normally and allow it to create/verify its Main backup (slot 0).

## Inspect and package a foreign BIN

```bash
python tools/make_slot.py other-uvk1.bin --inspect-only
python tools/make_slot.py other-uvk1.bin \
  --name "TWEETY K1" \
  --version "v1.0" \
  --kind foreign \
  -o tweety-k1.fmb
```

A foreign image is rejected if its basic PY32F071 application vectors do not fit the UV-K1 application layout.

## Upload to a Multiboot slot

Install the PC dependency:

```bash
python -m pip install -r requirements.txt
```

Find the radio port:

```bash
python tools/upload_slot.py --list-ports
```

Upload to slot 2, for example:

```bash
python tools/upload_slot.py tweety-k1.fmb --port COM5 --slot 2
```

The uploader:

1. starts a `0x0514` session and latches a random timestamp;
2. erases only the selected external-flash user slot;
3. writes the firmware image at slot offset `0x1000`;
4. writes the 64-byte committed header last;
5. asks the radio to compute/verify the full slot CRC with `0x0726`.

It does **not** restore the image into MCU internal flash. That final action remains on the radio's Multiboot screen.

## Recovery rule for raw foreign firmware

If you select a raw foreign BIN and it boots, assume the F4HWN menu is no longer present.

To get back:

1. use the radio's normal factory USB/firmware recovery method;
2. reflash the patched F4HWN U1 base image;
3. do **not** erase the external SPI flash;
4. F4HWN can rescan the retained FMB1 slots.

The exact key sequence for entering the factory flasher depends on the UV-K1/K5 V3 bootloader and flashing tool you use; follow the known-good procedure for your radio.

## Do not import these as foreign slots

- UV-K5 V1/V2 DP32G030 firmware;
- images linked for an application base other than `0x08002800`;
- full-chip dumps containing bootloader/calibration/options rather than an application BIN;
- binaries larger than 118 KiB;
- images with a reset handler outside their own application extent;
- unknown encrypted/compressed update containers.

## Hardware-test status

The firmware has been successfully compiled from the official F4HWN v6.0.0 source and passed automated linker, size, vector, checksum and Multiboot RAM-stub checks. A real-radio test is still required before calling the U1 modification hardware-validated. Always keep a known-good F4HWN BIN and the factory recovery flashing path available.

## Credits

Based on the F4HWN UV-K1 / UV-K5 V3 firmware by Armel F4HWN and contributors.

WRCX212 Universal MultiBoot U1 project packaging and safety extensions for experimentation.
