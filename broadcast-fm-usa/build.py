#!/usr/bin/env python3
"""Reproduce the ARM overlay using upstream flags and packer; no firmware flash."""
import argparse, pathlib, subprocess, sys, os
p=argparse.ArgumentParser()
p.add_argument('--toolchain', help='Directory containing arm-none-eabi-gcc/objcopy; defaults to PATH')
a=p.parse_args()
root=pathlib.Path(__file__).resolve().parent
app=root/'App/apps/fm'
def run(args):
    print(' '.join(map(str,args)), flush=True)
    subprocess.run(list(map(str,args)),cwd=app,check=True,env={**os.environ,'PYTHONUTF8':'1'})
def tool(n): return str(pathlib.Path(a.toolchain).resolve()/('arm-none-eabi-'+n)) if a.toolchain else 'arm-none-eabi-'+n
run([sys.executable,'gen_assets.py','fm_assets.bin','fm_assets.h'])
run([tool('gcc'),'-mcpu=cortex-m0plus','-mthumb','-Os','-std=gnu11','-ffreestanding','-fno-builtin','-fno-common','-fomit-frame-pointer','-ffunction-sections','-fdata-sections','-Wall','-Wextra','-Werror','-fstack-usage','-nostdlib','-nostartfiles','-T','app.ld','-Wl,--defsym,APP_VMA=0x20000280','-Wl,--gc-sections','-Wl,-Map=fm.map','-Wl,--build-id=none','-Wl,--no-warn-rwx-segments','fm_app.c','-lgcc','-o','fm.elf'])
run([tool('objcopy'),'-O','binary','fm.elf','fm.bin'])
run([sys.executable,'../pack_app.py','fm.bin','WRCX212FM-USA.app','--name','WRCX212FM-USA','--ver','1.1-USA','--api-min','2','--vma','0x20000280','--shortcut','fm','--require','fm','--screensaver','--assets','fm_assets.bin'])
run([tool('size'),'fm.elf'])
