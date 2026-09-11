#!/usr/bin/env python3
from pathlib import Path
import json
import sys


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"expected exactly one anchor in {path}, found {count}: {old[:100]!r}")
    path.write_text(text.replace(old, new, 1))


def main(root: Path) -> None:
    bk = root / "App/driver/bk4829.c"
    menu = root / "App/ui/menu.c"
    presets = root / "CMakePresets.json"

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

    new_roger = r'''/* WRCX212 TWEETY TW1
 *
 * Native v6/Fusion Roger path: a fast 10-note Tweety warble.  This deliberately
 * keeps the original v6 TX-link setup, bandwidth handling and teardown instead
 * of binary-hooking an older firmware image.  The radio therefore retains the
 * v6 Multiboot selector and its normal transmitter cleanup path.
 */
static void BK4819_PlayRogerNormal(BK4819_FilterBandwidth_t Bandwidth)
{
    static const uint16_t tweety_hz[] = {
        1900u, 2600u, 2100u, 2850u, 2200u,
        3050u, 2300u, 3200u, 2450u, 3400u
    };
    static const uint8_t tweety_ms[] = {
        24u, 22u, 24u, 22u, 24u,
        22u, 24u, 22u, 24u, 30u
    };

    BK4819_EnterTxMute();
    BK4819_SetAF(BK4819_AF_MUTE);

    const uint8_t rogerToneGain = (Bandwidth == BK4819_FILTER_BW_WIDE) ? 32u : 67u;

    if (Bandwidth == BK4819_FILTER_BW_WIDE)
        BK4819_SetFilterBandwidth(BK4819_FILTER_BW_NARROW, true);

    /* Pre-load the first note before enabling the TX tone path, following the
     * stock v6 Roger sequence. */
    BK4819_WriteRegister(BK4819_REG_71, scale_freq(tweety_hz[0]));
    BK4819_EnableTXLink();
    SYSTEM_DelayMs(50);

    BK4819_WriteRegister(BK4819_REG_70,
        BK4819_REG_70_ENABLE_TONE1 |
        (rogerToneGain << BK4819_REG_70_SHIFT_TONE1_TUNING_GAIN));

    for (unsigned int i = 0; i < ARRAY_SIZE(tweety_hz); i++)
        BK4819_PlayToneRaw(tweety_hz[i], tweety_ms[i]);

    BK4819_WriteRegister(BK4819_REG_70, 0x0000);

    if (Bandwidth == BK4819_FILTER_BW_WIDE)
        BK4819_SetFilterBandwidth(Bandwidth, true);

    /* Preserve the stock v6 post-Roger radio state. */
    BK4819_WriteRegister(BK4819_REG_30, 0xC1FE);   // 1 1 0000 0 1 1111 1 1 1 0
}
'''
    replace_once(bk, old_roger, new_roger)

    replace_once(
        menu,
        'const char* const gSubMenu_ROGER[] =\n{\n    "OFF",\n    "ROGER",\n    "MDC"\n};\n',
        'const char* const gSubMenu_ROGER[] =\n{\n    "OFF",\n    "TWEETY",\n    "MDC"\n};\n'
    )

    data = json.loads(presets.read_text())
    fusion = next((p for p in data["configurePresets"] if p.get("name") == "Fusion"), None)
    if fusion is None:
        raise SystemExit("Fusion configure preset not found")
    cache = fusion.setdefault("cacheVariables", {})
    if cache.get("EDITION_STRING") != "Fusion":
        raise SystemExit(f"unexpected Fusion EDITION_STRING: {cache.get('EDITION_STRING')!r}")
    cache["EDITION_STRING"] = "WRCX212 TWEETY"
    cache["VERSION_STRING_2"] = "v6.0.0-TW1"
    cache["TARGET"] = "wrcx212.tweety"
    presets.write_text(json.dumps(data, indent=4) + "\n")

    print("WRCX212 TWEETY TW1 native-v6 patch applied successfully")
    print("Roger menu: OFF / TWEETY / MDC")
    print("Tweety: 10 notes, nominal tone body 238 ms")
    print("Edition: WRCX212 TWEETY")
    print("Version: v6.0.0-TW1")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("usage: apply_f4hwn_tweety_tw1.py PATH_TO_F4HWN_V6_SOURCE")
    main(Path(sys.argv[1]).resolve())
