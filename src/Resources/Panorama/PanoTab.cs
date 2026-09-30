using QFG5Extractor.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using QFG5Extractor.Core.UI;

namespace QFG5Extractor.Resources.Panorama
{
    public class PanoTab : UserControl
    {
        private QFG5PathField nodFolderTextBox;
        private QFG5PathField imgFolderTextBox;
        private QFG5PathField outputFolderTextBox;
        private QFG5Button runButton;
        private Label statusLabel;
        private Label currentFileLabel;
        private QFG5ProgressBar progressBar;
        private ListBox convertedListBox;
        private PictureBox previewPictureBox;
        private QFG5Button injectButton;

        public PanoTab()
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
            string nodDir = Path.Combine(path, "cdn" + Path.DirectorySeparatorChar + "NOD");
            if (!Directory.Exists(nodDir)) nodDir = Path.Combine(path, "cdn" + Path.DirectorySeparatorChar + "nod");
            string imgDir = Path.Combine(path, "cdn" + Path.DirectorySeparatorChar + "IMG");
            if (!Directory.Exists(imgDir)) imgDir = Path.Combine(path, "cdn" + Path.DirectorySeparatorChar + "img");
            
            if (Directory.Exists(nodDir)) nodFolderTextBox.Text = nodDir;
            if (Directory.Exists(imgDir)) imgFolderTextBox.Text = imgDir;
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.ProgressRowHeight));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.StatusRowHeight));

            runButton = new QFG5Button { Text = "Run Extraction", Width = 130, Height = 30 };
            runButton.Click += RunButtonClick;
            injectButton = new QFG5Button { Text = "Inject BMP", Width = 105, Height = 30 };
            injectButton.Click += InjectButtonClick;

            QFG5Button outBrowse;
            QFG5TableLayoutPanel outRow = QFG5Layout.CreatePathRow("Output Path:", out outputFolderTextBox, out outBrowse, runButton, injectButton);
            outBrowse.Click += (s, e) => outputFolderTextBox.Text = BrowseFolder(outputFolderTextBox.Text);

            QFG5TableLayoutPanel advTable = new QFG5TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            advTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            advTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            QFG5Button btnBrowseNod;
            QFG5TableLayoutPanel nodRow = QFG5Layout.CreatePathRow("NOD Path:", out nodFolderTextBox, out btnBrowseNod);
            btnBrowseNod.Click += (s, e) => nodFolderTextBox.Text = BrowseFolder(nodFolderTextBox.Text);
            foreach (Control c in nodRow.Controls) if (c is Label) c.ForeColor = Color.Gray;

            QFG5Button btnBrowseImg;
            QFG5TableLayoutPanel imgRow = QFG5Layout.CreatePathRow("IMG Path:", out imgFolderTextBox, out btnBrowseImg);
            btnBrowseImg.Click += (s, e) => imgFolderTextBox.Text = BrowseFolder(imgFolderTextBox.Text);
            foreach (Control c in imgRow.Controls) if (c is Label) c.ForeColor = Color.Gray;

            advTable.Controls.Add(nodRow, 0, 0);
            advTable.Controls.Add(imgRow, 0, 1);

            QFG5TableLayoutPanel splitGrid = new QFG5TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            splitGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            splitGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

            QFG5PreviewFrame listFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            convertedListBox = new ListBox { Dock = DockStyle.Fill, BackColor = Color.Black, ForeColor = Color.White, BorderStyle = BorderStyle.None };
            convertedListBox.SelectedIndexChanged += ConvertedListBox_SelectedIndexChanged;
            listFrame.Controls.Add(convertedListBox);
            splitGrid.Controls.Add(listFrame, 0, 0);

            QFG5PreviewFrame previewFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            previewPictureBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
            previewFrame.Controls.Add(previewPictureBox);
            splitGrid.Controls.Add(previewFrame, 1, 0);

            progressBar = new QFG5ProgressBar { Dock = DockStyle.Fill, Height = 18 };

            QFG5FlowLayoutPanel statusRow = new QFG5FlowLayoutPanel { Dock = DockStyle.Fill };
            currentFileLabel = new Label { Text = "Ready", ForeColor = Color.White, AutoSize = true, Margin = new Padding(0, 5, 20, 0) };
            statusLabel = new Label { Text = "Select output and click Run.", ForeColor = Color.Gold, AutoSize = true, Margin = new Padding(0, 5, 0, 0) };
            statusRow.Controls.Add(currentFileLabel);
            statusRow.Controls.Add(statusLabel);

            mainLayout.Controls.Add(outRow, 0, 0);
            mainLayout.Controls.Add(advTable, 0, 1);
            mainLayout.Controls.Add(splitGrid, 0, 2);
            mainLayout.Controls.Add(progressBar, 0, 3);
            mainLayout.Controls.Add(statusRow, 0, 4);

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
            string nodFolder = nodFolderTextBox.Text.Trim();
            string imgFolder = imgFolderTextBox.Text.Trim();
            string outputFolder = outputFolderTextBox.Text.Trim();

            if (!Directory.Exists(nodFolder) || !Directory.Exists(imgFolder) || !Directory.Exists(outputFolder))
            {
                MessageBox.Show("Please ensure all paths exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            runButton.Enabled = false;
            convertedListBox.Items.Clear();
            SetStatus("Running...", Color.RoyalBlue);

            int converted = 0;
            int missing = 0;
            int failed = 0;

            try
            {
                string[] allNodFiles = Directory.GetFiles(nodFolder, "*.nod");
                if (allNodFiles.Length == 0) allNodFiles = Directory.GetFiles(nodFolder, "*.NOD");
                
                if (allNodFiles.Length == 0)
                {
                    SetStatus("No NOD files found.", Color.Orange);
                    runButton.Enabled = true;
                    return;
                }

                progressBar.Value = 0;
                int total = allNodFiles.Length;

                foreach (string nodFile in allNodFiles)
                {
                    string baseName = Path.GetFileNameWithoutExtension(nodFile);
                    currentFileLabel.Text = "Extracting: " + baseName;
                    Application.DoEvents();

                    string imgFile = FindFileCaseInsensitive(imgFolder, baseName + ".img");
                    if (string.IsNullOrEmpty(imgFile))
                    {
                        missing++;
                    }
                    else
                    {
                        try
                        {
                            string outFile = Path.Combine(outputFolder, baseName + ".bmp");
                            PanoConverter.exportFromFiles(nodFile, imgFile, outFile);
                            converted++;
                            convertedListBox.Items.Add(baseName + ".bmp");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("PanoTab", ex.Message);
                            failed++;
                        }
                    }
                    progressBar.Value = (float)(converted + missing + failed) / total * 100;
                }
                SetStatus($"Done. Extracted: {converted}, missing IMG: {missing}, failed: {failed}", Color.ForestGreen);
                MessageBox.Show($"Extraction complete.\nConverted: {converted}\nMissing IMG: {missing}\nFailed: {failed}", "Pano Tab", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Error: " + ex.Message, Color.Firebrick);
                Logger.LogError("PanoTab", ex.Message);
            }
            finally { runButton.Enabled = true; }
        }

        private void InjectButtonClick(object sender, EventArgs e)
        {
            string bmpFile = "";
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "BMP files (*.bmp)|*.bmp", Title = "Select Edited BMP" })
            {
                if (ofd.ShowDialog() != DialogResult.OK) return;
                bmpFile = ofd.FileName;
            }

            string baseName = Path.GetFileNameWithoutExtension(bmpFile);
            string nodFolder = nodFolderTextBox.Text.Trim();
            string imgFolder = imgFolderTextBox.Text.Trim();

            if (!Directory.Exists(nodFolder) || !Directory.Exists(imgFolder))
            {
                MessageBox.Show("Please select valid NOD and IMG folders first to find original files.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string originalNod = FindFileCaseInsensitive(nodFolder, baseName + ".nod");
            string originalImg = FindFileCaseInsensitive(imgFolder, baseName + ".img");

            if (string.IsNullOrEmpty(originalNod) || string.IsNullOrEmpty(originalImg))
            {
                // Try alternate locations using BaseDataPath
                if (!string.IsNullOrEmpty(SharedState.BaseDataPath))
                {
                    string alternateNodFolder = Path.Combine(SharedState.BaseDataPath, "hdn" + Path.DirectorySeparatorChar + "gra");
                    string alternateImgFolder = Path.Combine(SharedState.BaseDataPath, "hdn" + Path.DirectorySeparatorChar + "img");

                    if (string.IsNullOrEmpty(originalNod) && Directory.Exists(alternateNodFolder))
                        originalNod = FindFileCaseInsensitive(alternateNodFolder, baseName + ".nod");
                    
                    if (string.IsNullOrEmpty(originalImg) && Directory.Exists(alternateImgFolder))
                        originalImg = FindFileCaseInsensitive(alternateImgFolder, baseName + ".img");
                }
            }

            if (string.IsNullOrEmpty(originalNod) || string.IsNullOrEmpty(originalImg))
            {
                string msg = $"Could not find matching original .nod/.img files for {baseName}.\nSearched in:\n- {nodFolder}\n- {imgFolder}";
                if (!string.IsNullOrEmpty(SharedState.BaseDataPath)) msg += $"\n- {SharedState.BaseDataPath}/hdn/gra\n- {SharedState.BaseDataPath}/hdn/img";
                MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string outNod = Path.Combine(outputFolderTextBox.Text.Trim(), baseName + ".nod");
            string outImg = Path.Combine(outputFolderTextBox.Text.Trim(), baseName + ".img");

            try
            {
                PanoConverter.importToFiles(bmpFile, originalNod, originalImg, outNod, outImg);
                MessageBox.Show($"Successfully injected and saved to output folder:\n{baseName}.nod\n{baseName}.img", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error injecting BMP: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("PanoTab", "Injection error: " + ex.Message);
            }
        }

        private void ConvertedListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (convertedListBox.SelectedItem == null) return;
            string fullPath = Path.Combine(outputFolderTextBox.Text, convertedListBox.SelectedItem.ToString());
            if (File.Exists(fullPath))
            {
                if (previewPictureBox.Image != null) previewPictureBox.Image.Dispose();
                previewPictureBox.Image = Image.FromFile(fullPath);
            }
        }

        private string FindFileCaseInsensitive(string folder, string fileName)
        {
            if (!Directory.Exists(folder)) return null;
            string[] files = Directory.GetFiles(folder);
            return Array.Find(files, f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
        }

        private void SetStatus(string text, Color color) { statusLabel.Text = text; statusLabel.ForeColor = color; }
    }
}
