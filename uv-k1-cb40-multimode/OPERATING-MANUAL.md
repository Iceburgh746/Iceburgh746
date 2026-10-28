# WRCX212 CB-40 Multimode v0.3 — Operating Manual

**Radio:** Quansheng UV-K1 with a compatible F4HWN Labs application loader  
**Application:** `WRCX212-CB40-MULTIMODE-v0.3.app`  
**Intended use:** 40-channel CB reception and scanning; receive-only  
**Firmware cited by community submission:** F4HWN v6.3.0  
**Document revision:** 1.0 (October 28, 2026)

> **Verification note:** This manual is based on the supplied v0.3 compiled application's embedded interface strings and published project information. Controls are identified from the on-screen labels, but their detailed transitions and modulation performance have **not been independently demonstrated on a physical UV-K1**. If your radio displays different controls, follow the application's screen prompts.

## 1. Before you begin

1. Confirm your radio is the supported UV-K1 hardware revision and is running a compatible **F4HWN Labs** firmware. The community submission lists **v6.3.0** and the catalog validator identifies **API level 2**.
2. Save a backup of your radio configuration and known-good firmware if possible.
3. Install the **`.app` application overlay** through **UV Studio → Apps** (or the appropriate Labs application manager). Do **not** flash an `.app` file as full radio firmware.
4. Select **WRCX212 CB-40** from the radio's application list and launch it.

The author's catalog submission includes an embedded application binary; this repository currently provides project documentation and a **reconstructed reference C file**, not the original buildable v0.3 source.

## 2. Screen and controls

The following exact messages were identified inside the supplied v0.3 application:

| Display text | Meaning / operating guidance |
|---|---|
| `WRCX212 CB-40` | Application title |
| `M:MODE 5:SCAN *:MON` | **M** invokes mode selection; **5** invokes scanning; **\*** invokes monitor |
| `MODE: UP/DOWN` | Use **UP/DOWN** in the mode-selection screen |
| `M/EXIT: RETURN` | Use **M** or **EXIT** to return from the current selection screen |
| `CH 01 USB RX ONLY` | Sample channel/mode display; `USB` is **upper sideband**, not a USB cable connection |
| `LSB UNAVAILABLE` | The application explicitly indicates that lower-sideband reception is unavailable in at least one operating state |
| `FCALIBRATING NOISE` | Noise calibration in progress |
| `MONITOR RX ONLY` | Monitoring mode, with transmission blocked |
| `SIGNAL / SCAN WAIT` | Activity detected; scanner appears to be waiting |
| `SCAN / TX BLOCKED` | Scanning state; transmission blocked |
| `HOLD / TX BLOCKED` | Hold state; transmission blocked |

**Important:** The application's visible interface contains USB/LSB-related messages. Do not assume it implements conventional DSB demodulation merely because the project is named “Multimode.” Functional mode support must be checked by receiving known AM/FM/SSB test signals.

## 3. Listening to a CB channel

1. Open **WRCX212 CB-40**.
2. Note the channel number and displayed frequency.
3. Use the application's displayed channel-selection controls to move among channels. The exact channel-change key mapping was **not positively verified** from embedded text and should be confirmed on the radio.
4. Use **M** to enter the mode-selection interface. Where prompted, use **UP/DOWN** to select a mode and **M/EXIT** to return.
5. Listen for traffic. For ordinary US CB voice channels, **AM** is a good starting mode. FM or sideband reception depends on the receiving implementation and the signal being monitored.

CB channels are numbered **1–40**. The channel table is in the [project README](README.md); example: channel 19 is **27.185 MHz**.

## 4. Automatic scanning

1. On the application's main screen, press **5** where `5:SCAN` is shown.
2. Observe channel changes and the `SIGNAL / SCAN WAIT` indicator. This suggests signal-based stopping or holding, but the exact dwell and resume timings have **not** been verified for the v0.3 binary.
3. Press **5** again to test whether it stops or restarts scanning; confirm the actual behavior on your radio.
4. To leave the application or a submenu, follow the displayed **M/EXIT** prompt. Test this first with no important radio traffic in progress.

**Practical test:** Set a second authorized signal source to a known channel, verify that scanning pauses while it is active and resumes after it ceases. Do not transmit on unauthorized frequencies.

## 5. Monitor and squelch

- On the main screen, **\*** corresponds to `MON` (monitor). Use it to inspect weak/noisy reception behavior.
- The v0.3 code also contains the phrase `FCALIBRATING NOISE`; allow its noise-calibration process to complete if shown.
- If the speaker stays open continuously or scanning never pauses, review antenna connections, interference and the selected squelch/monitor state.
- The exact numerical squelch controls and settings of **v0.3 are not confirmed**. Do not assume the `1/7:SQ` controls from the older AM-only v0.2 release still apply.

## 6. AM / FM / sideband modes

| Mode | Typical purpose | Verification status |
|---|---|---|
| AM | Conventional CB voice reception | Expected application function; requires radio test |
| FM | CB FM signals where permitted | User-facing multimode design; demodulation unverified |
| USB | Upper-sideband CB voice | USB-related display confirmed; actual decoding unverified |
| LSB | Lower-sideband CB voice | `LSB UNAVAILABLE` appears in the binary; **do not advertise as supported** |
| DSB | Double-sideband reception | Project-level label only; **actual demodulation unverified** |

Sideband reception usually requires accurate fine tuning to sound natural. There is no basis yet to promise that this build includes fine tuning, an adjustable BFO or complete SSB decoding.

## 7. Troubleshooting

| Symptom | What to check |
|---|---|
| App doesn't appear | Installed to **Apps**, not flashed as firmware; verify supported firmware/API |
| App closes or radio freezes | Remove the overlay and restore known-good settings; report firmware version and actions causing the failure |
| No signal received | Try channel 19 AM (27.185 MHz), a suitable CB antenna, and local active traffic; the UV-K1's 27 MHz RF performance may be limited |
| Signals are distorted | Verify the mode against a known signal; sideband reception may need capabilities not present in this build |
| Scanner sticks on one channel | Check monitor/squelch state and local RF noise; compare behavior with and without a known signal |
| LSB won't select | The binary includes **`LSB UNAVAILABLE`**; this is currently a limitation, not necessarily a malfunction |
| Transmit does not work | **Expected:** this application is **receive-only** |

## 8. Recommended acceptance test

Before calling v0.3 fully tested, confirm on the actual UV-K1:

- [ ] App installs and launches on F4HWN v6.3.0.
- [ ] Channel selection reaches all 40 frequencies in correct CB-channel order.
- [ ] AM receives a known local signal intelligibly.
- [ ] FM and sideband selections match actual working demodulation, or are explicitly documented as unavailable.
- [ ] **5:SCAN** starts, holds on activity, and resumes reliably.
- [ ] **\*:MON** and **M:MODE** perform their indicated functions.
- [ ] EXIT returns safely to the radio menu.
- [ ] No PTT or RF transmission occurs from the application.

## 9. Source, support and status

- [CB-40 Multimode project](README.md)
- [Published C reference source](cb40_multimode_reference.c) — **not** the original v0.3 build source
- [Fusion Community catalog submission #17](https://github.com/armel/fusion-community/issues/17)

**App integrity:** The community submission validator confirmed the binary header, code size and CRC. It does not certify receiver performance. Report observed radio behavior and firmware version when providing feedback.

**WRCX212 Radio Lab — experimental software, receive only.**
