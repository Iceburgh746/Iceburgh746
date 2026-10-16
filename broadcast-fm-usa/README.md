# WRCX212 Broadcast FM USA — 1.1-USA

Custom receive-only Broadcast FM overlay for Quansheng UV-K1 running **official F4HWN Labs v6.2.0**. The radio's app list identifies it as **WRCX212FM-USA** (the header permits only 15 visible characters). The receiver screen says **FM USA**.

This is a compiled `.app`, built from the official Broadcast FM source with a USA frequency-plan adaptation. It is an independent customization, not an official F4HWN release or endorsement.

Version 1.1-USA fixes the reported label/frequency overlap by drawing FM USA with the single-page small font at page 0; the frequency remains on pages 1–2. This rebuild passed compilation, format/checksum checks and source layout review. The emulator suite could not be rerun because its local dependency directory was inaccessible; prior behavior-test results are retained separately in VALIDATION.json.

## Download and install

- [Download the installable .app](downloads/WRCX212-Broadcast-FM-USA-Labs-6.2.0.app?raw=true)
- [Download the complete source ZIP](downloads/WRCX212-Broadcast-FM-USA-source.zip?raw=true)
- [SHA-256 checksums](downloads/SHA256SUMS.txt)
- [Browse the source](App/apps/fm/fm_app.c) · [Validation record](VALIDATION.json) · [Upstream reproduction check](BASELINE.json)

On GitHub's file view, use **Download raw file** if the download does not start. This is an overlay app, not a firmware image.

