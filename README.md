# WRCX212 Radio Lab

**Quansheng UV-K1 / UV-K5 custom firmware • GMRS • Multiboot • Smart Mic audio • Windows radio tools**

Welcome to the **WRCX212 Radio Lab**. This repository collects practical radio-firmware experiments, tested builds, Windows utilities, release notes, checksums, and recovery information for Quansheng radios and related GMRS/radio projects.

> **Latest validated project:** [UV-K1 Space Tweety + Smart Mic SM2 SAFE](uv-k1-universal-multiboot/releases/TW3-SM2/README.md)  
> Hardware-tested on a physical UV-K1. Normal boot, Smart Mic transmit leveling, Space Tweety, and Multiboot operation were confirmed.

## Featured Projects

| Project | What it does | Status |
|---|---|---|
| [**UV-K1 Space Tweety + Smart Mic SM2 SAFE**](uv-k1-universal-multiboot/releases/TW3-SM2/README.md) | Space-style 10-note Tweety roger tone, stronger mic drive, and BK4829 2:1 TX compression via `SmtMic = TX` | **Hardware validated** |
| [**UV-K1 Universal MultiBoot U1**](uv-k1-universal-multiboot/) | Extends F4HWN v6.0.0 Multiboot for compatible UV-K1 / UV-K5 V3 application images while preserving the factory recovery path | Build/vector/checksum verified |
| [**UV Console v0.9.15**](uv-console/README.md) | Native Windows programming, live LCD viewer, calibration, boot-logo, RF log, and DFU maintenance tools | Published |
| [**WRCX212 Gateway**](wrcx212-gateway/) | Radio/gateway experimentation and supporting tools | Active project |
| [**WRCX212 Tweety Bird v1.0**](releases/WRCX212_Tweety_Bird_v1.0_GitHub_Release.zip) | Quansheng-compatible Tweety Bird firmware release package | Published |
| [**UV-K5(8) MIC V1.7 Tweety Bird**](releases/MIC_V1.7_WRCX212_TWEETY_BIRD_RELEASE1_CRC_OK.packed.bin) | CRC-checked UV-K5(8) audio-control / Tweety Bird firmware | Published |

## Latest: UV-K1 Space Tweety + Smart Mic

The newest validated UV-K1 work combines the **TW3 Space Tweety** sound with the conservative **SM2 SAFE Smart Mic** transmit-audio modification.

- **Space Tweety:** fast 10-note sci-fi / telemetry-style roger tone
- **Smart Mic:** set `SmtMic = TX` for the BK4829 hardware **2:1 TX compressor**
- **Mic drive:** approximately **+3 dB** compared with the TW3 baseline
- **Boot safety:** no changes to the vector table, startup area, EEPROM layout, menu enumeration, Multiboot layout, or firmware length
- **Physical test:** normal boot and improved transmit modulation confirmed on a UV-K1
- **SHA-256:** `0408e48a548bc031196bb17f60a255d60496c9c59ba3ad4b11b78e7cb20efc63`

[View the TW3-SM2 release notes, manifest, and checksum →](uv-k1-universal-multiboot/releases/TW3-SM2/README.md)

## Downloads

Start with the [**full release catalog**](releases/README.md) for published downloads and flashing notes.

- [UV Console v0.9.15 GitHub package](releases/UVConsole_WRCX212_v0.9.15_GitHub_Package.zip)
- [WRCX212 Tweety Bird v1.0 GitHub release](releases/WRCX212_Tweety_Bird_v1.0_GitHub_Release.zip)
- [UV-K5(8) MIC V1.7 Tweety Bird firmware](releases/MIC_V1.7_WRCX212_TWEETY_BIRD_RELEASE1_CRC_OK.packed.bin)
- [UV-K1 Universal MultiBoot U1 project and verified build information](uv-k1-universal-multiboot/)

## What You Will Find Here

This repository focuses on hands-on radio development and experimentation, including:

- Quansheng **UV-K1** and **UV-K5 / UV-K5 V3** firmware
- **F4HWN v6.x** experiments and Multiboot work
- **BK4819 / BK4829** audio and RF-control experiments
- Microphone gain, transmit compression, deviation, and audio optimization
- Custom **Tweety Bird** and Space Tweety roger tones
- GMRS-oriented radio programming and testing
- Windows radio programming/control utilities
- Firmware manifests, SHA-256 checksums, release notes, and recovery guidance

## Before Flashing Custom Firmware

Custom firmware can make a radio fail to boot if the wrong image is used. Before experimenting:

1. Confirm the firmware is intended for your exact radio/hardware revision.
2. Back up calibration, EEPROM/settings, and your known-good firmware when possible.
3. Keep the normal factory recovery/flashing method available.
4. Verify the checksum of important firmware images.
5. After flashing, test boot, receive, transmit, audio, controls, and recovery behavior before relying on the build.

## Share the Radio Lab

If these projects are useful to you, **star the repository, share the project link, and point other UV-K1 / UV-K5 experimenters here**:

**https://github.com/Iceburgh746/Iceburgh746**

When sharing a specific project, link directly to that project or release so people can immediately see the documentation, compatibility notes, and checksums.

---

**WRCX212** • Radio firmware experimentation • Quansheng UV-K1 / UV-K5 • GMRS • Custom radio tools
