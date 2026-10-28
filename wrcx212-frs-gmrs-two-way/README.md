# WRCX212 FRS/GMRS Two-Way v1.0 — UV-K1 Labs 6.3

**Status:** Planning / engineering reference; **no completed two-way application or radio TX validation**.  
**Hardware target:** Quansheng UV-K1 with compatible F4HWN Labs 6.3 firmware.  
**Design:** 22-channel FM receiver/scanner, channel display, RSSI, squelch, activity hold, configurable tones where supported, and a proposed physical PTT interface. **Transmit locked out by default** until hardware and legal requirements are met.

## IMPORTANT: FCC restrictions on transmitting

**A frequency-unlocked radio is not automatically legal to use on FRS/GMRS.**

- **GMRS transmitter certification:** 47 CFR **§95.1761** requires GMRS transmitters to be FCC-certified for GMRS (Part 95E). A GMRS license does not waive transmitter certification. See [FCC rule](https://www.ecfr.gov/current/title-47/section-95.1761).
- **FRS transmitter certification:** 47 CFR **§95.561** requires certification specifically for FRS. The shared 22 channels do not make an uncertified device an authorized FRS transmitter. See [FCC rule](https://www.ecfr.gov/current/title-47/section-95.561).
- **Confirm the exact equipment:** Do **not** presume the Quansheng UV-K1 has GMRS or FRS equipment approval. Verify the particular hardware's FCC ID and certification before enabling over-the-air transmissions.
- **Channel and power rules still apply:** FRS is limited to **0.5 W ERP on channels 8–14**, **2 W ERP on channels 1–7 and 15–22**, and **12.5 kHz occupied bandwidth** (47 CFR §§95.567, 95.573). GMRS power, bandwidth and emission limits vary by channel and station type (47 CFR §§95.1767, 95.1773). Compliance requires more than choosing a menu option.
- **Experimental RF tests:** A dummy load is not automatic legal authorization to operate an uncertified transmitter. Any bench RF testing must be carried out in a legally permitted manner with suitable containment, attenuation and no harmful interference.

**This README is not permission to transmit. In the experimental UV-K1 application, TX must remain disabled by default and on-air GMRS/FRS operation must not be enabled unless the actual equipment and operation are authorized.**

## Project requirements

1. Scan all 22 shared FRS/GMRS receive frequencies in standard channel order; stop on activity and resume after a configured hold.
2. Display channel number, actual tuned center frequency, RSSI, RX/TX state, and tone/squelch status.
3. Provide appropriate narrowband receive filtering and calibrated, service-compliant settings where applicable.
4. Add optional CTCSS/DCS and manual channel selection only where the firmware APIs support them.
5. Design physical PTT logic with release-on-exit, transmit timeout, no automatic keying, and a **default-disabled TX gate**. A transmit function is **not yet implemented or verified**.
6. Validate the frequency plan, firmware API, RF tuning, and receiver behavior before distributing a flashable app.

## FCC 22-channel frequency reference

Frequency centers are given exactly in **MHz**; use resolution sufficient to represent 12.5 kHz offsets. Standard channel order is not simple ascending frequency order.

| Channel | Center (MHz) |
|---:|---:|
| 1 | 462.5625 |
| 2 | 462.5875 |
| 3 | 462.6125 |
| 4 | 462.6375 |
| 5 | 462.6625 |
| 6 | 462.6875 |
| 7 | 462.7125 |
| 8 | 467.5625 |
| 9 | 467.5875 |
| 10 | 467.6125 |
| 11 | 467.6375 |
| 12 | 467.6625 |
| 13 | 467.6875 |
| 14 | 467.7125 |
| 15 | 462.5500 |
| 16 | 462.5750 |
| 17 | 462.6000 |
| 18 | 462.6250 |
| 19 | 462.6500 |
| 20 | 462.6750 |
| 21 | 462.7000 |
| 22 | 462.7250 |

## Build and verification status

- **Available today:** project scope, FCC compliance notice and verified frequency plan.
- **Not established:** successful compilation of a two-way Labs overlay, BK4819 tuning behavior, PTT safety, TX frequency/power/deviation, and on-air legal authorization.
- **Release rule:** Never describe the project as “TX-tested,” “FCC compliant” or “ready to transmit” until those statements are independently established.

## Further references

- [FRS channels, 47 CFR §95.563](https://www.ecfr.gov/current/title-47/section-95.563)
- [GMRS transmitter certification, 47 CFR §95.1761](https://www.ecfr.gov/current/title-47/section-95.1761)
- [FRS transmitter certification, 47 CFR §95.561](https://www.ecfr.gov/current/title-47/section-95.561)
- [GMRS power limits, 47 CFR §95.1767](https://www.ecfr.gov/current/title-47/section-95.1767)
- [FRS transmit power, 47 CFR §95.567](https://www.ecfr.gov/current/title-47/section-95.567)

WRCX212 Radio Lab — experimental radio engineering project.
