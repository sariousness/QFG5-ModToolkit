import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1] / "src"


class ModelTextureUiTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.model_tab = (ROOT / "Resources" / "Model" / "ModelTab.cs").read_text()
        cls.texture_tab = (ROOT / "Core" / "TextureEditorTab.cs").read_text()

    def test_model_outputs_can_switch_between_list_and_bmp_thumbnails(self):
        self.assertIn('Text = "List"', self.model_tab)
        self.assertIn('Text = "Thumbs"', self.model_tab)
        self.assertIn("private void SetFileView(bool showThumbnails)", self.model_tab)
        self.assertIn("private void ThumbnailListView_SelectedIndexChanged", self.model_tab)
        self.assertIn("convertedListBox.SelectedIndex = index;", self.model_tab)
        self.assertIn('string.Equals(Path.GetExtension(file), ".bmp"', self.model_tab)
        self.assertIn("Thumbnails are available for BMP exports.", self.model_tab)

    def test_model_preview_and_thumbnails_do_not_lock_source_bitmaps(self):
        self.assertIn("using (Image source = Image.FromFile(filePath))", self.model_tab)
        self.assertIn("using (Bitmap thumbnailCopy = new Bitmap(thumbnail))", self.model_tab)
        self.assertIn("using (Image source = Image.FromFile(fullPath))", self.model_tab)
        self.assertIn("previewPictureBox.Image = new Bitmap(source);", self.model_tab)

    def test_texture_palette_is_a_scrollable_single_row_of_larger_swatches(self):
        self.assertIn("private FlowLayoutPanel paletteGrid;", self.texture_tab)
        self.assertIn("AutoScroll = true,", self.texture_tab)
        self.assertIn("WrapContents = false,", self.texture_tab)
        self.assertIn("Width = 26,", self.texture_tab)
        self.assertIn("Height = 52,", self.texture_tab)
        self.assertIn("List<Color> colors = CreateHighColorSamples();", self.texture_tab)
        self.assertIn("List<Color> colors = new List<Color>(96);", self.texture_tab)


if __name__ == "__main__":
    unittest.main()
