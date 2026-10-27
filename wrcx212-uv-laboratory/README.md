# WRCX212 UV Laboratory — development branch

**Status:** Early offline test-bench source; not a finished replacement for UV Studio.  
**Owner:** Edward Phillips — WRCX212.  
**Radio target:** Quansheng UV-K1 Mini Kong, F4HWN Labs 6.3.

## Immediate use (Windows PowerShell)

Requires Python 3.10+:

```powershell
git clone --branch wrcx212-uvlab-development https://github.com/Iceburgh746/Iceburgh746.git
cd Iceburgh746\wrcx212-uv-laboratory
py -3 uvlab.py test
py -3 uvlab.py inventory
```

The `test` command runs self-contained AX.25 frame/FCS tests without a radio. `inventory` checks installed tools and available APRS app sources under `C:\WRCX212-LAB`.

## What is implemented

- Offline AX.25 UI-frame address parsing, FCS checking and test-frame generation.
- Three Python unit tests (checksum reference, decode round trip, corruption rejection).
- Read-only local development-environment inventory.

## What is NOT implemented

- The UV-K1 device serial protocol, live screen streaming, real keypad commands or install/flash.
- Bell 202 AFSK demodulation and live APRS decoding.
- Actual 4-KiB Labs overlay binary or on-device LCD improvements.
- Direct radio I/O, RF transmission or Windows installed executable.

## Hardware safety

Existing F4HWN UV Studio has demonstrated connected status and 1 FPS screen mirroring on the user's physical radio. That does not establish our own serial protocol. Do not run another serial client concurrently with UV Studio. Back up calibration and flash before any future write operations. Keep RF transmit disabled until hardware and operating authority are verified.

## Roadmap

1. Verify offline unit tests on Windows, document test results.
2. Add framed prerecorded APRS packet fixture tests, then an AFSK demodulator.
3. Implement a USB serial read-only client after confirming F4HWN serial protocol.
4. Add LCD test layout and application build adapters with reproducible version pins.
5. Validate each hardware-facing operation on the actual UV-K1.
