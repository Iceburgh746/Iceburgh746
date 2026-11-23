# WRCX212 RT-950 Pro Pittsburgh channel & zone plan — 2026-11-23

## Scope
Channel plan built for RT-950 Pro running **nicFW950**. This is **NOT** a native RMS image or existing-radio backup. The radio's current codeplug has not been supplied. **Never overwrite existing memories or zones without first reading and backing up the radio.**

Five separate standard CHIRP-compatible channel CSV files are provided. Import one zone at a time into a deliberately selected empty nicFW group/zone with an RT-950/950Pro editor that supports nicFW950; do **not** assume importer preserves comments, TX inhibit, slot positions, scan skip, groups, or wattage. Verify all fields and especially every receive-only channel after import and BEFORE any radio write.

### Zone layout
1. 01_GMRS_SIMPLEX — 22 standard GMRS channels. Ch 8–14 receive-only on a mobile; CH 19 notes Friday simplex.
2. 02_REPEATER_PAIRS — 8 generic +5 MHz GMRS repeater pairs; no access tone, scan skipped until configured.
3. 03_VERIFIED_LOCAL — Pittsburgh Observatory Hill (462.575 +5 PL141.3), Greensburg (462.600 +5 PL136.5).
4. 04_UNVERIFIED_LOCAL — 7 extra area repeater candidates, tagged RX-only and excluded from scanning; verify the owner/tone/access and on-air status before enabling TX.
5. 05_NOAA_WEATHER — 7 NOAA weather frequencies, receive-only; scan skipped.

## Safety and programming
- Import only into empty channels/groups, or merge into a current **backed-up** `.dat` with a visual preview and frequency/name conflict review. A CHIRP CSV is not an RMS-native image.
- The provisional repeater entries use `Duplex=off`, not a usable TX offset. For operational repeater programming, confirm owner permission, then set +5.000 MHz TX and the confirmed tone in the editor.
- The 462.575 Pittsburgh tone conflicts across sources: RepeaterBook gives CTCSS 141.3; K3IR lists DCS 654. This plan uses 141.3 as the RepeaterBook-confirmed listing, but verify against the repeater operator or actual RF before transmitting.
- 467.5625–467.7125 MHz interstitial GMRS channels 8–14 are **receive-only** in this mobile plan (FCC restriction on mobile transmission).
- There is no implied authorization to transmit on business, amateur, public safety, or weather frequencies.
- The Power column is an importer hint, **not** a guarantee of actual RF output. Verify model-specific settings and legal limits.
- The publicly accessible listings establish entries, not that a given repeater is currently audible or operational. Some K3IR entries are on an explicitly 'under construction' page.
- This CSV intentionally does not carry APRS, VFO, scan group, programmable key, or calibration parameters.

## Source cross-checks (2026-11-23)
- RepeaterBook Pittsburgh: https://www.repeaterbook.com/gmrs/location_search.php?loc=Pittsburgh&state_id=42&type=city
- RepeaterBook Pennsylvania: https://www.repeaterbook.com/gmrs/Display_SS.php?state_id=42
- K3IR local listings: https://www.k3ir.org/local-gmrs-nets
- NicFW-capable editor: https://github.com/cruzerdlc/RT-950-950Pro-Editor
- CSV schema: https://github.com/pskillen/codeplug-tool/blob/main/docs/reference/chirp/channels.md

## Recommended import sequence
1. First **read the radio** with a nicFW-compatible editor and save the original configuration.
2. Inspect existing channels and zones, including scan assignments; do not erase duplicates blindly.
3. Load one zone CSV, preview and explicitly choose an empty group and free channel slots.
4. Check names, RX/TX, +5 MHz offset, PL/DCS, scan skip, RX-only flags, and power for every entry.
5. Save a separate new `.dat`; do not flash firmware. Only write the channel configuration after verifying and keeping the original backup.
6. Perform a readback and compare before calling it complete.

## Why no one-click native RMS file?
Current nicFW6 RMS native memory layout was not independently validated against your *actual* current codeplug, so manufacturing a byte-level image would risk destroying existing memories. The editor is documented to support nicFW950 channel import, zone targeting, and post-write verification.
