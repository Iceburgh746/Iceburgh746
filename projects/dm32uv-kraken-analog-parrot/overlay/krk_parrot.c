/*
 * WRCX212 Analog Parrot replacement for DM-32UV Kraken.
 * Experimental: replaces DMR Parrot using the existing 5,616-byte shared buffer.
 * 2-bit adaptive delta audio at 4 kHz => approximately 5.6 s recording.
 * The analog FM RX and TX PCM paths are borrowed from krk_aprsrx.c / krk_aprs_tx.c.
 *
 * SAFETY:
 * - Disarmed on boot and every power-on;
 * - no automatic RF send except while Status screen is open;
 * - manual send needs a second deliberate OK;
 * - RX/TX frequency and A/B side must remain unchanged during playback;
 * - stock FM send gates, bounded time, abort and PTT release;
 * - receive monitoring never keys up.
 * This file is not validated on a physical DM-32UV.
 */
#include "krk.h"
#include "krk_stock.h"
#include "krk_vframe.h"
#define APMAGIC 0x41503031u
#define OWNER 1u
#define CAP KRK_VF_BIG_BYTES
#define TX_DLY 150u     /* 1.5 seconds (100 Hz OS tick) */
#define COOL_DLY 300u   /* 3 seconds */
#define MIN_SAMPLES 400u /* 0.1 second @ 4 kHz */
#define AUDIO_FM_PLAY 0x40u
static const u16 steps[] = {
    64,86,115,154,206,276,370,496,664,890,1193,1599,2142,2870,3846,5153,
    6904,9250,12392,16601,22238,29794
};
static struct {
    u32 magic, last_tick;
    u8 armed, rec, ready, playing, status_open, path_owned, rx_side;
    u8 *buffer;
    u32 rx_freq, tx_freq, samples, pos, echo_due, cool_until;
    u32 echoes, recorded_calls;
    u32 last_sw, last_half_tick, sig;
    int predictor, idx;
    u8 error;
} A KRK_FEAT;
static int clamp(int x,int lo,int hi){return x<lo?lo:(x>hi?hi:x);}
static void init(void)
{
    if (A.magic != APMAGIC || (int)(OS_TICKS-A.last_tick)<0) {
        u8 *p=(u8 *)&A;
        unsigned i;
        for(i=0;i<sizeof A;i++)p[i]=0;
        A.magic=APMAGIC;
        A.last_tick=OS_TICKS;
    }
}
static void release_path(void)
{
    if (A.path_owned) {
        AUDIO_CONTROL &= ~AUDIO_FM_PLAY;
        if ((VFO_FLAGS8&0xf0)==0 && !(LOADFLAGS&8))
            gpio_set_output_bit(GPIO_AF_SWITCH);
        A.path_owned=0;
    }
}
static void reset_codec(void){A.predictor=0;A.idx=6;}
static int path_ok(void)
{
    if ((VFO_FLAGS8&0xf0)!=0 || (VFO_FLAGS8&8) || (LOADFLAGS&8))return 0;
    if (CALL_ACTIVE&TX_UP || (ptt_pin_down()&1))return 0;
    if (dual_watch_state()!=0)return 0;
    if (krk_aprs_tx_busy())return 0;
    if (krk_screen_owner==KRK_SCR_MAGIC && !A.status_open)return 0;
    return 1;
}
static void path_assert(void)
{
    if (!A.path_owned) {
        gpio_clear_output_bit(GPIO_AF_SWITCH);
        AUDIO_CONTROL |= AUDIO_FM_PLAY;
        A.path_owned=1;
    } else {
        gpio_clear_output_bit(GPIO_AF_SWITCH);
        AUDIO_CONTROL |= AUDIO_FM_PLAY;
    }
}
/* 2-bit ADPCM at 4 kHz. Process two adjacent 8-kHz PCM samples as one.
 * Quarter-byte encodes sign (bit1) and magnitude (bit0).
 */
