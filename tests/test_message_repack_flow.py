import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1] / "src"


class MessageRepackFlowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.converter = (ROOT / "Resources" / "Message" / "MsgConverter.cs").read_text()
        cls.tab = (ROOT / "Resources" / "Message" / "MsgTab.cs").read_text()

    def test_qgm_parser_uses_declared_records_options_labels_and_block_flags(self):
        self.assertIn("BitConverter.ToInt32(msgData, 4)", self.converter)
        self.assertIn("BitConverter.ToUInt32(data, 8)", self.converter)
        self.assertIn("BitConverter.ToUInt16(data, offset + 16)", self.converter)
        self.assertIn("BitConverter.ToUInt16(data, offset + 26)", self.converter)
        self.assertIn("BitConverter.ToUInt16(data, offset + 24)", self.converter)
        self.assertIn("(blockFlags & 0x0004) != 0", self.converter)
        self.assertIn("(labelPresent == 0 ? 0 : 13)", self.converter)
        self.assertIn("(long)optionCount * 8", self.converter)
        self.assertIn("offset != data.Length", self.converter)

    def test_sidecar_preserves_original_records_and_changed_text_is_rebuilt_safely(self):
        self.assertIn('return textFilePath + ".qgmmeta";', self.converter)
        self.assertIn("writer.Write(originalData);", self.converter)
        self.assertIn("writer.Write(messages[i]);", self.converter)
        self.assertIn("File.WriteAllBytes(outputQgmPath, originalData);", self.converter)
        self.assertIn("(flags & ~0x0004)", self.converter)
        self.assertIn("BitConverter.GetBytes((ushort)rebuiltLength)", self.converter)
        self.assertIn("writer.Write(originalData, block.PayloadOffset + block.TextLength, 4);", self.converter)
        self.assertIn("Keep the blank-line paragraph structure unchanged", self.converter)

    def test_message_tab_rebuilds_qgms_before_repacking_original_cdn_archive(self):
        self.assertIn("MsgConverter.CreateMetadata(file, outputFile);", self.tab)
        self.assertIn("MsgConverter.RebuildMsg(", self.tab)
        self.assertIn('SpkConverter.FindArchiveFile(SharedState.BaseDataPath, "CDN.SPK")', self.tab)
        self.assertIn('SharedState.GetArchiveModsPath("CDN.SPK")', self.tab)
        self.assertIn("SpkConverter.Repack(", self.tab)
        self.assertIn('"repacked", "CDN.SPK"', self.tab)
        self.assertNotIn("under development", self.tab.lower())


if __name__ == "__main__":
    unittest.main()
