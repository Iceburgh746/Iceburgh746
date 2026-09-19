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
    app_c = root / "App/app/app.c"
    app_menu = root / "App/app/menu.c"
    cmake_app = root / "App/CMakeLists.txt"
    misc_c = root / "App/misc.c"
    misc_h = root / "App/misc.h"
    radio_c = root / "App/radio.c"
    radio_h = root / "App/radio.h"
    settings_c = root / "App/settings.c"
    ui_menu_c = root / "App/ui/menu.c"
    ui_menu_h = root / "App/ui/menu.h"
    bk4829 = root / "App/driver/bk4829.c"
    multiboot_c = root / "App/ui/multiboot.c"

    # ---------- Build flag ----------
    replace_once(
        cmake_app,
        "enable_feature(ENABLE_FEAT_F4HWN_AUDIO)\n",
        "enable_feature(ENABLE_FEAT_F4HWN_AUDIO)\n"
        "enable_feature(ENABLE_FEAT_WRCX_SMART_MIC)\n",
    )

    # ---------- Global Smart Mic state ----------
    replace_once(
        misc_c,
        "    #ifdef ENABLE_FEAT_F4HWN_AUDIO\n"
        "        uint8_t       gSetting_set_audio_fm = 0;\n"
        "        uint8_t       gSetting_set_audio_am = 0;\n"
        "    #endif\n",
        "    #ifdef ENABLE_FEAT_F4HWN_AUDIO\n"
        "        uint8_t       gSetting_set_audio_fm = 0;\n"
        "        uint8_t       gSetting_set_audio_am = 0;\n"
        "    #endif\n"
        "    #ifdef ENABLE_FEAT_WRCX_SMART_MIC\n"
        "        uint8_t       gSetting_smart_mic = 1; // 0=OFF, 1=NORMAL, 2=STRONG\n"
        "    #endif\n",
    )
    replace_once(
        misc_h,
        "    #ifdef ENABLE_FEAT_F4HWN_AUDIO\n"
        "        extern uint8_t            gSetting_set_audio_fm;\n"
        "        extern uint8_t            gSetting_set_audio_am;\n"
        "    #endif\n",
        "    #ifdef ENABLE_FEAT_F4HWN_AUDIO\n"
        "        extern uint8_t            gSetting_set_audio_fm;\n"
        "        extern uint8_t            gSetting_set_audio_am;\n"
        "    #endif\n"
        "    #ifdef ENABLE_FEAT_WRCX_SMART_MIC\n"
        "        extern uint8_t            gSetting_smart_mic;\n"
        "    #endif\n",
    )

    # Default Smart Mic to NORMAL when this new firmware version first migrates settings.
    replace_once(
        settings_c,
        "            configByte[4] &= (uint8_t)~0x3C;  // SET_KEY = 0\\n"
        "            //configByte[4] &= (uint8_t)~0x40;  // SET_NAV = 0\\n",
        "            configByte[4] &= (uint8_t)~0x3C;  // SET_KEY = 0\\n"
        "            #ifdef ENABLE_FEAT_WRCX_SMART_MIC\\n"
        "                configByte[3] = 1;  // NORMAL on first Smart Mic firmware boot\\n"
        "            #endif\\n"
        "            //configByte[4] &= (uint8_t)~0x40;  // SET_NAV = 0\\n",
    )

    # ---------- Persistent setting ----------
    replace_once(
        settings_c,
        "    #ifdef ENABLE_NOAA\n"
        "        gEeprom.NOAA_AUTO_SCAN   = (Data[3] <  2) ? Data[3] : false;\n"
        "    #endif\n",
        "    #ifdef ENABLE_NOAA\n"
        "        gEeprom.NOAA_AUTO_SCAN   = (Data[3] <  2) ? Data[3] : false;\n"
        "    #elif defined(ENABLE_FEAT_WRCX_SMART_MIC)\n"
        "        gSetting_smart_mic       = (Data[3] < 3) ? Data[3] : 1;\n"
        "    #endif\n",
    )
    replace_once(
        settings_c,
        "    #ifdef ENABLE_NOAA\n"
        "        State[3] = gEeprom.NOAA_AUTO_SCAN;\n"
        "    #else\n"
        "        State[3] = false;\n"
        "    #endif\n",
        "    #ifdef ENABLE_NOAA\n"
        "        State[3] = gEeprom.NOAA_AUTO_SCAN;\n"
        "    #elif defined(ENABLE_FEAT_WRCX_SMART_MIC)\n"
        "        State[3] = gSetting_smart_mic;\n"
        "    #else\n"
        "        State[3] = false;\n"
        "    #endif\n",
    )

    # ---------- Menu ID and strings ----------
    replace_once(
        ui_menu_h,
        "    MENU_MIC,\n    MENU_MIC_BAR,\n    MENU_COMPAND,\n",
        "    MENU_MIC,\n    MENU_MIC_BAR,\n"
        "#ifdef ENABLE_FEAT_WRCX_SMART_MIC\n"
        "    MENU_SMART_MIC,\n"
        "#endif\n"
        "    MENU_COMPAND,\n",
    )
    replace_once(
        ui_menu_h,
        "extern const char* const            gSubMenu_ROGER[3];\n",
        "#ifdef ENABLE_FEAT_WRCX_SMART_MIC\n"
        "extern const char* const            gSubMenu_SMART_MIC[3];\n"
        "#endif\n"
        "extern const char* const            gSubMenu_ROGER[3];\n",
    )
    replace_once(
        ui_menu_c,
        '    {"Mic",         MENU_MIC           },\n    {"MicBar",      MENU_MIC_BAR       },\n',
        '    {"Mic",         MENU_MIC           },\n    {"MicBar",      MENU_MIC_BAR       },\n'
        '#ifdef ENABLE_FEAT_WRCX_SMART_MIC\n'
        '    {"SmartMic",    MENU_SMART_MIC     },\n'
        '#endif\n',
    )
    replace_once(
        ui_menu_c,
        'const char* const gSubMenu_ROGER[] =\n{\n    "OFF",\n    "ROGER",\n    "MDC"\n};\n',
        '#ifdef ENABLE_FEAT_WRCX_SMART_MIC\n'
        'const char* const gSubMenu_SMART_MIC[] =\n'
        '{\n'
        '    "OFF",\n'
        '    "NORMAL",\n'
        '    "STRONG"\n'
        '};\n'
        '#endif\n\n'
        'const char* const gSubMenu_ROGER[] =\n'
        '{\n'
        '    "OFF",\n'
        '    "TWEETY",\n'
        '    "MDC"\n'
        '};\n',
    )
    replace_once(
        ui_menu_c,
        "        case MENU_MIC_BAR:\n"
        "            #ifdef ENABLE_AUDIO_BAR\n"
        "                strcpy(String, gSubMenu_OFF_ON[gSubMenuSelection]);\n"
        "            #else\n"
        "                strcpy(String, gSubMenu_NA);\n"
        "            #endif\n"
        "            break;\n",
        "        case MENU_MIC_BAR:\n"
        "            #ifdef ENABLE_AUDIO_BAR\n"
        "                strcpy(String, gSubMenu_OFF_ON[gSubMenuSelection]);\n"
        "            #else\n"
        "                strcpy(String, gSubMenu_NA);\n"
        "            #endif\n"
        "            break;\n\n"
        "        #ifdef ENABLE_FEAT_WRCX_SMART_MIC\n"
        "        case MENU_SMART_MIC:\n"
        "            strcpy(String, gSubMenu_SMART_MIC[gSubMenuSelection]);\n"
        "            break;\n"
        "        #endif\n",
    )
    replace_once(
        ui_menu_c,
        "static const uint8_t CatAudio[]   = {\n"
        "    MENU_MIC, MENU_MIC_BAR, MENU_BEEP,\n",
        "static const uint8_t CatAudio[]   = {\n"
        "    MENU_MIC, MENU_MIC_BAR,\n"
        "#ifdef ENABLE_FEAT_WRCX_SMART_MIC\n"
        "    MENU_SMART_MIC,\n"
        "#endif\n"
        "    MENU_BEEP,\n",
    )

    # ---------- Menu behavior ----------
    anchor = "        #ifdef ENABLE_AUDIO_BAR\n            case MENU_MIC_BAR:\n        #endif\n        case MENU_BCL:\n"
    text = app_menu.read_text()
    if anchor not in text:
        raise SystemExit("menu limits anchor not found")
    text = text.replace(
        anchor,
        "        #ifdef ENABLE_FEAT_WRCX_SMART_MIC\n"
        "        case MENU_SMART_MIC:\n"
        "            *pMax = 2;\n"
        "            break;\n"
        "        #endif\n\n" + anchor,
        1,
    )
    app_menu.write_text(text)

    replace_once(
        app_menu,
        "        #ifdef ENABLE_AUDIO_BAR\n"
        "            case MENU_MIC_BAR:\n"
        "                gSetting_mic_bar = gSubMenuSelection;\n"
        "                break;\n"
        "        #endif\n",
        "        #ifdef ENABLE_AUDIO_BAR\n"
        "            case MENU_MIC_BAR:\n"
        "                gSetting_mic_bar = gSubMenuSelection;\n"
        "                break;\n"
        "        #endif\n"
        "        #ifdef ENABLE_FEAT_WRCX_SMART_MIC\n"
        "            case MENU_SMART_MIC:\n"
        "                gSetting_smart_mic = gSubMenuSelection;\n"
        "                RADIO_SmartMicReset();\n"
        "                break;\n"
        "        #endif\n",
    )
    replace_once(
        app_menu,
        "#ifdef ENABLE_AUDIO_BAR\n"
        "        case MENU_MIC_BAR:\n"
        "            gSubMenuSelection = gSetting_mic_bar;\n"
        "            break;\n"
        "#endif\n",
        "#ifdef ENABLE_AUDIO_BAR\n"
        "        case MENU_MIC_BAR:\n"
        "            gSubMenuSelection = gSetting_mic_bar;\n"
        "            break;\n"
        "#endif\n"
        "#ifdef ENABLE_FEAT_WRCX_SMART_MIC\n"
        "        case MENU_SMART_MIC:\n"
        "            gSubMenuSelection = gSetting_smart_mic;\n"
        "            break;\n"
        "#endif\n",
    )

    # ---------- Smart Mic engine ----------
    smart_impl = r'''
#ifdef ENABLE_FEAT_WRCX_SMART_MIC
/* WRCX212 Smart Mic v1
 * REG_64 supplies live TX voice amplitude. REG_7D low 6 bits control
 * microphone gain in 0.5 dB steps. The controller uses a progressive
 * soft-knee reduction for peaks and a deliberately slow recovery.
 */
static uint8_t  gSmartMicGain;
static uint16_t gSmartMicEnvelope;
static uint8_t  gSmartMicRecover;

void RADIO_SmartMicReset(void)
{
    gSmartMicGain = gEeprom.MIC_SENSITIVITY_TUNING & 0x3Fu;
    gSmartMicEnvelope = 0u;
    gSmartMicRecover = 0u;
    BK4819_WriteRegister(BK4819_REG_7D, 0xE940u | gSmartMicGain);
}

void RADIO_SmartMicTimeSlice(void)
{
    if (gSetting_smart_mic == 0u)
        return;

    uint16_t sample = BK4819_GetVoiceAmplitudeOut() & 0x7FFFu;

    if (sample > gSmartMicEnvelope)
        gSmartMicEnvelope += (sample - gSmartMicEnvelope + 1u) >> 1;
    else
        gSmartMicEnvelope -= (gSmartMicEnvelope - sample) >> 3;

    const uint8_t base = gEeprom.MIC_SENSITIVITY_TUNING & 0x3Fu;
    const uint8_t max_boost = (gSetting_smart_mic >= 2u) ? 32u : 24u;
    const uint8_t max_gain = (base + max_boost > 63u) ? 63u : (uint8_t)(base + max_boost);
    const uint8_t min_gain = (base > 12u) ? (uint8_t)(base - 12u) : 0u;
    uint8_t next = gSmartMicGain;

    if (gSmartMicEnvelope > 3600u) {
        next = (next > min_gain + 3u) ? (uint8_t)(next - 4u) : min_gain;
        gSmartMicRecover = 0u;
    } else if (gSmartMicEnvelope > 2600u) {
        next = (next > min_gain + 1u) ? (uint8_t)(next - 2u) : min_gain;
        gSmartMicRecover = 0u;
    } else if (gSmartMicEnvelope > 1900u) {
        next = (next > min_gain) ? (uint8_t)(next - 1u) : min_gain;
        gSmartMicRecover = 0u;
    } else if (gSmartMicEnvelope >= 350u && gSmartMicEnvelope < 1100u) {
        if (++gSmartMicRecover >= 5u) {
            gSmartMicRecover = 0u;
            if (next < max_gain)
                next++;
        }
    } else if (gSmartMicEnvelope < 300u) {
        gSmartMicRecover = 0u;
        if (next > base)
            next--;
    } else {
        gSmartMicRecover = 0u;
    }

    if (next != gSmartMicGain) {
        gSmartMicGain = next;
        BK4819_WriteRegister(BK4819_REG_7D, 0xE940u | gSmartMicGain);
    }
}
#endif

'''
    replace_once(
        radio_c,
        "void RADIO_SetTxParameters(void)\n{\n",
        smart_impl + "void RADIO_SetTxParameters(void)\n{\n",
    )
    replace_once(
        radio_c,
        "    BK4819_PrepareTransmit();\n\n    SYSTEM_DelayMs(10);\n",
        "    BK4819_PrepareTransmit();\n"
        "#ifdef ENABLE_FEAT_WRCX_SMART_MIC\n"
        "    RADIO_SmartMicReset();\n"
        "#endif\n\n"
        "    SYSTEM_DelayMs(10);\n",
    )

    replace_once(
        radio_h,
        "void RADIO_SetTxParameters(void);\n",
        "void RADIO_SetTxParameters(void);\n"
        "#ifdef ENABLE_FEAT_WRCX_SMART_MIC\n"
        "void RADIO_SmartMicReset(void);\n"
        "void RADIO_SmartMicTimeSlice(void);\n"
        "#endif\n",
    )

    replace_once(
        app_c,
        "    if (gCurrentFunction == FUNCTION_TRANSMIT)\n"
        "    {   // transmitting\n",
        "    if (gCurrentFunction == FUNCTION_TRANSMIT)\n"
        "    {   // transmitting\n"
        "#ifdef ENABLE_FEAT_WRCX_SMART_MIC\n"
        "        if ((gFlashLightBlinkCounter % (20 / 10)) == 0)\n"
        "            RADIO_SmartMicTimeSlice();\n"
        "#endif\n",
    )

    # ---------- TW3 Space Tweety ----------
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
    new_roger = r'''/* WRCX212 Space Tweety TW3: 10-note telemetry-style warble. */
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

    # ---------- WRCX212 multiboot banner ----------
    replace_once(multiboot_c, 'GUI_DisplaySmallestInverse("F4HWN MULTIBOOT", 34, 0, true, true, 94);', 'GUI_DisplaySmallestInverse("WRCX212 MULTIBOOT", 34, 0, true, true, 94);')

    print("WRCX212 TW3 Space Tweety + Smart Mic patch applied")
    print("SmartMic menu: OFF / NORMAL / STRONG")
    print("NORMAL max boost: +12 dB; STRONG max boost: +16 dB")
    print("Space Tweety: 10 notes with TW3 frequency/timing table, gain 110/127")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("usage: apply_f4hwn_smartmic_tw3.py PATH_TO_F4HWN_V6_SOURCE")
    main(Path(sys.argv[1]).resolve())
