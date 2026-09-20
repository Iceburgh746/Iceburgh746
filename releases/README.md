# WRCX212 Release Catalog

This catalog collects published WRCX212 downloads plus the newest validated UV-K1 development work. Verify filenames and checksums before flashing firmware.

## Latest validated UV-K1 project — Space Tweety + Smart Mic SM2 SAFE

**Release documentation:** [TW3-SM2 release notes and validation](../uv-k1-universal-multiboot/releases/TW3-SM2/README.md)

This is the newest hardware-validated UV-K1 project in the repository. It combines the TW3 Space Tweety roger tone with the conservative SM2 SAFE Smart Mic modification.

- Physical UV-K1 boot test passed
- Smart Mic transmit leveling confirmed
- Space Tweety retained
- Multiboot retained
- `SmtMic = TX` enables the BK4829 2:1 TX compressor/leveler
- Microphone table increased by approximately +3 dB
- SHA-256: `0408e48a548bc031196bb17f60a255d60496c9c59ba3ad4b11b78e7cb20efc63`

The project folder includes the build manifest, patch notes, and checksum documentation.

## UV-K1 Universal MultiBoot U1

**Firmware:** [WRCX212_UVK1_F4HWN_6.0.0_Universal_MultiBoot_U1.bin](../uv-k1-universal-multiboot/releases/U1/WRCX212_UVK1_F4HWN_6.0.0_Universal_MultiBoot_U1.bin)

**Project:** [UV-K1 Universal MultiBoot U1](../uv-k1-universal-multiboot/)

Source-level F4HWN v6.0.0 Multiboot extension for compatible Quansheng UV-K1 / UV-K5 V3 PY32F071 application images. Build, vector, size, and checksum checks passed. See the project README for the current hardware-test status and recovery limitations.

## UV Console v0.9.15

**File:** [UVConsole_WRCX212_v0.9.15_GitHub_Package.zip](UVConsole_WRCX212_v0.9.15_GitHub_Package.zip)

Native Windows desktop console for compatible Quansheng UV-K1 / UV-K5 V3 Fusion radios. The package includes source, Windows build instructions, and a checksum file.

## WRCX212 Tweety Bird v1.0

**File:** [WRCX212_Tweety_Bird_v1.0_GitHub_Release.zip](WRCX212_Tweety_Bird_v1.0_GitHub_Release.zip)

Rebranded firmware release package. Includes the `.bin` image, SHA-256 checksum, release notes, and flashing guide. Visible build labels were changed to `WRCX212 Tweety Bird v1.0`.

## UV-K5(8) MIC V1.7 Tweety Bird Release 1

**File:** [MIC_V1.7_WRCX212_TWEETY_BIRD_RELEASE1_CRC_OK.packed.bin](MIC_V1.7_WRCX212_TWEETY_BIRD_RELEASE1_CRC_OK.packed.bin)

Packed UV-K5(8) firmware image with CRC-checked release packaging. Project notes identify this as the MIC V1.7 audio-control and Tweety Bird release.

## Flashing reminder

Use only firmware intended for the compatible radio and hardware revision. Back up calibration and your existing firmware before flashing, keep a known-good recovery image available, and test normal boot, receive, transmit, audio, controls, and recovery behavior after flashing.

---

Return to the [**WRCX212 Radio Lab home page**](../README.md).
