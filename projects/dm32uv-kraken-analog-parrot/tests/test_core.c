#include "analog_parrot_core.h"
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
int main(void){
    uint8_t buf[4096]={0};
    int16_t x[256],y[256];
    ap_voice a;
    ap_init(&a,buf,sizeof(buf));
    assert(ap_play_begin(&a)==0);
    assert(ap_record_begin(&a)==1);
    for(int i=0;i<256;i++)x[i]=(int16_t)((i%64-32)*400);
    assert(ap_record_write(&a,x,256)==256);
    assert(a.samples==256);
    assert(ap_record_end(&a)==1);
    assert(ap_play_begin(&a)==1);
    assert(ap_play_read(&a,y,256)==256);
    assert(a.state==AP_READY);
    assert(ap_play_read(&a,y,256)==0);
    assert(ap_play_begin(&a)==1);
    assert(ap_play_read(&a,y,4)==4);
    ap_stop(&a);
    assert(a.state==AP_READY);
    assert(ap_record_begin(&a)==1);
    assert(ap_record_write(&a,x,13)==13);
    assert(ap_record_end(&a)==1);
    assert(ap_play_begin(&a)==1);
    assert(ap_play_read(&a,y,256)==13);
    ap_init(&a,buf,2);
    assert(ap_record_begin(&a)==1);
    assert(ap_record_write(&a,x,256)==4);
    assert(ap_record_end(&a)==1);
    assert(ap_play_begin(&a)==1);
    assert(ap_play_read(&a,y,256)==4);
    ap_init(&a,buf,sizeof(buf));
    assert(ap_record_begin(&a)==1);
    assert(ap_record_end(&a)==0);
    assert(ap_play_begin(&a)==0);
    puts("PASS: ADPCM4 record/playback, odd samples, bounds, stop, empty buffer");
    return 0;
}
