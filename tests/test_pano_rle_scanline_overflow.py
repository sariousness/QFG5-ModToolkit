import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1] / "src"


class PanoRleScanlineOverflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.converter = (ROOT / "Resources" / "Panorama" / "PanoConverter.cs").read_text()

    def test_runs_crossing_scanline_edges_are_consumed_and_clipped(self):
        unpack_bits = self.converter[
            self.converter.index("static byte[] unpackBits"):
            self.converter.rindex("\n    }")
        ]

        self.assertIn("int pixelsToWrite = Math.Min(runLength, bitmapWidth - column);", unpack_bits)
        self.assertIn("pixelData[row * bitmapWidth + column + j] = value;", unpack_bits)
        self.assertIn("if (j < pixelsToWrite)", unpack_bits)
        self.assertIn("column += pixelsToWrite;", unpack_bits)
        self.assertNotIn("column + runLength > bitmapWidth", unpack_bits)


if __name__ == "__main__":
    unittest.main()
