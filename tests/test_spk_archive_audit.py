import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


class SpkArchiveAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.converter = (ROOT / "src" / "Storage" / "SPK" / "SpkConverter.cs").read_text()
        cls.tab = (ROOT / "src" / "Storage" / "SPK" / "SpkTab.cs").read_text()
        cls.guide = (ROOT / "USER_GUIDE.md").read_text()

    def test_audit_separates_missing_entries_from_candidate_extensions(self):
        self.assertIn("public static SpkArchiveAuditResult Audit(", self.converter)
        self.assertIn("MissingFromModsCount", self.converter)
        self.assertIn("CandidateUnhandledCount", self.converter)
        self.assertIn("extractedPaths.Contains(archivePath)", self.converter)
        self.assertIn("StringComparer.OrdinalIgnoreCase", self.converter)

    def test_known_workflow_extensions_are_explicit_and_conservative(self):
        for extension in (".GRA", ".MDL", ".ANM", ".IMG", ".NOD", ".QGM", ".FTR"):
            self.assertIn('case "' + extension + '":', self.converter)
        self.assertIn("Candidate formats not covered by a dedicated tool workflow", self.tab)
        self.assertIn("not proof of missing data", self.tab)

    def test_archive_tab_refreshes_and_displays_exact_entry_paths(self):
        self.assertIn('Text = "Audit"', self.tab)
        self.assertIn('Text = "Audit Details"', self.tab)
        self.assertIn("RefreshAudit();", self.tab)
        self.assertIn('result.ArchiveFileName + ": " + entry.FileName', self.tab)
        self.assertIn("MatchesAuditFilter", self.tab)
        self.assertIn("exact archive and entry paths", self.guide)


if __name__ == "__main__":
    unittest.main()
