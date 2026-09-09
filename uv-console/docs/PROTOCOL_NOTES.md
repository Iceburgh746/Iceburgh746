# Protocol notes — UV Console by WRCX 212

These notes document the portions translated into the native Windows build.

## Viewer stream

- Serial: 38,400 baud, 8 data bits, no parity, 1 stop bit
- Base keepalive: `55 AA 00 00`
- RF Log feature keepalive: `55 AA 05 FEATURES`
- Display header: `AA 55 TYPE SIZE_HI SIZE_LO ... 0A`
- Optional new-format marker before header: high nibble `F`; low bits carry radio state
  - bit 0: deep sleep
  - bit 1: red LED
  - bit 2: green LED
- type `01`: complete 1024-byte 128×64 framebuffer
- type `02`: differential framebuffer chunks, 9 bytes each: chunk index + 8 bytes
- type `03`: short virtual key
- type `04`: long virtual key
- type `05`: RF Log main packet
- type `06`: RF Log history packet

### Virtual keys

- 0–9: `00`–`09`
- MENU: `0A`
- K1 left / K5 up: `0B`
- K1 right / K5 down: `0C`
- EXIT: `0D`
- `*`: `0E`
- F / `#`: `0F`
- SIDE2: `11`
- SIDE1: `12`

Key packet: `AA 55 TYPE KEY`, where TYPE is `03` for short and `04` for long.

## Maintenance framing

Message payload begins with:
- `uint16le message_type`
- `uint16le data_length`
- data bytes

Transport packet:
- `AB CD`
- padded message length, uint16 little-endian
- message bytes (even-byte padded)
- CRC-CCITT (poly 0x1021, initial 0)
- `DC BA`

The message plus CRC region is XOR-obfuscated using the repeating 16-byte table:

`16 6C 14 E6 2E 91 0D 40 21 35 D5 40 13 03 E9 80`

### Known-answer test

Message `05DD` (reboot), data length zero, encodes to exactly:

`AB CD 04 00 CB 69 14 E6 5B EB DC BA`

This test is used to validate the native CRC/obfuscation implementation.

## Maintenance message types

- `0518` bootloader device-info notification
- `0530` bootloader-version notification/handshake
- `0519` program firmware
- `051A` program firmware response
- `0514` device-info request
- `0515` device-info response
- `051B` read EEPROM
- `051C` read EEPROM response
- `051D` write EEPROM
- `051E` write EEPROM response
- `05DD` reboot

## Calibration

- 512 bytes total
- 16-byte transfers
- firmware before v5: base `0x1E00`
- firmware v5+: base `0xB000`

## Boot logo

- EEPROM compatibility offset: `0xC000`
- 8-byte magic: ASCII `F4HWNLGO`
- bitmap: 1024 bytes, 128×64, ST7565-native page/column layout, LSB top
- complete useful data: 1032 bytes
- padded transfer size: 1040 bytes

## RF Log v2

- main type `05`, history type `06`
- version 2
- 64 rows per page
- row size 25 bytes
- channel name: 10 printable ASCII bytes
- rows include frequency (10-Hz units), sequence, duration, memory channel, flags, S-meter/TX power index, battery and channel name
