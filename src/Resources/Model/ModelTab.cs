using QFG5Extractor.Core;
using QFG5Extractor.Resources.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using QFG5Extractor.Core.UI;

namespace QFG5Extractor.Resources.Model
{
    public class ModelTab : UserControl
    {
        private QFG5ComboBox operationComboBox;
        private QFG5PathField inputFolderTextBox;
        private QFG5PathField outputFolderTextBox;
        private QFG5Button runButton;
        private Label statusLabel;
        private QFG5ProgressBar progressBar;
        private Label currentFileLabel;
        private ListBox convertedListBox;
        private PictureBox previewPictureBox;

        public ModelTab()
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
            string mdlDir = Path.Combine(path, "hdnw" + Path.DirectorySeparatorChar + "mdl");
            if (!Directory.Exists(mdlDir)) mdlDir = Path.Combine(path, "hdnw" + Path.DirectorySeparatorChar + "MDL");
            if (Directory.Exists(mdlDir)) inputFolderTextBox.Text = mdlDir;
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

            // Row 1: Operation + Run
            QFG5TableLayoutPanel row1 = new QFG5TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            row1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, QFG5Layout.LabelWidth));
            row1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, QFG5Layout.ButtonWidth));
            row1.Controls.Add(new Label { Text = "Operation:", ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            operationComboBox = new QFG5ComboBox { Dock = DockStyle.Fill, Height = 38 };
            operationComboBox.ComboBox.Items.AddRange(new object[] { "Export BMP (MDL -> BMP)", "Import BMP (BMP -> MDL)", "Export HAK (MDL -> HAK)", "Import HAK (HAK -> MDL)" });
            operationComboBox.ComboBox.SelectedIndex = 0;
            row1.Controls.Add(operationComboBox, 1, 0);
            runButton = new QFG5Button { Text = "RUN", Dock = DockStyle.Fill, Height = 30 };
            runButton.Click += RunButton_Click;
            row1.Controls.Add(runButton, 2, 0);

            // Row 2: Folders
            QFG5TableLayoutPanel advTable = new QFG5TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            advTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            advTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            QFG5Button btnBrowseIn;
            QFG5TableLayoutPanel inRow = QFG5Layout.CreatePathRow("Input Path:", out inputFolderTextBox, out btnBrowseIn);
            btnBrowseIn.Click += (s, e) => { using (var fbd = new FolderBrowserDialog()) if (fbd.ShowDialog() == DialogResult.OK) inputFolderTextBox.Text = fbd.SelectedPath; };
            // Soften label color for secondary paths
            foreach (Control c in inRow.Controls) if (c is Label) c.ForeColor = Color.Gray;

            QFG5Button btnBrowseOut;
            QFG5TableLayoutPanel outRow = QFG5Layout.CreatePathRow("Output Path:", out outputFolderTextBox, out btnBrowseOut);
            btnBrowseOut.Click += (s, e) => { using (var fbd = new FolderBrowserDialog()) if (fbd.ShowDialog() == DialogResult.OK) outputFolderTextBox.Text = fbd.SelectedPath; };
            foreach (Control c in outRow.Controls) if (c is Label) c.ForeColor = Color.Gray;

            advTable.Controls.Add(inRow, 0, 0);
            advTable.Controls.Add(outRow, 0, 1);

            // Row 3: Content
            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };
            QFG5Layout.SetSplitterRatioOnce(split, 0.35);

            QFG5PreviewFrame listFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            convertedListBox = new ListBox { Dock = DockStyle.Fill, BackColor = Color.Black, ForeColor = Color.White, BorderStyle = BorderStyle.None };
            convertedListBox.SelectedIndexChanged += ConvertedListBox_SelectedIndexChanged;
            listFrame.Controls.Add(convertedListBox);
            split.Panel1.Controls.Add(listFrame);

            QFG5PreviewFrame previewFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            previewPictureBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
            previewFrame.Controls.Add(previewPictureBox);
            split.Panel2.Controls.Add(previewFrame);

            progressBar = new QFG5ProgressBar { Dock = DockStyle.Fill, Height = 18 };

            currentFileLabel = new Label { Text = "Ready", ForeColor = Color.White, AutoSize = true, Margin = new Padding(0, 5, 20, 0) };
            statusLabel = new Label { Text = "Select mode and click Run.", ForeColor = Color.Gold, AutoSize = true, Margin = new Padding(0, 5, 0, 0) };
            QFG5FlowLayoutPanel statusRow = new QFG5FlowLayoutPanel { Dock = DockStyle.Fill };
            statusRow.Controls.Add(currentFileLabel);
            statusRow.Controls.Add(statusLabel);

            mainLayout.Controls.Add(row1, 0, 0);
            mainLayout.Controls.Add(advTable, 0, 1);
            mainLayout.Controls.Add(split, 0, 2);
            mainLayout.Controls.Add(progressBar, 0, 3);
            mainLayout.Controls.Add(statusRow, 0, 4);

            this.Controls.Add(mainLayout);
        }

        private void RunButton_Click(object sender, EventArgs e)
        {
            // Implementation preserved but updated to use new fields
            string inputFolder = inputFolderTextBox.Text.Trim();
            string outputFolder = outputFolderTextBox.Text.Trim();
            if (!Directory.Exists(inputFolder) || !Directory.Exists(outputFolder)) {
                MessageBox.Show("Invalid paths."); return;
            }

            runButton.Enabled = false;
            convertedListBox.Items.Clear();
            SetStatus("Running...", Color.RoyalBlue);
            
            int op = operationComboBox.ComboBox.SelectedIndex;
            
            try {
                bool exportBitmap = (op == 0);
                bool importBitmap = (op == 1);
                bool exportMesh   = (op == 2);
                bool importMesh   = (op == 3);

                string[] allFiles = Directory.GetFiles(inputFolder);
                List<string> workingFiles = new List<string>();

                foreach (string f in allFiles) {
                    if (exportBitmap || exportMesh) {
                        if (f.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase)) workingFiles.Add(f);
                    } else if (importBitmap) {
                        if (f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)) workingFiles.Add(f);
                    } else if (importMesh) {
                        if (f.EndsWith(".hak", StringComparison.OrdinalIgnoreCase)) workingFiles.Add(f);
                    }
                }

                if (workingFiles.Count == 0) {
                    SetStatus("No applicable files found.", Color.Orange);
                    return;
                }
                
                progressBar.Value = 0;
                int count = 0;
                List<string> processedBases = new List<string>();

                foreach (string inputFile in workingFiles) {
                    string baseName = Path.GetFileNameWithoutExtension(inputFile);
                    string originalMdlName = baseName;
                    
                    int firstDash = baseName.IndexOf('-');
                    if (firstDash > 0) originalMdlName = baseName.Substring(0, firstDash);

                    if (importBitmap || importMesh) {
                        if (processedBases.Contains(originalMdlName.ToLower())) continue;
                        processedBases.Add(originalMdlName.ToLower());
                    }

                    currentFileLabel.Text = "Processing: " + originalMdlName;
                    Application.DoEvents();
                    
                    string outputFile = "";
                    string converterInputFile = inputFile;

                    if (exportBitmap) outputFile = Path.Combine(outputFolder, baseName + ".bmp");
                    else if (exportMesh) outputFile = Path.Combine(outputFolder, baseName + ".hak");
                    else if (importBitmap || importMesh) {
                        converterInputFile = Path.Combine(inputFolder, originalMdlName + (importBitmap ? ".bmp" : ".hak"));
                        outputFile = Path.Combine(outputFolder, originalMdlName + ".mdl");
                    }

                    try {
                        ModelConverter.importOrExportBitmapOrMesh(converterInputFile, outputFile, exportBitmap, importBitmap, exportMesh, importMesh);
                        count++;
                        convertedListBox.Items.Add(Path.GetFileName(outputFile));
                    } catch (Exception ex) {
                        Logger.LogError("ModelTab", $"Error processing {originalMdlName}: {ex.Message}");
                    }

                    progressBar.Value = (int)((float)count / workingFiles.Count * 100);
                }
                SetStatus($"Operation completed. Processed {count} files.", Color.ForestGreen);
            } catch (Exception ex) {
                SetStatus("Error: " + ex.Message, Color.Firebrick);
                Logger.LogError("ModelTab", ex.Message);
            } finally { runButton.Enabled = true; }
        }

        private void ConvertedListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (convertedListBox.SelectedItem == null) return;
            string fileName = convertedListBox.SelectedItem.ToString();
            // Only preview BMP files
            if (!fileName.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("Preview only available for BMP exports.", Color.Gray);
                return;
            }
            string fullPath = Path.Combine(outputFolderTextBox.Text, fileName);
            if (File.Exists(fullPath))
            {
                try
                {
                    if (previewPictureBox.Image != null) previewPictureBox.Image.Dispose();
                    previewPictureBox.Image = Image.FromFile(fullPath);
                    SetStatus("Previewing: " + fileName, Color.White);
                }
                catch (Exception ex)
                {
                    SetStatus("Preview error: " + ex.Message, Color.Firebrick);
                }
            }
            else
            {
                SetStatus("File not found: " + fullPath, Color.Orange);
            }
        }

        private void SetStatus(string text, Color color) { statusLabel.Text = text; statusLabel.ForeColor = color; }
    }
}
