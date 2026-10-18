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

    def test_flags_only_save_does_not_commit_inventory_grid(self):
        body = method_body(
            self.main_form,
            "private void CoordinateFlagsSave(SaveTab saveTab, FlagsTab flagsTab)",
        )
        self.assertNotIn("CommitPendingEdits", body)
        self.assertIn("flagsTab.SaveActiveSession();", body)

    def test_save_editor_still_validates_and_commits_before_saving(self):
        body = method_body(
            self.save_tab,
            "private void BtnSave_Click(object sender, EventArgs e)",
        )
        self.assertIn("if (!CommitPendingEdits())", body)
        self.assertLess(body.index("CommitPendingEdits()"), body.index("session.Save()"))

    def test_both_tabs_share_the_same_save_session(self):
        self.assertIn("SaveSession saveSession = new SaveSession();", self.main_form)
        self.assertIn("new SaveTab(saveSession)", self.main_form)
        self.assertIn("new FlagsTab(saveSession)", self.main_form)
        save_body = method_body(
            self.flags_tab,
            "public void SaveActiveSession()",
        )
        self.assertIn("session.Save();", save_body)

    def test_flags_save_keeps_existing_failure_feedback(self):
        body = method_body(
            self.flags_tab,
            "public void SaveActiveSession()",
        )
        self.assertIn("catch (Exception ex)", body)
        self.assertIn('"Error saving flags: " + ex.Message', body)
        self.assertIn('"Flags saved successfully."', body)

    def test_inventory_sync_is_limited_to_header_declared_count(self):
        save_session = (ROOT / "Resources" / "Save" / "SaveSession.cs").read_text()
        read_count = method_body(save_session, "private static int ReadInventoryCount(byte[] candidate)")
        self.assertIn("candidate[12]", read_count)
        self.assertIn("candidate[13]", read_count)
        self.assertIn("encodedCount ^ 0xBEAD", read_count)

        active_slot = method_body(save_session, "public bool IsActiveInventorySlot(int offset)")
        self.assertIn("InventoryCount <= InventorySlotCount", active_slot)
        self.assertIn("slotIndex < InventoryCount", active_slot)

        sync_row = method_body(self.save_tab, "private bool SyncInventoryRow(DataGridViewRow row)")
        self.assertLess(
            sync_row.index("session.IsActiveInventorySlot(offset)"),
            sync_row.index("parser.UpdateInventoryItem(offset, idHex, amount)"),
        )


if __name__ == "__main__":
    unittest.main()
