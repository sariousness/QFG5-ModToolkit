using QFG5Extractor.Core;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using QFG5Extractor.Core.UI;

namespace QFG5Extractor.Storage.SPK
{
    public class SpkTab : UserControl
    {
        private QFG5PathField spkFileTextBox;
        private QFG5PathField outputFolderTextBox;
        private QFG5Button runButton;
        private QFG5Button repackButton;
        private CheckedListBox filesListBox;
        private Label statusLabel;
        private Label lblSelectionCount;
        private QFG5ProgressBar progressBar;
        private CheckBox chkStandard;

        public SpkTab()
        {
            Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;
            InitializeComponent();
            
            // Auto-load if path exists in SharedState
            if (!string.IsNullOrEmpty(SharedState.BaseDataPath)) {
                string dataPath = Path.Combine(SharedState.BaseDataPath, "hdnw");
                if (Directory.Exists(dataPath)) {
                    spkFileTextBox.Text = dataPath;
                    LoadSpkFiles(dataPath);
                }
            }
            if (!string.IsNullOrEmpty(SharedState.ExtractionPath)) {
                outputFolderTextBox.Text = SharedState.ExtractionPath;
            }

            SharedState.OnBaseDataPathChanged += (path) => {
                string dataPath = Path.Combine(path, "hdnw");
                if (Directory.Exists(dataPath)) {
                    spkFileTextBox.Text = dataPath;
                    LoadSpkFiles(dataPath);
                }
            };
            SharedState.OnExtractionPathChanged += (path) => {
                outputFolderTextBox.Text = path;
            };
        }

