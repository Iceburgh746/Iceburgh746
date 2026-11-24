# WRCX212 Autonomous Engineering Standard

Standing project instruction for future PowerShell engineering controllers and cross-platform build tooling.

## Operating contract
- Prioritize unattended progress, not repeated user copy-and-paste.
- Discover available local tools (PowerShell, Git, Python, WSL, Docker, GitHub CLI, C toolchains, Ollama, Aider); select only tools whose availability and purpose are verified.
- Reuse existing source, logs, backups, firmware baselines and previously successful build outputs.
- Keep original sources and known-good firmware immutable. Store modifications in isolated working copies, hash backups and avoid flashing, calibration writes or radio I/O without separate authorization.
- Report every native command's actual exit code. Stderr alone is not a failure; classify failures using both process status and output.
- Apply bounded, evidence-based repair strategies (transient network retries, line-ending normalization, missing generated file regeneration, dependency discovery, clean known-source rebuild) with explicit postcondition tests and checkpoints.
- Never execute arbitrary LLM-generated shell instructions without review, provenance and constrained validation. Optional locally installed AI may suggest diagnostics but not bypass safety gates.
- Retry failed steps within a configured budget; don't loop indefinitely or conceal blocked conditions.
- Preserve local full logs and JSON handoffs; when authenticated GitHub CLI is present, publish only redacted status summaries to Issues and keep full logs private.
- Do not claim ChatGPT automatically receives a new message from PowerShell: no supported direct injection into this conversation exists. GitHub Issues/Actions provide a verifiable external handoff, and can be read by a connected GitHub app in a subsequent chat.
- Distinguish SOURCE_PATCHED, BUILD_PASSED, QEMU_PASSED and HARDWARE_VALIDATED. A passed build is not evidence of successful radio behavior.
- Do not propose Work mode when the user asks to stay in Chat mode.
- When results are unavailable, state BLOCKED and the precise cause; never invent a completed artifact.

## Current project
Baofeng DM-32UV Kraken Analog Parrot implementation, source and generator checks completed. Current outstanding gates: real compiler, QEMU, analog audio capture/playback quality, carrier gating, transmission safety, and hardware verification.

## Durable references
- `AUTONOMOUS_ENGINEER.ps1` -- local autonomous PowerShell controller
- `.github/workflows/wrcx212-analog-parrot-cloud.yml` at repository root -- hosted firmware build and QEMU checks
- `.github/workflows/wrcx212-autonomous-controller-check.yml` at repository root -- PowerShell parse and safety checks
