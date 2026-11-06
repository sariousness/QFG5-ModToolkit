import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src"


class AudioExplorerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.main_form = (SOURCE / "Core" / "MainForm.cs").read_text()
        cls.audio_tab = (SOURCE / "Resources" / "Audio" / "AudioTab.cs").read_text()
        cls.inspector = (SOURCE / "Resources" / "Audio" / "AudioResourceInspector.cs").read_text()
        cls.project = (SOURCE / "QFG5Extractor.csproj").read_text()

    def test_audio_tab_is_registered_and_compiled(self):
        self.assertIn("pages[11] = new AudioTab()", self.main_form)
        self.assertIn("((AudioTab)pages[index]).AutoLoad()", self.main_form)
        self.assertIn('"Audio Explorer"', self.main_form)
        self.assertIn('Compile Include="Resources\\Audio\\AudioTab.cs"', self.project)
        self.assertIn('Compile Include="Resources\\Audio\\AudioResourceInspector.cs"', self.project)

    def test_audio_browser_scans_configured_folders_and_id_named_assets(self):
        self.assertIn("SharedState.BaseDataPath, SharedState.ExtractionPath", self.audio_tab)
        self.assertIn("Directory.EnumerateFiles(root, \"*\", SearchOption.AllDirectories)", self.audio_tab)
        self.assertIn('string.Equals(extension, ".wav"', self.inspector)
        self.assertIn('string.Equals(extension, ".aud"', self.inspector)
        self.assertIn("TryParseResourceIds(Path.GetFileName(path)", self.inspector)
        self.assertIn("(name[0] != 'A' && name[0] != 'a')", self.inspector)

    def test_inspector_surfaces_wave_header_and_resource_identifiers(self):
        self.assertIn('riffId != "RIFF" || waveId != "WAVE"', self.inspector)
        self.assertIn('chunkId == "fmt "', self.inspector)
        self.assertIn('chunkId == "LIST"', self.inspector)
        self.assertIn('chunkId == "bext"', self.inspector)
        self.assertIn('chunkId == "cart"', self.inspector)
        self.assertIn('chunkId == "ID3 " || chunkId == "id3 "', self.inspector)
        self.assertIn('GetInfoTag(infoTags, "INAM")', self.inspector)
        self.assertIn("ParseId3Title", self.inspector)
        self.assertIn("DecodeText", self.inspector)
        self.assertIn('ReadFourCc(reader);', self.inspector)
        self.assertIn("ParseBase36(qgmId)", self.inspector)
        self.assertIn("ParseBase36(ids[i])", self.inspector)
        self.assertIn("chunkEnd != riffEnd", self.inspector)
        self.assertIn("DurationSummary", self.inspector)
        self.assertIn('audioList.Columns.Add("Duration"', self.audio_tab)
        self.assertIn('audioList.Columns.Add("Header name"', self.audio_tab)
        self.assertIn('audioList.Columns.Add("Resource IDs"', self.audio_tab)
        self.assertIn("item.SubItems.Add(info.DurationSummary,", self.audio_tab)
        self.assertIn("item.SubItems.Add(string.IsNullOrEmpty(info.HeaderName)", self.audio_tab)
        self.assertIn("item.SubItems.Add(string.IsNullOrEmpty(info.ResourceIds)", self.audio_tab)

    def test_audio_controls_use_in_app_playback_and_leave_lipsync_out(self):
        self.assertIn("new SoundPlayer(info.FullPath)", self.audio_tab)
        self.assertIn("player.Play()", self.audio_tab)
        self.assertIn("player.Stop()", self.audio_tab)
        self.assertIn("PlayWithFfplay(info)", self.audio_tab)
        self.assertIn('Arguments = "-nodisp -autoexit -loglevel error -i "', self.audio_tab)
        self.assertIn("FindExecutable(\"ffplay\", \"ffplay.exe\")", self.audio_tab)
        self.assertIn("valid RIFF/WAVE audio resource", self.audio_tab)
        self.assertNotIn('string.Equals(extension, ".snc"', self.inspector)

    def test_scanning_runs_off_the_ui_thread_and_can_be_cancelled(self):
        self.assertIn("await Task.Run(", self.audio_tab)
        self.assertIn("ScanFiles(roots, cancellation.Token)", self.audio_tab)
        self.assertIn("cancellationToken.ThrowIfCancellationRequested()", self.audio_tab)
        self.assertIn("Scanning configured folders in the background...", self.audio_tab)

    def test_scanning_includes_extracted_cda_and_hdn_folders(self):
        self.assertIn('string.Equals(leafName, "cda"', self.audio_tab)
        self.assertIn('string.Equals(leafName, "hdn"', self.audio_tab)
        self.assertIn('Path.Combine(contentRoot, "cda")', self.audio_tab)
        self.assertIn('Path.Combine(contentRoot, "hdn")', self.audio_tab)
        self.assertIn("roots.AddRange(hdnRoots)", self.audio_tab)

    def test_audio_favorites_can_be_toggled_filtered_and_persisted(self):
        self.assertIn("audio-favorites.txt", self.audio_tab)
        self.assertIn("LoadFavoriteFiles()", self.audio_tab)
        self.assertIn("SaveFavoriteFiles()", self.audio_tab)
        self.assertIn("ToggleSelectedFavorite()", self.audio_tab)
        self.assertIn('Text = "Show favorites only"', self.audio_tab)
        self.assertIn('"\\u2605" : "\\u2606"', self.audio_tab)


if __name__ == "__main__":
    unittest.main()
