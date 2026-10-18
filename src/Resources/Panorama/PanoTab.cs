using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using QFG5Extractor.Resources.Panorama;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace QFG5Extractor.Resources.Panorama
{
    public class PanoTab : UserControl
    {
        private QFG5PathField nodFolderTextBox;
        private QFG5PathField imgFolderTextBox;
        private QFG5PathField outputFolderTextBox;
        private QFG5Button runButton;
        private QFG5Button injectButton;
        private Label statusLabel;
        private Label currentFileLabel;
        private Label lblFileCount;
        private QFG5ProgressBar progressBar;
        private ListBox convertedListBox;
        private PictureBox previewPictureBox;

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
            ToolTip toolTip = new ToolTip();

            QFG5TableLayoutPanel mainLayout = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 146)); // Collapsible folder paths
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Center: Split view (List + Preview)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));  // Bottom: 2-Row Footer

            // --- Top Section: 3 Symmetric Path Rows (NOD, IMG, Output) ---
            QFG5TableLayoutPanel topTable = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                Margin = new Padding(0, 0, 0, 8)
            };
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, QFG5Layout.LabelWidth)); // 100px
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));                    // Stretch
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));                    // Browse button width (95px)

            topTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Row 0: NOD Path
            topTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Row 1: IMG Path
            topTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Row 2: Output Path

            // Row 0: NOD Path
            Label lblNod = new Label
            {
                Text = "NOD Path:",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            nodFolderTextBox = new QFG5PathField { Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 4, 3), TabIndex = 0, AccessibleName = "NOD Path" };
            toolTip.SetToolTip(nodFolderTextBox, "Source directory containing Quest for Glory V .NOD panorama files (cdn/NOD)");

            QFG5Button btnBrowseNod = new QFG5Button { Text = "Browse", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 0, 3), TabIndex = 1 };
            btnBrowseNod.Click += (s, e) => nodFolderTextBox.Text = BrowseFolder(nodFolderTextBox.Text);
            toolTip.SetToolTip(btnBrowseNod, "Browse for folder containing .NOD files");

            topTable.Controls.Add(lblNod, 0, 0);
            topTable.Controls.Add(nodFolderTextBox, 1, 0);
            topTable.Controls.Add(btnBrowseNod, 2, 0);

            // Row 1: IMG Path
            Label lblImg = new Label
            {
                Text = "IMG Path:",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            imgFolderTextBox = new QFG5PathField { Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 4, 3), TabIndex = 2, AccessibleName = "IMG Path" };
            toolTip.SetToolTip(imgFolderTextBox, "Source directory containing matching .IMG panorama files (cdn/IMG)");

            QFG5Button btnBrowseImg = new QFG5Button { Text = "Browse", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 0, 3), TabIndex = 3 };
            btnBrowseImg.Click += (s, e) => imgFolderTextBox.Text = BrowseFolder(imgFolderTextBox.Text);
            toolTip.SetToolTip(btnBrowseImg, "Browse for folder containing matching .IMG files");

            topTable.Controls.Add(lblImg, 0, 1);
            topTable.Controls.Add(imgFolderTextBox, 1, 1);
            topTable.Controls.Add(btnBrowseImg, 2, 1);

            // Row 2: Output Path
            Label lblOutput = new Label
            {
                Text = "Output Path:",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            outputFolderTextBox = new QFG5PathField { Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 4, 3), TabIndex = 4, AccessibleName = "Output Path" };
            toolTip.SetToolTip(outputFolderTextBox, "Destination folder for extracted panorama .BMP files or injected files");

            QFG5Button outBrowse = new QFG5Button { Text = "Browse", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 0, 3), TabIndex = 5 };
            outBrowse.Click += (s, e) => outputFolderTextBox.Text = BrowseFolder(outputFolderTextBox.Text);
            toolTip.SetToolTip(outBrowse, "Browse for destination output directory");

            topTable.Controls.Add(lblOutput, 0, 2);
            topTable.Controls.Add(outputFolderTextBox, 1, 2);
            topTable.Controls.Add(outBrowse, 2, 2);

            // --- Center Section: Split View (Extracted Panoramas List + Preview) ---
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                SplitterWidth = 6,
                Margin = new Padding(0, 0, 0, 6)
            };
            QFG5Layout.SetSplitterRatioOnce(split, 0.35);

            // Panel 1: Extracted Panoramas List Frame
            QFG5PreviewFrame listFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            Panel listContent = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            Label lblListHeader = new Label
            {
                Text = " EXTRACTED PANORAMAS ",
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
            toolTip.SetToolTip(convertedListBox, "List of extracted panorama .BMP images. Click to preview.");

            listContent.Controls.Add(convertedListBox);
            listContent.Controls.Add(lblListHeader);
            listFrame.Controls.Add(listContent);
            split.Panel1.Controls.Add(listFrame);

            // Panel 2: Panorama Preview Frame
            QFG5PreviewFrame previewFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            Panel previewContent = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            Label lblPreviewHeader = new Label
            {
                Text = " PANORAMA PREVIEW ",
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
                    string emptyMsg = "No panorama preview selected.\n\nRun extraction and select a .BMP file from the list to preview.";
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    using (var brush = new SolidBrush(QFG5Theme.ColorTextMuted))
                    {
                        pe.Graphics.DrawString(emptyMsg, QFG5Theme.BodyFont, brush, previewPictureBox.ClientRectangle, sf);
                    }
                }
            };
            toolTip.SetToolTip(previewPictureBox, "Preview of the selected panorama image");

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
                Margin = new Padding(0, 4, 0, 0)
            };
            footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); // Left status/file info
            footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));  // Right progress / action buttons

            footerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Row 0: Status + Progress
            footerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Row 1: File Info + Actions

            // Row 0: High-Level Status + Progress Bar
            statusLabel = new Label
            {
                Text = "Ready — Click 'Run Extraction' to export panoramas.",
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
                AccessibleName = "Panorama extraction progress"
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
            actionButtonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f)); // Inject BMP
            actionButtonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f)); // Run Extraction

            injectButton = new QFG5Button { Text = "Inject BMP", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 1, 4, 1), TabIndex = 7 };
            injectButton.Click += InjectButtonClick;
            toolTip.SetToolTip(injectButton, "Inject an edited .BMP image back into .NOD and .IMG panorama files");

            runButton = new QFG5Button { Text = "Run Extraction", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(2, 1, 0, 1), TabIndex = 8 };
            runButton.Click += RunButtonClick;
            toolTip.SetToolTip(runButton, "Extract all .NOD and .IMG panorama pairs into .BMP images");

            actionButtonsPanel.Controls.Add(injectButton, 0, 0);
            actionButtonsPanel.Controls.Add(runButton, 1, 0);

            footerTable.Controls.Add(fileInfoPanel, 0, 1);
            footerTable.Controls.Add(actionButtonsPanel, 1, 1);

            // Assemble main layout
            mainLayout.Controls.Add(new QFG5CollapsibleSection("PANORAMA FOLDERS", topTable, 146), 0, 0);
            mainLayout.Controls.Add(split, 0, 1);
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
            string nodFolder = nodFolderTextBox.Text.Trim();
            string imgFolder = imgFolderTextBox.Text.Trim();
            string outputFolder = outputFolderTextBox.Text.Trim();

            if (!Directory.Exists(nodFolder) || !Directory.Exists(imgFolder) || !Directory.Exists(outputFolder))
            {
                MessageBox.Show("Please ensure all paths exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            runButton.Enabled = false;
            injectButton.Enabled = false;
            ClearPreview();
            convertedListBox.Items.Clear();
            lblFileCount.Text = "(0 files)";
            SetStatus("Running extraction...", Color.RoyalBlue);

            int converted = 0;
            int missing = 0;
            int failed = 0;
            int imgDirectoryEnumerations = 0;
            System.Diagnostics.Stopwatch extractionWatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                string[] allNodFiles = Directory.GetFiles(nodFolder, "*.nod");
                if (allNodFiles.Length == 0) allNodFiles = Directory.GetFiles(nodFolder, "*.NOD");

                if (allNodFiles.Length == 0)
                {
                    SetStatus("No NOD files found in selected path.", Color.Orange);
                    currentFileLabel.Text = "Status: Idle";
                    return;
                }

                progressBar.Value = 0;
                int total = allNodFiles.Length;
                imgDirectoryEnumerations++;
                string[] imgFiles = Directory.GetFiles(imgFolder);
                Dictionary<string, string> imgFilesByName =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string imgPath in imgFiles)
                {
                    string fileName = Path.GetFileName(imgPath);
                    if (!imgFilesByName.ContainsKey(fileName))
                        imgFilesByName.Add(fileName, imgPath);
                }

                foreach (string nodFile in allNodFiles)
                {
                    string baseName = Path.GetFileNameWithoutExtension(nodFile);
                    currentFileLabel.Text = "Extracting: " + baseName;
                    Application.DoEvents();

                    string imgFile;
                    imgFilesByName.TryGetValue(baseName + ".img", out imgFile);
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
                            string actualOutFile = outFile;
                            if (!File.Exists(actualOutFile))
                            {
                                string alt = Path.ChangeExtension(outFile, Path.GetExtension(outFile).ToUpperInvariant());
                                if (File.Exists(alt)) actualOutFile = alt;
                            }
                            convertedListBox.Items.Add(Path.GetFileName(actualOutFile));
                            lblFileCount.Text = $"({converted} of {total} files)";
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("PanoTab", ex.Message);
                            failed++;
                        }
                    }
                    progressBar.Value = (int)((float)(converted + missing + failed) / total * 100);
                }

                extractionWatch.Stop();
                Logger.LogPerf($"Pano extraction completed in {extractionWatch.ElapsedMilliseconds} ms; IMG directory enumerations: {imgDirectoryEnumerations}");
                SetStatus($"Done. Extracted: {converted}, missing IMG: {missing}, failed: {failed}", Color.ForestGreen);
                currentFileLabel.Text = "Status: Finished";
                lblFileCount.Text = $"({converted} files)";
                MessageBox.Show($"Extraction complete.\nConverted: {converted}\nMissing IMG: {missing}\nFailed: {failed}", "Pano Tab", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                extractionWatch.Stop();
                Logger.LogPerf($"Pano extraction failed after {extractionWatch.ElapsedMilliseconds} ms; IMG directory enumerations: {imgDirectoryEnumerations}");
                SetStatus("Error: " + ex.Message, Color.Firebrick);
                currentFileLabel.Text = "Status: Error";
                Logger.LogError("PanoTab", ex.Message);
            }
            finally
            {
                runButton.Enabled = true;
                injectButton.Enabled = true;
            }
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
            if (convertedListBox.SelectedItem == null)
            {
                ClearPreview();
                return;
            }

            string fileName = convertedListBox.SelectedItem.ToString();
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
                    SetStatus("Previewing: " + Path.GetFileName(fullPath), QFG5Theme.ColorTextPrimary);
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

        private string FindFileCaseInsensitive(string folder, string fileName)
        {
            if (!Directory.Exists(folder)) return null;
            string[] files = Directory.GetFiles(folder);
            return Array.Find(files, f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
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