static u8 encode(int sample)
{
    int step=steps[A.idx], d=sample-A.predictor;
    u8 negative=(u8)(d<0),large;
    int delta;
    if (d<0)d=-d;
    large=(u8)(d>step*2/3);
    delta=large?step:step/3;
    A.predictor=clamp(A.predictor+(negative?-delta:delta),-32768,32767);
    A.idx=clamp(A.idx+(large?2:-1),0,(int)(sizeof steps/sizeof *steps)-1);
    return (u8)((negative<<1)|large);
}
static short decode(u8 code)
{
    int step=steps[A.idx];
    int delta=(code&1)?step:step/3;
    A.predictor=clamp(A.predictor+((code&2)?-delta:delta),-32768,32767);
    A.idx=clamp(A.idx+((code&1)?2:-1),0,(int)(sizeof steps/sizeof *steps)-1);
    return (short)A.predictor;
}
static void record_half(volatile const u8 *h)
{
    unsigned i;
    for(i=0;i<256 && A.samples<CAP*4u;i+=2) {
        int s0=(short)((h[i*2]<<8)|h[i*2+1]);
        int s1=(short)((h[(i+1)*2]<<8)|h[(i+1)*2+1]);
        u8 code=encode((s0+s1)/2);
        u32 j=A.samples>>2;
        unsigned sh=(A.samples&3u)*2u;
        if (!sh) A.buffer[j]=0;
        A.buffer[j]|=(u8)(code<<sh);
        A.samples++;
    }
}
static void record_finish(void)
{
    if (!A.rec)return;
    A.rec=0;
    if(A.samples>=MIN_SAMPLES) {
        A.ready=1;
        A.recorded_calls++;
        A.echo_due=OS_TICKS+TX_DLY;
    } else {
        A.ready=0;A.samples=0;A.echo_due=0;
    }
}
static void disarm(void)
{
    release_path();
    A.armed=0;
    A.rec=0;A.ready=0;A.playing=0;
    A.echo_due=0;
    A.samples=0;A.pos=0;
    A.buffer=0;
    krk_vf_big_release(OWNER);
}
void krk_parrot_rx_start(void){}  /* DMR hook preserved for ABI but intentionally unused */
void krk_parrot_rx_end(void){}    /* DMR hook preserved for ABI but intentionally unused */
void krk_parrot_tick(void)
{
    init();
    /* Maintenance watchdog: transmitter is owned by krk_fm_send_opt;
       DMR vocoder hooks have no active session. */
}
/* Installed into the existing 10 ms background tick, prior to APRS RX Monitor.
 * Does not transmit from background context, ever.
 */
