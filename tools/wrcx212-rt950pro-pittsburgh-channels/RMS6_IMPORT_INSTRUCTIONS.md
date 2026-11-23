# WRCX212 RMS6 Channel Import — November 23, 2026

## File format: Direct RMS6 channel import CSV
`WRCX212_NICFW950_RMS6_CHIRP_IMPORT_301-367.csv` uses the **CHIRP channel CSV schema supported by NicSure's nicFW950 RMS6 Channels tab**. This is NOT the same as the earlier master planning CSV, and NOT a native .dat or storage image.

See NicSure's official RMS channel guide: https://github.com/nicsure/RMS880/wiki/Channel-Tab
RMS reads CHIRP CSV rows by channel Location number (1..999). These rows deliberately occupy slots **301 through 367**. This is ONLY a proposed block: **check that every slot is empty first**, or importing may overwrite existing channels. There is no way to prove slots empty without reading your radio's current memory.

## Import without erasing your existing configuration

1. Open NicSure **RMS6 matching your nicFW950 firmware** and connect using the normal supported connection.
2. Read all radio channels and settings. Make a full backup of the **existing** configuration and storage before any import/write.
3. In the RMS6 Channels tab, inspect channels **301..367**. If *any* are occupied, STOP. Do not import this file. A remapped import file must first be generated for a known-empty contiguous slot range.
4. Use **CHIRP Import / Import from CSV** on the Channels tab and select the supplied CSV.
5. **Before writing to radio**, review each row's RX/TX, FM bandwidth, tones, power, and transmit-disable. The `Duplex=off` plus `Power=N/T` markers are intended to disable TX on monitoring candidates; actual import behavior is NOT hardware-tested and may not round-trip. If a monitor channel shows TX enabled, STOP and change it to RMS's explicit **N/T** (No Transmit) selection.
6. Group letters A-N are documented in a separate `RMS6_GROUP_ASSIGNMENTS.csv`. **CSV does not have a verified RMS6 group-membership import field.** Set group membership and names in RMS6's Groups controls *after import*. Do not assume the comments assign memberships automatically; the official CHIRP driver's special group-letter comment rule is not proven to apply in RMS.
7. All repeater candidates are RX-only / scan-skipped until owner access, on-air operation, tone and distance are verified. Even listed-open repeaters can require operator approval.
8. Only write the configuration after preserving a known-good backup and previewing all modifications. No firmware flash is required.

## Critical validation notes
- Slot 999 may be reserved for APRS; this file does not touch it.
- Entries in groups G-N are exploratory directional candidates; these are NOT verified to be reachable from Penn Hills.
- Group K's Uniontown station was marked off-air in the cited listing; included only as receive-only reference, not an active scan choice.
- Chickaree Mountain is a **user-confirmed successful contact**, and remains an enabled operational repeater in group F. Directional group J contains a separate RX-only duplicate for planning.
- Generic RPT channels group B are **N/T** until access tone/operator is configured.
- GMRS channels 8-14 are **N/T** in this mobile plan, regardless of how a third-party importer interprets their placeholders.
- Weather frequencies are RX-only N/T.
- Do not interpret `8.0W` as an actual power setting guarantee; choose the appropriate legal radio power in RMS.
- The CSV has 67 separate channel memory entries. Some frequencies are duplicated intentionally in different groups. It includes 14 proposed groups; the radio allows A-Z.
- If RMS6 does not recognize `N/T` as CSV Power, **do not import or write** until a one-entry verified export from your exact RMS version determines the proper encoding.
- If your firmware is newer than 6.00.19d, use the RMS release paired with it.

## References
- NicSure RMS Channels: https://github.com/nicsure/RMS880/wiki/Channel-Tab
- NicFW950 custom editor: https://github.com/cruzerdlc/RT-950-950Pro-Editor
- Nicsure CSV dialect comparison: https://github.com/wa2n-code/Radtel-CSV-Converter
- User-built reference channel/repeater research: https://github.com/Iceburgh746/Iceburgh746/tree/main/tools/wrcx212-rt950pro-pittsburgh-channels

**Unverified:** Exact CSV import semantics for the current installed RMS6, automatic group membership, reserved memory preservation and the user radio's available channel slots. For a guaranteed native codeplug preserving present channels, obtain a backup/readout from the exact current radio configuration first, then merge and validate offline.
