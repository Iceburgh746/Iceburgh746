/*
 * WRCX212 embedded analog parrot host regression.
 * Compile the REAL krk_parrot.c against test mocks, not the separate ADPCM4 core.
 * This does not claim RF or QEMU validation.
 */
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include "../overlay/krk_parrot.c"

volatile u32 mock_ticks, mock_rx=4625750, mock_tx=4625750, mock_fm_sw, mock_audio_control;
volatile u8 mock_vfo, mock_load, mock_call, mock_band;
volatile u32 krk_screen_owner;
const char *const krk_offon_labels[]={"Off","On"};
static u8 big[KRK_VF_BIG_BYTES];
static u8 owner;
static u8 rx_half[512];
static volatile u8 playback[512];
static int sent, halfCount, mute;
static int rx_active;
volatile const u8 *FM_RX_HALF(int n) { (void)n;return rx_half; }
void gpio_clear_output_bit(u32 pin){ (void)pin; }
void gpio_set_output_bit(u32 pin){ (void)pin; }
int stock_rx_active(void){return rx_active;}
int ptt_pin_down(void){return 0;}
int dual_watch_state(void){return 0;}
int krk_aprs_tx_busy(void){return 0;}
int tx_is_inhibited(void){return 0;}
int krk_key_scan(void){return SCAN_NONE;}
int krk_tx_arm(const char *s){(void)s;return 1;}
int krk_mon_on(void){return 0;}
void krk_mon_set(int on){(void)on;}
u8 *krk_vf_big_claim(u8 requested){
    if(owner && owner!=requested)return NULL;
    owner=requested;return big;
}
void krk_vf_big_release(u8 requested){if(owner==requested)owner=0;}
void krk_line(int line,const char *str,u16 color){(void)line;(void)str;(void)color;}
void krk_cat(char *dest,const char *str){strcat(dest,str);}
void krk_cati(char *dest,int val){char b[32];snprintf(b,sizeof b,"%d",val);strcat(dest,b);}
void krk_popup(int kind,const char *title,const char *info,int ms){
    (void)kind;(void)title;(void)info;(void)ms;
}
int krk_fm_send_opt(int (*fill)(void*,volatile u8*),void *ctx,
                    const struct krk_fm_opts *opt,const char **why){
    int end,i,nonzero=0;
    (void)opt; (void)why;
    sent=1;
    for(i=0;i<128;i++){
        end=fill(ctx,playback);
        halfCount++;
        for(int j=0;j<512;j++)nonzero |= playback[j];
        if(end){mute=nonzero;return 0;}
    }
    return -1;
}
static void check(int yes,const char *message){
    if(!yes){fprintf(stderr,"FAILED: %s\n",message);exit(1);}
}
int main(void){
    unsigned i;
    memset(rx_half,0,sizeof rx_half);
    mock_ticks=1000;
    krk_parrot_arm_screen.opt->set(1);
    check(A.armed==1 && owner==1,"claiming the shared buffer");
    check(A.buffer==big,"buffer pointer");
    rx_active=1;
    krk_analog_parrot_bg_tick();
    for(i=0;i<12;i++){
        int j;
        for(j=0;j<256;j++){
            /* Constant but non-zero signal checks predictor convergence, sample packing. */
            int v=6000;
            rx_half[2*j]=(u8)(v>>8);
            rx_half[2*j+1]=(u8)v;
        }
        mock_fm_sw = (i%2)?1:2;
        mock_ticks++;
        krk_analog_parrot_bg_tick();
    }
    check(A.samples==12*128,"sample counts and ping-pong capture");
    rx_active=0;
    mock_ticks++;
    krk_analog_parrot_bg_tick();
    check(A.ready==1,"finish on carrier loss");
    check(A.recorded_calls==1,"recorded call count");
    check(A.echo_due>mock_ticks,"echo delay computed");
    krk_parrot_status_screen.enter();
    check(A.echo_due==0,"status entry clears stale pending echo");
    int result=transmit_recording();
    check(result==0,"FM TX mock invoked");
    check(sent && halfCount>0 && mute,"decoded FM PCM is nonzero");
    check(A.echoes==1,"echo counter increment");
    check(!A.playing,"PTT session cleanup state");
    krk_parrot_arm_screen.opt->set(0);
    check(!A.armed && !owner && !A.buffer,"release big buffer when disarmed");
    puts("PASS: embedded 2-bit codec, capture lifecycle, FM playback, shared SRAM release");
    return 0;
}
