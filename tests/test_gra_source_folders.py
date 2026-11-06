import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1] / "src"


class GraSourceFolderTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.gra_tab = (ROOT / "Resources" / "Gra" / "GraTab.cs").read_text()
        cls.converter = (ROOT / "Resources" / "Gra" / "GraConverter.cs").read_text()
        cls.cm_defaults = (ROOT / "Resources" / "Gra" / "gra-column-major-defaults.txt").read_text().splitlines()
        cls.project = (ROOT / "QFG5Extractor.csproj").read_text()
        cls.model_bitmap = (ROOT / "Resources" / "Model" / "qfg5modelBitmap.cs").read_text()
        cls.model_shared = (ROOT / "Resources" / "Model" / "qfg5modelShared.cs").read_text()

    def test_gra_tab_scans_both_game_data_folders(self):
        self.assertIn('SharedState.GetAssetDataPath("cdn", "GRA"), "cdn/GRA"', self.gra_tab)
        self.assertIn('SharedState.GetAssetDataPath("hdn", "gra"), "hdn/gra"', self.gra_tab)
        self.assertIn('gridFiles.Columns.Add("Source", "Folder")', self.gra_tab)
        self.assertIn("gridFiles.Rows[rowIndex].Tag = f;", self.gra_tab)

    def test_preview_edit_and_export_use_selected_source_path(self):
        self.assertIn("private GraFile GetSelectedGraFile()", self.gra_tab)
        self.assertIn("gridFiles.SelectedRows[0].Tag as GraFile", self.gra_tab)
        self.assertIn("string path = selectedFile.FullPath;", self.gra_tab)
        self.assertIn("string graPath = GetSelectedGraPath();", self.gra_tab)
        self.assertIn("string fullPath = GetGraFilePath(row);", self.gra_tab)

    def test_extracted_files_follow_their_source_folder(self):
        self.assertIn("Path.Combine(SharedState.ExtractionPath, file.RelativeFolder)", self.gra_tab)
        self.assertIn("GetGraOutputDirectory(row)", self.gra_tab)
        self.assertIn("Output folder(s):", self.gra_tab)

    def test_list_and_thumbnail_views_share_selection_and_favorite_filter(self):
        self.assertIn('btnThumbnailView = CreateGraToolbarButton("Thumbs", 62)', self.gra_tab)
        self.assertIn("private static Button CreateGraToolbarButton", self.gra_tab)
        self.assertIn("FlatStyle = FlatStyle.Flat", self.gra_tab)
        self.assertIn('CreateGraToolbarButton("☆", 34)', self.gra_tab)
        self.assertIn('CreateGraToolbarButton("↻", 34)', self.gra_tab)
        self.assertIn("Panel listContentHost = new Panel", self.gra_tab)
        self.assertNotIn('Name = "Favorite"', self.gra_tab)
        self.assertNotIn('Name = "Restore"', self.gra_tab)
        self.assertIn("Alignment = ListViewAlignment.Top", self.gra_tab)
        self.assertIn("MultiSelect = true", self.gra_tab)
        self.assertIn('Text = "CM"', self.gra_tab)
        self.assertIn("ApplyColumnMajorToSelection()", self.gra_tab)
        self.assertIn("thumbnailView.SelectedItems.Cast<ListViewItem>()", self.gra_tab)
        self.assertIn('CreateGraToolbarButton("Save CM List", 94)', self.gra_tab)
        self.assertIn('"gra-column-major.txt"', self.gra_tab)
        self.assertIn("SaveColumnMajorGraFiles()", self.gra_tab)
        self.assertIn("hasSavedColumnMajorList", self.gra_tab)
        self.assertIn("thumbnailView.SelectedIndexChanged", self.gra_tab)
        self.assertIn("row.Selected = true;", self.gra_tab)
        self.assertIn("chkFavoritesOnly.CheckedChanged += (s, e) => ApplyGraFavoriteFilter();", self.gra_tab)
        self.assertIn('Environment.SpecialFolder.LocalApplicationData', self.gra_tab)
        self.assertIn('"gra-favorites.txt"', self.gra_tab)

    def test_thumbnail_decoding_is_incremental_and_protected_from_reentry(self):
        self.assertIn("thumbnailLoadTimer.Tick += ThumbnailLoadTimer_Tick;", self.gra_tab)
        self.assertIn("if (thumbnailImagesLoaded || thumbnailLoadInProgress) return;", self.gra_tab)
        self.assertIn("int batchEnd = Math.Min(thumbnailLoadIndex + 8, thumbnailView.Items.Count)", self.gra_tab)
        self.assertNotIn("Application.DoEvents();", self.gra_tab[
            self.gra_tab.index("private void LoadThumbnailImages()"):
            self.gra_tab.index("private void ClearThumbnailImageCache()")
        ])

    def test_batch_export_buttons_have_room_for_their_labels(self):
        self.assertIn("RowStyle(SizeType.Absolute, 40)); // Batch Selection Bar", self.gra_tab)
        self.assertEqual(self.gra_tab.count('Text = "PNG All", Width = 100, Height = 34'), 1)
        self.assertEqual(self.gra_tab.count('Text = "PNG None", Width = 100, Height = 34'), 1)
        self.assertEqual(self.gra_tab.count('Text = "MP4 All", Width = 100, Height = 34'), 1)
        self.assertEqual(self.gra_tab.count('Text = "MP4 None", Width = 100, Height = 34'), 1)
        self.assertIn('Text = "(Shift+Click toggles selected)"', self.gra_tab)

    def test_reviewed_cm_list_is_embedded_and_used_as_the_default(self):
        self.assertIn(
            '<EmbeddedResource Include="Resources\\Gra\\gra-column-major-defaults.txt" />',
            self.project,
        )
        self.assertIn(
            '"QFG5Extractor.Resources.Gra.gra-column-major-defaults.txt"',
            self.gra_tab,
        )
        self.assertIn("GetManifestResourceStream(BuiltInColumnMajorResourceName)", self.gra_tab)
        self.assertGreater(len(self.cm_defaults), 0)
        self.assertEqual(len(self.cm_defaults), len(set(path.casefold() for path in self.cm_defaults)))
        for path in self.cm_defaults:
            self.assertRegex(path, r"^(cdn/GRA|hdn/gra)/[0-9]+\.GRA$")

    def test_original_gra_restore_uses_persistent_backup(self):
        self.assertIn('".qfg5extractor.bak"', self.gra_tab)
        self.assertIn("RestoreOriginalGra(file)", self.gra_tab)
        self.assertIn("File.Replace(replacementPath, file.FullPath, null)", self.gra_tab)

    def test_auto_cm_uses_flag_or_clear_decode_quality_difference(self):
        auto_cm_body = self.gra_tab[
            self.gra_tab.index("private void AutoDetectColumnMajor()"):
            self.gra_tab.index("private void AnimTimer_Tick")
        ]
        self.assertIn("columnMajorGraFiles.Contains(GetColumnMajorKey(file))", auto_cm_body)
        self.assertNotIn("TrySuggestColumnMajor", auto_cm_body)
        self.assertIn("rowScore = GetImageContinuityScore(rowMajor)", self.converter)
        self.assertIn("columnScore = GetImageContinuityScore(columnMajor)", self.converter)
        self.assertIn("difference < 0.10 || difference < Math.Min(rowScore, columnScore) * 0.10", self.converter)
        self.assertIn("(flags & 1) != 0", self.converter)
        self.assertIn("Applied the verified CM list to", auto_cm_body)

    def test_mdl_bmp_export_uses_same_display_palette_range_as_viewer(self):
        self.assertIn("MDL_DISPLAY_PALETTE_ADDRESS = MDL_HEADER_BITMAPPALETTE_ADDRESS - BYTES_PER_INT",
                      self.model_shared)
        self.assertIn("BITMAP_DISPLAY_PALETTE_LENGTH = BITMAP_PALETTE_LENGTH + BYTES_PER_INT",
                      self.model_shared)
        export_body = self.model_bitmap[
            self.model_bitmap.index("public static void exportBMPfile"):
            self.model_bitmap.index("private static void writeBMP")
        ]
        self.assertIn("Constants.MDL_DISPLAY_PALETTE_ADDRESS", export_body)
        self.assertIn("Constants.BITMAP_DISPLAY_PALETTE_LENGTH", export_body)


if __name__ == "__main__":
    unittest.main()
