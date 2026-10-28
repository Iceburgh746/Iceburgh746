/*
 * WRCX212 CB-40 Multimode -- reconstructable reference source
 * IMPORTANT: This is NOT the original source of the v0.3 .app binary.
 * The original C source was not included with the uploaded compiled file.
 *
 * This file contains the authentic FCC 40-channel CB frequency plan,
 * a portable scan/activity state machine, and requested AM/FM/DSB
 * mode-selection interfaces. The radio-specific hardware functions below
 * MUST be implemented using a verified F4HWN Labs build before compilation
 * into a working overlay app. Not a flashable standalone program.
 *
 * Target: Quansheng UV-K1; receive only.
 */
#include <stdint.h>
#include <stdbool.h>

enum cb_mode { CB_AM = 0, CB_FM = 1, CB_DSB = 2 };
enum { CB_CHANNEL_COUNT = 40, CB_DWELL_MS = 120, CB_HOLD_MS = 2000 };

/* kHz channel centers, standard FCC channel number order (1-40). */
static const uint16_t cb_frequency_khz[CB_CHANNEL_COUNT] = {
  26965,26975,26985,27005,27015,27025,27035,27055,27065,27075,
  27085,27105,27115,27125,27135,27155,27165,27175,27185,27205,
  27215,27225,27255,27235,27245,27265,27275,27285,27295,27305,
  27315,27325,27335,27345,27355,27365,27375,27385,27395,27405
};

/* Hardware adapter: port against the actual selected Labs firmware API.
 * No transmit functions are used or provided.
 * DSB requires a real demodulator; changing a display label is insufficient.
 */
struct cb_radio {
  bool (*tune_rx_khz)(uint32_t khz);
  bool (*set_rx_mode)(enum cb_mode mode);
  bool (*carrier_open)(void);
  int16_t (*rssi_dbm)(void);
  uint32_t (*millis)(void);
  void (*show)(uint8_t channel_1based, uint32_t khz,
               enum cb_mode mode, bool active, int16_t rssi);
};

struct cb_state {
  uint8_t channel;             /* 0..39 */
  enum cb_mode mode;
  bool scan_enabled;
  bool active;
  uint32_t tuned_at, last_carrier_at;
  uint32_t hits;
};

static bool cb_tune(struct cb_state *s, const struct cb_radio *r) {
  if (!r->tune_rx_khz || !r->set_rx_mode) return false;
  if (!r->set_rx_mode(s->mode)) return false;
  if (!r->tune_rx_khz(cb_frequency_khz[s->channel])) return false;
  s->tuned_at = r->millis();
  s->last_carrier_at = s->tuned_at;
  s->active = false;
  return true;
}

bool cb_start(struct cb_state *s, const struct cb_radio *r) {
  s->channel = 0;
  s->mode = CB_AM;
  s->scan_enabled = true;
  s->active = false;
  s->hits = 0;
  return cb_tune(s, r);
}

bool cb_select_mode(struct cb_state *s, const struct cb_radio *r,
                    enum cb_mode mode) {
  if (mode > CB_DSB || !r->set_rx_mode) return false;
  if (!r->set_rx_mode(mode)) return false;
  s->mode = mode;
  return true;
}

bool cb_step_channel(struct cb_state *s, const struct cb_radio *r, int8_t dir) {
  if (dir > 0) s->channel = (uint8_t)((s->channel + 1) % CB_CHANNEL_COUNT);
  else if (dir < 0) s->channel = (uint8_t)
    ((s->channel + CB_CHANNEL_COUNT - 1) % CB_CHANNEL_COUNT);
  else return true;
  return cb_tune(s, r);
}

bool cb_tick(struct cb_state *s, const struct cb_radio *r) {
  if (!r->millis || !r->carrier_open) return false;
  uint32_t now = r->millis();
  bool carrier = r->carrier_open();
  if (carrier) {
    if (!s->active) ++s->hits;
    s->active = true;
    s->last_carrier_at = now;
  } else if (s->active &&
             (uint32_t)(now - s->last_carrier_at) >= CB_HOLD_MS) {
    s->active = false;
    s->last_carrier_at = now;
  }
  if (r->show)
    r->show((uint8_t)(s->channel + 1), cb_frequency_khz[s->channel],
            s->mode, s->active, r->rssi_dbm ? r->rssi_dbm() : 0);
  if (s->scan_enabled && !s->active &&
      (uint32_t)(now - s->tuned_at) >= CB_DWELL_MS)
    return cb_step_channel(s, r, 1);
  return true;
}