1. Start the radio normally in Labs 6.2.0, connect its data cable, and open [UV Studio](https://armel.github.io/uvstudio/) in a supported desktop browser.
2. In **Apps (Labs only)**, choose the local `.app` installation option and select the delivered file. Use an empty slot if you want to retain the original app. Wait for installation and verification to finish.
3. On the radio press **F**, then **7**, select **WRCX212FM-USA**, and press **M**.

The app advertises the FM shortcut. If the stock Broadcast FM app is also installed, the compatible app in the lower-numbered slot receives that shortcut. Launch this one by name to avoid ambiguity. See the [official overlay guide](https://github.com/armel/uv-k1-k5v3-firmware-custom/wiki/Overlay-apps) for installer and launcher details.

## Behavior and controls

- Frequencies: **88.1, 88.3, …, 107.9 MHz**, exactly 100 channels; navigation and scanning use **200 kHz** increments. Both ends wrap during manual tuning and manual seek.
- Hardware stays on BK1080 band 0. Its 87.5 MHz hardware reference remains unchanged; the app restricts the selectable frequencies in software. Changing the hardware reference to 88.1 would mistune the receiver.
- Navigation follows firmware `SetNav` (UV-K1 left/right). Hold for repeat.
- Enter `881` for 88.1 or `1079` for 107.9. Even tenths and out-of-range input are rejected, leaving the prior frequency selected.
- **F+3**, or long **3**, switches VFO/memory mode.
- In VFO, **M** opens save, navigation or a two-digit slot number chooses 01–48, and **M** confirms.
- In memory mode, **M**, then **M** deletes the current preset. Numeric entry selects a slot; navigation skips unusable slots.
- **\*** starts/stops manual seek. Navigation changes direction. A found station becomes the selected VFO frequency.
- **F+\***, or long **\***, starts auto-scan. It checks from 88.1 through 107.9 and stops at the end or after 48 stations. **Auto-scan clears and replaces the shared 48-preset table**, as the upstream app does. A direction change at a band endpoint stops rather than wrapping indefinitely.
- **F+1**, or long **1**, leaves the fixed USA plan unchanged.
- **EXIT** cancels partial input or a save/delete prompt first; otherwise it exits. **F+0**, or long **0**, also exits.

Existing presets are shared with the stock FM receiver. Off-plan presets are skipped, not rounded or erased simply by launching this app. 107.9 MHz presets are valid. An imported off-plan VFO is clamped to the range and an even tenth is rounded down to an odd tenth. With no usable memory, the app falls back to VFO. Normal exit stages the FM state and preset table for the resident firmware's deferred persistence.

## Validation and limits

Built with Arm GNU Toolchain **13.3.Rel1 / GCC 13.3.1**, the version selected by upstream's Dockerfile. Building the *unmodified* v6.2.0 FM source reproduced the official release app's code and assets **byte for byte**; see `BASELINE.json`.

The customized file has:

| Item | Value |
|---|---|
| File size | 2,382 bytes |
| Overlay allocation, including globals | 2,236 / 4,096 bytes |
| Read-only assets | 82 bytes |
| Header / ABI / minimum API | 1 / 1 / 2 |
| Link address | `0x20000280` |
| Required capability | FM (`0x00000001`) |
| Entry offset | 0, Thumb entry |
| Flags | committed, screen saver, FM shortcut (`0x0103`) |

Compiler warnings are treated as errors. The linker bounds the entire overlay, and validation checks the entry, unresolved symbols, header fields, lengths, code CRC-32 and asset checksum. The version 1.0-USA compiled Thumb binary was run under Unicorn with mocked firmware callbacks: all 100 channels in both directions, 221 input frequencies, imported settings, navigation, presets, scan endpoints/full table/no stations, cancellation and screen wake. All 15 reported test groups passed for 1.0-USA; see `VALIDATION.json` and `test_binary.py`.

**Hardware evidence is limited.** A user-provided UV-K1 photo showed the earlier build running at 104.5 MHz and revealed the label overlap corrected in 1.1-USA. That photo confirms the earlier app reached its receiver screen; it does not verify audio, station detection, all controls or persistence. No physical radio was available to the developer. The 1.1-USA display fix and full receiver behavior still need on-radio confirmation. Simulated callbacks verify the app's calls and control flow, not the implementation or timing of the real hardware. No firmware was flashed and no radio settings were changed during development.

This changes the **frequency plan**, not BK1080 audio/de-emphasis settings, sensitivity thresholds, antenna performance, or the firmware's station-lock algorithm. It preserves the upstream FM audio path and lack of simultaneous normal-radio dual watch while this overlay runs. It does not add transmit capability. Other firmware editions, custom builds, and versions are not validated targets even if their loader accepts the metadata.

For a first radio check, tune a known station, verify 107.9 → 88.1 wrap, test manual seek, save/recall a preset, exit normally, and check the preset after restarting. Run auto-scan only when willing to replace the shared preset list.

## Version history

- **1.1-USA (2026-10-16):** smaller single-row FM USA label to prevent overlap with large frequency digits; frequency plan and controls retained.
- **1.0-USA (2026-10-16):** initial USA plan, odd-tenth entry validation, 200 kHz navigation/scan, preset filtering, inclusive 107.9 MHz memory support, and bounded auto-scan reversal.

## Source and reproduction

Official source: [armel/uv-k1-k5v3-firmware-custom, v6.2.0](https://github.com/armel/uv-k1-k5v3-firmware-custom/tree/v6.2.0).

Pinned commit: `0ade03c0b1c94514566fadf7ce1c7a1f542a2283`.

This package includes all source needed to rebuild this overlay, the exact upstream API/header/packer and asset helper, the modified FM source, a unified patch, licensing notices, ELF/map/stack-usage evidence and validation scripts. A full firmware checkout is not needed.

Install Python 3 and Arm GNU Toolchain 13.3.Rel1 for your operating system. From this directory:

```text
python -X utf8 build.py --toolchain PATH_TO_ARM_TOOLCHAIN_BIN
```

The built file is `App/apps/fm/WRCX212FM-USA.app`; it is byte-identical to the longer-named delivered `.app`. `build.sh` also preserves the upstream shell build flow. For binary tests:

```text
python -m pip install unicorn==2.1.4 pyelftools==0.33
python -X utf8 test_binary.py --toolchain PATH_TO_ARM_TOOLCHAIN_BIN
```

Toolchain download source used: [Arm 13.3.Rel1 Windows archive](https://armkeil.blob.core.windows.net/developer/Files/downloads/gnu/13.3.rel1/binrel/arm-gnu-toolchain-13.3.rel1-mingw-w64-i686-arm-none-eabi.zip). The compiler and test libraries are not bundled.

SHA-256 of the installable app:

```text
e66886162fe5808d6aad95596eb9dcdb9ca163d953313bb9086ce3929d47ba85
```

Upstream Apache-2.0 license and NOTICE are retained. Local modifications are identified in `changes.patch` and the source comments, dated 2026-10-16.
