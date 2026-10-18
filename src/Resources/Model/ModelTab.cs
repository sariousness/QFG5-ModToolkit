using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using QFG5Extractor.Resources.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

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
        private Label lblFileCount;
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
            ToolTip toolTip = new ToolTip();

            QFG5TableLayoutPanel mainLayout = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = QFG5Theme.ColorBgDark,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 166)); // Collapsible paths and operation
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Center: Content split (list + preview)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));  // Bottom: 2-row footer (progress + status)

            // --- Top Section: 3 Symmetric Path & Operation Rows ---
            QFG5TableLayoutPanel topTable = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(6, 4, 6, 4),
                BackColor = QFG5Theme.ColorSurface
            };
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, QFG5Layout.LabelWidth)); // 100px
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));                    // Stretch
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));                    // Action button width (95px)

            topTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Row 0: Input Path
            topTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Row 1: Output Path
            topTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Row 2: Operation + Run

            // Row 0: Input Path
            Label lblInput = new Label
            {
                Text = "Input Path:",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            inputFolderTextBox = new QFG5PathField { Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 4, 3), TabIndex = 0, AccessibleName = "Input Path" };
            toolTip.SetToolTip(inputFolderTextBox, "Folder containing source Quest for Glory V MDL models, BMP textures, or HAK meshes");

            QFG5Button btnBrowseIn = new QFG5Button { Text = "Browse", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 0, 3), TabIndex = 1 };
            btnBrowseIn.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    if (fbd.ShowDialog() == DialogResult.OK) inputFolderTextBox.Text = fbd.SelectedPath;
                }
            };
            toolTip.SetToolTip(btnBrowseIn, "Browse for input directory containing MDL, BMP, or HAK files");

            topTable.Controls.Add(lblInput, 0, 0);
            topTable.Controls.Add(inputFolderTextBox, 1, 0);
            topTable.Controls.Add(btnBrowseIn, 2, 0);

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
            toolTip.SetToolTip(outputFolderTextBox, "Destination folder for converted or extracted files");

            QFG5Button btnBrowseOut = new QFG5Button { Text = "Browse", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 0, 3), TabIndex = 3 };
            btnBrowseOut.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    if (fbd.ShowDialog() == DialogResult.OK) outputFolderTextBox.Text = fbd.SelectedPath;
                }
            };
            toolTip.SetToolTip(btnBrowseOut, "Browse for destination folder to receive converted files");

            topTable.Controls.Add(lblOutput, 0, 1);
            topTable.Controls.Add(outputFolderTextBox, 1, 1);
            topTable.Controls.Add(btnBrowseOut, 2, 1);

            // Row 2: Operation + RUN
            Label lblOperation = new Label
            {
                Text = "Operation:",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            operationComboBox = new QFG5ComboBox { Dock = DockStyle.Fill, Height = 32, Margin = new Padding(0, 2, 4, 2), TabIndex = 4 };
            operationComboBox.ComboBox.Items.AddRange(new object[] {
                "Export BMP (MDL -> BMP)",
                "Import BMP (BMP -> MDL)",
                "Export HAK (MDL -> HAK)",
                "Import HAK (HAK -> MDL)"
            });
            operationComboBox.ComboBox.SelectedIndex = 0;
            operationComboBox.ComboBox.SelectedIndexChanged += (s, e) =>
            {
                if (runButton.Enabled)
                {
                    switch (operationComboBox.ComboBox.SelectedIndex)
                    {
                        case 0:
                            SetStatus("Export BMP — Extracts textures from .MDL to .BMP", QFG5Theme.ColorGold);
                            break;
                        case 1:
                            SetStatus("Import BMP — Injects .BMP textures into .MDL models", QFG5Theme.ColorGold);
                            break;
                        case 2:
                            SetStatus("Export HAK — Extracts 3D mesh geometry (.HAK) from .MDL", QFG5Theme.ColorGold);
                            break;
                        case 3:
                            SetStatus("Import HAK — Injects .HAK mesh geometry into .MDL models", QFG5Theme.ColorGold);
                            break;
                    }
                }
            };
            toolTip.SetToolTip(operationComboBox, "Select batch conversion operation for models, textures, or meshes");

            runButton = new QFG5Button { Text = "RUN", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 0, 3), TabIndex = 5 };
            runButton.Click += RunButton_Click;
            toolTip.SetToolTip(runButton, "Execute the selected batch operation on all matching files in the input folder");

            topTable.Controls.Add(lblOperation, 0, 2);
            topTable.Controls.Add(operationComboBox, 1, 2);
            topTable.Controls.Add(runButton, 2, 2);

            // --- Center Section: Split View (Converted Files List + Texture Preview) ---
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                SplitterWidth = 6,
                Margin = new Padding(0, 0, 0, 6)
            };
            QFG5Layout.SetSplitterRatioOnce(split, 0.35);

            // Panel 1: Converted Files List Frame
            QFG5PreviewFrame listFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            Panel listContent = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            Label lblListHeader = new Label
            {
                Text = " CONVERTED FILES ",
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
                TabIndex = 6
            };
            convertedListBox.SelectedIndexChanged += ConvertedListBox_SelectedIndexChanged;
            toolTip.SetToolTip(convertedListBox, "List of generated output files. Click a .bmp file to preview its texture.");

            listContent.Controls.Add(convertedListBox);
            listContent.Controls.Add(lblListHeader);
            listFrame.Controls.Add(listContent);
            split.Panel1.Controls.Add(listFrame);

            // Panel 2: Texture Preview Frame
            QFG5PreviewFrame previewFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            Panel previewContent = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            Label lblPreviewHeader = new Label
            {
                Text = " TEXTURE PREVIEW ",
                ForeColor = QFG5Theme.ColorGold,
                Font = QFG5Theme.SectionHeaderFont,
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = QFG5Theme.ColorSurface,
                Padding = new Padding(6, 0, 0, 0)
            };
            QFG5Theme.ApplyHeaderStroke(lblPreviewHeader);

            previewPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = QFG5Theme.ColorSurfaceRecessed
            };
            previewPictureBox.Paint += (s, pe) =>
            {
                if (previewPictureBox.Image == null)
                {
                    string emptyMsg = "No texture preview selected.\n\nRun 'Export BMP' and select a file to preview.";
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    using (var brush = new SolidBrush(QFG5Theme.ColorTextMuted))
                    {
                        pe.Graphics.DrawString(emptyMsg, QFG5Theme.BodyFont, brush, previewPictureBox.ClientRectangle, sf);
                    }
                }
            };
            toolTip.SetToolTip(previewPictureBox, "Preview of the selected .bmp texture");

            previewContent.Controls.Add(previewPictureBox);
            previewContent.Controls.Add(lblPreviewHeader);
            previewFrame.Controls.Add(previewContent);
            split.Panel2.Controls.Add(previewFrame);

            // --- Bottom Section: 2-Row Cohesive Footer ---
            QFG5TableLayoutPanel footerTable = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0, 6, 0, 0)
            };
            footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); // Left/Center status
            footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));  // Right progress/count

            footerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Row 0
            footerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Row 1

            // Row 0: High-Level Status + Progress Bar
            statusLabel = new Label
            {
                Text = "Ready — Select an operation and click RUN.",
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
                AccessibleName = "Model conversion progress"
            };

            footerTable.Controls.Add(statusLabel, 0, 0);
            footerTable.Controls.Add(progressBar, 1, 0);

            // Row 1: Detailed File Status + File Count Badge
            currentFileLabel = new Label
            {
                Text = "Status: Idle",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            lblFileCount = new Label
            {
                Text = "(0 files)",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight
            };

            footerTable.Controls.Add(currentFileLabel, 0, 1);
            footerTable.Controls.Add(lblFileCount, 1, 1);

            // Add sections to main layout
            mainLayout.Controls.Add(new QFG5CollapsibleSection("MODEL FOLDERS & OPERATION", topTable, 166), 0, 0);
            mainLayout.Controls.Add(split, 0, 1);
            mainLayout.Controls.Add(footerTable, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private void RunButton_Click(object sender, EventArgs e)
        {
            // Implementation preserved but updated to use new fields
            string inputFolder = inputFolderTextBox.Text.Trim();
            string outputFolder = outputFolderTextBox.Text.Trim();
            if (!Directory.Exists(inputFolder) || !Directory.Exists(outputFolder))
            {
                MessageBox.Show("Invalid paths.");
                return;
            }

            runButton.Enabled = false;
            ClearPreview();
            convertedListBox.Items.Clear();
            lblFileCount.Text = "(0 files)";
            SetStatus("Running batch conversion...", Color.RoyalBlue);

            int op = operationComboBox.ComboBox.SelectedIndex;

            try
            {
                bool exportBitmap = (op == 0);
                bool importBitmap = (op == 1);
                bool exportMesh   = (op == 2);
                bool importMesh   = (op == 3);

                string[] allFiles = Directory.GetFiles(inputFolder);
                List<string> workingFiles = new List<string>();

                foreach (string f in allFiles)
                {
                    if (exportBitmap || exportMesh)
                    {
                        if (f.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase)) workingFiles.Add(f);
                    }
                    else if (importBitmap)
                    {
                        if (f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)) workingFiles.Add(f);
                    }
                    else if (importMesh)
                    {
                        if (f.EndsWith(".hak", StringComparison.OrdinalIgnoreCase)) workingFiles.Add(f);
                    }
                }

                if (workingFiles.Count == 0)
                {
                    SetStatus("No applicable files found in input path.", Color.Orange);
                    currentFileLabel.Text = "Status: Idle";
                    return;
                }

                progressBar.Value = 0;
                int count = 0;
                List<string> processedBases = new List<string>();

                foreach (string inputFile in workingFiles)
                {
                    string baseName = Path.GetFileNameWithoutExtension(inputFile);
                    string originalMdlName = baseName;

                    int firstDash = baseName.IndexOf('-');
                    if (firstDash > 0) originalMdlName = baseName.Substring(0, firstDash);

                    if (importBitmap || importMesh)
                    {
                        if (processedBases.Contains(originalMdlName.ToLower())) continue;
                        processedBases.Add(originalMdlName.ToLower());
                    }

                    currentFileLabel.Text = "Processing: " + originalMdlName;
                    Application.DoEvents();

                    string outputFile = "";
                    string converterInputFile = inputFile;

                    if (exportBitmap) outputFile = Path.Combine(outputFolder, baseName + ".bmp");
                    else if (exportMesh) outputFile = Path.Combine(outputFolder, baseName + ".hak");
                    else if (importBitmap || importMesh)
                    {
                        converterInputFile = Path.Combine(inputFolder, originalMdlName + (importBitmap ? ".bmp" : ".hak"));
                        outputFile = Path.Combine(outputFolder, originalMdlName + ".mdl");
                    }

                    try
                    {
                        ModelConverter.importOrExportBitmapOrMesh(converterInputFile, outputFile, exportBitmap, importBitmap, exportMesh, importMesh);
                        count++;
                        string actualOutputFile = outputFile;
                        if (!File.Exists(actualOutputFile))
                        {
                            string altExt = Path.ChangeExtension(outputFile, Path.GetExtension(outputFile).ToUpperInvariant());
                            if (File.Exists(altExt)) actualOutputFile = altExt;
                        }
                        convertedListBox.Items.Add(Path.GetFileName(actualOutputFile));
                        lblFileCount.Text = $"({count} of {workingFiles.Count} files)";
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("ModelTab", $"Error processing {originalMdlName}: {ex.Message}");
                    }

                    progressBar.Value = (int)((float)count / workingFiles.Count * 100);
                }

                SetStatus($"Operation completed. Processed {count} files.", Color.ForestGreen);
                currentFileLabel.Text = "Status: Finished";
                lblFileCount.Text = $"({count} files)";
            }
            catch (Exception ex)
            {
                SetStatus("Error: " + ex.Message, Color.Firebrick);
                currentFileLabel.Text = "Status: Error";
                Logger.LogError("ModelTab", ex.Message);
            }
            finally
            {
                runButton.Enabled = true;
            }
        }

        private void ConvertedListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (convertedListBox.SelectedItem == null)
            {
                ClearPreview();
                return;
            }

            string fileName = convertedListBox.SelectedItem.ToString();

            // Only preview BMP files
            if (!fileName.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
            {
                ClearPreview();
                SetStatus("Preview only available for BMP exports.", QFG5Theme.ColorTextMuted);
                return;
            }

            string fullPath = Path.Combine(outputFolderTextBox.Text, fileName);
            if (!File.Exists(fullPath))
            {
                string upper = Path.Combine(outputFolderTextBox.Text, Path.GetFileNameWithoutExtension(fileName) + ".BMP");
                string lower = Path.Combine(outputFolderTextBox.Text, Path.GetFileNameWithoutExtension(fileName) + ".bmp");
                if (File.Exists(upper)) fullPath = upper;
                else if (File.Exists(lower)) fullPath = lower;
            }

            if (File.Exists(fullPath))
            {
                try
                {
                    ClearPreview();
                    previewPictureBox.Image = Image.FromFile(fullPath);
                    SetStatus("Previewing: " + fileName, QFG5Theme.ColorTextPrimary);
                }
                catch (Exception ex)
                {
                    ClearPreview();
                    SetStatus("Preview error: " + ex.Message, Color.Firebrick);
                }
            }
            else
            {
                ClearPreview();
                SetStatus("File not found: " + fullPath, Color.Orange);
            }
        }

        private void ClearPreview()
        {
            if (previewPictureBox.Image != null)
            {
                var oldImg = previewPictureBox.Image;
                previewPictureBox.Image = null;
                oldImg.Dispose();
            }
            previewPictureBox.Invalidate();
        }

        private void SetStatus(string text, Color color)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = color;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearPreview();
            }
            base.Dispose(disposing);
        }
    }
}
