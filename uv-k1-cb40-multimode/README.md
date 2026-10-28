# WRCX212 CB-40 Multimode — Quansheng UV-K1

**Project status: WRCX212-CB40-MULTIMODE-v0.3.app provided and structurally verified, with a Fusion Community submission under review. Radio operation and individual modulation modes have not been independently hardware-tested in this review.**

A 40-channel Citizens Band (CB) receiver concept for the Quansheng UV-K1 and F4HWN Labs environment. The intended operating modes are **AM**, **FM**, and **DSB** (double-sideband). Mode selection and actual demodulation behavior require a compatible implementation and hardware verification; a channel-list file alone does not add radio demodulation modes.

## Operating manual

[**Read the WRCX212 CB-40 Multimode v0.3 Operating Manual**](OPERATING-MANUAL.md) — installation, on-screen controls, channel operation, scanning, monitor and squelch, modulation notes, troubleshooting and a hardware-test checklist.

## Source reference

[**View/copy the CB-40 multimode C reference source**](cb40_multimode_reference.c). This is a documented reconstruction of the channel table, scanner state machine and radio-adapter interfaces, **not the original source used to compile v0.3**. The original source has not been recovered from the binary; the reference requires hardware-specific implementation before it can build into a working `.app`.

## Channel plan

The standard US 40-channel CB frequencies are listed below. All frequencies are in MHz. Channel order follows the standard CB numbering (not sorted by frequency).

| Channel | MHz | Channel | MHz | Channel | MHz | Channel | MHz |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 26.965 | 11 | 27.085 | 21 | 27.215 | 31 | 27.315 |
| 2 | 26.975 | 12 | 27.105 | 22 | 27.225 | 32 | 27.325 |
| 3 | 26.985 | 13 | 27.115 | 23 | 27.255 | 33 | 27.335 |
| 4 | 27.005 | 14 | 27.125 | 24 | 27.235 | 34 | 27.345 |
| 5 | 27.015 | 15 | 27.135 | 25 | 27.245 | 35 | 27.355 |
| 6 | 27.025 | 16 | 27.155 | 26 | 27.265 | 36 | 27.365 |
| 7 | 27.035 | 17 | 27.165 | 27 | 27.275 | 37 | 27.375 |
| 8 | 27.055 | 18 | 27.175 | 28 | 27.285 | 38 | 27.385 |
| 9 | 27.065 | 19 | 27.185 | 29 | 27.295 | 39 | 27.395 |
| 10 | 27.075 | 20 | 27.205 | 30 | 27.305 | 40 | 27.405 |

## Development references

- Existing project baseline: **WRCX212-CB40-AM-v0.2-squelch.app** (AM-specific; not a verified multimode release).
- User-created programming tables include AM/FM/DSB channel entries and DSB-labeled channels.
- Planned controls: channel selection, automatic scanning, pause on receive activity, and mode selection.
- Target hardware: **Quansheng UV-K1 / compatible F4HWN Labs build**.
- Intended use: **receive only**.

## Compatibility and limitations

An actual CB40 multimode-labeled v0.3 overlay binary has now been supplied. The binary's structural integrity does **not** by itself establish that AM, FM and DSB demodulation all work correctly. In particular, FM or DSB display labels must not be mistaken for verified demodulation. The uploaded binary has an FAP1 header, embedded version 0.3, API level 2 (per Fusion Community validation), 3,428 code bytes within the 4,096-byte overlay budget, and an internally matching CRC32. Exact firmware compatibility and receiver functionality still need on-radio verification. Reception near 27 MHz may be limited by RF front-end behavior.

**Supplied build:** `WRCX212-CB40-MULTIMODE-v0.3.app` (3,492 bytes total). SHA-256: `c0f6cfecb7e8008d7bbde6d84b0c53a592045f6ed116fcd0d0e5d9456573c1e3`. App code CRC32: `c2a819b9`. Embedded strings include `MODE: UP/DOWN`, `M:MODE 5:SCAN *:MON`, `MONITOR RX ONLY`, and `LSB UNAVAILABLE`; mode support should not be assumed beyond physical verification.

**Installation:** Install only a verified compatible `.app` using the Labs app installer, not the full firmware flasher. The binary is not yet uploaded in this GitHub folder. The Fusion Community submission embeds the actual app binary. The published C reference is not its original build source.

---

WRCX212 Radio Lab · Experimental software · No guaranteed performance
