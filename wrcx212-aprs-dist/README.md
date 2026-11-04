# WRCX212 APRS DIST v1.6 — Quansheng UV-K1

**APRS receive and straight-line distance display for Quansheng UV-K1 running F4HWN Labs 6.3.0.**

**Release status:** v1.6 built and tested on a physical UV-K1 for live APRS reception, callsign display, distance, geographic label, coordinates, and the DIGI indicator. The `.app` executable and source tree have **not yet been uploaded to this GitHub folder**. No GitHub download is available until the actual tested files are added.

## Features

- Receives terrestrial **144.390 MHz FM APRS** packets via the UV-K1 radio and locally decodes AX.25 / 1200-baud AFSK.
- Displays the **originating call sign and SSID** prominently.
- Parses supported APRS latitude and longitude formats and displays **north/south and east/west** coordinates.
- Calculates **approximate straight-line (great-circle-style local approximation) mileage** from the configured listening reference, using the transmitted position—not the final RF hop.
- Displays a **coarse Pittsburgh-area geographic label** when the position falls within its supported region, otherwise **AREA UNKNOWN**. This is not street-level reverse geocoding.
- Differentiates **NO GPS FIX** (all-zero coordinates) from **NO POSITION** (no usable position decoded).
- Displays **DIGI**, **DIRECT?**, or **UNKNOWN** according to the AX.25 address-path repeated/H-bit evidence. `DIRECT?` means no used digipeater hop is indicated; it does *not* prove a direct link.
- Keeps the **call sign, miles, approximate area, coordinates and RF-path label on one screen**.

## Tested and not yet tested

**Demonstrated on a real radio:** reception, callsign and SSID, coordinates, miles, approximate area, `NO GPS FIX`, `NO POSITION`, and the `DIGI` indicator. The v1.6 screen also displayed an out-of-area position as `AREA UNKNOWN`.

**Automated build tests reported passing on the Windows/Docker build computer:** 21 integration checks for callsign/SSID, supported GPS forms, missing/zero GPS, mileage, coarse region labels, and AX.25 path classification; ARM compilation; 4 KiB executable limit; FAP1 metadata and CRC checks.

**Outstanding field verification:** On-radio `DIRECT?` and `UNKNOWN` packets; independent validation of the original reported coordinates and mileage against complete received packet data.

## Hardware / firmware

| Item | Requirement |
|---|---|
| Radio | Quansheng UV-K1 |
| Firmware | F4HWN Labs Edition 6.3.0 application API 3 |
| Reception | 144.390 MHz FM (US APRS channel) |
| Application | `APRS DIST` v1.6, FAP1 Labs overlay |
| Usage | **Receive-only**; this is not an APRS beacon/transmitter |

## Local development files

The original build remains on the development Windows PC:

- Application: `C:\Radtel\Quanshing\WRCX212-APRS-DIST-v1.6-UNTESTED.app`
- Source: `C:\Radtel\Quanshing\WRCX212-APRS-DIST\source-v1.6`
- Report: `C:\Radtel\Quanshing\WRCX212-APRS-DIST\APRS-DIST-V16-VERIFICATION.txt`

The `UNTESTED` filename was assigned by the build script to distinguish software/package tests from final on-radio certification. Live radio operation has since been observed, but some field cases remain pending.

## Installation (when the actual binary is published)

Use the **Labs-compatible app installer / UV Studio Apps** workflow, *not* the radio firmware flasher. Confirm your radio is compatible before installing. Keep the working v1.5.2 application as a rollback baseline.

## Important APRS interpretation

The packet's reported geographic location **may not be the RF transmitter your antenna actually heard**. Digipeaters can retransmit packets. The calculated miles describe the original packet's reported location relative to the configured listening reference. The app cannot derive a digipeater's own distance from an AX.25 path alone.

## Source attribution

Built as a custom extension of the APRS receive application in [F4HWN Labs / UV-K1-K5V3 firmware](https://github.com/armel/uv-k1-k5v3-firmware-custom). Consult upstream licenses and include required notices when publishing the derived source.

---

**Release files pending:** The actual `.app` and final source must be uploaded before this page can provide an installable download.
