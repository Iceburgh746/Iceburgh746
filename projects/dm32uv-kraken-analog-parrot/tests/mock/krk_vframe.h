#ifndef MOCK_VFRAME_H
#define MOCK_VFRAME_H
#include "krk.h"
#define KRK_VF_BIG_BYTES 5616
u8 *krk_vf_big_claim(u8 owner);
void krk_vf_big_release(u8 owner);
#endif
