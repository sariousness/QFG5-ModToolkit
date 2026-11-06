import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src"


class AnmRetargetTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.retarget_tab = (SOURCE / "Resources" / "Model" / "AnmRetargetTab.cs").read_text()
        cls.animation_loader = (SOURCE / "Resources" / "Model" / "AnmAnimationLoader.cs").read_text()
        cls.preview_control = (SOURCE / "Resources" / "Save" / "CharacterModelPreviewControl.cs").read_text()
        cls.main_form = (SOURCE / "Core" / "MainForm.cs").read_text()
        cls.project = (SOURCE / "QFG5Extractor.csproj").read_text()

    def test_experimental_tab_is_registered_separately(self):
        self.assertIn("new UserControl[12]", self.main_form)
        self.assertIn("pages[10] = new AnmRetargetTab()", self.main_form)
        self.assertIn("pages[11] = new AudioTab()", self.main_form)
        self.assertIn("ANM Lab (Experimental)", self.main_form)
        self.assertIn("Audio Explorer", self.main_form)
        self.assertIn('Compile Include="Resources\\Model\\AnmRetargetTab.cs"', self.project)

    def test_mapping_supports_reused_tracks_and_static_recipient_parts(self):
        self.assertIn('private readonly string staticTrackLabel = "Static (pose ANM or identity)"', self.retarget_tab)
        self.assertIn("trackColumn.Items.AddRange(donorTrackLabels.ToArray())", self.retarget_tab)
        self.assertIn('mappingGrid.Rows.Add(recipientParts[i], selectedTrack, "0", "0", "0")', self.retarget_tab)
        self.assertIn("int source = mapping[target]", self.retarget_tab)
        self.assertIn("CopyTrack(donorBytes, sourceOffset, output, targetOffset)", self.retarget_tab)
        self.assertIn("CopyTrack(recipientBytes, sourceOffset, output, targetOffset)", self.retarget_tab)
        self.assertIn("CreateIdentityTrack()", self.retarget_tab)
        self.assertIn('HeaderText = "X deg"', self.retarget_tab)
        self.assertIn('HeaderText = "Y deg"', self.retarget_tab)
        self.assertIn('HeaderText = "Z deg"', self.retarget_tab)
        self.assertIn("ApplyOrientationOffset(output, targetOffset, offsets, target)", self.retarget_tab)
        self.assertIn("CreateEulerRotation(", self.retarget_tab)
        self.assertIn("Multiply(basis, CreateEulerRotation(", self.retarget_tab)
        self.assertIn("recipientParts.Count, mapping, offsets,", self.retarget_tab)
        self.assertIn("ApplyOrientationOffset(Matrix4 matrix, float[,] offsets, int target)", self.retarget_tab)
        self.assertIn("EstimateOrientationOffsets(", self.retarget_tab)
        self.assertIn("donorFrames.Length != recipientFrames.Length", self.retarget_tab)
        self.assertIn('Text = "Estimate offsets"', self.retarget_tab)
        self.assertIn("private void EstimateOrientationOffsets()", self.retarget_tab)
        self.assertIn("float[] angles = ExtractEulerAngles(delta)", self.retarget_tab)
        self.assertIn("float[] x = Normalize(new[] { basis[0, 0], basis[0, 1], basis[0, 2] })", self.retarget_tab)
        self.assertIn("A reference animation contains a reflected or degenerate orientation basis.", self.retarget_tab)
        self.assertIn("BuildPreviewFrames(", self.retarget_tab)
        self.assertIn("recipientPreview.SetAnimationFrames(frames)", self.retarget_tab)
        self.assertIn("mappingGrid.CellValueChanged += MappingGrid_CellValueChanged", self.retarget_tab)
        self.assertIn("private readonly Timer previewRefreshTimer = new Timer { Interval = 180 }", self.retarget_tab)
        self.assertIn("ScheduleRetargetPreview()", self.retarget_tab)
        self.assertIn("ReadCurrentMapping(out offsets, commitEdit: false)", self.retarget_tab)
        self.assertIn("private void PreviewRefreshTimer_Tick", self.retarget_tab)
        self.assertIn("Text = \"Transplant\"", self.retarget_tab)

    def test_part_name_matching_supports_common_terms_and_avoids_ambiguous_guesses(self):
        self.assertIn("FindMatchingDonorPart(recipientParts[i], donorParts)", self.retarget_tab)
        self.assertIn("GetPartTokens(recipientPart)", self.retarget_tab)
        self.assertIn("CanonicalizePartToken(tokenBuilder.ToString())", self.retarget_tab)
        self.assertIn('case "upperarm":', self.retarget_tab)
        self.assertIn('case "feet": return "foot";', self.retarget_tab)
        self.assertIn("recipientSide != 0 && donorSide != 0 && recipientSide != donorSide", self.retarget_tab)
        self.assertIn("return tiedForBest ? -1 : bestIndex", self.retarget_tab)
        self.assertIn("Exact or unique part-name matches are selected automatically; ambiguous parts stay static.", self.retarget_tab)

    def test_mapping_is_directly_editable_and_uses_application_buttons(self):
        self.assertIn("DataGridViewEditMode.EditOnEnter", self.retarget_tab)
        self.assertIn("mappingGrid.BeginEdit(true)", self.retarget_tab)
        self.assertIn("combo.DroppedDown = true", self.retarget_tab)
        self.assertIn("mappingGrid.DataError += MappingGrid_DataError", self.retarget_tab)
        self.assertIn("QFG5Button loadPartsButton = new QFG5Button", self.retarget_tab)
        self.assertIn("QFG5Button exportButton = new QFG5Button", self.retarget_tab)
        self.assertIn("QFG5Button refreshButton = new QFG5Button", self.retarget_tab)

    def test_asset_selection_uses_lists_from_configured_data_folders(self):
        self.assertIn("donorModelList = CreateAssetList()", self.retarget_tab)
        self.assertIn("donorAnimationList = CreateAssetList()", self.retarget_tab)
        self.assertIn("recipientModelList = CreateAssetList()", self.retarget_tab)
        self.assertIn("recipientPoseList = CreateAssetList()", self.retarget_tab)
        self.assertIn('SharedState.GetAssetDataPath(archiveFolder, assetFolder)', self.retarget_tab)
        self.assertIn('GetAssetFiles("hdnw", "mdl", ".mdl")', self.retarget_tab)
        self.assertIn('GetAssetFiles("hdnw", "anm", ".anm")', self.retarget_tab)
        self.assertIn("SharedState.OnGameDataExtracted += RefreshAssetLists", self.retarget_tab)
        self.assertIn('Text = "Refresh lists"', self.retarget_tab)

    def test_model_lists_show_embedded_names_and_preview_is_zoomed_in(self):
        self.assertIn("GetModelAssetFiles()", self.retarget_tab)
        self.assertIn("viewerLogic.getName(model.Path)", self.retarget_tab)
        self.assertIn('Path.GetFileName(model.Path) + " - [" + modelName + "]"', self.retarget_tab)
        self.assertIn("meshScale = size > 0.0001f ? 2.4f / size : 1.0f", self.preview_control)

    def test_animation_lists_filter_to_selected_model_prefix(self):
        self.assertIn("availableAnimations = GetAnimationAssetFiles()", self.retarget_tab)
        self.assertIn("AnmAnimationLoader.ReadAnimationName(animation.Path)", self.retarget_tab)
        self.assertIn('Path.GetFileName(animation.Path) + " - [" + animationName + "]"', self.retarget_tab)
        self.assertIn("donorModelList.SelectedIndexChanged += DonorModelSelectionChanged", self.retarget_tab)
        self.assertIn("recipientModelList.SelectedIndexChanged += RecipientModelSelectionChanged", self.retarget_tab)
        self.assertIn("Path.GetFileNameWithoutExtension(modelPath ?? string.Empty)", self.retarget_tab)
        self.assertIn(".StartsWith(modelName, StringComparison.OrdinalIgnoreCase)", self.retarget_tab)
        self.assertIn('new AssetListItem(string.Empty, "<None — identity pose>")', self.retarget_tab)
        self.assertIn("PopulateDonorAnimations(null, selectFirstWhenMissing: true)", self.retarget_tab)
        self.assertIn("CompareAnimationSequence(left, right, modelName)", self.retarget_tab)

    def test_animation_name_is_read_from_eight_byte_header_field(self):
        self.assertIn("public static string ReadAnimationName(string filePath)", self.animation_loader)
        self.assertIn("reader.BaseStream.Seek(0x08, SeekOrigin.Begin)", self.animation_loader)
        self.assertIn("reader.ReadBytes(8)", self.animation_loader)
        self.assertIn("Encoding.ASCII.GetString(nameBytes)", self.animation_loader)
        self.assertIn("name.IndexOf('\\0')", self.animation_loader)

    def test_export_creates_new_anm_with_optional_recipient_pose(self):
        self.assertIn('Text = "Export new ANM..."', self.retarget_tab)
        self.assertIn('Title = "Export a new recipient-sized ANM"', self.retarget_tab)
        self.assertIn('poses.Insert(0, new AssetListItem(string.Empty, "<None — identity pose>"))', self.retarget_tab)
        self.assertIn("GetSelectedPath(recipientPoseList)", self.retarget_tab)
        self.assertIn("recipientBytes = recipient == null ? null : File.ReadAllBytes(recipientAnimationPath)", self.retarget_tab)
        self.assertNotIn("retargetUsingReferences", self.retarget_tab)
        self.assertNotIn("donorReferenceAnimationPath", self.retarget_tab)

    def test_scale_preserving_mode_uses_reference_tracks_for_preview_and_export(self):
        self.assertIn('Text = "Preserve recipient scale / translation"', self.retarget_tab)
        self.assertIn("preserveRecipientScaleCheckBox.Checked = true", self.retarget_tab)
        self.assertIn("GetSelectedPath(donorReferenceList)", self.retarget_tab)
        self.assertIn("GetSelectedPath(recipientReferenceList)", self.retarget_tab)
        self.assertGreaterEqual(
            self.retarget_tab.count("preserveRecipientScaleCheckBox.Checked,"), 4)
        self.assertIn("BuildTrackReferences(donorReferenceFrames)", self.retarget_tab)
        self.assertIn("BuildTrackReferences(recipientReferenceFrames)", self.retarget_tab)
        self.assertIn("ApplyRecipientScaleRetarget(", self.retarget_tab)
        self.assertIn("recipientReference.X +", self.retarget_tab)
        self.assertIn("(donorFrame.M41 - donorReference.X) * translationScale", self.retarget_tab)
        self.assertIn("private static Matrix4 ReadTrackMatrix(", self.retarget_tab)
        self.assertIn("private static void WriteTrackMatrix(", self.retarget_tab)
        self.assertIn("TrackRecordLength = 56", self.retarget_tab)

        preview = self.retarget_tab[
            self.retarget_tab.index("public static Matrix4[][] BuildPreviewFrames("):
            self.retarget_tab.index("private static Matrix4[] CreateIdentityMatrices(")]
        export = self.retarget_tab[
            self.retarget_tab.index("public static void WriteAnimation("):
            self.retarget_tab.index("private static void CopyTrack(")]
        self.assertIn("ApplyRecipientScaleRetarget(", preview)
        self.assertIn("ApplyRecipientScaleRetarget(", export)
        self.assertIn("if (source >= 0 && preserveRecipientScale)", export)

    def test_scale_preserving_references_are_independent_of_target_clip_length(self):
        self.assertIn("AnmAnimationLoader.LoadFrames(donorReferencePath, donorTrackCount)", self.retarget_tab)
        self.assertIn("AnmAnimationLoader.LoadFrames(recipientReferencePath, recipientTrackCount)", self.retarget_tab)
        self.assertNotIn("donorReferenceFrames.Length != recipientReferenceFrames.Length", self.retarget_tab)
        self.assertIn("PopulateReferenceAnimations(donorReferenceList", self.retarget_tab)
        self.assertIn("PopulateReferenceAnimations(recipientReferenceList", self.retarget_tab)

    def test_transplant_all_uses_current_mapping_and_sequential_non_overwriting_names(self):
        self.assertIn('Text = "Transplant all"', self.retarget_tab)
        self.assertIn("private void TransplantSelectedAnimation()", self.retarget_tab)
        self.assertIn("GetAnimationsForModel(donorModelPath)", self.retarget_tab)
        self.assertIn("AnmRetargeter.ValidateAnimation(animation.Path, donorParts.Count)", self.retarget_tab)
        self.assertIn("AnmRetargeter.FindNextAnimationSequence(dialog.SelectedPath, recipientPrefix)", self.retarget_tab)
        self.assertIn('modelPrefix + sequence.ToString("D2") + ".ANM"', self.retarget_tab)
        self.assertIn("AnimationPathExistsIgnoreCase(path)", self.retarget_tab)
        self.assertIn("recipientParts.Count, mapping, offsets,", self.retarget_tab)
        self.assertIn("FileMode.CreateNew", self.retarget_tab)
        self.assertIn("int[] mapping = ReadCurrentMapping(out offsets)", self.retarget_tab)
        self.assertIn("private void TransplantSelectedAnimation()", self.retarget_tab)
        self.assertIn("string donorAnimationPath = GetSelectedPath(donorAnimationList)", self.retarget_tab)
        self.assertIn("donorModelPath, donorAnimationPath, recipientModelPath,", self.retarget_tab)
        self.assertIn("donorModelPath, animations[i].Path, recipientModelPath, recipientPosePath,", self.retarget_tab)

    def test_donor_and_recipient_have_independent_playable_previews(self):
        self.assertIn('new CharacterModelPreviewControl("DONOR PREVIEW", true)', self.retarget_tab)
        self.assertIn('new CharacterModelPreviewControl("RECIPIENT PREVIEW", true)', self.retarget_tab)
        self.assertIn("donorColumnSplit.Panel2.Controls.Add(donorPreviewSplit)", self.retarget_tab)
        self.assertIn("recipientPreviewSplit.Panel1.Controls.Add(recipientPreview)", self.retarget_tab)
        self.assertIn('CreateAssetSelector("DONOR"', self.retarget_tab)
        self.assertIn('CreateAssetSelector("RECIPIENT"', self.retarget_tab)
        self.assertIn("SplitContainer recipientPreviewSplit", self.retarget_tab)
        self.assertIn("SplitContainer donorPreviewSplit", self.retarget_tab)
        self.assertIn("workspaceSplit.Panel2Collapsed = !workspaceSplit.Panel2Collapsed", self.retarget_tab)
        self.assertIn('Text = "Hide parts"', self.retarget_tab)
        self.assertIn('togglePartsButton.Text = workspaceSplit.Panel2Collapsed ? "Show parts" : "Hide parts"', self.retarget_tab)
        self.assertIn("donorPreview.SetPreviewFiles(donorModelPath, donorAnimationPath)", self.retarget_tab)
        self.assertIn("recipientPreview.SetPreviewFiles(recipientModelPath, recipientAnimationPath)", self.retarget_tab)
        self.assertIn("recipientPreview.SetPreviewFiles(GetSelectedPath(recipientModelList), dialog.FileName)", self.retarget_tab)
        self.assertIn("animationFrame = (animationFrame + 1) % animationFrames.Length", self.preview_control)
        self.assertIn('spinButton = CreatePreviewButton("Spin Off", 56)', self.preview_control)
        self.assertIn("spinButton.Click += (s, e) => SetSpinning(!spinning)", self.preview_control)
        self.assertIn("spinning = !showPlaybackButton", self.preview_control)
        self.assertIn("GL.Translate(0.0f, 0.0f, -5.5f / zoom)", self.preview_control)
        self.assertIn("glControl.MouseWheel += GlControl_MouseWheel", self.preview_control)
        self.assertIn("glControl.MouseDown += GlControl_MouseDown", self.preview_control)
        self.assertIn("glControl.MouseMove += GlControl_MouseMove", self.preview_control)
        self.assertIn("glControl.MouseUp += GlControl_MouseUp", self.preview_control)
        self.assertIn("rotation = (rotation + deltaX * 0.7f) % 360.0f", self.preview_control)
        self.assertIn("dragRotationX = Math.Max(-85.0f", self.preview_control)
        self.assertIn("GL.Rotate(dragRotationX, 1.0f, 0.0f, 0.0f)", self.preview_control)
        self.assertIn("private void SetZoom(float value)", self.preview_control)
        self.assertIn("(spinning || animationPlaying)", self.preview_control)
        self.assertIn("public void SetHighlightedTrack(int trackIndex)", self.preview_control)
        self.assertIn("if (meshIndex == highlightedTrackIndex)", self.preview_control)
        self.assertIn("private static void DrawMeshHighlight(MeshData mesh)", self.preview_control)
        self.assertIn("mappingGrid.SelectionChanged += MappingGrid_SelectionChanged", self.retarget_tab)
        self.assertIn("private void UpdateHighlightedParts()", self.retarget_tab)
        self.assertIn("recipientPreview.SetHighlightedTrack(rowIndex)", self.retarget_tab)
        self.assertIn("donorPreview.SetHighlightedTrack(donorTrack)", self.retarget_tab)

    def test_reference_offsets_survive_animation_selection_changes(self):
        start = self.retarget_tab.index("private void AssetSelectionChanged(")
        end = self.retarget_tab.index("private void UpdatePreviews()", start)
        handler = self.retarget_tab[start:end]
        self.assertNotIn("ClearLoadedMapping()", handler)
        self.assertIn("UpdatePreviews()", handler)
        self.assertIn("RefreshRetargetPreview()", handler)

    def test_generated_animation_uses_recipient_track_count_and_protects_inputs(self):
        self.assertIn("GetBytes(recipientTrackCount)", self.retarget_tab)
        self.assertIn("TrackRecordLength = 56", self.retarget_tab)
        self.assertIn("source files are never overwritten", self.retarget_tab)
        self.assertIn("ClearLoadedMapping()", self.retarget_tab)


if __name__ == "__main__":
    unittest.main()
