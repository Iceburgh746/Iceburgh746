#!/usr/bin/env python3
"""Execute the delivered Thumb binary with mocked Labs callbacks (not RF hardware).
Requires: pip install unicorn pyelftools. Pass --toolchain for ABI-offset probe.
"""
import pathlib, struct, subprocess, sys, argparse, json, zlib, hashlib
from unicorn import Uc, UC_ARCH_ARM, UC_MODE_THUMB, UC_MODE_MCLASS, UC_HOOK_CODE
from unicorn.arm_const import UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2,UC_ARM_REG_R3,UC_ARM_REG_SP,UC_ARM_REG_LR,UC_ARM_REG_PC
from elftools.elf.elffile import ELFFile
root=pathlib.Path(__file__).resolve().parent
app=root/'App/apps/fm'
p=argparse.ArgumentParser();p.add_argument('--toolchain',required=True);args=p.parse_args()
tc=pathlib.Path(args.toolchain).resolve()
names=['display_clear','status_clear','draw_battery','print_string','print_normal','display_freq','blit_status','blit_full','backlight_on','backlight_update','get_key','delay_ms','fm_enter','fm_exit','fm_set_freq','fm_mute','fm_valid','fm_state','fm_commit','nav_dir','asset_read','fm_channels','status_line']
probe=app/'abi_probe.c'
probe.write_text('#include <stddef.h>\n#include "../app_api.h"\nconst unsigned offsets[] = {'+','.join('offsetof(app_api_t,'+n+')' for n in names)+'};\n')
subprocess.run([str(tc/'arm-none-eabi-gcc'),'-mcpu=cortex-m0plus','-mthumb','-c',str(probe),'-o',str(app/'abi_probe.o')],check=True)
with (app/'abi_probe.o').open('rb') as f:
    elf=ELFFile(f); offsets=struct.unpack('<'+'I'*len(names),elf.get_section_by_name('.rodata').data())
offsets=dict(zip(names,offsets))
blob=(app/'WRCX212FM-USA.app').read_bytes()
h=struct.unpack('<4sHBBIIHH16s16sIIHH',blob[:64])
magic,hver,abi,api,size,crc,entry,flags,name,ver,vma,caps,asz,acrc=h
assert (magic,hver,abi,api,vma,caps,entry)==(b'FAP1',1,1,2,0x20000280,1,0)
assert flags==0x103 and 2<=size<=4096 and asz<=3840
assert len(blob)==64+size+asz
code=blob[64:64+size];assets=blob[64+size:]
assert zlib.crc32(code)==crc and zlib.crc32(assets)&65535==acrc
with (app/'fm.elf').open('rb') as f:
    elf=ELFFile(f);symbols={s.name:s['st_value'] for s in elf.get_section_by_name('.symtab').iter_symbols()}
    assert symbols['app_main']&~1==vma and symbols['__app_end']<=vma+4096
    assert not [s.name for s in elf.get_section_by_name('.symtab').iter_symbols() if s.name and s['st_shndx']=='SHN_UNDEF']

def press(*keys):
    return [v for k in keys for v in (k,19)]
def digits(f): return press(*map(int,str(f)))
class Radio:
    def __init__(self,keys=(),freq=881,band=0,mr=0,ch=0,channels=None,stations=(),nav=1):
        self.u=Uc(UC_ARCH_ARM,UC_MODE_THUMB|UC_MODE_MCLASS)
        self.u.mem_map(0x20000000,0x4000); self.u.mem_map(0x08000000,0x2000)
        self.u.mem_write(vma,code)
        self.api=0x20002000;self.channels=0x20002400
        self.u.mem_write(self.channels,struct.pack('<48H',*(channels or [65535]*48)))
        self.initial=struct.pack('<HHBBB',freq,freq,band,mr,ch)+b'\0'
        self.keys=iter(keys);self.stations=set(stations);self.nav=nav
        self.tunes=[];self.checked=[];self.text=[];self.saved=None;self.commits=0;self.exits=0;self.muted=False;self.frames=0;self.minsp=0x20004000
        self.callbacks={}
        for i,n in enumerate(names):
            addr=0x08000000+4*i
            self.callbacks[addr]=n
            self.u.mem_write(addr,b'\x70\x47') # bx lr
            value=self.channels if n=='fm_channels' else 0x20002500 if n=='status_line' else addr|1
            self.u.mem_write(self.api+offsets[n],struct.pack('<I',value))
        self.u.mem_write(self.api,struct.pack('<BBH',1,2,512))
        self.u.hook_add(UC_HOOK_CODE,self.hook)
    def hook(self,u,address,size,_):
        self.minsp=min(self.minsp,u.reg_read(UC_ARM_REG_SP))
        if address==0x08001000: u.emu_stop();return
        n=self.callbacks.get(address)
        if not n:return
        a,b,c=[u.reg_read(r) for r in (UC_ARM_REG_R0,UC_ARM_REG_R1,UC_ARM_REG_R2)]
        ret=0
        if n=='get_key':
            self.frames+=1
            if self.frames>3000:raise AssertionError('app failed to exit')
            ret=next(self.keys,19)
        elif n in ('fm_enter','fm_set_freq'):
            assert 881<=a<=1079 and a%2==1 and b==0,(n,a,b)
            self.tunes.append(a)
        elif n=='fm_valid':
            assert b==881
            self.checked.append(a);ret=0 if a in self.stations else 0xffffffff
        elif n=='fm_state':
            if b:self.saved=struct.unpack('<HHBBB',bytes(u.mem_read(a,7)))
            else:u.mem_write(a,self.initial)
        elif n=='fm_commit':self.commits+=1
        elif n=='fm_exit':self.exits+=1
        elif n=='fm_mute':self.muted=bool(a)
        elif n=='nav_dir':ret=(self.nav if a==11 else -self.nav)&0xffffffff
        elif n=='asset_read':
            data=assets[a:a+c];u.mem_write(b,data);ret=len(data)
        elif n in ('print_normal','print_string','display_freq'):
            self.text.append(bytes(u.mem_read(a,32)).split(b'\0')[0].decode('ascii'))
        u.reg_write(UC_ARM_REG_R0,ret)
    def run(self):
        self.u.reg_write(UC_ARM_REG_SP,0x20003ff0)
        self.u.reg_write(UC_ARM_REG_R0,self.api)
        self.u.reg_write(UC_ARM_REG_LR,0x08001001)
        self.u.emu_start(vma|1,0x08001000,count=3000000)
        assert self.saved is not None and self.commits==1 and self.exits==1
        self.ch=list(struct.unpack('<48H',self.u.mem_read(self.channels,96)))
        return self
