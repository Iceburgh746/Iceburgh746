#include "analog_parrot_core.h"
#include <string.h>
/* IMA ADPCM4: 8 kHz mono, 4 kB/sec. No hardware accesses.
 * A fresh stream begins with predictor=0, step_index=0, which is
 * also its decoder starting state; odd sample counts are supported.
 */
static const int st[89]={7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,80,88,97,107,118,130,143,157,173,190,209,230,253,279,307,337,371,408,449,494,544,598,658,724,796,876,963,1060,1166,1282,1411,1552,1707,1878,2066,2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,8630,9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,29794,32767};
static const int shift[8]={-1,-1,-1,-1,2,4,6,8};
static int clip(int x,int l,int u){return x<l?l:x>u?u:x;}
static uint8_t process_encode(ap_voice *a,int16_t x){
    int step=st[a->step_index],d=(int)x-(int)a->predictor;
    int sign=d<0?8:0,code=0,change=step>>3;
    if(d<0)d=-d;
    if(d>=step){code|=4;d-=step;change+=step;}
    if(d>=(step>>1)){code|=2;d-=step>>1;change+=step>>1;}
    if(d>=(step>>2)){code|=1;change+=step>>2;}
    a->predictor=(int16_t)clip((int)a->predictor+(sign?-change:change),-32768,32767);
    a->step_index=clip(a->step_index+shift[code],0,88);
    return (uint8_t)(code|sign);
}
static int16_t process_decode(ap_voice *a,uint8_t n){
    int step=st[a->step_index],change=step>>3,code=n&7;
    if(code&4)change+=step;
    if(code&2)change+=step>>1;
    if(code&1)change+=step>>2;
    a->predictor=(int16_t)clip((int)a->predictor+((n&8)?-change:change),-32768,32767);
    a->step_index=clip(a->step_index+shift[code],0,88);
    return a->predictor;
}
void ap_init(ap_voice*a,uint8_t*storage,size_t cap){
    if(!a)return;
    memset(a,0,sizeof(*a));a->data=storage;a->capacity=cap;
}
int ap_record_begin(ap_voice*a){
    if(!a||!a->data||!a->capacity||a->state==AP_PLAYING)return 0;
    a->samples=a->position=0;a->predictor=0;a->step_index=0;
    a->state=AP_RECORDING;return 1;
}
size_t ap_record_write(ap_voice*a,const int16_t*pcm,size_t count){
    size_t i;
    if(!a||!pcm||a->state!=AP_RECORDING)return 0;
    for(i=0;i<count && a->samples<a->capacity*2;i++){
        size_t idx=a->samples>>1;
        uint8_t nib=process_encode(a,pcm[i]);
        if(a->samples&1)a->data[idx]=(uint8_t)(a->data[idx]|(nib<<4));
        else a->data[idx]=nib;
        a->samples++;
    }
    return i;
}
int ap_record_end(ap_voice*a){
    if(!a||a->state!=AP_RECORDING)return 0;
    a->state=a->samples?AP_READY:AP_IDLE;return a->state==AP_READY;
}
int ap_play_begin(ap_voice*a){
    if(!a||a->state!=AP_READY||!a->samples)return 0;
    a->position=0;a->predictor=0;a->step_index=0;
    a->state=AP_PLAYING;return 1;
}
size_t ap_play_read(ap_voice*a,int16_t*pcm,size_t count){
    size_t i=0;
    if(!a||!pcm||a->state!=AP_PLAYING)return 0;
    for(;i<count && a->position<a->samples;i++,a->position++){
        uint8_t b=a->data[a->position>>1];
        pcm[i]=process_decode(a,(uint8_t)((a->position&1)?(b>>4):(b&15)));
    }
    if(a->position==a->samples)a->state=AP_READY;
    return i;
}
void ap_stop(ap_voice*a){
    if(!a)return;
    if(a->state==AP_RECORDING)a->state=a->samples?AP_READY:AP_IDLE;
    else if(a->state==AP_PLAYING)a->state=AP_READY;
}
