import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1] / "src"


def method_body(source, signature):
    start = source.index(signature)
    opening_brace = source.index("{", start)
    depth = 0
    for index in range(opening_brace, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[opening_brace + 1:index]
    raise AssertionError("Method body is not closed: " + signature)


class ModelAnimationOrderTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.viewer = (ROOT / "Viewers" / "Model" / "MdlViewer.cs").read_text()

    def test_model_files_and_animation_dropdown_are_sorted_ascending(self):
        animation_scan = method_body(self.viewer, "private void ScanAnimationsForModel(string mdlFileName)")
        model_load = method_body(self.viewer, "private void LoadFilesInFolder(string folderPath)")

        self.assertIn('string.Equals(Path.GetExtension(path), ".anm", StringComparison.OrdinalIgnoreCase)', animation_scan)
        self.assertIn(
            "StringComparer.OrdinalIgnoreCase.Compare(Path.GetFileName(left), Path.GetFileName(right))",
            animation_scan,
        )
        self.assertIn("foreach(var anm in foundAnms)", animation_scan)
        self.assertIn("Array.Sort(files, StringComparer.OrdinalIgnoreCase)", model_load)
        self.assertIn("foreach (var f in files)", model_load)

    def test_animation_compatibility_warning_keeps_custom_animations_available(self):
        initialize = method_body(self.viewer, "private void InitializeComponent()")
        scan = method_body(self.viewer, "private void ScanAnimationsForModel(string mdlFileName)")
        apply = method_body(self.viewer, "private void ApplyAnimation(string filePath)")

        self.assertIn("matching bone counts do not guarantee compatibility", initialize)
        self.assertIn("Renamed or cross-model ANMs can scatter meshes", initialize)
        self.assertIn("cmbAnimations.ComboBox.Items.Add(name)", scan)
        self.assertIn("AnmAnimationLoader.LoadFrames(filePath, currentMeshes.Count)", apply)

    def test_animation_dropdown_shows_embedded_names_but_loads_the_original_path(self):
        scan = method_body(self.viewer, "private void ScanAnimationsForModel(string mdlFileName)")
        selection = method_body(self.viewer, "private void CmbAnimations_SelectedIndexChanged(object sender, EventArgs e)")

        self.assertIn("AnmAnimationLoader.ReadAnimationName(anm)", scan)
        self.assertIn('fileName + " - [" + animationName + "]"', scan)
        self.assertIn("cmbAnimations.ComboBox.Items.Add(name)", scan)
        self.assertIn("ApplyAnimation(availableAnimations[animationIndex])", selection)

    def test_animation_buttons_have_a_reserved_sidebar_row(self):
        initialize = method_body(self.viewer, "private void InitializeComponent()")

        self.assertIn("sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 156));", initialize)
        self.assertIn("animPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));", initialize)


if __name__ == "__main__":
    unittest.main()
