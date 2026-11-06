import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1] / "src"


class PanoNodCaseHandlingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.pano_tab = (ROOT / "Resources" / "Panorama" / "PanoTab.cs").read_text()

    def test_extraction_includes_nod_files_regardless_of_extension_case(self):
        self.assertIn('string[] allNodFiles = GetFilesByExtension(nodFolder, ".nod");', self.pano_tab)
        self.assertNotIn('Directory.GetFiles(nodFolder, "*.nod")', self.pano_tab)
        self.assertNotIn('Directory.GetFiles(nodFolder, "*.NOD")', self.pano_tab)

        extension_filter = self.pano_tab[
            self.pano_tab.index("private string[] GetFilesByExtension"):
            self.pano_tab.index("private void LoadOutputFiles")
        ]
        self.assertIn("Path.GetExtension(file)", extension_filter)
        self.assertIn("StringComparison.OrdinalIgnoreCase", extension_filter)


if __name__ == "__main__":
    unittest.main()