        private void InitializeComponent()
        {
            QFG5TableLayoutPanel mainLayout = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

            ToolTip toolTip = new ToolTip();

            // --- Row 1: Data Folder Path Row ---
            QFG5Button btnScan = new QFG5Button { Text = "Scan", Width = 95, Height = 30 };
            btnScan.Click += (s, e) => LoadSpkFiles(spkFileTextBox.Text);
            toolTip.SetToolTip(btnScan, "Scan the data folder for .SPK archives");

            QFG5Button btnBrowse;
            QFG5TableLayoutPanel row1 = QFG5Layout.CreatePathRow("Data Folder:", out spkFileTextBox, out btnBrowse, btnScan);
            row1.Margin = new Padding(0, 0, 0, 4);
            toolTip.SetToolTip(btnBrowse, "Browse for folder containing Quest for Glory V .SPK files");
            spkFileTextBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) LoadSpkFiles(spkFileTextBox.Text); };
            btnBrowse.Click += (s, e) => {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog() == DialogResult.OK) {
                        spkFileTextBox.Text = fbd.SelectedPath;
                        LoadSpkFiles(fbd.SelectedPath);
                    }
            };

            // --- Row 2: Output Path Row ---
            runButton = new QFG5Button { Text = "Extract", Width = 95, Height = 30 };
            runButton.Click += RunButton_Click;
            toolTip.SetToolTip(runButton, "Extract checked SPK archives to the output directory");

            QFG5Button btnBrowseOut;
            QFG5TableLayoutPanel row2 = QFG5Layout.CreatePathRow("Output Path:", out outputFolderTextBox, out btnBrowseOut, runButton);
            row2.Margin = new Padding(0, 0, 0, 6);
            toolTip.SetToolTip(btnBrowseOut, "Browse for destination folder to receive extracted files");
            btnBrowseOut.Click += (s, e) => {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog() == DialogResult.OK) outputFolderTextBox.Text = fbd.SelectedPath;
            };

            // --- Row 3: Result / Archive List Area ---
            QFG5PreviewFrame listFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            filesListBox = new CheckedListBox {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                BorderStyle = BorderStyle.None,
                CheckOnClick = true,
                IntegralHeight = false
            };
            filesListBox.ItemCheck += (s, e) => this.BeginInvoke((MethodInvoker)UpdateSelectionStatus);
            listFrame.Controls.Add(filesListBox);

            // --- Row 4: Two-Row Cohesive Footer ---
            progressBar = new QFG5ProgressBar { Height = 22, Dock = DockStyle.Fill, AccessibleName = "SPK archive operation progress" };
            statusLabel = new Label {
                Text = "Ready — Click 'Scan' to discover SPK archives.",
                ForeColor = QFG5Theme.ColorGold,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            chkStandard = new CheckBox {
                Text = "Preserve Subfolders",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                Checked = true,
                AutoSize = true,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat
            };
            toolTip.SetToolTip(chkStandard, "When checked, extracts each archive into its own named subfolder (e.g. out/001/...) and updates SharedState.");

            repackButton = new QFG5Button { Text = "Repack All", Width = 130, Height = 30 };
            repackButton.Click += RepackButton_Click;
            toolTip.SetToolTip(repackButton, "Repack modified files from the output path into new SPK archives");

            QFG5Button btnAll = new QFG5Button { Text = "All", Width = 60, Height = 28 };
            btnAll.Click += (s, e) => {
                for (int i = 0; i < filesListBox.Items.Count; i++) filesListBox.SetItemChecked(i, true);
                UpdateSelectionStatus();
            };
            toolTip.SetToolTip(btnAll, "Select all archives in the list");

            QFG5Button btnNone = new QFG5Button { Text = "None", Width = 60, Height = 28 };
            btnNone.Click += (s, e) => {
                for (int i = 0; i < filesListBox.Items.Count; i++) filesListBox.SetItemChecked(i, false);
                UpdateSelectionStatus();
            };
            toolTip.SetToolTip(btnNone, "Deselect all archives in the list");

            lblSelectionCount = new Label {
                Text = "(0 files)",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                AutoSize = true,
                Margin = new Padding(8, 6, 0, 0)
            };

            QFG5TableLayoutPanel footer = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Margin = new Padding(0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));

            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            // Footer Row 0: Hierarchy Checkbox (Left) | Status Label (Fill) | Progress Bar (Right)
            footer.Controls.Add(chkStandard, 0, 0);
            footer.Controls.Add(statusLabel, 1, 0);

            Panel progressWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 5) };
            progressWrapper.Controls.Add(progressBar);
            footer.Controls.Add(progressWrapper, 2, 0);

            // Footer Row 1: All/None Selection Controls (Left) | Empty Spacer (Fill) | Repack Button (Right)
            FlowLayoutPanel selPanel = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            selPanel.Controls.Add(btnAll);
            selPanel.Controls.Add(btnNone);
            selPanel.Controls.Add(lblSelectionCount);
            footer.Controls.Add(selPanel, 0, 1);

            footer.Controls.Add(new Panel { Dock = DockStyle.Fill }, 1, 1);

            Panel repackWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 2, 0, 2) };
            repackButton.Dock = DockStyle.Right;
            repackWrapper.Controls.Add(repackButton);
            footer.Controls.Add(repackWrapper, 2, 1);

            QFG5TableLayoutPanel pathsTable = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            pathsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pathsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            pathsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            pathsTable.Controls.Add(row1, 0, 0);
            pathsTable.Controls.Add(row2, 0, 1);
            QFG5CollapsibleSection pathSection = new QFG5CollapsibleSection("ARCHIVE FOLDERS", pathsTable, 106);

            mainLayout.Controls.Add(pathSection, 0, 0);
            mainLayout.Controls.Add(listFrame, 0, 1);
            mainLayout.Controls.Add(footer, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private void UpdateSelectionStatus()
        {
            if (lblSelectionCount == null) return;
            int checkedCount = filesListBox.CheckedItems.Count;
            int totalCount = filesListBox.Items.Count;
            if (totalCount > 0)
                lblSelectionCount.Text = $"({checkedCount} of {totalCount} selected)";
            else
                lblSelectionCount.Text = "(0 files)";
        }

        private void LoadSpkFiles(string folder)
        {
            filesListBox.Items.Clear();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) {
                statusLabel.Text = "Data folder does not exist.";
                UpdateSelectionStatus();
                return;
            }

            try {
                string[] files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
                foreach (string file in files) {
                    if (file.EndsWith(".spk", StringComparison.OrdinalIgnoreCase)) {
                        filesListBox.Items.Add(Path.GetFileName(file), true);
                    }
                }
                if (filesListBox.Items.Count > 0)
                    statusLabel.Text = $"Found {filesListBox.Items.Count} SPK files.";
                else
                    statusLabel.Text = "No .SPK files found in folder.";
                UpdateSelectionStatus();
            } catch (Exception ex) {
                statusLabel.Text = "Error scanning folder: " + ex.Message;
                MessageBox.Show("Error scanning folder: " + ex.Message);
            }
        }

        private void RunButton_Click(object sender, EventArgs e)
        {
            if (filesListBox.CheckedItems.Count == 0) {
                MessageBox.Show("Please select at least one SPK file to extract.");
                return;
            }

            string outDir = outputFolderTextBox.Text;
            if (string.IsNullOrEmpty(outDir)) {
                MessageBox.Show("Please select an output folder.");
                return;
            }

            runButton.Enabled = false;
            repackButton.Enabled = false;
            progressBar.Value = 0;
            int count = 0;
            int total = filesListBox.CheckedItems.Count;

            foreach (string fileName in filesListBox.CheckedItems) {
                try {
                    string fullPath = Path.Combine(spkFileTextBox.Text, fileName);
                    string target = chkStandard.Checked ? Path.Combine(outDir, Path.GetFileNameWithoutExtension(fileName)) : outDir;
                    
                    statusLabel.Text = "Extracting " + fileName + "...";
                    Application.DoEvents();
                    
                    SpkConverter.Extract(fullPath, target);
                    count++;

                    // Trigger global path detection if we are using standard hierarchy
                    if (chkStandard.Checked) {
                        SharedState.BaseDataPath = outDir;
                    }

                    progressBar.Value = (int)((float)count / total * 100);

                } catch (Exception ex) {
                    Logger.LogError("SpkTab", $"Error extracting {fileName}: {ex.Message}");
                }
            }
            
            statusLabel.Text = $"Extracted {count} files successfully.";
            runButton.Enabled = true;
            repackButton.Enabled = true;
            MessageBox.Show("Extraction complete.");
        }

        private void RepackButton_Click(object sender, EventArgs e)
        {
            if (filesListBox.CheckedItems.Count == 0) {
                MessageBox.Show("Please select at least one SPK file to repack.");
                return;
            }

            string modDir = outputFolderTextBox.Text;
            if (!Directory.Exists(modDir)) {
                MessageBox.Show("Point 'Output Path' to the folder containing modified files.");
                return;
            }

            runButton.Enabled = false;
            repackButton.Enabled = false;
            progressBar.Value = 0;
            int count = 0;
            int total = filesListBox.CheckedItems.Count;

            foreach (string fileName in filesListBox.CheckedItems) {
                try {
                    string originalPath = Path.Combine(spkFileTextBox.Text, fileName);
                    string sourceDir = chkStandard.Checked ? Path.Combine(modDir, Path.GetFileNameWithoutExtension(fileName)) : modDir;
                    
                    // We'll create a 'repacked' folder next to the original
                    string repackDir = Path.Combine(spkFileTextBox.Text, "repacked");
                    if (!Directory.Exists(repackDir)) Directory.CreateDirectory(repackDir);
                    string outPath = Path.Combine(repackDir, fileName);

                    statusLabel.Text = "Repacking " + fileName + "...";
                    Application.DoEvents();

                    SpkConverter.Repack(originalPath, sourceDir, outPath);
                    count++;
                    progressBar.Value = (int)((float)count / total * 100);
                } catch (Exception ex) {
                    Logger.LogError("SpkTab", $"Error repacking {fileName}: {ex.Message}");
                }
            }

            statusLabel.Text = $"Repacked {count} files into 'repacked' folder.";
            runButton.Enabled = true;
            repackButton.Enabled = true;
            MessageBox.Show("Repack complete. Check the 'repacked' subfolder in your data directory.");
        }
    }
}
