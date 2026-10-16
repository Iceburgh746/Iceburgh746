#!/usr/bin/env python3
# Modified 2026-10-16 for WRCX212 Broadcast FM USA; see changes.patch.
# Broadcast FM read-only assets: texts, band names, the F icon and the
# decimal places.
#
#   ./gen_assets.py fm_assets.bin fm_assets.h
import os, sys
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from app_assets import Assets

a = Assets("FM")
a.text("T_FM", "FM USA")
a.text("T_SAVE", "SAVE?")
a.text("T_DEL", "DEL?")
a.text("T_ASCAN", "A-SCAN(")
a.text("T_MSCAN", "M-SCAN")
a.text("T_MR_CH", "MR(CH")
a.text("T_VFO", "VFO")
a.text("T_VFO_CH", "VFO(CH")
a.text("T_CH", "CH-")
a.table("BAND_NAME", ["88.1-107.9"])   # by band 0..3
a.u8("BMP_F", [0x3e,0x7f,0x41,0x75,0x75,0x75,0x7d,0x7f,0x3e])
a.u16("PLACE", [1000, 100, 10, 1])    # decimal places: numbers without division
a.main()
