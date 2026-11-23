#ifndef MOCK_KRK_H
#define MOCK_KRK_H
#include <stdint.h>
typedef uint8_t u8;
typedef uint16_t u16;
typedef uint32_t u32;
#define KRK_FEAT
#define KRK_LINE_BUF 64
#define KRK_ACCENT 1
#define KRK_WARN 2
#define KRK_DIM 3
#define KRK_FG 4
#define KRK_POP_ERR 5
#define KRK_POP_OK 6
#define KRK_POP_INFO 7
#define KRK_SF_ONESHOT 8
#define KRK_SCR_MAGIC 0x53435252u
#define SCAN_NONE 0
#define SCAN_BACK 3
struct krk_opt {
  const char* title;
  const char*const*labels;
  int n;
  int (*get)(void);
  void (*set)(int);
};
struct krk_screen {
  const char* label;
  void (*enter)(void);
  void (*tick)(u8);
  void (*exit)(void);
  const struct krk_opt* opt;
  int flags;
};
struct krk_fm_opts { int (*abort)(void); u32 budget; };
extern const char*const krk_offon_labels[];
extern volatile u32 krk_screen_owner;
void krk_mon_set(int on);
int krk_mon_on(void);
void krk_line(int line,const char*text,u16 color);
void krk_cat(char*dest,const char*src);
void krk_cati(char*dest,int n);
void krk_popup(int kind,const char*title,const char*info,int ms);
int krk_key_scan(void);
int krk_tx_arm(const char*message);
int krk_fm_send_opt(int(*fill)(void*,volatile u8*),void*ctx,const struct krk_fm_opts*,const char**why);
#endif
