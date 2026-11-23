# WRCX212 Analog Parrot for Baofeng DM-32UV Kraken — experimental development

**Current status: working portable voice storage core; NOT integrated with firmware, NOT flashable.**
This project prioritizes analog FM Parrot, and may replace DMR Parrot in the UI.

The source audit identified **both** usable audio paths in Kraken:
- RX 8 kHz signed big-endian PCM: FM receive ping-pong RAM in `krk_aprsrx.c`; analog APRS capture uses GPIO_AF_SWITCH (0x2f) plus AUDIO_CONTROL bit6.
- TX 8 kHz signed big-endian PCM: `krk_fm_send_opt()` in `krk_aprs_tx.c`; transmitter routes FM Send RAM to the modulator with original stock PTT protection.
- Existing Parrot state machine: `krk_parrot_sm.c` including delay, abort, cooldown, unkey watchdog.

Portable code here implements IMA ADPCM4 recording/playback at **4 kB/second**, a quarter of the uncompressed 16 kB/second audio. A 16 KiB allocation yields approximately 4 seconds, and 48 KiB 12 seconds. Buffer feasibility must be checked against Kraken SRAM allocation. Do not claim free SRAM without a linker map.

## Build and test on PC
`cc -std=c99 -Wall -Wextra -Werror -fsanitize=address,undefined -o test tests/test_core.c analog_parrot_core.c && ./test`

On toolchains that lack sanitizers, omit `-fsanitize=address,undefined`.

## Firmware integration still required
1. Claim an isolated SRAM buffer and verify exact linker allocation, overlap and stack budget.
2. Hook analog RX half-buffer events into `ap_record_write` when squelch opens; stop at carrier drop. Do not record our own transmit or transmit while recording.
3. Decode with `ap_play_read` into the 256-sample FM Send RAM fill callback (big-endian int16). Ensure the last partial half is zero padded.
4. Use `krk_fm_send_opt` with bounded air-time budget and an abort function. Recheck channel TX permission, busy channel, dual-watch, antenna/radio restrictions and PTT unkey.
5. Adapt or replace existing Parrot menu with `Analog Parrot Off/On`, `Record`, `Replay`, `Status`; automatic echo **OFF by default**. Keep channel/side lock and a cooldown to avoid echo loops.
6. Ensure no concurrent APRS RX Monitor, Burst Voice, APRS Beacon or other FM RAM owner.
7. Build using Kraken's C-SKY toolchain and run QEMU tests, regression suite and hardware test with dummy load. Preserve unmodified stock image and rollback path. **Do not flash an unverified image.**

## Sources
- https://github.com/awkaplan/dm32uv-kraken/blob/main/patches/kraken_ui/krk_aprsrx.c
- https://github.com/awkaplan/dm32uv-kraken/blob/main/patches/kraken_ui/krk_aprs_tx.c
- https://github.com/awkaplan/dm32uv-kraken/blob/main/patches/kraken_ui/krk_parrot_sm.c
- https://github.com/awkaplan/dm32uv-kraken/blob/main/docs/user-guide/parrot-and-burst-voice.md
