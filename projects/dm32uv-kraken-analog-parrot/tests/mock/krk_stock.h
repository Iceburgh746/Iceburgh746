#ifndef MOCK_STOCK_H
#define MOCK_STOCK_H
#include "krk.h"
extern volatile u32 mock_ticks, mock_rx, mock_tx, mock_fm_sw, mock_audio_control;
extern volatile u8 mock_vfo, mock_load, mock_call, mock_band;
#define OS_TICKS mock_ticks
#define RX_FREQ10 mock_rx
#define TX_FREQ10 mock_tx
#define VFO_FLAGS8 mock_vfo
#define LOADFLAGS mock_load
#define CALL_ACTIVE mock_call
#define BAND_FLAGS mock_band
#define AUDIO_CONTROL mock_audio_control
#define FM_ADDR_SW mock_fm_sw
#define TX_UP 8
#define GPIO_AF_SWITCH 0x2f
#define SIDE_SELECTED (0)
volatile const u8 *FM_RX_HALF(int n);
void gpio_clear_output_bit(u32 pin);
void gpio_set_output_bit(u32 pin);
int stock_rx_active(void);
int ptt_pin_down(void);
int dual_watch_state(void);
int krk_aprs_tx_busy(void);
int tx_is_inhibited(void);
#endif
