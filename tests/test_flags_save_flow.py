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


class FlagsSaveFlowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.main_form = (ROOT / "Core" / "MainForm.cs").read_text()
        cls.save_tab = (ROOT / "Resources" / "Save" / "SaveTab.cs").read_text()
        cls.flags_tab = (ROOT / "Resources" / "Save" / "FlagsTab.cs").read_text()
        cls.save_session = (ROOT / "Resources" / "Save" / "SaveSession.cs").read_text()
        cls.project_file = (ROOT / "QFG5Extractor.csproj").read_text()
        cls.panorama_tab = (ROOT / "Resources" / "Panorama" / "RoomStudioTab.cs").read_text()

    def test_save_editor_still_validates_and_commits_before_saving(self):
        body = method_body(
            self.save_tab,
            "private void BtnSave_Click(object sender, EventArgs e)",
        )
        self.assertIn("if (!CommitPendingEdits())", body)
        self.assertLess(body.index("CommitPendingEdits()"), body.index("session.Save()"))

    def test_flags_are_embedded_as_a_save_editor_screen(self):
        self.assertIn("SaveSession saveSession = new SaveSession();", self.main_form)
        self.assertIn("new SaveTab(saveSession)", self.main_form)
        self.assertIn("new FlagsTab(saveSession)", self.main_form)
        self.assertIn("saveTab.AttachFlagsScreen(flagsTab)", self.main_form)
        self.assertIn("CreateViewButton(\"Flags\")", self.save_tab)
        self.assertIn("SetSaveScreen(SaveEditorScreen.Flags)", self.save_tab)
        self.assertNotIn('"Flags (Beta)"', self.main_form)

    def test_patches_screen_is_embedded_and_renders_get_boat(self):
        self.assertIn("Flags,\n            Patches", self.save_tab)
        self.assertIn('CreateViewButton("Patches")', self.save_tab)
        self.assertIn("SetSaveScreen(SaveEditorScreen.Patches)", self.save_tab)
        self.assertIn("patchesScreenPanel.Visible = screen == SaveEditorScreen.Patches;", self.save_tab)
        patches_tab = (ROOT / "Resources" / "Save" / "PatchesTab.cs").read_text()
        self.assertIn("new PatchesTab(tab)", self.save_tab)
        self.assertIn('Text = "Get Boat"', patches_tab)
        self.assertIn('TrySetFlagByName("fRowBoat", true', patches_tab)
        self.assertIn("Save the file, then leave the current scene in-game", patches_tab)
        self.assertNotIn('"fBoat"', patches_tab)

    def test_patches_resolve_named_flags_and_refuse_unsafe_or_missing_flags(self):
        flags_tab = (ROOT / "Resources" / "Save" / "FlagsTab.cs").read_text()
        patches_tab = (ROOT / "Resources" / "Save" / "PatchesTab.cs").read_text()
        get_named_flag = method_body(
            flags_tab,
            "public bool TryGetFlagByName(string name, out bool value, out string error)",
        )
        set_named_flag = method_body(
            flags_tab,
            "public bool TrySetFlagByName(string name, bool value, out string error)",
        )
        refresh = method_body(patches_tab, "public void RefreshFromSession()")

        self.assertIn("FindFlagIndex(name)", get_named_flag)
        self.assertIn("session.IsLoaded", get_named_flag)
        self.assertIn("session.CanEditFlags", get_named_flag)
        self.assertIn("session.TryGetFlag(flagIndex, flagNames.Count, out value)", get_named_flag)
        self.assertIn("session.SetFlag(flagIndex, flagNames.Count, value)", set_named_flag)
        self.assertIn("btnGetBoat.Enabled = false", refresh)
        self.assertIn("not present in the loaded flag-name list", get_named_flag)

    def test_flags_use_high_contrast_true_false_toggle_buttons(self):
        initialize = method_body(self.flags_tab, "private void InitializeComponent()")
        toggle = method_body(self.flags_tab, "private void GridFlags_CellContentClick")
        paint = method_body(self.flags_tab, "private void GridFlags_CellPainting")
        refresh = method_body(self.flags_tab, "private void RefreshGrid()")
        self.assertIn("new DataGridViewButtonColumn", initialize)
        self.assertIn("newValue = !currentValue", toggle)
        self.assertIn("session.TryGetFlag(flagIndex, flagNames.Count, out bool currentValue)", toggle)
        self.assertIn("session.SetFlag(flagIndex, flagNames.Count, newValue)", toggle)
        self.assertIn('newValue ? "True" : "False"', toggle)
        self.assertIn("session.TryGetFlag(i, flagNames.Count, out bool value)", refresh)
        self.assertIn('"Unknown"', refresh)
        self.assertIn("session.CanEditFlags", refresh)
        self.assertIn("CellPainting += GridFlags_CellPainting", initialize)
        self.assertIn("Color.White", paint)
        self.assertIn("Color.FromArgb(44, 112, 76)", paint)
        self.assertIn("Color.FromArgb(132, 70, 54)", paint)
        self.assertIn("QFG5Theme.ColorBorderHighlight", paint)
        self.assertIn("QFG5Theme.ColorSurfaceRecessed", paint)
        self.assertIn("QFG5Theme.ColorBgDark", paint)
        self.assertIn("DataGridViewCheckBoxColumn", initialize)

    def test_flags_support_search_multi_selection_and_bulk_toggle(self):
        initialize = method_body(self.flags_tab, "private void InitializeComponent()")
        search = method_body(self.flags_tab, "private void ApplySearchFilter()")
        click = method_body(self.flags_tab, "private void GridFlags_CellContentClick")
        toggle = method_body(self.flags_tab, "private void ToggleSelectedFlags()")
        selected = method_body(self.flags_tab, "private HashSet<int> GetSelectedFlagIndices()")

        self.assertIn("searchBox.TextChanged += (s, e) => ApplySearchFilter()", initialize)
        self.assertIn("index.IndexOf(query, StringComparison.OrdinalIgnoreCase)", search)
        self.assertIn("name.IndexOf(query, StringComparison.OrdinalIgnoreCase)", search)
        self.assertIn("MultiSelect = true", initialize)
        self.assertIn("Keys.Shift", click)
        self.assertIn("new HashSet<int>(checkedFlagIndices)", click)
        self.assertIn("SetFlags(targets, newValue)", click)
        self.assertIn('Text = "Toggle Selected"', initialize)
        self.assertIn("session.SetFlag(flagIndex, flagNames.Count, !currentValue)", toggle)
        self.assertIn("checkedFlagIndices", selected)
        self.assertIn("gridFlags.SelectedRows", selected)

    def test_flag_array_is_derived_from_documented_variable_length_sections(self):
        locate_flags = method_body(
            self.save_session,
            "private static int FindFlagArrayOffset(byte[] candidate, out string error)",
        )
        self.assertIn("ReadInventoryCount(candidate)", locate_flags)
        self.assertIn("dialogueRoomCount", locate_flags)
        self.assertIn("ObjectStateRoomCount", locate_flags)
        self.assertIn("EngineStateLength", locate_flags)
        self.assertIn("dataSize == 0 ? 4 : 12", locate_flags)
        self.assertNotIn("0x125B", self.flags_tab)

    def test_flag_names_are_copied_to_the_application_output_directory(self):
        self.assertIn('None Include="..\\..\\Resources\\QG5Edit\\flags.dat"', self.project_file)
        self.assertIn("<Link>flags.dat</Link>", self.project_file)
        self.assertIn("<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>", self.project_file)

    def test_inventory_sync_uses_the_header_declared_count_without_a_fixed_cap(self):
        save_session = self.save_session
        read_count = method_body(save_session, "private static int ReadInventoryCount(byte[] candidate)")
        self.assertIn("ReadUInt16(candidate, 12)", read_count)
        self.assertIn("^ 0xBEAD", read_count)

        active_slot = method_body(save_session, "public bool IsActiveInventorySlot(int offset)")
        self.assertIn("slotIndex < InventoryCount", active_slot)
        self.assertNotIn("InventorySlotCount", active_slot)

        parser = (ROOT / "Resources" / "Save" / "SaveGameParser.cs").read_text()
        get_inventory = method_body(parser, "public List<InventoryItem> GetInventory()")
        self.assertIn("session.InventoryCount", get_inventory)
        self.assertIn("session.HasInventoryRange(detectedOffset, session.InventoryCount)", get_inventory)

        sync_row = method_body(self.save_tab, "private bool SyncInventoryRow(DataGridViewRow row)")
        self.assertLess(
            sync_row.index("session.IsActiveInventorySlot(offset)"),
            sync_row.index("parser.UpdateInventoryItem(offset, idHex, amount)"),
        )

    def test_save_load_validates_documented_header_and_inventory_layout(self):
        save_session = self.save_session
        validation = method_body(save_session, "private static int ValidateInventory(byte[] candidate)")
        self.assertIn('"QG5P1.000 "', validation)
        self.assertIn("0xDE89", validation)
        self.assertIn("0xA8A5", validation)
        self.assertIn("MinimumNoiseLength", validation)
        self.assertIn("MaximumNoiseLength", validation)
        self.assertIn("PlayerStateLength", validation)
        self.assertIn("(long)InventorySlotSize * inventoryCount", validation)
        self.assertIn("all header-declared inventory slots", validation)

    def test_character_model_detection_uses_room_relative_fallback_when_marker_is_invalid(self):
        save_session = (ROOT / "Resources" / "Save" / "SaveSession.cs").read_text()
        detection = method_body(save_session, "private static int FindCharacterModelOffset(byte[] candidate, int engineStateOffset,")
        record_validation = method_body(save_session, "private static bool IsSupportedCharacterModelRecord(byte[] candidate, int modelOffset)")

        self.assertIn("CharacterModelOffsetBeforeRoomLabel", detection)
        self.assertIn("CharacterModelOffsetAfterEngineState", detection)
        self.assertIn("engineRelativeRecordValid", detection)
        self.assertIn("FindUniqueRoomLabelOffset(candidate", detection)
        self.assertIn("IsSupportedCharacterModelRecord(candidate, roomRelativeModelOffset)", detection)
        self.assertIn("engineRelativeModelOffset != roomRelativeModelOffset", detection)
        self.assertIn("CharacterPlacementRecordLength", record_validation)
        self.assertIn("modelId > byte.MaxValue", record_validation)
        self.assertIn("IsFinite(size)", record_validation)

    def test_global_map_room_record_at_end_of_file_is_displayed_without_hiding_character(self):
        save_session = self.save_session
        load = method_body(
            save_session,
            "public bool TryLoad(string path, Func<bool> approveReplacingDirtyDocument)",
        )
        map_room = method_body(
            save_session,
            "private static int FindRoomStateOffset(byte[] candidate, int engineStateOffset,",
        )
        find_model = method_body(
            save_session,
            "private static int FindCharacterModelOffset(byte[] candidate, int engineStateOffset,",
        )
        find_placement = method_body(
            save_session,
            "private static int FindCharacterPlacementOffset(byte[] candidate, int modelOffset,",
        )
        find_room = method_body(
            save_session,
            "private static int FindRoomIdOffset(byte[] candidate, int engineStateOffset,",
        )
        set_room = method_body(save_session, "public void SetRoomId(int roomId)")
        get_room = method_body(save_session, "public bool TryGetRoomId(out int roomId)")
        refresh_room = method_body(self.save_tab, "private void RefreshRoomControls()")

        self.assertIn("candidateFlagArrayOffset", load)
        self.assertIn("FindRoomStateOffset(candidate, engineStateOffset", find_room)
        self.assertIn("EngineStateLength", map_room)
        self.assertIn("CharacterEquipmentLength", map_room)
        self.assertIn("candidateRoomId == 0 || candidateRoomId == 900", map_room)
        self.assertIn("candidateRoomId == 900", map_room)
        self.assertIn("roomIdOffset + 4 == candidate.Length", map_room)
        self.assertIn("isPaddedRoomRecord", map_room)
        self.assertIn("!isTerminalMapRecord && !isPaddedRoomRecord", map_room)
        self.assertNotIn("isGlobalMap", find_model)
        self.assertNotIn("isGlobalMap", find_placement)
        self.assertIn("if (!roomHasData)", set_room)
        self.assertIn("value == 0 && !roomHasData", get_room)
        self.assertIn("candidateIsGlobalMap", load)
        self.assertIn("session.CanEditRoomId", refresh_room)
        self.assertIn("session.RoomEditUnavailableReason", refresh_room)

    def test_truncated_character_record_is_readable_but_not_editable_or_savable(self):
        save_session = (ROOT / "Resources" / "Save" / "SaveSession.cs").read_text()
        find_partial = method_body(save_session, "private static int FindIncompleteRoomLabelOffset(byte[] candidate)")
        load = method_body(save_session, "public bool TryLoad(string path, Func<bool> approveReplacingDirtyDocument)")
        save = method_body(save_session, "public bool Save()")
        refresh = method_body(self.save_tab, "private void RefreshCharacterModelControls()")

        self.assertIn("prefixLength = 2", find_partial)
        self.assertIn("prefixLength < RoomLabelMarker.Length", find_partial)
        self.assertIn("prefixLength += 2", find_partial)
        self.assertIn("RoomLabelMarker[i]", find_partial)
        self.assertIn("IsSupportedCharacterModelRecord(candidate, modelOffset)", find_partial)
        self.assertIn("candidateHasIncompleteCharacterRecord", load)
        self.assertIn("hasIncompleteCharacterRecord", save)
        self.assertIn("bool canRead", refresh)
        self.assertIn("session.CanEditCharacterData", refresh)
        self.assertIn("characterModelPreview.SetModelId(modelId)", refresh)

    def test_character_screen_has_landscape_room_preview_and_responsive_details(self):
        initialize = method_body(self.save_tab, "private void InitializeComponent()")
        self.assertIn("SplitContainer characterDetailsRoomSplit = new SplitContainer", initialize)
        self.assertIn("Orientation = Orientation.Horizontal", initialize)
        self.assertIn("Panel1.Controls.Add(characterDetails)", initialize)
        self.assertIn("Panel2.Controls.Add(roomPreviewLayout)", initialize)
        self.assertIn("SetSplitterRatioOnce(characterDetailsRoomSplit, 0.48, false)", initialize)
        self.assertIn("SplitContainer characterScreenSplit = new SplitContainer", initialize)
        self.assertIn("Orientation = Orientation.Vertical", initialize)
        self.assertIn("Panel1.Controls.Add(characterDetailsRoomSplit)", initialize)
        self.assertIn("Panel2.Controls.Add(characterModelPreview)", initialize)
        self.assertIn("SetSplitterRatioOnce(characterScreenSplit, 0.56)", initialize)
        self.assertIn("characterModelPreview = new CharacterModelPreviewControl", initialize)
        self.assertIn("roomPreview = new PanoramaProjectionControl", initialize)
        self.assertIn("Height = 66", initialize)
        self.assertIn("WrapContents = true", initialize)
        self.assertIn("Width = 150", initialize)
        self.assertIn("Width = 190", initialize)

    def test_panorama_camera_stays_inside_the_available_image(self):
        maximum_fov = method_body(self.panorama_tab, "private int GetMaximumFieldOfView()")
        clamp = method_body(self.panorama_tab, "private void ClampCameraToPanorama()")
        move = method_body(self.panorama_tab, "protected override void OnMouseMove")
        resize = method_body(self.panorama_tab, "protected override void OnResize")

        self.assertIn("heightLimitedFov", maximum_fov)
        self.assertIn("widthLimitedFov", maximum_fov)
        self.assertIn("startAngleRadians + halfFieldOfView", clamp)
        self.assertIn("startAngleRadians + angleSpanRadians - halfFieldOfView", clamp)
        self.assertIn("maximumPitch", clamp)
        self.assertIn("ClampCameraToPanorama()", move)
        self.assertIn("ClampCameraToPanorama()", resize)

    def test_save_creates_one_time_backup_and_rejects_external_changes(self):
        save_session = (ROOT / "Resources" / "Save" / "SaveSession.cs").read_text()
        save_body = method_body(save_session, "public bool Save()")
        self.assertIn("FilePath + \".bak\"", save_body)
        self.assertIn("FileMode.CreateNew", save_body)
        self.assertIn("loadedFileSnapshot", save_body)
        self.assertIn("changed on disk after it was loaded", save_body)

    def test_inventory_slot_insertion_and_deletion_are_blocked_to_protect_following_data(self):
        parser = (ROOT / "Resources" / "Save" / "SaveGameParser.cs").read_text()
        insert_body = method_body(parser, "public void InsertInventoryItem(int index)")
        insert_empty_body = method_body(parser, "public void InsertEmptyInventoryItem(int index)")
        delete_body = method_body(parser, "public void DeleteInventoryItem(int index)")
        self.assertIn("would shift the save's following data", insert_body)
        self.assertIn("would shift the save's following data", insert_empty_body)
        self.assertIn("would shift the save's following data", delete_body)

    def test_saved_item_ids_resolve_to_exact_sprite_ids(self):
        body = method_body(self.save_tab, "private Image GetItemImageCached(string idHex, string itemName = null)")
        self.assertIn("0x10000 | lowByte | (highByte << 8)", body)
        self.assertIn("RotateFlipType.Rotate90FlipNone", body)
        self.assertNotIn("CategoryIndex", body)
        graphics = ROOT.parent / "UI Kit" / "Inventory Graphics"
        samples = {
            "478B": "101191",
            "F68A": "101110",
            "F28A": "101106",
            "298B": "101161",
            "208B": "101152",
            "7F8C": "101503",
        }
        for item_id, sprite_id in samples.items():
            self.assertTrue(
                any((graphics / category / (sprite_id + ".png")).is_file()
                    for category in ("Items", "Spells", "Equipment")),
                item_id + " should resolve to " + sprite_id,
            )

    def test_shared_scroll_and_drink_icons_have_category_limited_fallbacks(self):
        group_body = method_body(self.save_tab, "private static string GetSharedIconGroup(string itemName)")
        fallback_body = method_body(self.save_tab, "private void ApplySharedIconFallbacks()")
        self.assertIn('normalized.Contains("scroll")', group_body)
        self.assertIn('normalized.Contains("potion")', group_body)
        self.assertIn('normalized.Contains("water")', group_body)
        self.assertIn("candidate.Category == item.Category", fallback_body)
        self.assertIn("candidate.Image != null", fallback_body)

    def test_save_tab_has_switchable_inventory_views_and_separate_catalog(self):
        initialize = method_body(self.save_tab, "private void InitializeComponent()")
        self.assertIn('CreateViewButton("List")', initialize)
        self.assertIn('CreateViewButton("Icons")', initialize)
        self.assertIn("inventoryViewHost", initialize)
        self.assertIn("catalogGrid", initialize)
        self.assertIn("CreateViewButton(category)", initialize)
        self.assertIn("catalogCategoryButtons.Controls.Add(categoryButton)", initialize)
        self.assertIn("SelectCatalogCategory(category)", initialize)
        self.assertIn("catalogCategoryButtons.Layout +=", initialize)
        self.assertIn("catalogSide.SizeChanged +=", initialize)
        self.assertIn("UpdateCatalogCategoryButtonSizes();", initialize)
        self.assertNotIn("split.Panel2.Controls.Add(inventoryIconView)", initialize)
        self.assertNotIn("catalogCategoryFilter", self.save_tab)
        catalog = method_body(self.save_tab, "private void PopulateCatalog()")
        self.assertIn("ItemDatabase.GetItems()", catalog)
        self.assertIn("GetItemImage(idHex, entry.Name, false)", catalog)
        self.assertIn("NormalizeCatalogCategory(entry.Category)", catalog)

    def test_item_catalog_is_shipped_and_not_dependent_on_working_directory(self):
        item_database = (ROOT / "Resources" / "Save" / "ItemDatabase.cs").read_text()
        project = (ROOT / "QFG5Extractor.csproj").read_text()
        catalog_lines = (ROOT.parent.parent / "Resources" / "QG5Edit" / "objects.dat").read_text().splitlines()
        item_id_index = catalog_lines.index("478B")

        self.assertEqual(catalog_lines[item_id_index + 1], "Drachma")
        self.assertIn("AppDomain.CurrentDomain.BaseDirectory", item_database)
        self.assertIn("Environment.CurrentDirectory", item_database)
        self.assertIn("Resources", item_database)
        self.assertIn("QG5Edit", item_database)
        self.assertIn(r'..\..\Resources\QG5Edit\objects.dat', project)
        self.assertIn("<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>", project)

    def test_save_editor_has_inventory_character_and_flags_screens(self):
        initialize = method_body(self.save_tab, "private void InitializeComponent()")
        switch = method_body(self.save_tab, "private void SetSaveScreen(SaveEditorScreen screen)")

        self.assertIn('CreateViewButton("Inventory")', initialize)
        self.assertIn('CreateViewButton("Character")', initialize)
        self.assertIn('CreateViewButton("Flags")', initialize)
        self.assertIn("btnInventoryScreen.Click += (s, e) => SetSaveScreen(SaveEditorScreen.Inventory)", initialize)
        self.assertIn("btnCharacterScreen.Click += (s, e) => SetSaveScreen(SaveEditorScreen.Character)", initialize)
        self.assertIn("btnFlagsScreen.Click += (s, e) => SetSaveScreen(SaveEditorScreen.Flags)", initialize)
        self.assertIn("inventoryScreenPanel.Visible = screen == SaveEditorScreen.Inventory", switch)
        self.assertIn("characterScreenPanel.Visible = screen == SaveEditorScreen.Character", switch)
        self.assertIn("flagsScreenPanel.Visible = screen == SaveEditorScreen.Flags", switch)
        self.assertNotIn("TabControl saveScreens", initialize)

    def test_character_model_selection_updates_spinning_preview(self):
        preview = (ROOT / "Resources" / "Save" / "CharacterModelPreviewControl.cs").read_text()
        numeric_change = method_body(self.save_tab, "private void CharacterModelId_ValueChanged")
        name_change = method_body(self.save_tab, "private void CharacterModelName_SelectedIndexChanged")

        self.assertIn("characterModelPreview.SetModelId((int)numericCharacterModelId.Value)", numeric_change)
        self.assertIn("characterModelPreview.SetModelId(model.Id)", name_change)
        self.assertIn("rotation = (rotation + 1.2f) % 360.0f", preview)
        self.assertIn("spinTimer.Tick +=", preview)
        tick = method_body(preview, "private void LoadSelectedModel()")
        self.assertIn("animationFrame = 0", tick)
        self.assertNotIn("animationFrame = (animationFrame + 1)", preview)

    def test_character_preview_loads_three_digit_mdl_names_from_asset_folder(self):
        preview = (ROOT / "Resources" / "Save" / "CharacterModelPreviewControl.cs").read_text()
        find_model = method_body(preview, "private static string FindModelPath(string directory, int selectedModelId)")
        load_model = method_body(preview, "private void LoadSelectedModel()")

        self.assertIn('selectedModelId.ToString("D3") + ".MDL"', find_model)
        self.assertIn("StringComparison.OrdinalIgnoreCase", find_model)
        self.assertIn('SharedState.GetGameDataPath("hdnw", "mdl")', load_model)
        self.assertIn('SharedState.GetModsPath("hdnw", "mdl")', load_model)
        self.assertIn(".MDL was not found in the configured hdnw/mdl folder.", load_model)

    def test_character_preview_loads_and_applies_a_matching_animation(self):
        preview = (ROOT / "Resources" / "Save" / "CharacterModelPreviewControl.cs").read_text()
        load_animation = method_body(
            preview,
            "private static Matrix4[][] LoadCharacterAnimation(int selectedModelId, int expectedBoneCount,",
        )
        calculate_bounds = method_body(preview, "private void CalculateAnimatedModelBounds()")
        draw_meshes = method_body(preview, "private void DrawMeshes()")
        loader = (ROOT / "Resources" / "Model" / "AnmAnimationLoader.cs").read_text()
        viewer = (ROOT / "Viewers" / "Model" / "MdlViewer.cs").read_text()

        self.assertIn('SharedState.GetGameDataPath("hdnw", "anm")', load_animation)
        self.assertIn('SharedState.GetModsPath("hdnw", "anm")', load_animation)
        self.assertIn("AnmAnimationLoader.LoadFrames(candidate, expectedBoneCount)", load_animation)
        self.assertIn("animationFrame = 0", preview)
        self.assertIn("Vector4.Transform(", calculate_bounds)
        self.assertIn("Matrix4 bone = frame[meshIndex]", draw_meshes)
        self.assertIn("GL.MultMatrix(ref bone)", draw_meshes)
        self.assertIn("AnmAnimationLoader.LoadFrames(filePath, currentMeshes.Count)", viewer)
        self.assertIn("boneRecordCount = (long)frameCount * boneCount", loader)
        self.assertIn("(boneRecordCount - 1) * BoneMatrixLength + BoneMatrixDataLength", loader)

    def test_character_screen_previews_selected_room_panorama(self):
        room_preview = method_body(self.save_tab, "private void LoadRoomPreview(int roomId)")
        find_room = method_body(self.save_tab, "private static string FindRoomPanoramaPath(int roomId)")
        refresh_room = method_body(self.save_tab, "private void RefreshRoomControls()")
        self.assertIn("roomPreview.SetPanorama", room_preview)
        self.assertIn("PanoConverter.TryReadProjectionAngles", room_preview)
        self.assertIn("(roomId * 10).ToString", find_room)
        self.assertIn("LoadRoomPreview(canRead ? roomId : -1)", refresh_room)
        self.assertIn("btnFlagsScreen", self.save_tab)

    def test_context_menu_actions_only_operate_on_active_non_header_rows(self):
        initialize = method_body(self.save_tab, "private void InitializeComponent()")
        opening = method_body(self.save_tab, "private void InventoryContextMenu_Opening")
        self.assertIn('"Duplicate Item Below"', initialize)
        self.assertIn('"Add Empty Slot Below"', initialize)
        self.assertIn("selectedIndex < session.InventoryCount", opening)
        self.assertIn("!selectedItem.IsHeader", opening)

    def test_catalog_drag_drop_targets_existing_active_inventory_slots(self):
        body = method_body(self.save_tab, "private void ApplyCatalogItemToRow(DataGridViewRow row, PaletteItem item)")
        self.assertIn("session.IsActiveInventorySlot(offset)", body)
        self.assertIn('row.Cells["IdHex"].Value = item.IdHex', body)
        self.assertIn("SyncInventoryRow(row)", body)


if __name__ == "__main__":
    unittest.main()
