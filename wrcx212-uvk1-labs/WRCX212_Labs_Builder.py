#!/usr/bin/env python3
"""WRCX212 UV-K1 Labs 6.3: one-file Python builder.

Produces a RECEIVE-ONLY 22-channel FRS/GMRS VFO selector overlay.
The radio executes compiled ARM C (not Python). Python orchestrates the build.
Uses upstream F4HWN API3, linker, and pack_app.py. Never flashes the radio.
"""
from __future__ import annotations
import hashlib
import os
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import urllib.request
import zlib

HOME = Path.home()
ROOT = HOME / "Documents" / "WRCX212-UVK1-LABS"
REPO = ROOT / "upstream"
SRC = ROOT / "source"
OUT = ROOT / "Output"
LOG = ROOT / "build.log"
URL = "https://github.com/armel/uv-k1-k5v3-firmware-custom.git"
MODEL = "qwen2.5-coder:3b"  # Existing Ollama model is not required for deterministic build.
FREQS = [
    46256250,46258750,46261250,46263750,46266250,46268750,46271250,
    46756250,46758750,46761250,46763750,46766250,46768750,46771250,
    46255000,46257500,46260000,46262500,46265000,46267500,46270000,46272500,
]
C_SOURCE = r"""
#include <stdint.h>
#include <stdbool.h>
#include "../app_api.h"
/* Channels are in 10-Hz units, never MHz or kHz. */
static const uint32_t f[22] = {
  46256250u,46258750u,46261250u,46263750u,46266250u,46268750u,46271250u,
  46756250u,46758750u,46761250u,46763750u,46766250u,46768750u,46771250u,
  46255000u,46257500u,46260000u,46262500u,46265000u,46267500u,46270000u,46272500u
};
static void show(const app_api_t *a, unsigned ch, const char *message) {
  char num[4], *p;
  a->display_clear();
  a->status_clear();
  a->print_normal("WRCX212 22CH RX",2,0,0);
  a->print_normal("CHANNEL",2,0,2);
  p = a->append_u32(num, ch+1u);
  *p = '\0';
  a->print_normal(num,86,0,2);
  a->draw_frequency(f[ch],2,3);
  a->print_normal(message,2,0,6);
  a->blit_full();
}
__attribute__((section(".text.entry"),used))
void app_main(const app_api_t *a) {
  app_beam_channel_t vfo;
  uint8_t prior=APP_KEY_INVALID, key, config=0;
  unsigned ch=0;
  if (!a || a->abi_major!=APP_ABI_MAJOR || a->api_level<3 ||
      !a->beam_get || !a->vfo_stage || !a->nav_dir) {
    if (a) {
      a->display_clear();
      a->print_normal("API / VFO N/A",2,0,2);
      a->blit_full();
      a->delay_ms(1200);
    }
    return;
  }
  a->cfg_load(&config,1);
  if (config < 22u) ch=config;
  show(a,ch,"MENU=SET EXIT=BACK");
  for (;;) {
    key=a->get_key();
    if (key!=prior && key!=APP_KEY_INVALID) {
      int8_t dir=a->nav_dir(key);
      if (dir > 0) { ch=(ch+1u)%22u; show(a,ch,"MENU=SET EXIT=BACK"); }
      else if (dir < 0) { ch=(ch+21u)%22u; show(a,ch,"MENU=SET EXIT=BACK"); }
      else if (key==APP_KEY_EXIT) return;
      else if (key==APP_KEY_MENU) {
        a->beam_get(&vfo); /* Preserve every valid preset field from the radio. */
        vfo.rx_frequency=f[ch];
        vfo.tx_offset_direction=0;
        vfo.tx_offset_frequency=0;
        vfo.tx_lock=1; /* Never authorize transmission. */
        if (!a->vfo_stage(&vfo)) {
          show(a,ch,"VFO REJECTED");
          a->delay_ms(1200);
          show(a,ch,"MENU=SET EXIT=BACK");
        } else {
          config=(uint8_t)ch;
          a->cfg_save(&config,1);
          show(a,ch,"RX SET ON EXIT");
          a->delay_ms(500);
          return;
        }
      }
    }
    prior=key;
    a->backlight_update();
    a->delay_ms(40);
  }
}
"""

def log(message: str) -> None:
    print(message, flush=True)
    with LOG.open("a", encoding="utf-8") as fp:
        fp.write(message + "\n")

def run(args: list[str], cwd: Path | None = None) -> None:
    log("COMMAND: " + " ".join(map(str,args)))
    with subprocess.Popen(list(map(str,args)), cwd=cwd, stdout=subprocess.PIPE,
                          stderr=subprocess.STDOUT, text=True, errors="replace") as p:
        assert p.stdout
        for line in p.stdout:
            log(line.rstrip())
        rc=p.wait()
    if rc: raise RuntimeError(f"Command exited {rc}: {args[0]}")

def find(name: str) -> str | None:
    x=shutil.which(name)
    if x and Path(x).is_file(): return x
    roots = [Path(os.environ.get("ProgramFiles", "C:/Program Files")),
             Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")),
             HOME / "AppData" / "Local" / "Programs", Path("C:/Arm"), Path("C:/msys64")]
    for root in roots:
        if root.is_dir():
            for base,dirs,files in os.walk(root):
                if name.lower() in (f.lower() for f in files):
                    for f in files:
                        if f.lower()==name.lower(): return str(Path(base)/f)
                if "node_modules" in dirs: dirs.remove("node_modules")
    return None

