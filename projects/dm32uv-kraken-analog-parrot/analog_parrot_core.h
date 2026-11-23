#ifndef WRCX212_ANALOG_PARROT_CORE_H
#define WRCX212_ANALOG_PARROT_CORE_H
#include <stddef.h>
#include <stdint.h>
typedef enum { AP_IDLE=0, AP_RECORDING, AP_READY, AP_PLAYING } ap_state;
typedef struct {
    uint8_t *data;
    size_t capacity, samples, position;
    int16_t predictor;
    int step_index;
    ap_state state;
} ap_voice;
void ap_init(ap_voice *, uint8_t *, size_t);
int ap_record_begin(ap_voice *);
size_t ap_record_write(ap_voice *, const int16_t *, size_t);
int ap_record_end(ap_voice *);
int ap_play_begin(ap_voice *);
size_t ap_play_read(ap_voice *, int16_t *, size_t);
void ap_stop(ap_voice *);
#endif