results=[]
def ok(s):results.append(s);print('PASS:',s)

for f in [0,640,760,875,880,881,882,1000,1078,1079,1080,65535]:
    r=Radio(press(13),freq=f,band=3).run()
    expected=max(881,min(1079,f));expected-=not(expected%2)
    assert r.saved[:3]==(expected,expected,0)
ok('Imported VFO clamp/parity and hardware band 0')
r=Radio(press(*([11]*100),13)).run()
assert r.tunes==[881]+list(range(883,1080,2))+[881]
r=Radio(press(*([12]*100),13)).run()
assert r.tunes==[881]+list(range(1079,880,-2))
ok('All 100 channels traversed both ways, including both wrap boundaries')
r=Radio(press(11,12,13),nav=-1).run();assert r.tunes==[881,1079,881]
ok('UV-K1 reversed physical navigation mapping')
for f in range(870,1091):
    r=Radio(digits(f)+press(13)).run()
    expected=f if 881<=f<=1079 and f%2 else 881
    assert r.saved[0]==expected,(f,r.saved)
ok('221 direct-entry frequencies: accept only odd tenths 88.1-107.9')
r=Radio(press(15,1,11,13)).run();assert r.saved[0]==883
ok('F+1 cannot leave USA plan')
channels=[880,881,882,1079,1080]+[65535]*43
r=Radio(press(11,11,12,13),mr=1,channels=channels).run()
assert r.tunes==[881,1079,881,1079] and r.ch==channels
ok('MR skips invalid presets, retains 107.9, wraps, preserves all slots')
r=Radio(press(13),mr=1,channels=[880]*48).run();assert r.saved[3]==0 and r.ch==[880]*48
ok('No usable presets falls back to VFO without erasing presets')
r=Radio(digits(1079)+press(10)+press(4,8)+press(10,15,3,13)).run()
assert r.ch[47]==1079 and r.saved[0]==1079 and r.saved[3]==1
ok('Save preset 48 and recall in MR')
r=Radio(press(10,10,13),mr=1,channels=channels).run();assert r.ch[1]==65535 and r.saved[0]==1079
ok('Delete preset advances to next valid station')
r=Radio(press(14)+[19]*8+press(13),freq=1079,stations=[883]).run()
assert r.checked==[881,883] and r.saved[0]==883
ok('Manual seek wraps and stops at mocked station')
r=Radio(press(14,12)+[19]*6+press(13),stations=[1079]).run()
assert r.saved[0]==1079
ok('Manual seek direction reversal')
for stations in [[],[881,1079],list(range(881,1080,2))]:
    r=Radio(press(15,14)+[19]*210+press(13),stations=stations).run()
    expected=stations[:48]
    assert r.ch==expected+[65535]*(48-len(expected))
    assert r.checked==list(range(881,881+2*(48 if len(stations)>48 else 100),2))
    assert not r.muted
ok('Auto-scan: empty band, both endpoints, 48-station limit, unmute and terminate')
r=Radio(press(15,14,12)+[19]*6+press(13)).run();assert len(r.checked)<10 and not r.muted
ok('Auto-scan direction reversal terminates at lower boundary')
r=Radio(press(14,14,13)).run();assert not r.muted
ok('Scan cancellation unmutes')
r=Radio([253,253,254]+press(13)).run();assert 'FM USA' in r.text and '88.1-107.9' in r.text
ok('Screen saver/wake and USA screen assets')
report={'target':'F4HWN Labs v6.2.0','upstream_commit':'0ade03c0b1c94514566fadf7ce1c7a1f542a2283','sha256':hashlib.sha256(blob).hexdigest(),'file_bytes':len(blob),'overlay_bytes':size,'asset_bytes':asz,'ABI':abi,'API_min':api,'VMA':hex(vma),'required_caps':caps,'tests':results,'limitations':'Thumb binary executed with mocked firmware services; no physical radio, RF, audio, actual EEPROM persistence or UV Studio transfer tested.'}
(root/'VALIDATION.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
