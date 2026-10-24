using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using QFG5Extractor.Resources.Model;
using QFG5Extractor.Storage.SPK;
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
        private QFG5Button runButton;
        private Label statusLabel;
        private QFG5ProgressBar progressBar;
        private Label currentFileLabel;
        private Label lblFileCount;
        private ListBox convertedListBox;
        private ListView thumbnailListView;
        private ImageList thumbnailImages;
        private Label thumbnailEmptyLabel;
        private Button listViewButton;
        private Button thumbnailViewButton;
        private PictureBox previewPictureBox;
        private bool syncingViews;

        public ModelTab()
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
                RowCount = 3,
                BackColor = QFG5Theme.ColorBgDark,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); // Operation selector
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Center: Content split (list + preview)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));  // Bottom: 2-row footer (progress + status)

            // --- Top Section: Model operation row ---
            QFG5TableLayoutPanel topTable = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(6, 4, 6, 4),
                BackColor = QFG5Theme.ColorSurface
            };
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, QFG5Layout.LabelWidth)); // 100px
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));                    // Stretch
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));                    // Action button width (95px)

            topTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            // Operation + RUN
            Label lblOperation = new Label
            {
                Text = "Operation:",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            operationComboBox = new QFG5ComboBox { Dock = DockStyle.Fill, Height = 32, Margin = new Padding(0, 2, 4, 2), TabIndex = 0 };
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
            toolTip.SetToolTip(operationComboBox, "Select operation. Source and destination folders are derived from the DATA and Mods folders in Settings.");

            runButton = new QFG5Button { Text = "RUN", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 0, 3), TabIndex = 1 };
            runButton.Click += RunButton_Click;
            toolTip.SetToolTip(runButton, "Run the selected operation using the configured DATA and Mods folders");

            topTable.Controls.Add(lblOperation, 0, 0);
            topTable.Controls.Add(operationComboBox, 1, 0);
            topTable.Controls.Add(runButton, 2, 0);

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

            FlowLayoutPanel viewToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 30,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(4, 2, 0, 0),
                BackColor = QFG5Theme.ColorSurface
            };
            listViewButton = new Button
            {
                Text = "List",
                Width = 62,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                ForeColor = QFG5Theme.ColorTextPrimary,
                BackColor = QFG5Theme.ColorSurface
            };
            thumbnailViewButton = new Button
            {
                Text = "Thumbs",
                Width = 72,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                ForeColor = QFG5Theme.ColorTextPrimary,
                BackColor = QFG5Theme.ColorSurface
            };
            listViewButton.Click += (s, e) => SetFileView(false);
            thumbnailViewButton.Click += (s, e) => SetFileView(true);
            viewToolbar.Controls.Add(listViewButton);
            viewToolbar.Controls.Add(thumbnailViewButton);

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

            thumbnailImages = new ImageList
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(96, 72),
                TransparentColor = Color.Transparent
            };
            thumbnailListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.LargeIcon,
                LargeImageList = thumbnailImages,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                BorderStyle = BorderStyle.None,
                MultiSelect = false,
                HideSelection = false,
                Visible = false
            };
            thumbnailListView.SelectedIndexChanged += ThumbnailListView_SelectedIndexChanged;
            toolTip.SetToolTip(thumbnailListView, "BMP texture thumbnails. Select one to preview it.");

            thumbnailEmptyLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Thumbnails are available for BMP exports.",
                ForeColor = QFG5Theme.ColorTextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            listContent.Controls.Add(convertedListBox);
            listContent.Controls.Add(thumbnailListView);
            listContent.Controls.Add(thumbnailEmptyLabel);
            listContent.Controls.Add(viewToolbar);
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
                    string emptyMsg = "No texture preview selected.\n\nModel textures load automatically when this tab opens.";
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
            mainLayout.Controls.Add(topTable, 0, 0);
            mainLayout.Controls.Add(split, 0, 1);
            mainLayout.Controls.Add(footerTable, 0, 2);

            this.Controls.Add(mainLayout);
        }

        public void AutoLoad()
        {
            if (operationComboBox.ComboBox.SelectedIndex == 0)
                RunOperation(true);
            else
            {
                string inputFolder = SharedState.GetAssetDataPath("hdnw", "mdl");
                if (!DirectoryHasFiles(inputFolder, ".mdl"))
                {
                    try
                    {
                        AssetArchiveHelper.EnsureExtracted("HDNW.SPK");
                    }
                    catch (Exception ex)
                    {
                        SetStatus("Unable to load model files: " + ex.Message, Color.Firebrick);
                        currentFileLabel.Text = "Status: Error";
                        Logger.LogError("ModelTab", ex.Message);
                    }
                }
                LoadOutputFiles(SharedState.GetModsPath("hdnw", "mdl"), ".bmp");
            }
        }

        private void RunButton_Click(object sender, EventArgs e)
        {
            RunOperation(false);
        }

        private void RunOperation(bool automatic)
        {
            int op = operationComboBox.ComboBox.SelectedIndex;
            string inputFolder = op == 0 || op == 2
                ? SharedState.GetAssetDataPath("hdnw", "mdl")
                : SharedState.GetModsPath("hdnw", "mdl");
            string outputFolder = SharedState.GetModsPath("hdnw", "mdl");

            if (op == 0 && !DirectoryHasFiles(inputFolder, ".mdl"))
            {
                try
                {
                    AssetArchiveHelper.EnsureExtracted("HDNW.SPK");
                    inputFolder = SharedState.GetModsPath("hdnw", "mdl");
                }
                catch (Exception ex)
                {
                    SetStatus("Unable to load model files: " + ex.Message, Color.Firebrick);
                    currentFileLabel.Text = "Status: Error";
                    Logger.LogError("ModelTab", ex.Message);
                    if (!automatic)
                        MessageBox.Show(ex.Message, "Model extraction", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (op == 1 || op == 3)
                Directory.CreateDirectory(inputFolder);
            if (!Directory.Exists(inputFolder))
            {
                SetStatus("The configured source folder is missing. Check Settings.", Color.Firebrick);
                if (!automatic)
                    MessageBox.Show("The configured source folder is missing. Check the Game DATA and Mods folders in Settings.");
                return;
            }
            Directory.CreateDirectory(outputFolder);

            runButton.Enabled = false;
            ClearPreview();
            if (automatic)
                LoadOutputFiles(outputFolder, ".bmp");
            else
                convertedListBox.Items.Clear();
            lblFileCount.Text = $"({convertedListBox.Items.Count} files)";
            SetStatus(automatic ? "Loading model textures..." : "Running batch conversion...", Color.RoyalBlue);

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
                    if (automatic && convertedListBox.Items.Count > 0)
                        convertedListBox.SelectedIndex = 0;
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

                    if (automatic && exportBitmap)
                    {
                        string existingOutput = FindOutputFile(outputFolder, baseName, ".bmp");
                        if (existingOutput != null)
                        {
                            count++;
                            progressBar.Value = (int)((float)count / workingFiles.Count * 100);
                            continue;
                        }
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
                lblFileCount.Text = $"({convertedListBox.Items.Count} files)";
                if (automatic && convertedListBox.Items.Count > 0)
                    convertedListBox.SelectedIndex = 0;
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

        private bool DirectoryHasFiles(string folder, string extension)
        {
            return Directory.Exists(folder) &&
                Array.Exists(Directory.GetFiles(folder), file =>
                    string.Equals(Path.GetExtension(file), extension, StringComparison.OrdinalIgnoreCase));
        }

        private void LoadOutputFiles(string folder, string extension)
        {
            convertedListBox.Items.Clear();
            thumbnailListView.Items.Clear();
            thumbnailImages.Images.Clear();
            if (!Directory.Exists(folder)) return;

            string[] files = Directory.GetFiles(folder);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                if (string.Equals(Path.GetExtension(file), extension, StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = Path.GetFileName(file);
                    convertedListBox.Items.Add(fileName);
                    if (string.Equals(Path.GetExtension(file), ".bmp", StringComparison.OrdinalIgnoreCase))
                        AddThumbnail(file, fileName);
                }
            }
            lblFileCount.Text = $"({convertedListBox.Items.Count} files)";
        }

        private void AddThumbnail(string filePath, string fileName)
        {
            try
            {
                using (Image source = Image.FromFile(filePath))
                using (Image thumbnail = source.GetThumbnailImage(96, 72, () => false, IntPtr.Zero))
                {
                    Bitmap thumbnailCopy = new Bitmap(thumbnail);
                    thumbnailImages.Images.Add(fileName, thumbnailCopy);
                    // Don't dispose thumbnailCopy here - ImageList manages the image lifetime
                }
                thumbnailListView.Items.Add(new ListViewItem(fileName, fileName));
            }
            catch (Exception ex)
            {
                Logger.LogError("ModelTab", "Could not create thumbnail for " + fileName + ": " + ex.Message);
            }
        }

        private void SetFileView(bool showThumbnails)
        {
            convertedListBox.Visible = !showThumbnails;
            thumbnailListView.Visible = showThumbnails;
            thumbnailEmptyLabel.Visible = false;
            listViewButton.Enabled = showThumbnails;
            thumbnailViewButton.Enabled = !showThumbnails;

            if (showThumbnails)
            {
                string selectedFile = convertedListBox.SelectedItem == null
                    ? null
                    : convertedListBox.SelectedItem.ToString();
                bool hasThumbnails = thumbnailListView.Items.Count > 0;
                thumbnailListView.Visible = hasThumbnails;
                thumbnailEmptyLabel.Visible = !hasThumbnails;
                thumbnailListView.SelectedItems.Clear();
                if (!string.IsNullOrEmpty(selectedFile))
                {
                    foreach (ListViewItem item in thumbnailListView.Items)
                    {
                        if (string.Equals(item.Text, selectedFile, StringComparison.OrdinalIgnoreCase))
                        {
                            item.Selected = true;
                            item.EnsureVisible();
                            break;
                        }
                    }
                }
            }
            else if (thumbnailListView.SelectedItems.Count > 0)
            {
                thumbnailListView.Visible = true;
                thumbnailEmptyLabel.Visible = false;
                string selectedFile = thumbnailListView.SelectedItems[0].Text;
                int index = convertedListBox.Items.IndexOf(selectedFile);
                if (index >= 0)
                    convertedListBox.SelectedIndex = index;
            }
        }

        private void ThumbnailListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (syncingViews || thumbnailListView.SelectedItems.Count == 0)
                return;

            syncingViews = true;
            try
            {
                string selectedFile = thumbnailListView.SelectedItems[0].Text;
                int index = convertedListBox.Items.IndexOf(selectedFile);
                if (index >= 0)
                    convertedListBox.SelectedIndex = index;
            }
            finally
            {
                syncingViews = false;
            }
        }

        private string FindOutputFile(string folder, string baseName, string extension)
        {
            if (!Directory.Exists(folder)) return null;
            string expectedName = baseName + extension;
            foreach (string file in Directory.GetFiles(folder))
            {
                if (string.Equals(Path.GetFileName(file), expectedName, StringComparison.OrdinalIgnoreCase))
                    return file;
            }
            return null;
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

            string outputFolder = SharedState.GetModsPath("hdnw", "mdl");
            string fullPath = Path.Combine(outputFolder, fileName);
            if (!File.Exists(fullPath))
            {
                string upper = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(fileName) + ".BMP");
                string lower = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(fileName) + ".bmp");
                if (File.Exists(upper)) fullPath = upper;
                else if (File.Exists(lower)) fullPath = lower;
            }

            if (File.Exists(fullPath))
            {
                try
                {
                    ClearPreview();
                    using (Image source = Image.FromFile(fullPath))
                        previewPictureBox.Image = new Bitmap(source);
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
                if (thumbnailImages != null)
                    thumbnailImages.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
