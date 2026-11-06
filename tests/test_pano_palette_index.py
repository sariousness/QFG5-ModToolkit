import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1] / "src"


class PanoPaletteIndexTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.converter = (ROOT / "Resources" / "Panorama" / "PanoConverter.cs").read_text()

    def test_img_palette_indices_are_not_shifted_by_nod_header_bytes(self):
        self.assertNotIn("paletteBytes[6]", self.converter)
        self.assertNotIn("nodBytes[6]", self.converter)
        self.assertIn("static byte[] unpackBits(ref int bitmapWidth, ref int bitmapHeight, ref byte[] pixelBytes)", self.converter)
        self.assertIn("byte value = pixelBytes[i++];", self.converter)
        self.assertIn("ms.WriteByte(pixelData[i]);", self.converter)
        self.assertIn("ms.WriteByte(pixelData[i + k]);", self.converter)


if __name__ == "__main__":
    unittest.main()
