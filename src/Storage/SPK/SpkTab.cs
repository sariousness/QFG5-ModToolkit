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
        private QFG5Button runButton;
        private QFG5Button repackButton;
        private CheckBox uppercaseOutputNamesCheckBox;
        private CheckedListBox filesListBox;
        private Label statusLabel;
        private Label lblSelectionCount;
        private QFG5ProgressBar progressBar;

        public SpkTab()
        {
            Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;
            InitializeComponent();
            SharedState.OnBaseDataPathChanged += path => LoadSpkFiles(SharedState.BaseDataPath);
            LoadSpkFiles(SharedState.BaseDataPath);
        }

        private void InitializeComponent()
        {
            QFG5TableLayoutPanel mainLayout = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

            ToolTip toolTip = new ToolTip();

            QFG5TableLayoutPanel actionBar = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 4)
            };
            actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            actionBar.Controls.Add(new Label
            {
                Text = "SPK archives are read from DATA; extracted files follow the Mods folder structure.",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            QFG5Button btnScan = new QFG5Button { Text = "Scan", Dock = DockStyle.Fill, Height = 30 };
            btnScan.Click += (s, e) => LoadSpkFiles(SharedState.BaseDataPath);
            toolTip.SetToolTip(btnScan, "Rescan the configured DATA folder for SPK archives");
            actionBar.Controls.Add(btnScan, 1, 0);

            runButton = new QFG5Button { Text = "Extract", Width = 95, Height = 30 };
            runButton.Click += RunButton_Click;
            toolTip.SetToolTip(runButton, "Extract checked SPK archives into their matching folders under Mods");
            actionBar.Controls.Add(runButton, 2, 0);

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
            filesListBox.ItemCheck += (s, e) =>
            {
                if (filesListBox.IsHandleCreated)
                    filesListBox.BeginInvoke((MethodInvoker)UpdateSelectionStatus);
                else
                    UpdateSelectionStatus();
            };
            listFrame.Controls.Add(filesListBox);

            // --- Row 4: Two-Row Cohesive Footer ---
            progressBar = new QFG5ProgressBar { Height = 22, Dock = DockStyle.Fill, AccessibleName = "SPK archive operation progress" };
            statusLabel = new Label {
                Text = "Ready — select archives to extract or repack.",
                ForeColor = QFG5Theme.ColorGold,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            repackButton = new QFG5Button { Text = "Repack All", Width = 130, Height = 30 };
            repackButton.Click += RepackButton_Click;
            toolTip.SetToolTip(repackButton, "Repack modified files from the matching Mods folder into Mods/repacked");

            uppercaseOutputNamesCheckBox = new CheckBox
            {
                Text = "Uppercase filename",
                Width = 130,
                Dock = DockStyle.Left,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.CaptionFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };
            toolTip.SetToolTip(uppercaseOutputNamesCheckBox,
                "Write repacked SPK filenames in uppercase in Mods/repacked.");

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

            // Footer Row 0: Status and progress
            footer.Controls.Add(statusLabel, 0, 0);
            footer.SetColumnSpan(statusLabel, 2);

            Panel progressWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 5) };
            progressWrapper.Controls.Add(progressBar);
            footer.Controls.Add(progressWrapper, 2, 0);

            // Footer Row 1: All/None Selection Controls (Left) | Repack Button (Right)
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
            repackWrapper.Controls.Add(uppercaseOutputNamesCheckBox);
            footer.Controls.Add(repackWrapper, 2, 1);

            mainLayout.Controls.Add(actionBar, 0, 0);
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
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                    if (string.Equals(Path.GetExtension(file), ".spk", StringComparison.OrdinalIgnoreCase))
                        filesListBox.Items.Add(Path.GetFileName(file), true);
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

            string sourceFolder = SharedState.BaseDataPath;
            string outDir = SharedState.ExtractionPath;
            if (!Directory.Exists(sourceFolder)) {
                MessageBox.Show("The configured Game DATA Folder does not exist.");
                return;
            }
            Directory.CreateDirectory(outDir);

            runButton.Enabled = false;
            repackButton.Enabled = false;
            progressBar.Value = 0;
            int count = 0;
            int total = filesListBox.CheckedItems.Count;

            foreach (string fileName in filesListBox.CheckedItems) {
                try {
                    string fullPath = Path.Combine(sourceFolder, fileName);
                    string target = SharedState.GetArchiveModsPath(fileName);
                    
                    statusLabel.Text = "Extracting " + fileName + "...";
                    Application.DoEvents();
                    
                    SpkConverter.Extract(fullPath, target);
                    count++;

                    progressBar.Value = (int)((float)count / total * 100);

                } catch (Exception ex) {
                    Logger.LogError("SpkTab", $"Error extracting {fileName}: {ex.Message}");
                }
            }
            
            statusLabel.Text = $"Extracted {count} files successfully.";
            if (count > 0) SharedState.NotifyGameDataExtracted();
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

            string sourceFolder = SharedState.BaseDataPath;
            string modDir = SharedState.ExtractionPath;
            if (!Directory.Exists(sourceFolder) || !Directory.Exists(modDir)) {
                MessageBox.Show("The configured Game DATA or Mods folder is missing.");
                return;
            }

            runButton.Enabled = false;
            repackButton.Enabled = false;
            progressBar.Value = 0;
            int count = 0;
            int total = filesListBox.CheckedItems.Count;

            foreach (string fileName in filesListBox.CheckedItems) {
                try {
                    string originalPath = Path.Combine(sourceFolder, fileName);
                    string sourceDir = SharedState.GetArchiveModsPath(fileName);
                    
                    string repackDir = Path.Combine(modDir, "repacked");
                    if (!Directory.Exists(repackDir)) Directory.CreateDirectory(repackDir);
                    string outputFileName = uppercaseOutputNamesCheckBox.Checked
                        ? fileName.ToUpperInvariant()
                        : fileName;
                    string outPath = Path.Combine(repackDir, outputFileName);

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
            MessageBox.Show("Repack complete. Check the Mods/repacked folder.");
        }
    }
}