def require(name: str, package: str) -> str:
    path=find(name)
    if path: return path
    winget=shutil.which("winget")
    if not winget: raise RuntimeError(f"Missing {name}; WinGet unavailable")
    log(f"Installing missing tool {package}")
    run([winget,"install","--id",package,"--exact",
         "--accept-package-agreements","--accept-source-agreements","--disable-interactivity"])
    path=find(name)
    if not path: raise RuntimeError(f"{name} still missing after install; see build.log")
    return path

def verify(path: Path, code: bytes) -> dict:
    data=path.read_bytes()
    if len(data)<64: raise RuntimeError("Short FAP1 image")
    (magic,hdv,abi,api,size,crc,entry,flags,name,ver,vma,caps,
     asize,acrc)=struct.unpack_from("<4sHBBIIHH16s16sIIHH",data)
    if magic!=b"FAP1" or abi!=1 or api!=3 or vma!=0x20000280:
        raise RuntimeError("Unexpected FAP1 ABI/API/VMA")
    if not(0<size<=4096) or size!=len(code) or data[64:64+size]!=code:
        raise RuntimeError("Code size/content mismatch")
    if len(data)!=64+size+asize or (zlib.crc32(code)&0xffffffff)!=crc:
        raise RuntimeError("Image length/CRC mismatch")
    assets=data[64+size:]
    if asize and (zlib.crc32(assets)&0xffff)!=acrc:
        raise RuntimeError("Asset CRC mismatch")
    return {"bytes":len(data),"code_bytes":size,
            "sha256":hashlib.sha256(data).hexdigest(),
            "abi":abi,"api_min":api,"vma":hex(vma)}

def main():
    for p in (ROOT,SRC,OUT): p.mkdir(parents=True,exist_ok=True)
    LOG.write_text("",encoding="utf-8")
    report=OUT/"BUILD-STATUS.txt"
    try:
        if sys.platform!="win32": raise RuntimeError("Windows builder; requires ARM Windows toolchain")
        git=require("git.exe","Git.Git")
        gcc=require("arm-none-eabi-gcc.exe","Arm.ArmGnuToolchain")
        objcopy=require("arm-none-eabi-objcopy.exe","Arm.ArmGnuToolchain")
        if not (REPO/"App"/"apps"/"app_api.h").is_file():
            run([git,"clone","--depth","1",URL,str(REPO)])
        # Never auto-update existing references: keep ABI stable within this run.
        api=(REPO/"App"/"apps"/"app_api.h").read_text(encoding="utf-8")
        if "APP_API_LEVEL  3u" not in api or "(*vfo_stage)" not in api:
            raise RuntimeError("Reference API is not verified API3; do not build")
        app=SRC/"gmrs"
        app.mkdir(exist_ok=True)
        source=app/"gmrs_app.c"
        source.write_text(C_SOURCE,encoding="utf-8")
        linker=REPO/"App"/"apps"/"foxhunt"/"app.ld"
        packer=REPO/"App"/"apps"/"pack_app.py"
        for path in (linker,packer):
            if not path.is_file(): raise RuntimeError(f"Missing upstream build file: {path}")
        elf=app/"gmrs.elf"
        binfile=app/"gmrs.bin"
        target=OUT/"WRCX212-22CH-RX-Labs63-BENCH.app"
        for path in (elf,binfile,target): path.unlink(missing_ok=True)
        run([gcc,"-mcpu=cortex-m0plus","-mthumb","-Os","-std=gnu11",
             "-ffreestanding","-fno-builtin","-fno-common","-fomit-frame-pointer",
             "-ffunction-sections","-fdata-sections","-Wall","-Wextra",
             "-nostdlib","-nostartfiles","-T",str(linker),
             "-Wl,--defsym,APP_VMA=0x20000280","-Wl,--gc-sections",
             "-Wl,--build-id=none","-Wl,--no-warn-rwx-segments",
             str(source),"-lgcc","-o",str(elf)],cwd=app)
        run([objcopy,"-O","binary",str(elf),str(binfile)])
        code=binfile.read_bytes()
        if not 0<len(code)<=4096: raise RuntimeError("Overlay exceeds 4096 bytes")
        run([sys.executable,str(packer),str(binfile),str(target),
             "--name","WRCX212 22CH RX","--ver","1.0",
             "--api-min","3","--vma","0x20000280","--shortcut","none"])
        info=verify(target,code)
        report.write_text(
            "BUILD AND PACKAGE CHECKS PASSED\n"+
            "\n".join(f"{k}: {v}" for k,v in info.items())+
            "\n\nRADIO TEST: NOT PERFORMED\n"+
            "BAD PRESET REPAIR OF PREVIOUS APP: NOT VERIFIED\n"+
            "FUNCTION: 22-channel RECEIVE VFO selector, applied after exit\n"+
            "LIVE SCANNING: NOT IMPLEMENTED\nTX: LOCKED IN STAGED VFO\n"+
            "Do not assume the radio is transmit-inhibited outside this app.\n",
            encoding="utf-8")
        log(f"VERIFIED BENCH APP: {target}")
    except Exception as exc:
        import traceback
        log("BUILD BLOCKED: "+str(exc))
        log(traceback.format_exc())
        report.write_text("BUILD BLOCKED\n"+str(exc)+"\nSee "+str(LOG)+"\n",encoding="utf-8")
    finally:
        log(f"REPORT: {report}")
        os.startfile(str(OUT))

if __name__=="__main__":
    main()
