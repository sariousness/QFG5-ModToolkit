import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src" / "Core" / "TextureEditorTab.cs"


class GifPropTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.editor = SOURCE.read_text()
        cls.readme = (ROOT / "README.md").read_text()
        cls.user_guide = (ROOT / "USER_GUIDE.md").read_text()

    def test_png_props_remain_and_gif_addition_is_available(self):
        self.assertIn('Filter = "PNG images|*.png"', self.editor)
        self.assertIn('Filter = "GIF images|*.gif"', self.editor)
        self.assertIn('MakeHeaderBtn("Add GIF"', self.editor)
        self.assertIn("AddCustomGifProp()", self.editor)

    def test_gif_frames_and_delays_drive_the_animated_preview(self):
        self.assertIn("image.GetFrameCount(FrameDimension.Time)", self.editor)
        self.assertIn("image.SelectActiveFrame(FrameDimension.Time, frameIndex)", self.editor)
        self.assertIn("ReadGifFrameDelays(image, frameCount)", self.editor)
        self.assertIn("image.GetPropertyItem(0x5100)", self.editor)
        self.assertIn("propAnimationTimer.Start()", self.editor)
        self.assertIn("PropAnimationTimer_Tick", self.editor)
        self.assertIn("selectedAnimatedProp.FrameDelays[selectedAnimatedPropFrame]", self.editor)

    def test_normal_placement_uses_preview_and_shift_mapping_supports_cycle_and_span(self):
        apply = self.editor[
            self.editor.index("private void ApplySelectedProp("):
            self.editor.index("private int GetAnimatedPropFrameForGra(")
        ]
        mapping = self.editor[
            self.editor.index("private int GetAnimatedPropFrameForGra("):
            self.editor.index("private void CancelPropPlacement(")
        ]
        self.assertIn("DrawPropSticker(currentBitmap, imageX, imageY", apply)
        self.assertIn("GetAnimatedPropFrameForGra(frameIndex, loadedGraFrames.Count)", apply)
        self.assertIn("DrawPropSticker(frame, frameX, frameY, frameProp", apply)
        self.assertIn('== "Span"', mapping)
        self.assertIn("graFrameIndex", mapping)
        self.assertIn("graFrameIndex % selectedAnimatedProp.Frames.Count", mapping)

    def test_gif_frame_resources_are_released_with_the_gallery(self):
        self.assertIn("DisposeAnimatedProp(customProp)", self.editor)
        self.assertIn("button.Tag as AnimatedProp", self.editor)
        self.assertIn("DisposeAnimatedProp(animatedProp)", self.editor)

    def test_gra_prop_transform_is_stored_per_frame(self):
        self.assertIn("class PropFrameState", self.editor)
        self.assertIn("graPropFrameStates", self.editor)
        self.assertIn("StorePropFrameState(loadedGraFrameIndex)", self.editor)
        self.assertIn("RestorePropFrameState(frameIndex)", self.editor)
        self.assertIn("previewState.Scale", self.editor)

    def test_gif_prop_workflow_is_documented(self):
        self.assertIn("animated GIF props", self.readme)
        self.assertIn("Custom transparent PNGs and animated GIFs", self.user_guide)
        self.assertIn("Cycle", self.user_guide)
        self.assertIn("Span", self.user_guide)
        self.assertIn("per frame", self.user_guide)


if __name__ == "__main__":
    unittest.main()
