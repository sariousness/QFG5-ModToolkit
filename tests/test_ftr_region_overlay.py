import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1] / "src"


class FtrRegionOverlayTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.converter = (ROOT / "Resources" / "Panorama" / "FtrConverter.cs").read_text()
        cls.studio = (ROOT / "Resources" / "Panorama" / "RoomStudioTab.cs").read_text()
        cls.project = (ROOT / "QFG5Extractor.csproj").read_text()

    def test_parser_reads_signed_region_headers_and_point_pairs_with_bounds_checks(self):
        self.assertIn("int regionCount = ReadInt16(data, 0);", self.converter)
        self.assertIn("short portalFlag = ReadInt16(data, offset + 4);", self.converter)
        self.assertIn("short pointCount = ReadInt16(data, offset + 6);", self.converter)
        self.assertIn("EnsureRange(data, offset, 8, \"region header\");", self.converter)
        self.assertIn("EnsureRange(data, offset, pointBytes, \"region points\");", self.converter)
        self.assertIn("if (offset != data.Length)", self.converter)

    def test_scenes_can_select_ftr_and_overlay_portal_candidate_regions(self):
        self.assertIn('SharedState.GetModsPath("cdn", "FTR")', self.studio)
        self.assertIn('SharedState.GetGameDataPath("cdn", "FTR")', self.studio)
        self.assertIn("file.RoomId == roomId", self.studio)
        self.assertIn("preferredResourceName", self.studio)
        self.assertIn("FtrConverter.Read(item.Path)", self.studio)
        self.assertIn("region.PortalFlag != 0", self.studio)
        self.assertIn("SetRegionOverlay(regions, showFtrOverlay.Checked, imageSize)", self.studio)
        self.assertIn("TryReadImageDimensions", self.studio)
        self.assertIn("regionCoordinateSize.Height - 1 - point.Y", self.studio)
        self.assertIn("return new PointF(point.X, point.Y);", self.studio)

    def test_img_dimensions_are_read_from_duplicate_little_endian_header(self):
        converter = (ROOT / "Resources" / "Panorama" / "PanoConverter.cs").read_text()
        self.assertIn("int decodedWidth = BitConverter.ToInt32(header, 32);", converter)
        self.assertIn("int decodedHeight = BitConverter.ToInt32(header, 36);", converter)
        self.assertIn("imageSize = new Size(decodedHeight, decodedWidth);", converter)

    def test_ftr_converter_is_included_in_the_project(self):
        self.assertIn('Compile Include="Resources\\Panorama\\FtrConverter.cs"', self.project)


if __name__ == "__main__":
    unittest.main()
