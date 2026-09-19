# WRCX212 UV-K1 Space Tweety Smart Mic SM2

**Status: VALIDATED WORKING**

Tested on a physical UV-K1 on 2026-09-19 by WRCX212:
- Normal boot confirmed
- Smart Mic function confirmed
- Transmit audio reported/observed as sounding good
- Space Tweety retained
- Multiboot retained

## Firmware
`WRCX212_UVK1_SPACE_TWEETY_SMARTMIC_v6.0.0_SM2_SAFE.bin`

## Smart Mic use
The existing six-character `Compnd` menu label is renamed to `SmtMic`.
Set **SmtMic = TX** to enable the BK4829 hardware 2:1 TX compressor/leveler.
The conservative microphone table is raised by about +3 dB.

## Safety design
SM2 is a fixed-size patch of the exact known-good TW3 Space Tweety binary.
It does **not** change the vector table, boot/startup area, menu enumeration, EEPROM layout, firmware length, multiboot layout, or Tweety tables.

## SHA-256
`0408e48a548bc031196bb17f60a255d60496c9c59ba3ad4b11b78e7cb20efc63`

Keep the known-good TW3 firmware available as a recovery image.
