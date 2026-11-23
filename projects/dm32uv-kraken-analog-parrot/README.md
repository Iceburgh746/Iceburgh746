# WRCX212 Kraken Analog FM Parrot Replacement

**Status (2026-11-23): SOURCE REPLACEMENT WRITTEN AND PUBLISHED; HOST/BUILD/QEMU AND HARDWARE RESULTS NOT YET VERIFIED. NEVER FLASH SOLELY BECAUSE A SOURCE PATCH EXISTS.**

## Purpose
Replace the Baofeng DM-32UV Kraken firmware's DMR-only Parrot with a narrow-band analog FM store-and-forward recorder. The user explicitly authorizes replacing DMR Parrot; preserving it is no longer required.

## Current code deliverables
- `overlay/krk_parrot.c`: replacement Parrot source; DMR RX hook entry points remain inert for ABI compatibility; original Parrot screens now control analog recording/replay.
- `apply_analog_parrot.py`: backs up three original source files with SHA256, copies the replacement, hooks the existing 10ms background tick before APRS monitor, relabels Channel Set rows and writes a JSON modification report. Refuses unknown baselines.
- `BUILD_ANALOG_PARROT_V2.ps1`: isolated one-shot Windows/WSL2/Docker source fetch, patch, generator, Kraken build and QEMU test controller. No radio flash.
- `analog_parrot_core.c/.h`, `tests/test_core.c`: earlier portable **4-bit ADPCM** research core with a separate codec test. Not linked into firmware; DO NOT mistake its tests for testing the embedded 2-bit codec.
- `tests/mock/` and `.github/workflows/wrcx212-analog-parrot.yml`: host syntax smoke test for the replacement source using mocked public interfaces; it does not emulate the radio.

## Implementation
- **Receive:** analog FM 8kHz 16-bit big-endian receive RAM via `FM_RX_HALF`, `FM_ADDR_SW` and GPIO `GPIO_AF_SWITCH` with `AUDIO_CONTROL[6]`. These are the same pathways as upstream `krk_aprsrx.c`.
- **Storage:** existing shared `krk_vf_big_claim(1)` 5,616-byte RAM, shared with Burst Voice; exclusive owner, no new SRAM claim. 8kHz receive samples downsample to 4kHz and are compressed using an experimental **2-bit adaptive differential codec** at 1kB/sec. Theoretical cap 5.616 seconds; **audio intelligibility and bandwidth are NOT hardware-verified**.
- **Transmit:** `krk_fm_send_opt` with big-endian 8kHz PCM fill, original analog FM modem route, abort polling, time budget, busy-channel and TX inhibit gates.
- **UI:** existing Channel Set rows rebranded `Analog Parrot`, `Replay Audio`, `Audio Check`, `Clear Audio`, `Status`. `Audio Check` displays the captured duration and does not transmit.
- **Auto-echo:** only while the Status screen remains open; 1.5 s quiet before replay, 3 s cooldown after replay. Manual replay needs two presses of OK.
- **Safe defaults:** Off after power-on; no automatic RX-to-TX loop when closed; no change to calibration/firmware during source patching; no auto radio write; refuse operation on DMR channels, dual-watch, active TX or TX inhibited channels.

## One-run local build controller (Windows PowerShell)
The controller is published at:
https://raw.githubusercontent.com/Iceburgh746/Iceburgh746/main/projects/dm32uv-kraken-analog-parrot/BUILD_ANALOG_PARROT_V2.ps1

It requires Windows PowerShell, Git, Python3, WSL Ubuntu and Docker Desktop with WSL integration. It discovers installed tools, clones upstream to a separate directory, installs the overlay with backups, runs code generators, builds its Docker image if not installed, runs Kraken build/QEMU tests and logs the results. It DOES NOT FLASH. It does not claim success unless the actual native command returns exit code 0.

To run in PowerShell (with administrator rights only if your Docker/WSL configuration needs them):

```powershell
$u='https://raw.githubusercontent.com/Iceburgh746/Iceburgh746/main/projects/dm32uv-kraken-analog-parrot/BUILD_ANALOG_PARROT_V2.ps1'
$p='C:\Radtel\WRCX212_KRAKEN_ANALOG_PARROT\BUILD_ANALOG_PARROT_V2.ps1'
New-Item -ItemType Directory -Force -Path (Split-Path $p) | Out-Null
Invoke-WebRequest $u -OutFile $p -UseBasicParsing -ErrorAction Stop
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $p
```

## Verification gates — required before any radio write
1. The host mock compilation and both codec tests must pass; the embedded 2-bit codec still needs test vectors and audio SNR/quality checks.
2. Python source patcher must verify backups, expected upstream signature, generated menu and manifest.
3. Real Kraken toolchain build must complete with **no flash/packed SRAM claim overlaps**, all expected symbols, and verified update-file size/hash.
4. Kraken QEMU regression suite should pass; replace/adjust only assertions explicitly about removed DMR Parrot rather than hiding unrelated failures.
5. Hardware tests must be performed with a dummy load or authorized low-power test channel. Check receive capture with clean audio, no echo-on-boot, PTT release under abort/timeouts, transceiver power-save, APRS/Burst Voice mutual exclusion, side/frequency lock, and analog deviation.
6. Confirm current jurisdiction/band rules for automatic voice relay; do not deploy unattended without authorization.

## Known limitations
- Existing Kraken DMR Parrot **is replaced**, not preserved. DMR test expectations may require updating.
- The 2-bit codec is designed for low SRAM use; **intelligibility, input filtering, clipping, modulation deviation and sample continuity need physical measurement**.
- Existing DMR hook splices remain loaded but are no-ops for Parrot, and Burst Voice should keep its original functionality.
- Firmware build cannot be validated by static code inspection. The Windows builder still needs to run on a system with the Kraken Docker image and stock firmware.
- The flash update file is generated only by the original Kraken build system on the user's computer; never commit or publicly redistribute a firmware image containing Baofeng stock code.

## Upstream source references
- https://github.com/awkaplan/dm32uv-kraken/blob/main/patches/kraken_ui/krk_aprsrx.c
- https://github.com/awkaplan/dm32uv-kraken/blob/main/patches/kraken_ui/krk_aprs_tx.c
- https://github.com/awkaplan/dm32uv-kraken/blob/main/patches/kraken_ui/krk_parrot.c
- https://github.com/awkaplan/dm32uv-kraken/blob/main/patches/kraken_ui/krk_periodic.c
- https://github.com/awkaplan/dm32uv-kraken/blob/main/docs/building.md
