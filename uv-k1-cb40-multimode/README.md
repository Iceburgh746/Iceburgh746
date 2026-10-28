# WRCX212 CB-40 Multimode — Quansheng UV-K1

**Project status: channel plan/documentation published; multimode `.app` binary not yet located or verified.**

A 40-channel Citizens Band (CB) receiver concept for the Quansheng UV-K1 and F4HWN Labs environment. The intended operating modes are **AM**, **FM**, and **DSB** (double-sideband). Mode selection and actual demodulation behavior require a compatible implementation and hardware verification; a channel-list file alone does not add radio demodulation modes.

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

The available AM scanner baseline and the channel data do **not** prove that a compiled AM/FM/DSB Labs app is available. In particular, FM or DSB display labels must not be mistaken for verified demodulation. A correct release needs the actual compiled `.app`, exact firmware API/version compatibility, build validation, and receiver tests on the UV-K1. Reception near 27 MHz may be limited by RF front-end behavior.

**Installation:** Do not attempt to install this README or a CSV as a radio application. An actual compatible `.app` must be installed using the Labs app installer, not the full firmware flasher.

---

WRCX212 Radio Lab · Experimental software · No guaranteed performance
