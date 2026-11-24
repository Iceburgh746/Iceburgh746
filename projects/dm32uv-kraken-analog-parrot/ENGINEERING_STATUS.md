# WRCX212 Kraken Analog FM Parrot — engineering checkpoint

Date: 2026-11-23

## What is verified
The user's Windows logs confirm upstream `dm32uv-kraken` and `dm32uv-re` cloned, required Git submodules completed, and the 3-file Analog Parrot source overlay was applied with SHA-256-verified backups. Kraken menu/manifest/claims generators passed in Ubuntu. No radio was flashed.

## What was added during the autonomous continuation
- `tests/test_embedded_parrot.c` compiles the actual `overlay/krk_parrot.c` with test mocks and exercises FM sample capture, packed 2-bit audio, carrier drop, decoded TX PCM output and release of the shared buffer.
- `.github/workflows/wrcx212-analog-parrot-cloud.yml` at repository root now runs that embedded test under ASan/UBSan, separate from the unused research ADPCM4 codec test.
- `AUTONOMOUS_ENGINEER.ps1` discovers available tools, attempts bounded local builds if Docker works, and can dispatch/watch GitHub Actions through an authenticated `gh` CLI. It retains full local JSON and log reports.
- `.github/workflows/wrcx212-autonomous-controller-check.yml` checks PowerShell parsing and basic safety markers.

## Limitations and blocked channels
- GitHub Issues are DISABLED for this repository (GitHub API returned HTTP 410); do not promise automatic Issue reporting.
- No supported API can automatically paste PowerShell output into the current user ChatGPT conversation.
- No authenticated remote shell connection to the user's Windows PC has been granted.
- GitHub Actions execution, actual Kraken cross-compilation, QEMU suite, codec intelligibility and hardware behavior are NOT yet verified.
- Never flash or deliver a flash-ready claim without verified tests and hardware safety review.

## Next autonomous task
Obtain real GitHub Actions logs and correct concrete host/C-SKY compiler or QEMU defects. Preserve source, backups, and calibration. Avoid a new round of Windows Docker scripts.

Cloud workflow: https://github.com/Iceburgh746/Iceburgh746/actions/workflows/wrcx212-analog-parrot-cloud.yml
PowerShell parser checks: https://github.com/Iceburgh746/Iceburgh746/actions/workflows/wrcx212-autonomous-controller-check.yml

## Autonomous controller improvements (continuation)
- Source recovery: absent Kraken checkouts are cloned into the isolated project workspace and patched by the reversible Python patcher; missing companion dm32uv-re checkout is cloned with submodules.
- Local build resolves the actual Ubuntu workspace mount with `wslpath` and passes an LF-only shell script on disk.
- Build failures are classified by observable native output; network failures receive a bounded retry; unrecognized compiler errors stop rather than executing generated code.
- SHA-256 hashes of the three modified source files are recorded before each local build.
- Blocked runs exit nonzero after producing their JSON report, rather than incorrectly succeeding.
- A separate ChatGPT hourly conditional automation watches repository build milestones and can notify on new results; it is not a direct PowerShell-to-chat transport.
- GitHub Issues remain disabled. Full local logs stay private, and Actions results are accessible through GitHub when available.
- Successful cross-compilation, passing QEMU, and live Analog FM testing remain UNVERIFIED until their actual logs are inspected.

## Controller fix — November 23 continuation
- Fixed a real PowerShell correctness bug: `Note` used `Tee-Object` on the success pipeline and polluted typed return values from `InvokeNative`, potentially corrupting result/exit-code checks.
- Added a hosted PowerShell contract test that extracts these functions and verifies zero logging pipeline objects, single native result object, and accurate 0/nonzero exit codes.
- The controller now clones missing Kraken and companion repositories, applies the known reversible overlay only when absent, and resolves the actual Windows project path through `wslpath` instead of hard-coding `C:\Radtel` as an Ubuntu mount.
- No actual hosted workflow run result was obtainable with the available GitHub connector: it exposes workflow jobs by known run ID but no general workflow-run listing. Do not claim success based on commits alone.
