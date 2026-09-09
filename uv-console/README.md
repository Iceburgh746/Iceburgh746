# UV Console by WRCX 212

A native Windows desktop implementation of the compatible Quansheng UV-K1 / UV-K5 V3 Fusion viewer and maintenance protocols. It does **not** open a browser and it does **not** run a local web server.

## Browser-parity screen transport (0.9.4)

The live viewer now deliberately mirrors the current UV Studio/K5Viewer transport: 38,400 baud, DTR asserted, a 200 ms base keepalive (`55 AA 00 00`), and an immediately-following feature keepalive (`55 AA 05 83` on restart, then `55 AA 05 03`). This matters on the radio's native USB CDC path because current F4HWN firmware only transmits K5Viewer data while DTR is asserted.


## What is implemented

- Native Windows COM-port connection at 38,400 baud
- Live 128×64 radio framebuffer viewer
- **Smooth LCD** default renderer: fast bulk framebuffer conversion with anti-aliased scaling, subtle glow and glass/bezel treatment
- **Raw LCD** exact diagnostic mode
- Four display palettes, invert and glow controls
- Real radio RED/GREEN LED state and deep-sleep status from viewer frame flags
- Complete viewer-protocol front panel: PTT indicator (protocol unavailable), SIDE1, SIDE2, MENU, EXIT, navigation, 0–9, *, and F/#
- Short presses on release; long presses fire at the 500 ms threshold while held, matching current UV Studio viewer behavior
- Dedicated screen-stream diagnostics showing received byte and valid-frame counts
- Manual **SYNC SCREEN** control plus automatic stream resynchronization
- PNG screenshots
- RF Log v2 live/history parser and CSV export
- Native firmware DFU flasher with CRC-CCITT, UV protocol obfuscation, bootloader validation and page retries
- 512-byte calibration backup and restore
- Firmware-aware calibration address selection (v5+ = 0xB000; older = 0x1E00)
- 128×64 boot-logo conversion, preview, upload and dump
- Single-operation interlock: viewer and maintenance functions cannot own the COM port simultaneously
- Write-critical confirmation dialogs and close protection

## Smooth LCD clarification

The radio sends a **1-bit framebuffer**, not character codes or font names. Therefore software cannot safely replace arbitrary on-radio glyphs with a desktop font without OCR/template recognition and the possibility of changing what the screen actually says. Smooth LCD preserves every received bit and uses a fast anti-aliased scale pass so enlarged characters and icons are less harsh/blocky without changing the radio data. Raw LCD remains available when exact pixel fidelity is required.

All surrounding application text uses Windows Segoe UI / Consolas so the desktop interface itself is clean and non-pixelated.

## Build on Windows 10/11

1. Copy this entire folder to the Windows PC.
2. Double-click **Build-Windows.cmd**.
3. The finished portable program is created as:
   `dist\UVConsole.exe`
4. Run the EXE normally; administrator rights are not requested.

The build script first uses the C# compiler shipped with the installed Microsoft .NET Framework. If that compiler is absent, install the Microsoft .NET Framework 4.8 Developer Pack and run the build script again.

## Viewer use

The screen area now has a dedicated layout row and cannot be covered by the activity scope. The status line reports **RX bytes** and **valid viewer frames**. If a COM connection is open but no valid frames arrive, the console says so explicitly instead of leaving a silently blank display.

1. Power the compatible radio normally.
2. Connect its programming/USB cable.
3. Launch UVConsole.exe.
4. Click **REFRESH**, choose the COM port, then **CONNECT**.
5. Use Smooth LCD or Raw LCD as desired.

For maximum compatibility, the app starts with the base viewer keepalive (`55 AA 00 00`). After the first valid screenshot/diff frame arrives, it enables the optional RF-log feature keepalive. This keeps screen synchronization independent from RF-log negotiation.

### Live-screen repair in v0.9.3

The Windows serial receive path now uses a dedicated background reader instead of relying on `SerialPort.DataReceived`. Incoming display updates are applied immediately to the 1024-byte framebuffer, while repaint requests are coalesced so the radio cannot overwhelm the WinForms UI at high frame rates. The Smooth LCD renderer was also rewritten to avoid thousands of drawing objects per frame.

## Firmware DFU use

Firmware flashing is write-critical.

1. Choose the correct `.bin` image.
2. Power the radio OFF.
3. Hold PTT.
4. Power the radio ON.
5. Release PTT.
6. Reconnect/select the COM port if Windows re-enumerates it.
7. Open Maintenance → Flash and press **FLASH FIRMWARE**.

The flasher requires bootloader 7.00.07 or newer, matching current UV Studio behavior.

## Calibration

Backup before making changes. A restore file must be exactly 512 bytes. Do not write another unit's calibration as a casual substitute; calibration is radio-specific RF data.

## Remote PTT

Not provided. Current UV Studio viewer protocol exposes virtual keypad commands but does not expose a remote PTT command. The desktop application deliberately does not pretend otherwise.

## Attribution / license

The protocol translation is based on the publicly available UV Studio sources by Armel FAUVEAU. UV Studio is Apache-2.0 licensed. The original LICENSE and NOTICE are retained in this package.

See `docs/PROTOCOL_NOTES.md` for the translated protocol details and test vectors.


## LCD fitting in v0.9.15
The live viewer now preserves the radio's exact 128x64 (2:1) framebuffer geometry. The image is centered with a small inner margin and is never stretched or cropped to fill the faceplate aperture.


## LCD fit in v0.9.15
The live 128x64 framebuffer is rendered at 424x212, exactly 2:1. Extra vertical room in the decorative faceplate opening is dark recessed bezel, not part of the LCD image.