void krk_analog_parrot_bg_tick(void)
{
    u32 sw, now;
    init();
    now=OS_TICKS;
    A.last_tick=now;
    if (!A.armed || !A.buffer || A.playing) {
        release_path();return;
    }
    if((int)(now-A.cool_until)<0) {
        release_path();return;
    }
    if(!path_ok()) {
        record_finish();
        release_path();
        return;
    }
    path_assert();
    if(stock_rx_active()) {
        if (!A.rec) {
            A.rec=1;A.ready=0;
            A.samples=0;A.echo_due=0;
            A.rx_freq=RX_FREQ10;
            A.tx_freq=TX_FREQ10;
            A.rx_side=(u8)SIDE_SELECTED;
            A.last_sw=FM_ADDR_SW&3u;
            A.last_half_tick=now;
            reset_codec();
        }
        if(RX_FREQ10!=A.rx_freq || TX_FREQ10!=A.tx_freq || SIDE_SELECTED!=A.rx_side) {
            record_finish();A.ready=0;return;
        }
        sw=FM_ADDR_SW&3u;
        if(sw!=A.last_sw && (sw==1||sw==2)){
            A.last_sw=sw;
            if(now-A.last_half_tick>10u) {
                /* Missing audio blocks invalidate recording rather than mix disconnected speech. */
                A.rec=0;A.ready=0;A.samples=0;
                return;
            }
            A.last_half_tick=now;
            record_half(FM_RX_HALF(sw==1?0:1));
            /* When the buffer fills, hold the capture until carrier drop.
               Do not overwrite the message with the tail of the same call. */
        }
    } else if(A.rec) {
        record_finish();
    }
}
static int playback_abort(void)
{
    if(krk_key_scan()!=SCAN_NONE)return 1;
    if(!A.armed || A.rx_freq!=RX_FREQ10 || A.tx_freq!=TX_FREQ10
       || SIDE_SELECTED!=A.rx_side)return 1;
    return 0;
}
static int playback_fill(void *ctx, volatile u8 *h)
{
    unsigned i;
    int v=0;
    (void)ctx;
    for(i=0;i<256;i++){
        if(A.pos<A.samples*2u){
            if(!(A.pos&1u)){
                u8 byte=A.buffer[(A.pos>>1)>>2];
                u8 code=(u8)((byte>>(((A.pos>>1)&3u)*2u))&3u);
                v=decode(code);
            }
            A.pos++;
        } else v=0;
        h[2*i]=(u8)(v>>8);
        h[2*i+1]=(u8)v;
    }
    return A.pos>=A.samples*2u;
}
static int transmit_recording(void)
{
    const char *why=0;
    struct krk_fm_opts opt;
    int rc;
    if(!A.armed || !A.ready || A.samples<MIN_SAMPLES)return -1;
    if((VFO_FLAGS8&0xf0) || tx_is_inhibited() || (BAND_FLAGS&0x20)
        || stock_rx_active() || (CALL_ACTIVE&TX_UP)
        || RX_FREQ10!=A.rx_freq || TX_FREQ10!=A.tx_freq
        || SIDE_SELECTED!=A.rx_side || dual_watch_state()!=0)
        return -2;
    A.playing=1; A.rec=0; A.echo_due=0;
    release_path();
    A.pos=0;reset_codec();
    opt.abort=playback_abort;
    opt.budget=(A.samples+3999u)/4000u*100u + 300u; /* 3 s margin */
    rc=krk_fm_send_opt(playback_fill,0,&opt,&why);
    A.playing=0;A.cool_until=OS_TICKS+COOL_DLY;
    if(rc==0){A.echoes++;return 0;}
    return rc==1?-3:-4;
}
static int get_arm(void){init();return A.armed!=0;}
static void set_arm(int on)
{
    init();
    if(!on){disarm();return;}
    if(A.armed)return;
    if((VFO_FLAGS8&0xf0) || dual_watch_state()!=0)return;
    krk_mon_set(0);               /* Parrot and APRS RX Monitor cannot own FM RX RAM simultaneously */
    A.buffer=krk_vf_big_claim(OWNER);
    if(A.buffer){
        A.armed=1;A.samples=0;A.ready=0;A.rec=0;A.echo_due=0;
        A.last_sw=FM_ADDR_SW&3u;
    }
}
static const struct krk_opt arm_opt={
    .title="Analog Parrot",.labels=krk_offon_labels,.n=2,.get=get_arm,.set=set_arm
};
const struct krk_screen krk_parrot_arm_screen={
    .label="Analog Parrot",.opt=&arm_opt,
};
static void clear_run(void)
{
    init();
    if(A.rec){krk_popup(KRK_POP_ERR,"Recording now",0,1200);return;}
    A.samples=0;A.ready=0;A.echo_due=0;
    krk_popup(KRK_POP_OK,"Audio cleared",0,1000);
}
static void replay_run(void)
{
    int r;
    init();
    if(!A.armed || !A.ready){krk_popup(KRK_POP_ERR,"No analog audio","Wait for FM call",1500);return;}
    if(!krk_tx_arm("OK again to TX"))return;
    krk_popup(KRK_POP_INFO,"Analog replay",0,0);
    r=transmit_recording();
    if(r==0)krk_popup(KRK_POP_OK,"Analog played",0,1200);
    else krk_popup(KRK_POP_ERR,"Replay refused",r==-2?"TX/channel gate":"Busy or timeout",1500);
}
static void check_run(void)
{
    char b[KRK_LINE_BUF];init();
    b[0]=0;krk_cat(b,"Captured ");
    krk_cati(b,(int)(A.samples/4u));krk_cat(b," ms");
    krk_popup(KRK_POP_INFO,"Analog audio status",b,1800);
}
static void status_enter(void)
{
    init();A.status_open=1;A.sig=0xffffffffu;A.echo_due=0;
}
static void status_exit(void)
{
    A.status_open=0;A.echo_due=0;
    /* Armed state and captured audio survive Back; auto repeat stops. */
}
static void status_tick(u8 scan)
{
    u32 sig,now=OS_TICKS;
    char l[KRK_LINE_BUF];
    init();
    if(scan==SCAN_BACK)A.echo_due=0;
    if(A.armed && A.ready && !A.rec && !A.playing && A.echo_due
       && (int)(now-A.echo_due)>=0 && (int)(now-A.cool_until)>=0
       && !stock_rx_active()){
        A.echo_due=0;
        transmit_recording();
    }
    sig=A.samples ^ (u32)A.rec<<24 ^ (u32)A.playing<<25 ^ (u32)A.armed<<26
        ^ A.echoes<<2 ^ (now/50u);
    if(sig==A.sig)return;
    A.sig=sig;
    krk_line(0,"WRCX212 Analog Parrot",KRK_ACCENT);
    krk_line(1,A.armed?"ARMED":"OFF",A.armed?KRK_WARN:KRK_DIM);
    krk_line(2,A.rec?"RECORDING FM":A.playing?"REPLAYING":A.ready?"AUDIO READY":"Listening",KRK_FG);
    l[0]=0;krk_cat(l,"Length ");
    krk_cati(l,(int)(A.samples/4u));krk_cat(l," ms");
    krk_line(3,l,KRK_FG);
    l[0]=0;krk_cat(l,"Echoes ");
    krk_cati(l,(int)A.echoes);
    krk_line(4,l,KRK_FG);
    krk_line(6,"Auto echo: Status only",KRK_DIM);
    krk_line(7,"Up to 5.6 seconds",KRK_DIM);
    krk_line(9,"Back: stop auto echo",KRK_DIM);
}
const struct krk_screen krk_parrot_status_screen={
    .label="Status",.enter=status_enter,.tick=status_tick,.exit=status_exit,
};
const struct krk_screen krk_parrot_play_screen={
    .label="Replay Audio",.enter=replay_run,.flags=KRK_SF_ONESHOT,
};
const struct krk_screen krk_parrot_check_screen={
    .label="Audio Check",.enter=check_run,.flags=KRK_SF_ONESHOT,
};
const struct krk_screen krk_parrot_clear_screen={
    .label="Clear Audio",.enter=clear_run,.flags=KRK_SF_ONESHOT,
};
int krk_parrot_armed(void){return A.magic==APMAGIC && A.armed;}
