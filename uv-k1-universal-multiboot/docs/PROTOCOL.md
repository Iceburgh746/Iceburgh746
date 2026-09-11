# F4HWN v6.0.0 Multiboot protocol notes used by U1

These notes document only the pieces required by the WRCX212 U1 host tools.

## Internal MCU layout

The PY32F071xB linker layout used by F4HWN v6.0.0 is:

- SRAM: `0x20000000`, 16 KiB
- application flash: `0x08002800`, 118 KiB
- application end: `0x08020000`

The region below `0x08002800` contains the factory boot/recovery implementation and is not touched by F4HWN's Multiboot restore routine.

## External SPI flash firmware slots

F4HWN uses five 128 KiB slots:

- slot 0 header: `0x020000` (firmware-managed Main backup)
- slot 1 header: `0x040000`
- slot 2 header: `0x060000`
- slot 3 header: `0x080000`
- slot 4 header: `0x0A0000`

Each slot:

- stride: `0x20000` (128 KiB)
- header sector: first `0x1000` bytes
- application image: begins at slot offset `0x1000`
- header structure: 64 bytes

## FMB1 header

Little-endian layout:

```text
u32 magic        = 0x31424D46   # "FMB1"
u16 hdr_version  = 1
u16 flags
u32 image_size
u32 image_crc32  # zlib/IEEE CRC32 over image_size bytes
char name[16]
char version[16]
u8 reserved[16]
```

Flags used by U1:

```text
bit 0  COMMITTED       existing F4HWN flag
bit 1  FOREIGN         U1: non-F4HWN PY32F071 application
bit 2  RETURN_PATCHED  U1 informational flag; do not set unless independently verified
```

U1 writes `WRCXU1` into the beginning of the reserved field. Stock v6.0.0 ignores this reserved data.

## Serial/VCP packet framing

Request framing:

```text
AB CD
u16 outer_size
xor(command_body + crc16)
DC BA
```

`command_body` is:

```text
u16 command_id
u16 payload_size
payload[payload_size]
```

CRC16 is CCITT polynomial `0x1021`, initial value zero, over the complete plaintext command body. The CRC is appended little-endian before XOR obfuscation.

Obfuscation repeats this 16-byte key:

```text
16 6C 14 E6 2E 91 0D 40 21 35 D5 40 13 03 E9 80
```

Replies use the same `AB CD`, little-endian outer size and XOR key. The reply body itself contains a nested `u16 reply_id`, `u16 payload_size`, followed by the reply payload. Replies have two obfuscated footer-padding bytes followed by `DC BA`.

## Commands used by U1

### Session init — `0x0514`

Request payload:

```text
u32 timestamp
```

The device latches this timestamp for the selected serial/VCP port. Slot mutations must repeat the same timestamp.

Expected reply: `0x0515`.

### Slot erase — `0x0722`

Request payload (6 bytes):

```text
u8 slot
u8 padding
u32 timestamp
```

Only user slots 1..4 are accepted by F4HWN.

Expected reply `0x0723` payload:

```text
u8 slot
u8 status
```

### Slot write — `0x0724`

Request payload:

```text
u8 slot
u8 padding
u32 offset
u16 len
u32 timestamp
u8 data[len]
```

Expected reply `0x0725` payload:

```text
u8 slot
u8 status
```

U1 writes the application at offset `0x1000` first and writes the 64-byte COMMITTED header at offset zero only after the application transfer is complete.

### Slot validate — `0x0726`

Request payload:

```text
u8 slot
```

Expected reply `0x0727` payload:

```text
u32 crc32
u8 slot
u8 status
```

The stock v6 validation checks FMB1 header integrity and full image CRC32. With the U1 radio-side patch, a slot carrying the `FOREIGN` flag also gets ARM vector/layout checks before it can be restored.

## U1 foreign-vector checks

For a `FOREIGN` slot:

1. initial MSP must be between `0x20000000` and `0x20004000`;
2. reset vector bit 0 must be 1 (Thumb);
3. `(reset_vector & ~1)` must be inside the actual supplied application image:
   `0x08002800 <= reset_handler < 0x08002800 + image_size`.

Passing these tests does not prove every peripheral/config assumption is compatible. It proves only that the binary looks like a plausible PY32F071 application linked to the same application base.
