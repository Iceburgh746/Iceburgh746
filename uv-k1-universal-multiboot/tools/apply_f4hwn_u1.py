#!/usr/bin/env python3
from pathlib import Path
import sys


def replace_once(path: Path, old: str, new: str):
    text = path.read_text()
    if old not in text:
        raise SystemExit(f"anchor not found in {path}: {old[:80]!r}")
    if text.count(old) != 1:
        raise SystemExit(f"anchor not unique in {path}")
    path.write_text(text.replace(old, new, 1))


def main(root: Path):
    h = root / "App/driver/mb_flash.h"
    c = root / "App/driver/mb_flash.c"
    ui = root / "App/ui/multiboot.c"

    replace_once(
        h,
        '#define MB_FLAG_COMMITTED   (1u << 0)     /* image written and verified */\n',
        '#define MB_FLAG_COMMITTED   (1u << 0)     /* image written and verified */\n'
        '#define MB_FLAG_FOREIGN     (1u << 1)     /* non-F4HWN PY32F071 application */\n'
        '#define MB_FLAG_RETURN_PATCHED (1u << 2)  /* informational: verified return hook */\n'
    )
    replace_once(
        h,
        '    MB_ERR_AUTH,         /* write refused: timestamp mismatch */\n'
        '    MB_ERR_RAM_LOAD      /* restore stub RAM copy mismatch    */\n',
        '    MB_ERR_AUTH,         /* write refused: timestamp mismatch */\n'
        '    MB_ERR_RAM_LOAD,     /* restore stub RAM copy mismatch    */\n'
        '    MB_ERR_VECTOR,       /* foreign image has invalid vectors */\n'
        '    MB_ERR_TARGET        /* foreign reset target incompatible */\n'
    )

    replace_once(
        c,
        '#define MB_FLASH_PAGE   256u\n',
        '#define MB_FLASH_PAGE   256u\n\n'
        '/* PY32F071xB SRAM geometry used for imported-image validation. */\n'
        '#define MB_SRAM_BASE    0x20000000u\n'
        '#define MB_SRAM_SIZE    0x00004000u\n'
    )

    marker = '''/* Validate one slot (header + image CRC-32), entirely via polled reads.\n * Never touches the internal flash. */\n'''
    injected = '''/* Validate the boot-critical ARM vector words of a foreign application.\n * This runs after CRC validation and before any internal-flash erase. */\nstatic uint8_t mb_validate_foreign_vectors(uint8_t slot,\n                                           const mb_slot_header_t *hdr)\n{\n    const uint32_t slotBase = MB_SLOT0_EXT_BASE +\n                              (uint32_t)slot * MB_SLOT_STRIDE;\n    uint32_t vectors[2];\n\n    mb_spi_err = 0;\n    mb_ext_read(slotBase + MB_SLOT_IMG_OFFSET,\n                (uint8_t *)vectors, sizeof(vectors));\n    if (mb_spi_err)\n        return MB_ERR_SPI;\n\n    const uint32_t initial_sp = vectors[0];\n    const uint32_t reset_vec  = vectors[1];\n    const uint32_t reset_addr = reset_vec & ~1u;\n    const uint32_t image_end  = MB_INT_APP_BASE + hdr->image_size;\n\n    if (initial_sp < MB_SRAM_BASE ||\n        initial_sp > MB_SRAM_BASE + MB_SRAM_SIZE ||\n        (initial_sp & 0x3u) != 0u)\n        return MB_ERR_VECTOR;\n\n    if ((reset_vec & 1u) == 0u)\n        return MB_ERR_VECTOR;\n\n    if (reset_addr < MB_INT_APP_BASE || reset_addr >= image_end)\n        return MB_ERR_TARGET;\n\n    return MB_OK;\n}\n\n'''+marker
    replace_once(c, marker, injected)

    replace_once(
        c,
        '    if (mb_spi_err)                               return MB_ERR_SPI;\n'
        '    if (crc != hdr->image_crc32)                  return MB_ERR_CRC;\n'
        '    return MB_OK;\n',
        '    if (mb_spi_err)                               return MB_ERR_SPI;\n'
        '    if (crc != hdr->image_crc32)                  return MB_ERR_CRC;\n\n'
        '    if (hdr->flags & MB_FLAG_FOREIGN)\n'
        '    {\n'
        '        err = mb_validate_foreign_vectors(slot, hdr);\n'
        '        if (err != MB_OK)\n'
        '            return err;\n'
        '    }\n\n'
        '    return MB_OK;\n'
    )

    replace_once(
        ui,
        '        case MB_ERR_RAM_LOAD:      return "RAM LOAD ERROR";\n',
        '        case MB_ERR_RAM_LOAD:      return "RAM LOAD ERROR";\n'
        '        case MB_ERR_VECTOR:        return "bad vectors";\n'
        '        case MB_ERR_TARGET:        return "wrong target";\n'
    )

    print("WRCX212 U1 patch applied successfully")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("usage: apply_f4hwn_u1.py PATH_TO_F4HWN_V6_SOURCE")
    main(Path(sys.argv[1]).resolve())
