using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using QFG5Extractor.Resources.Message;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace QFG5Extractor.Resources.Message
{
    public class MsgTab : UserControl
    {
        private QFG5PathField qgmFolderTextBox;
        private QFG5PathField outputFolderTextBox;
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

            SharedState.OnBaseDataPathChanged += UpdatePaths;
            if (!string.IsNullOrEmpty(SharedState.BaseDataPath))
            {
                UpdatePaths(SharedState.BaseDataPath);
            }
            SharedState.OnExtractionPathChanged += (p) => { outputFolderTextBox.Text = p; };
            if (!string.IsNullOrEmpty(SharedState.ExtractionPath)) outputFolderTextBox.Text = SharedState.ExtractionPath;
        }

        private void UpdatePaths(string path)
        {
            string qgmDir = Path.Combine(path, "cdn" + Path.DirectorySeparatorChar + "QGM");
            if (!Directory.Exists(qgmDir)) qgmDir = Path.Combine(path, "cdn" + Path.DirectorySeparatorChar + "qgm");
            if (Directory.Exists(qgmDir)) qgmFolderTextBox.Text = qgmDir;
        }

        private void InitializeComponent()
        {
            ToolTip toolTip = new ToolTip();

            QFG5TableLayoutPanel mainLayout = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // Collapsible QGM and output paths
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Center: Split view (List + Editor)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));  // Bottom: 2-Row Footer

            // --- Top Section: 2 Symmetric Path Rows (QGM Path, Output Path) ---
            QFG5TableLayoutPanel topTable = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 8)
            };
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, QFG5Layout.LabelWidth)); // 100px
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));                    // Stretch
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));                    // Browse button width (95px)

            topTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Row 0: QGM Path
            topTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Row 1: Output Path

            // Row 0: QGM Path
            Label lblQgm = new Label
            {
                Text = "QGM Path:",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            qgmFolderTextBox = new QFG5PathField { Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 4, 3), TabIndex = 0, AccessibleName = "QGM Path" };
            toolTip.SetToolTip(qgmFolderTextBox, "Source folder containing Quest for Glory V .QGM message files (cdn/QGM)");

            QFG5Button btnBrowseQgm = new QFG5Button { Text = "Browse", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 0, 3), TabIndex = 1 };
            btnBrowseQgm.Click += (s, e) => qgmFolderTextBox.Text = BrowseFolder(qgmFolderTextBox.Text);
            toolTip.SetToolTip(btnBrowseQgm, "Browse for folder containing .QGM files");

            topTable.Controls.Add(lblQgm, 0, 0);
            topTable.Controls.Add(qgmFolderTextBox, 1, 0);
            topTable.Controls.Add(btnBrowseQgm, 2, 0);

            // Row 1: Output Path
            Label lblOutput = new Label
            {
                Text = "Output Path:",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            outputFolderTextBox = new QFG5PathField { Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 4, 3), TabIndex = 2, AccessibleName = "Output Path" };
            toolTip.SetToolTip(outputFolderTextBox, "Destination folder for extracted .TXT message files");

            QFG5Button btnBrowseOut = new QFG5Button { Text = "Browse", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 0, 3), TabIndex = 3 };
            btnBrowseOut.Click += (s, e) => outputFolderTextBox.Text = BrowseFolder(outputFolderTextBox.Text);
            toolTip.SetToolTip(btnBrowseOut, "Browse for destination output directory");

            topTable.Controls.Add(lblOutput, 0, 1);
            topTable.Controls.Add(outputFolderTextBox, 1, 1);
            topTable.Controls.Add(btnBrowseOut, 2, 1);

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
            txtEditor.Text = "No message file selected.\n\nRun 'Extract All' to convert .QGM archives, then select a file from the list to view and edit its dialogue.";

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
                Text = "Ready — Click 'Extract All' to convert QGM message files.",
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
            toolTip.SetToolTip(repackButton, "Repack edited message files back into .QGM format (under development)");

            runButton = new QFG5Button { Text = "Extract All", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(2, 1, 0, 1), TabIndex = 8 };
            runButton.Click += RunButtonClick;
            toolTip.SetToolTip(runButton, "Extract all .QGM message files into editable .TXT files");

            actionButtonsPanel.Controls.Add(repackButton, 0, 0);
            actionButtonsPanel.Controls.Add(runButton, 1, 0);

            footerTable.Controls.Add(fileInfoPanel, 0, 1);
            footerTable.Controls.Add(actionButtonsPanel, 1, 1);

            // Assemble main layout
            mainLayout.Controls.Add(new QFG5CollapsibleSection("MESSAGE FOLDERS", topTable, 110), 0, 0);
            mainLayout.Controls.Add(msgSplit, 0, 1);
            mainLayout.Controls.Add(footerTable, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private static string BrowseFolder(string currentPath)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                if (!string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath)) dialog.SelectedPath = currentPath;
                if (dialog.ShowDialog() == DialogResult.OK) return dialog.SelectedPath;
            }
            return currentPath;
        }

        private void RunButtonClick(object sender, EventArgs e)
        {
            string qgmFolder = qgmFolderTextBox.Text.Trim();
            string outputFolder = outputFolderTextBox.Text.Trim();

            if (!Directory.Exists(qgmFolder) || !Directory.Exists(outputFolder))
            {
                MessageBox.Show("Please ensure both paths exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            runButton.Enabled = false;
            btnSaveEdits.Enabled = false;
            convertedListBox.Items.Clear();
            txtEditor.Text = "Extracting messages...";
            lblFileCount.Text = "(0 files)";
            SetStatus("Running extraction...", Color.RoyalBlue);

            int converted = 0;
            int failed = 0;

            try
            {
                string[] allFiles = Directory.GetFiles(qgmFolder, "*.qgm");
                if (allFiles.Length == 0) allFiles = Directory.GetFiles(qgmFolder, "*.QGM");

                if (allFiles.Length == 0)
                {
                    SetStatus("No QGM files found in selected path.", Color.Orange);
                    currentFileLabel.Text = "Status: Idle";
                    txtEditor.Text = "No .QGM message files found in source directory.";
                    return;
                }

                progressBar.Value = 0;
                int total = allFiles.Length;

                for (int i = 0; i < total; i++)
                {
                    string file = allFiles[i];
                    string baseName = Path.GetFileNameWithoutExtension(file);
                    currentFileLabel.Text = "Extracting: " + baseName;
                    Application.DoEvents();

                    try
                    {
                        MsgConverter.ExtractMsg(file, Path.Combine(outputFolder, baseName + ".txt"));
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

                SetStatus($"Done. Extracted {converted} files.", Color.ForestGreen);
                currentFileLabel.Text = "Status: Finished";
                lblFileCount.Text = $"({converted} files)";
                txtEditor.Text = $"Extraction complete ({converted} files converted).\n\nSelect a file on the left to view and edit dialogue.";
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

        private void RepackButtonClick(object sender, EventArgs e)
        {
            MessageBox.Show("Repack functionality for Message tab is under development.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ConvertedListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (convertedListBox.SelectedItem == null) return;
            string selectedFile = convertedListBox.SelectedItem.ToString();
            string outFolder = outputFolderTextBox.Text.Trim();
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
            if (convertedListBox.SelectedItem == null) return;
            string selectedFile = convertedListBox.SelectedItem.ToString();
            string fullPath = Path.Combine(outputFolderTextBox.Text.Trim(), selectedFile);
            try
            {
                File.WriteAllText(fullPath, txtEditor.Text);
                SetStatus($"Saved: {selectedFile}", Color.ForestGreen);
                btnSaveEdits.Enabled = false;
            }
            catch (Exception ex)
            {
                SetStatus($"Save Failed: {ex.Message}", Color.Firebrick);
            }
        }

        private void SetStatus(string text, Color color)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = color;
        }
    }
}
