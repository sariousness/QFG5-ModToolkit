using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using QFG5Extractor.Resources.Message;
using QFG5Extractor.Storage.SPK;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace QFG5Extractor.Resources.Message
{
    public class MsgTab : UserControl
    {
        private QFG5Button runButton;
        private QFG5Button repackButton;
        private QFG5Button btnSaveEdits;
        private Label statusLabel;
        private Label currentFileLabel;
        private Label lblFileCount;
        private Label lblEditorHeader;
        private QFG5ProgressBar progressBar;
        private ListBox convertedListBox;
        private RichTextBox txtEditor;
        private SplitContainer msgSplit;

        public MsgTab()
        {
            Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            ToolTip toolTip = new ToolTip();

            QFG5TableLayoutPanel mainLayout = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Center: Split view (List + Editor)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));  // Bottom: 2-Row Footer

            // --- Center Section: Split View (Message List + Text Editor) ---
            msgSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BorderStyle = BorderStyle.None,
                SplitterWidth = 6,
                Margin = new Padding(0, 0, 0, 6)
            };
            QFG5Layout.SetSplitterRatioOnce(msgSplit, 0.32);

            // Panel 1: Message Files List Frame
            QFG5PreviewFrame listFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            Panel listContent = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            Label lblListHeader = new Label
            {
                Text = " MESSAGE FILES ",
                ForeColor = QFG5Theme.ColorGold,
                Font = QFG5Theme.SectionHeaderFont,
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = QFG5Theme.ColorSurface,
                Padding = new Padding(6, 0, 0, 0)
            };
            QFG5Theme.ApplyHeaderStroke(lblListHeader);

            convertedListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                BorderStyle = BorderStyle.None,
                IntegralHeight = false,
                TabIndex = 4
            };
            convertedListBox.SelectedIndexChanged += ConvertedListBox_SelectedIndexChanged;
            toolTip.SetToolTip(convertedListBox, "List of extracted message .TXT files. Click a file to view and edit its contents.");

            listContent.Controls.Add(convertedListBox);
            listContent.Controls.Add(lblListHeader);
            listFrame.Controls.Add(listContent);
            msgSplit.Panel1.Controls.Add(listFrame);

            // Panel 2: Message Content Viewer / Editor Frame
            QFG5PreviewFrame editorFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            Panel editorContent = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            // Editor header toolbar with title and integrated Save Changes button
            QFG5TableLayoutPanel editorHeaderBar = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 28,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = QFG5Theme.ColorSurface,
                Padding = new Padding(6, 0, 4, 0),
                Margin = new Padding(0)
            };
            editorHeaderBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); // Title
            editorHeaderBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // Save button

            lblEditorHeader = new Label
            {
                Text = " MESSAGE VIEWER / EDITOR ",
                ForeColor = QFG5Theme.ColorGold,
                Font = QFG5Theme.SectionHeaderFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            QFG5Theme.ApplyHeaderStroke(lblEditorHeader);

            btnSaveEdits = new QFG5Button
            {
                Text = "Save Changes",
                Dock = DockStyle.Fill,
                Height = 26,
                Margin = new Padding(0, 1, 0, 1),
                Enabled = false,
                TabIndex = 6
            };
            btnSaveEdits.Click += BtnSaveEdits_Click;
            toolTip.SetToolTip(btnSaveEdits, "Save edited text changes back to the .TXT file on disk");

            editorHeaderBar.Controls.Add(lblEditorHeader, 0, 0);
            editorHeaderBar.Controls.Add(btnSaveEdits, 1, 0);

            txtEditor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = new Font("Consolas", 10f, FontStyle.Regular),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                ReadOnly = false,
                WordWrap = true,
                TabIndex = 5
            };
            txtEditor.TextChanged += (s, e) => { if (!string.IsNullOrEmpty(txtEditor.Text)) btnSaveEdits.Enabled = true; };
            toolTip.SetToolTip(txtEditor, "Viewer and editor for extracted dialogue and message text");

            // Empty state guidance in editor
            txtEditor.Text = "No message file selected.\n\nMessage files load automatically when this tab opens. Select a file from the list to view and edit its dialogue.";

            editorContent.Controls.Add(txtEditor);
            editorContent.Controls.Add(editorHeaderBar);
            editorFrame.Controls.Add(editorContent);
            msgSplit.Panel2.Controls.Add(editorFrame);

            // --- Bottom Section: 2-Row Cohesive Footer ---
            QFG5TableLayoutPanel footerTable = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0, 4, 0, 0)
            };
            footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); // Left status/file info
            footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));  // Right progress / action buttons

            footerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Row 0: Status + Progress
            footerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Row 1: File Info + Actions

            // Row 0: High-Level Status + Progress Bar
            statusLabel = new Label
            {
                Text = "Message files load automatically when this tab opens.",
                ForeColor = QFG5Theme.ColorGold,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            progressBar = new QFG5ProgressBar
            {
                Height = 20,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                AccessibleName = "Message extraction progress"
            };

            footerTable.Controls.Add(statusLabel, 0, 0);
            footerTable.Controls.Add(progressBar, 1, 0);

            // Row 1: Detailed File Info + Action Buttons
            QFG5FlowLayoutPanel fileInfoPanel = new QFG5FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                WrapContents = false
            };

            currentFileLabel = new Label
            {
                Text = "Status: Idle",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                AutoSize = true,
                Margin = new Padding(0, 6, 8, 0)
            };

            lblFileCount = new Label
            {
                Text = "(0 files)",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                AutoSize = true,
                Margin = new Padding(0, 7, 0, 0)
            };

            fileInfoPanel.Controls.Add(currentFileLabel);
            fileInfoPanel.Controls.Add(lblFileCount);

            // Right-aligned action buttons container
            QFG5TableLayoutPanel actionButtonsPanel = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            actionButtonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f)); // Repack All
            actionButtonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f)); // Extract All

            repackButton = new QFG5Button { Text = "Repack All", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 1, 4, 1), TabIndex = 7 };
            repackButton.Click += RepackButtonClick;
            toolTip.SetToolTip(repackButton, "Rebuild edited QGM files and repack CDN.SPK into Mods/repacked");

            runButton = new QFG5Button { Text = "Extract All", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(2, 1, 0, 1), TabIndex = 8 };
            runButton.Click += RunButtonClick;
            toolTip.SetToolTip(runButton, "Extract all .QGM message files into editable .TXT files");

            actionButtonsPanel.Controls.Add(repackButton, 0, 0);
            actionButtonsPanel.Controls.Add(runButton, 1, 0);

            footerTable.Controls.Add(fileInfoPanel, 0, 1);
            footerTable.Controls.Add(actionButtonsPanel, 1, 1);

            // Assemble main layout
            mainLayout.Controls.Add(msgSplit, 0, 0);
            mainLayout.Controls.Add(footerTable, 0, 1);

            this.Controls.Add(mainLayout);
        }

        public void AutoLoad()
        {
            RunExtraction(true);
        }

        private void RunButtonClick(object sender, EventArgs e)
        {
            RunExtraction(false);
        }

        private void RunExtraction(bool automatic)
        {
            string qgmFolder = SharedState.GetAssetDataPath("cdn", "QGM");
            string outputFolder = SharedState.GetModsPath("cdn", "QGM");

            if (!DirectoryHasFiles(qgmFolder, ".qgm"))
            {
                try
                {
                    AssetArchiveHelper.EnsureExtracted("CDN.SPK");
                    qgmFolder = SharedState.GetModsPath("cdn", "QGM");
                }
                catch (Exception ex)
                {
                    SetStatus("Unable to load message files: " + ex.Message, Color.Firebrick);
                    currentFileLabel.Text = "Status: Error";
                    Logger.LogError("MsgTab", ex.Message);
                    if (!automatic)
                        MessageBox.Show(ex.Message, "Message extraction", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (!Directory.Exists(qgmFolder))
            {
                SetStatus("The configured source folder is missing. Check Settings.", Color.Firebrick);
                if (!automatic)
                    MessageBox.Show("The Game DATA Folder does not contain cdn/QGM.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Directory.CreateDirectory(outputFolder);
            runButton.Enabled = false;
            btnSaveEdits.Enabled = false;
            if (automatic)
                LoadOutputFiles(outputFolder);
            else
                convertedListBox.Items.Clear();
            txtEditor.Text = "Extracting messages...";
            lblFileCount.Text = $"({convertedListBox.Items.Count} files)";
            SetStatus(automatic ? "Loading message files..." : "Running extraction...", Color.RoyalBlue);

            int converted = 0;
            int failed = 0;

            try
            {
                string[] allFiles = Directory.GetFiles(qgmFolder, "*.*");
                Array.Sort(allFiles, StringComparer.OrdinalIgnoreCase);
                List<string> qgmFiles = new List<string>();
                foreach (string file in allFiles)
                    if (string.Equals(Path.GetExtension(file), ".qgm", StringComparison.OrdinalIgnoreCase))
                        qgmFiles.Add(file);

                if (qgmFiles.Count == 0)
                {
                    SetStatus("No QGM files found in selected path.", Color.Orange);
                    currentFileLabel.Text = "Status: Idle";
                    txtEditor.Text = "No .QGM message files found in source directory.";
                    return;
                }

                progressBar.Value = 0;
                int total = qgmFiles.Count;

                for (int i = 0; i < total; i++)
                {
                    string file = qgmFiles[i];
                    string baseName = Path.GetFileNameWithoutExtension(file);
                    string outputFile = Path.Combine(outputFolder, baseName + ".txt");
                    if (automatic && FindOutputFile(outputFolder, baseName) != null)
                    {
                        string existingOutput = FindOutputFile(outputFolder, baseName);
                        string metadataPath = MsgConverter.GetMetadataPath(existingOutput);
                        if (!File.Exists(metadataPath))
                        {
                            currentFileLabel.Text = "Indexing: " + baseName;
                            try
                            {
                                MsgConverter.CreateMetadata(file, existingOutput);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError("MsgTab", "Could not prepare QGM sidecar for " + baseName + ": " + ex.Message);
                            }
                        }
                        progressBar.Value = (int)((float)(i + 1) / total * 100);
                        continue;
                    }

                    currentFileLabel.Text = "Extracting: " + baseName;
                    Application.DoEvents();

                    try
                    {
                        MsgConverter.ExtractMsg(file, outputFile);
                        MsgConverter.CreateMetadata(file, outputFile);
                        converted++;
                        convertedListBox.Items.Add(baseName + ".txt");
                        lblFileCount.Text = $"({converted} of {total} files)";
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("MsgTab", ex.Message);
                        failed++;
                    }
                    progressBar.Value = (int)((float)(i + 1) / total * 100);
                }

                SetStatus(automatic
                    ? $"Loaded {convertedListBox.Items.Count} message files."
                    : $"Done. Extracted {converted} files.", Color.ForestGreen);
                currentFileLabel.Text = "Status: Finished";
                lblFileCount.Text = $"({convertedListBox.Items.Count} files)";
                if (automatic && convertedListBox.Items.Count > 0)
                    convertedListBox.SelectedIndex = 0;
                else
                    txtEditor.Text = $"Extraction complete ({converted} files converted).\n\nSelect a file on the left to view and edit dialogue.";
                if (!automatic)
                    MessageBox.Show($"Done. Extracted {converted} files.", "Message Tab", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Error: " + ex.Message, Color.Firebrick);
                currentFileLabel.Text = "Status: Error";
                Logger.LogError("MsgTab", ex.Message);
            }
            finally
            {
                runButton.Enabled = true;
            }
        }

        private bool DirectoryHasFiles(string folder, string extension)
        {
            return Directory.Exists(folder) &&
                Array.Exists(Directory.GetFiles(folder), file =>
                    string.Equals(Path.GetExtension(file), extension, StringComparison.OrdinalIgnoreCase));
        }

        private void LoadOutputFiles(string folder)
        {
            convertedListBox.Items.Clear();
            if (!Directory.Exists(folder)) return;
            string[] files = Directory.GetFiles(folder);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                if (string.Equals(Path.GetExtension(file), ".txt", StringComparison.OrdinalIgnoreCase))
                    convertedListBox.Items.Add(Path.GetFileName(file));
            }
            lblFileCount.Text = $"({convertedListBox.Items.Count} files)";
        }

        private string FindOutputFile(string folder, string baseName)
        {
            if (!Directory.Exists(folder)) return null;
            string expectedName = baseName + ".txt";
            foreach (string file in Directory.GetFiles(folder))
            {
                if (string.Equals(Path.GetFileName(file), expectedName, StringComparison.OrdinalIgnoreCase))
                    return file;
            }
            return null;
        }

        private void RepackButtonClick(object sender, EventArgs e)
        {
            if (!SaveCurrentEdits())
                return;

            string textFolder = SharedState.GetModsPath("cdn", "QGM");
            string originalArchive = SpkConverter.FindArchiveFile(SharedState.BaseDataPath, "CDN.SPK");
            if (string.IsNullOrEmpty(originalArchive))
            {
                string message = "The original CDN.SPK could not be found in the configured Game DATA folder. It is required to repack messages.";
                SetStatus(message, Color.Firebrick);
                MessageBox.Show(message, "Message repack", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!Directory.Exists(textFolder))
            {
                string message = "No extracted message files were found under Mods/cdn/QGM.";
                SetStatus(message, Color.Firebrick);
                MessageBox.Show(message, "Message repack", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string[] textFiles = Directory.GetFiles(textFolder, "*.txt", SearchOption.TopDirectoryOnly);
            Array.Sort(textFiles, StringComparer.OrdinalIgnoreCase);
            if (textFiles.Length == 0)
            {
                SetStatus("No message TXT files were found to repack.", Color.Orange);
                return;
            }

            string tempFolder = Path.Combine(Path.GetTempPath(), "QFG5Extractor-QGM-" + Guid.NewGuid().ToString("N"));
            string outputArchive = Path.Combine(SharedState.ExtractionPath, "repacked", "CDN.SPK");
            repackButton.Enabled = false;
            runButton.Enabled = false;
            btnSaveEdits.Enabled = false;
            progressBar.Value = 0;

            try
            {
                Directory.CreateDirectory(tempFolder);
                for (int i = 0; i < textFiles.Length; i++)
                {
                    string textPath = textFiles[i];
                    string baseName = Path.GetFileNameWithoutExtension(textPath);
                    string metadataPath = MsgConverter.GetMetadataPath(textPath);
                    if (!File.Exists(metadataPath))
                        throw new InvalidDataException(
                            "Missing QGM sidecar for " + Path.GetFileName(textPath) +
                            ". Extract the message files again before repacking.");

                    currentFileLabel.Text = "Building: " + baseName;
                    Application.DoEvents();
                    MsgConverter.RebuildMsg(
                        textPath,
                        metadataPath,
                        Path.Combine(tempFolder, baseName + ".QGM"));
                    progressBar.Value = (int)((float)(i + 1) / textFiles.Length * 100);
                }

                foreach (string rebuiltFile in Directory.GetFiles(tempFolder, "*.QGM"))
                {
                    string targetFile = Path.Combine(textFolder, Path.GetFileName(rebuiltFile));
                    File.Copy(rebuiltFile, targetFile, true);
                }

                string outputDirectory = Path.GetDirectoryName(outputArchive);
                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);
                currentFileLabel.Text = "Repacking CDN.SPK...";
                Application.DoEvents();
                SpkConverter.Repack(
                    originalArchive,
                    SharedState.GetArchiveModsPath("CDN.SPK"),
                    outputArchive);

                SetStatus("Rebuilt " + textFiles.Length + " QGM files and repacked CDN.SPK.", Color.ForestGreen);
                currentFileLabel.Text = "Status: Finished";
                MessageBox.Show(
                    "Rebuilt " + textFiles.Length + " QGM files and repacked the archive:\n" + outputArchive,
                    "Message repack",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Repack failed: " + ex.Message, Color.Firebrick);
                currentFileLabel.Text = "Status: Error";
                Logger.LogError("MsgTab", "QGM repack failed: " + ex);
                MessageBox.Show(ex.Message, "Message repack failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempFolder))
                        Directory.Delete(tempFolder, true);
                }
                catch (IOException ex)
                {
                    Logger.LogError("MsgTab", "Could not remove temporary QGM files: " + ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Logger.LogError("MsgTab", "Could not remove temporary QGM files: " + ex.Message);
                }
                finally
                {
                    repackButton.Enabled = true;
                    runButton.Enabled = true;
                }
            }
        }

        private bool SaveCurrentEdits()
        {
            if (!btnSaveEdits.Enabled || convertedListBox.SelectedItem == null)
                return true;

            string selectedFile = convertedListBox.SelectedItem.ToString();
            string fullPath = Path.Combine(SharedState.GetModsPath("cdn", "QGM"), selectedFile);
            try
            {
                File.WriteAllText(fullPath, txtEditor.Text);
                btnSaveEdits.Enabled = false;
                return true;
            }
            catch (Exception ex)
            {
                SetStatus("Could not save pending edits: " + ex.Message, Color.Firebrick);
                Logger.LogError("MsgTab", "Could not save pending edits: " + ex);
                MessageBox.Show(ex.Message, "Message repack", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void ConvertedListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (convertedListBox.SelectedItem == null) return;
            string selectedFile = convertedListBox.SelectedItem.ToString();
            string outFolder = SharedState.GetModsPath("cdn", "QGM");
            string fullPath = Path.Combine(outFolder, selectedFile);

            if (File.Exists(fullPath))
            {
                try
                {
                    txtEditor.Text = File.ReadAllText(fullPath);
                    btnSaveEdits.Enabled = false;
                    lblEditorHeader.Text = $" MESSAGE VIEWER / EDITOR — {selectedFile} ";
                    SetStatus("Loaded: " + selectedFile, QFG5Theme.ColorTextPrimary);
                }
                catch (Exception ex)
                {
                    txtEditor.Text = $"[Error reading file: {ex.Message}]";
                    SetStatus("Error loading: " + selectedFile, Color.Firebrick);
                }
            }
            else
            {
                txtEditor.Text = $"[File not found: {fullPath}]";
                SetStatus("File not found: " + fullPath, Color.Orange);
            }
        }

        private void BtnSaveEdits_Click(object sender, EventArgs e)
        {
            if (SaveCurrentEdits())
                SetStatus("Saved: " + convertedListBox.SelectedItem, Color.ForestGreen);
        }

        private void SetStatus(string text, Color color)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = color;
        }
    }
}
