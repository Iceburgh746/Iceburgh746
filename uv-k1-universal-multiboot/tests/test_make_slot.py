import importlib.util
import struct
import unittest
from pathlib import Path

MODULE = Path(__file__).resolve().parents[1] / "tools" / "make_slot.py"
spec = importlib.util.spec_from_file_location("make_slot", MODULE)
make_slot = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(make_slot)


class SlotTests(unittest.TestCase):
    def make_image(self, size=1024):
        data = bytearray(b"\xFF" * size)
        struct.pack_into("<II", data, 0, 0x20004000, make_slot.APP_BASE + 0x101)
        return data

    def test_valid_foreign_image_roundtrip(self):
        image = bytes(self.make_image())
        info = make_slot.inspect_image(image)
        self.assertTrue(info["compatible"], info["errors"])
        package = make_slot.build_slot(image, "TEST", "v0", "foreign")
        self.assertEqual(len(package), make_slot.SLOT_SIZE)
        parsed = make_slot.parse_slot(package)
        self.assertTrue(parsed["crc_ok"])
        self.assertTrue(parsed["foreign"])
        self.assertEqual(parsed["name"], "TEST")
        self.assertTrue(parsed["inspection"]["compatible"])

    def test_wrong_application_base_is_rejected(self):
        image = self.make_image()
        struct.pack_into("<I", image, 4, 0x08000001)
        info = make_slot.inspect_image(bytes(image))
        self.assertFalse(info["compatible"])
        self.assertTrue(any("reset handler" in msg for msg in info["errors"]))

    def test_non_thumb_reset_is_rejected(self):
        image = self.make_image()
        struct.pack_into("<I", image, 4, make_slot.APP_BASE + 0x100)
        info = make_slot.inspect_image(bytes(image))
        self.assertFalse(info["compatible"])
        self.assertTrue(any("Thumb" in msg for msg in info["errors"]))

    def test_wrong_sram_stack_is_rejected(self):
        image = self.make_image()
        struct.pack_into("<I", image, 0, 0x20010000)
        info = make_slot.inspect_image(bytes(image))
        self.assertFalse(info["compatible"])
        self.assertTrue(any("SRAM" in msg for msg in info["errors"]))

    def test_oversize_is_rejected(self):
        image = b"\xFF" * (make_slot.APP_SIZE + 1)
        with self.assertRaises(make_slot.ImageError):
            make_slot.inspect_image(image)


if __name__ == "__main__":
    unittest.main()
