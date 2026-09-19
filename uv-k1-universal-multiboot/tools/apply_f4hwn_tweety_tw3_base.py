#!/usr/bin/env python3
from pathlib import Path
import sys


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"expected exactly one anchor in {path}, found {count}: {old[:120]!r}")
    path.write_text(text.replace(old, new, 1))


def main(root: Path) -> None:
    bk4829 = root / "App/driver/bk4829.c"
    menu = root / "App/ui/menu.c"
    multiboot = root / "App/ui/multiboot.c"

    old_roger = r'''static void BK4819_PlayRogerNormal(BK4819_FilterBandwidth_t Bandwidth)
{
    #if 0
        const uint32_t tone1_Hz = 500;
        const uint32_t tone2_Hz = 700;
    #else
        // motorola type
        const uint32_t tone1_Hz = 1540;
        const uint32_t tone2_Hz = 1310;
    #endif


    BK4819_EnterTxMute();
    BK4819_SetAF(BK4819_AF_MUTE);

    const uint8_t rogerToneGain = (Bandwidth == BK4819_FILTER_BW_WIDE) ? 32u : 67u;

    if (Bandwidth == BK4819_FILTER_BW_WIDE)
        BK4819_SetFilterBandwidth(BK4819_FILTER_BW_NARROW, true);

    BK4819_WriteRegister(BK4819_REG_71, scale_freq(tone1_Hz));

    BK4819_EnableTXLink();
    SYSTEM_DelayMs(50);

    BK4819_WriteRegister(BK4819_REG_70,
        BK4819_REG_70_ENABLE_TONE1 |
        (rogerToneGain << BK4819_REG_70_SHIFT_TONE1_TUNING_GAIN));

    BK4819_ExitTxMute();
    SYSTEM_DelayMs(80);
    BK4819_EnterTxMute();

    BK4819_PlayToneRaw(tone2_Hz, 80);

    BK4819_WriteRegister(BK4819_REG_70, 0x0000);

    if (Bandwidth == BK4819_FILTER_BW_WIDE)
        BK4819_SetFilterBandwidth(Bandwidth, true);

    BK4819_WriteRegister(BK4819_REG_30, 0xC1FE);   // 1 1 0000 0 1 1111 1 1 1 0
}
'''

    new_roger = r'''/* WRCX212 Space Tweety TW3: known-good 10-note telemetry-style warble. */
static void BK4819_PlayRogerNormal(BK4819_FilterBandwidth_t Bandwidth)
{
    static const uint16_t tweety_hz[] = {
        2850u, 4100u, 3300u, 4550u, 3650u,
        4350u, 3100u, 4650u, 3800u, 4450u
    };
    static const uint8_t tweety_ms[] = {
        28u, 26u, 24u, 30u, 24u,
        28u, 26u, 32u, 26u, 40u
    };
    static const uint8_t tweety_gap_ms[] = {
        12u, 10u, 13u, 10u, 12u, 10u, 13u, 10u, 14u
    };

    BK4819_EnterTxMute();
    BK4819_SetAF(BK4819_AF_MUTE);

    const uint8_t rogerToneGain = 110u;

    if (Bandwidth == BK4819_FILTER_BW_WIDE)
        BK4819_SetFilterBandwidth(BK4819_FILTER_BW_NARROW, true);

    BK4819_WriteRegister(BK4819_REG_71, scale_freq(tweety_hz[0]));
    BK4819_EnableTXLink();
    SYSTEM_DelayMs(50);
    BK4819_WriteRegister(BK4819_REG_70,
        BK4819_REG_70_ENABLE_TONE1 |
        (rogerToneGain << BK4819_REG_70_SHIFT_TONE1_TUNING_GAIN));
    BK4819_ExitTxMute();

    for (unsigned int i = 0; i < ARRAY_SIZE(tweety_hz); i++) {
        BK4819_PlayToneRaw(tweety_hz[i], tweety_ms[i]);
        if (i + 1u < ARRAY_SIZE(tweety_hz))
            SYSTEM_DelayMs(tweety_gap_ms[i]);
    }

    BK4819_EnterTxMute();
    BK4819_WriteRegister(BK4819_REG_70, 0x0000);

    if (Bandwidth == BK4819_FILTER_BW_WIDE)
        BK4819_SetFilterBandwidth(Bandwidth, true);

    BK4819_WriteRegister(BK4819_REG_30, 0xC1FE);
}
'''

    replace_once(bk4829, old_roger, new_roger)
    replace_once(
        menu,
        'const char* const gSubMenu_ROGER[] =\n{\n    "OFF",\n    "ROGER",\n    "MDC"\n};\n',
        'const char* const gSubMenu_ROGER[] =\n{\n    "OFF",\n    "TWEETY",\n    "MDC"\n};\n'
    )
    replace_once(
        multiboot,
        'GUI_DisplaySmallestInverse("F4HWN MULTIBOOT", 34, 0, true, true, 94);',
        'GUI_DisplaySmallestInverse("WRCX212 MULTIBOOT", 34, 0, true, true, 94);'
    )

    print("WRCX212 TW3 baseline patch applied")
    print("No Smart Mic changes; no menu-enum changes; no settings changes")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("usage: apply_f4hwn_tweety_tw3_base.py PATH_TO_F4HWN_V6_SOURCE")
    main(Path(sys.argv[1]).resolve())
