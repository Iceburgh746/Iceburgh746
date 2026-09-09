# v0.9.15

- Enlarged the live 128x64 LCD to 424x212 (exact 2:1).
- Changed the unused top/bottom letterbox area from pale LCD blue to a dark recessed bezel.
- Preserved the proven v0.9.14 renderer and v0.9.4 viewer transport.

# v0.9.14

- Restored the exact LCD renderer from the last confirmed-working display build.
- Removed the v0.9.13 aspect-fit changes that could produce a blank pale-blue screen.
- Added a separate LCD host panel for letterbox margins.
- Live framebuffer now renders inside a true 420x210 (2:1) viewport centered in the existing faceplate opening.
- Viewer serial transport remains unchanged.

# Changelog

## 0.9.14
- Fixed live LCD geometry so the exact 128x64 framebuffer is always displayed at its native 2:1 aspect ratio.
- Removed forced X/Y stretching from the faceplate display.
- Added a 10-pixel inner LCD safety margin and centered aspect-fit scaling.
- Unused aperture area now uses the LCD background color for clean margins instead of clipping or distortion.
- Viewer transport remains unchanged from the confirmed-working DTR/38400/200 ms implementation.

# v0.9.12

- Replaced the top-level TabPage faceplate with a normal Panel so the borderless console reliably becomes visible.
- Added startup/runtime exception logging to UVConsole_STARTUP_ERROR.log plus a visible error dialog.
- Added a Shown activation diagnostic.
- Viewer transport remains unchanged from the known-good v0.9.4 core.

# v0.9.12

- Fixed the custom window maximize/restore button compile error by explicitly using the `FormWindowState` enum.
- The middle chrome button now toggles between maximized and normal window states.
- No changes to the proven radio viewer transport or protocol layer.

# UV Console by WRCX 212 — Changelog

## v0.9.12 — Windows compiler compatibility repair
- Fixed C# auto-property initializers that are unsupported by the .NET Framework compiler used by `Build-Windows.cmd`.
- Fixed three accidental multiline string literals in `MainForm.cs` by using explicit CR/LF escapes.
- No changes to the proven v0.9.4-derived serial/viewer transport or faceplate behavior.

# UV Console by WRCX 212 — v0.9.12

- Rebased UI on the last confirmed-working v0.9.4 viewer/serial core.
- Uses the user's supplied 1536x1024 reference image itself as the native console faceplate.
- Live 128x64 framebuffer is overlaid into the exact LCD opening.
- Virtual keypad uses invisible hit areas aligned to the faceplate buttons, preserving short/long key protocol.
- COM selector, connection status, screen diagnostics, and event log are live controls over the reference artwork.
- Viewer transport remains 38400 baud with DTR ON and the v0.9.4 UV Studio-compatible keepalive behavior.
- Faceplate is embedded into UVConsole.exe at build time; no browser/web server is used.
