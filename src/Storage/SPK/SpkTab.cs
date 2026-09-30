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
        }

        private void InitializeComponent()
        {
            QFG5TableLayoutPanel mainLayout = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.RowHeight));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.RowHeight));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.ProgressRowHeight));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.StatusRowHeight));

            QFG5Button btnScan = new QFG5Button { Text = "Scan", Width = QFG5Layout.ButtonWidth, Height = 30 };
            btnScan.Click += (s, e) => LoadSpkFiles(spkFileTextBox.Text);

            QFG5Button btnBrowse;
            QFG5TableLayoutPanel row1 = QFG5Layout.CreatePathRow("Data Folder:", out spkFileTextBox, out btnBrowse, btnScan);
            spkFileTextBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) LoadSpkFiles(spkFileTextBox.Text); };
            btnBrowse.Click += (s, e) => {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog() == DialogResult.OK) {
                        spkFileTextBox.Text = fbd.SelectedPath;
                        LoadSpkFiles(fbd.SelectedPath);
                    }
            };

            runButton = new QFG5Button { Text = "Extract", Width = QFG5Layout.ButtonWidth, Height = 30 };
            runButton.Click += RunButton_Click;
            QFG5Button btnBrowseOut;
            QFG5TableLayoutPanel row2 = QFG5Layout.CreatePathRow("Output Path:", out outputFolderTextBox, out btnBrowseOut, runButton);
            btnBrowseOut.Click += (s, e) => {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog() == DialogResult.OK) outputFolderTextBox.Text = fbd.SelectedPath;
            };

            QFG5PreviewFrame listFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill, Margin = new Padding(2, 0, 2, 0) };
            filesListBox = new CheckedListBox {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                CheckOnClick = true
            };
            listFrame.Controls.Add(filesListBox);

            progressBar = new QFG5ProgressBar { Dock = DockStyle.Fill, Height = 25 };

            chkStandard = new CheckBox { Text = "Hierarchy", ForeColor = Color.Gray, Checked = true, AutoSize = true, Width = 100, Dock = DockStyle.Fill };
            statusLabel = new Label { Text = "Ready", ForeColor = Color.Gold };
            repackButton = new QFG5Button { Text = "Repack All", Width = 130, Height = 30 };
            repackButton.Click += RepackButton_Click;
            QFG5Button btnAll = new QFG5Button { Text = "All", Width = 50, Height = 30 };
            btnAll.Click += (s, e) => { for (int i = 0; i < filesListBox.Items.Count; i++) filesListBox.SetItemChecked(i, true); };
            QFG5Button btnNone = new QFG5Button { Text = "None", Width = 60, Height = 30 };
            btnNone.Click += (s, e) => { for (int i = 0; i < filesListBox.Items.Count; i++) filesListBox.SetItemChecked(i, false); };

            // Footer: hierarchy + status | All | None | Repack
            QFG5TableLayoutPanel statusRow = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1
            };
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));
            statusRow.Controls.Add(chkStandard, 0, 0);
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusRow.Controls.Add(statusLabel, 1, 0);
            statusRow.Controls.Add(btnAll, 2, 0);
            statusRow.Controls.Add(btnNone, 3, 0);
            statusRow.Controls.Add(repackButton, 4, 0);

            mainLayout.Controls.Add(row1, 0, 0);
            mainLayout.Controls.Add(row2, 0, 1);
            mainLayout.Controls.Add(listFrame, 0, 2);
            mainLayout.Controls.Add(progressBar, 0, 3);
            mainLayout.Controls.Add(statusRow, 0, 4);

            this.Controls.Add(mainLayout);
        }

        private void LoadSpkFiles(string folder)
        {
            filesListBox.Items.Clear();
            if (!Directory.Exists(folder)) return;

            try {
                if (!Directory.Exists(folder)) {
                    statusLabel.Text = "Folder does not exist.";
                    return;
                }
                string[] files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
                foreach (string file in files) {
                    if (file.EndsWith(".spk", StringComparison.OrdinalIgnoreCase)) {
                        filesListBox.Items.Add(Path.GetFileName(file), true);
                    }
                }
                statusLabel.Text = $"Found {filesListBox.Items.Count} SPK files.";
            } catch (Exception ex) {
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
            
            statusLabel.Text = $"Extracted {count} files.";
            runButton.Enabled = true;
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
            repackButton.Enabled = true;
            MessageBox.Show("Repack complete. Check the 'repacked' subfolder in your data directory.");
        }
    }
}
