# WRCX212 Analog Parrot - requested firmware build

Requested: 2026-11-23 (America/New_York)
Request: Compile actual DM-32UV Kraken Analog FM Parrot replacement and run source validation plus C-SKY/QEMU regression suite.

This file is deliberately committed under the project path to trigger the existing GitHub Actions workflow `.github/workflows/wrcx212-analog-parrot-cloud.yml` on push.

Acceptance gates: reversible source overlay, host embedded codec test with sanitizers, C-SKY cross-compilation, firmware patch allocation checks, QEMU regression suite and deterministic SHA256 output for any resulting update file.

Safety: no device flashing; no publication of proprietary stock-firmware binaries; do not mark as flash-ready without further hardware validation.
Build result: NOT YET OBSERVED (commit triggers a run but does not prove it executed).
