using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using QFG5Extractor.Resources.Panorama;
using QFG5Extractor.Storage.SPK;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace QFG5Extractor.Resources.Panorama
{
    public class PanoTab : UserControl
    {
        private QFG5Button runButton;
        private QFG5Button injectButton;
        private QFG5Button editButton;
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Center: Split view (List + Preview)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));  // Bottom: 2-Row Footer

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
            SharedState.OnPanoFilesChanged += PanoFilesChanged;
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
                    string emptyMsg = "No panorama preview selected.\n\nPanoramas load automatically when this tab opens.";
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
            footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));  // Right progress / action buttons

            footerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Row 0: Status + Progress
            footerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Row 1: File Info + Actions

            // Row 0: High-Level Status + Progress Bar
            statusLabel = new Label
            {
                Text = "Panorama files load automatically when this tab opens.",
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
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0)
            };
            actionButtonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f)); // Edit BMP
            actionButtonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f)); // Inject BMP
            actionButtonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f)); // Run Extraction

            injectButton = new QFG5Button { Text = "Inject BMP", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 1, 4, 1), TabIndex = 7 };
            injectButton.Click += InjectButtonClick;
            toolTip.SetToolTip(injectButton, "Inject an edited .BMP image back into .NOD and .IMG panorama files");

            editButton = new QFG5Button { Text = "Edit Selected", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 1, 4, 1), TabIndex = 7 };
            editButton.Enabled = false;
            editButton.Click += EditSelectedButtonClick;
            toolTip.SetToolTip(editButton, "Open the selected panorama bitmap in the Texture Editor.");

            runButton = new QFG5Button { Text = "Extract", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(2, 1, 0, 1), TabIndex = 8 };
            runButton.Click += RunButtonClick;
            toolTip.SetToolTip(runButton, "Re-extract all .NOD and .IMG panorama pairs into .BMP images");

            actionButtonsPanel.Controls.Add(editButton, 0, 0);
            actionButtonsPanel.Controls.Add(injectButton, 1, 0);
            actionButtonsPanel.Controls.Add(runButton, 2, 0);

            footerTable.Controls.Add(fileInfoPanel, 0, 1);
            footerTable.Controls.Add(actionButtonsPanel, 1, 1);

            // Assemble main layout
            mainLayout.Controls.Add(split, 0, 0);
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
            string nodFolder = SharedState.GetAssetDataPath("cdn", "NOD");
            string imgFolder = SharedState.GetAssetDataPath("cdn", "IMG");
            string outputFolder = SharedState.GetModsPath("cdn", "NOD");

            if (!DirectoryHasFiles(nodFolder, ".nod") || !DirectoryHasFiles(imgFolder, ".img"))
            {
                try
                {
                    AssetArchiveHelper.EnsureExtracted("CDN.SPK");
                    if (!DirectoryHasFiles(nodFolder, ".nod"))
                        nodFolder = SharedState.GetModsPath("cdn", "NOD");
                    if (!DirectoryHasFiles(imgFolder, ".img"))
                        imgFolder = SharedState.GetModsPath("cdn", "IMG");
                }
                catch (Exception ex)
                {
                    SetStatus("Unable to load panorama files: " + ex.Message, Color.Firebrick);
                    currentFileLabel.Text = "Status: Error";
                    Logger.LogError("PanoTab", ex.Message);
                    if (!automatic)
                        MessageBox.Show(ex.Message, "Panorama extraction", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (!Directory.Exists(nodFolder) || !Directory.Exists(imgFolder))
            {
                SetStatus("The configured source folders are missing. Check Settings.", Color.Firebrick);
                if (!automatic)
                    MessageBox.Show("The Game DATA Folder must contain cdn/NOD and cdn/IMG.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Directory.CreateDirectory(outputFolder);
            runButton.Enabled = false;
            injectButton.Enabled = false;
            editButton.Enabled = false;
            ClearPreview();
            if (automatic)
                LoadOutputFiles(outputFolder);
            else
                convertedListBox.Items.Clear();
            lblFileCount.Text = $"({convertedListBox.Items.Count} files)";
            SetStatus(automatic ? "Loading panorama images..." : "Running extraction...", Color.RoyalBlue);

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
                    if (automatic && convertedListBox.Items.Count > 0)
                        convertedListBox.SelectedIndex = 0;
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
                            string existingOutput = automatic ? FindOutputFile(outputFolder, baseName) : null;
                            if (existingOutput != null)
                            {
                                converted++;
                                progressBar.Value = (int)((float)(converted + missing + failed) / total * 100);
                                continue;
                            }

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
                lblFileCount.Text = $"({convertedListBox.Items.Count} files)";
                if (automatic && convertedListBox.Items.Count > 0)
                    convertedListBox.SelectedIndex = 0;
                if (!automatic)
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
                SharedState.NotifyPanoFileChanged(null);
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
                if (string.Equals(Path.GetExtension(file), ".bmp", StringComparison.OrdinalIgnoreCase))
                    convertedListBox.Items.Add(Path.GetFileName(file));
            }
            lblFileCount.Text = $"({convertedListBox.Items.Count} files)";
        }

        private string FindOutputFile(string folder, string baseName)
        {
            if (!Directory.Exists(folder)) return null;
            string expectedName = baseName + ".bmp";
            foreach (string file in Directory.GetFiles(folder))
            {
                if (string.Equals(Path.GetFileName(file), expectedName, StringComparison.OrdinalIgnoreCase))
                    return file;
            }
            return null;
        }

        private bool TryFindOriginalFiles(string baseName, out string originalNod, out string originalImg)
        {
            string nodFolder = SharedState.GetAssetDataPath("cdn", "NOD");
            string imgFolder = SharedState.GetAssetDataPath("cdn", "IMG");
            originalNod = FindFileCaseInsensitive(nodFolder, baseName + ".nod");
            originalImg = FindFileCaseInsensitive(imgFolder, baseName + ".img");

            if (string.IsNullOrEmpty(originalNod) || string.IsNullOrEmpty(originalImg))
            {
                string alternateNodFolder = SharedState.GetAssetDataPath("hdn", "gra");
                string alternateImgFolder = SharedState.GetAssetDataPath("hdn", "img");
                if (string.IsNullOrEmpty(originalNod))
                    originalNod = FindFileCaseInsensitive(alternateNodFolder, baseName + ".nod");
                if (string.IsNullOrEmpty(originalImg))
                    originalImg = FindFileCaseInsensitive(alternateImgFolder, baseName + ".img");
            }
            return !string.IsNullOrEmpty(originalNod) && !string.IsNullOrEmpty(originalImg);
        }

        private void EditSelectedButtonClick(object sender, EventArgs e)
        {
            if (convertedListBox.SelectedItem == null)
            {
                SetStatus("Select a panorama BMP to edit.", Color.Orange);
                return;
            }

            string fileName = convertedListBox.SelectedItem.ToString();
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string bmpFile = FindOutputFile(SharedState.GetModsPath("cdn", "NOD"), baseName);
            string originalNod;
            string originalImg;
            if (bmpFile == null || !TryFindOriginalFiles(baseName, out originalNod, out originalImg))
            {
                MessageBox.Show("Could not find the selected panorama BMP and its source NOD/IMG pair.",
                    "Panorama Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Bitmap editBitmap;
                using (Bitmap source = new Bitmap(bmpFile))
                    editBitmap = new Bitmap(source);
                SharedState.RequestEditPano(editBitmap, bmpFile, originalNod, originalImg);
                SharedState.RequestSwitchTab(6);
            }
            catch (Exception ex)
            {
                SetStatus("Could not open panorama for editing: " + ex.Message, Color.Firebrick);
                Logger.LogError("PanoTab", "Could not open panorama for editing: " + ex.Message);
                MessageBox.Show("Could not open panorama for editing: " + ex.Message,
                    "Panorama Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            string nodFolder = SharedState.GetAssetDataPath("cdn", "NOD");
            string imgFolder = SharedState.GetAssetDataPath("cdn", "IMG");
            string originalNod;
            string originalImg;

            if (!Directory.Exists(nodFolder) || !Directory.Exists(imgFolder) ||
                !TryFindOriginalFiles(baseName, out originalNod, out originalImg))
            {
                string msg = $"Could not find matching original .nod/.img files for {baseName}.\nSearched in:\n- {nodFolder}\n- {imgFolder}";
                if (!string.IsNullOrEmpty(SharedState.BaseDataPath)) msg += $"\n- {SharedState.BaseDataPath}/hdn/gra\n- {SharedState.BaseDataPath}/hdn/img";
                MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string nodOutputFolder = SharedState.GetModsPath("cdn", "NOD");
            string imgOutputFolder = SharedState.GetModsPath("cdn", "IMG");
            Directory.CreateDirectory(nodOutputFolder);
            Directory.CreateDirectory(imgOutputFolder);
            string outNod = Path.Combine(nodOutputFolder, baseName + ".nod");
            string outImg = Path.Combine(imgOutputFolder, baseName + ".img");

            try
            {
                PanoConverter.importToFiles(bmpFile, originalNod, originalImg, outNod, outImg);
                MessageBox.Show(
                    $"Panorama files were written to the Mods folders:\n" +
                    $"cdn/NOD/{baseName}.nod\ncdn/IMG/{baseName}.img\n\n" +
                    "The game will not use these files until you repack CDN.SPK in the SPK tab " +
                    "and install the resulting archive in the game's DATA folder.",
                    "Panorama files ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SharedState.NotifyPanoFileChanged(baseName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error injecting BMP: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("PanoTab", "Injection error: " + ex.Message);
            }
        }

        private void ConvertedListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            editButton.Enabled = convertedListBox.SelectedItem != null;
            if (convertedListBox.SelectedItem == null)
            {
                ClearPreview();
                return;
            }

            string fileName = convertedListBox.SelectedItem.ToString();
            string outputFolder = SharedState.GetModsPath("cdn", "NOD");
            string fullPath = Path.Combine(outputFolder, fileName);

            string resolvedPath = FindOutputFile(outputFolder, Path.GetFileNameWithoutExtension(fileName));
            if (resolvedPath != null) fullPath = resolvedPath;

            if (File.Exists(fullPath))
            {
                try
                {
                    ClearPreview();
                    using (Bitmap source = new Bitmap(fullPath))
                        previewPictureBox.Image = new Bitmap(source);
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

        private void PanoFilesChanged(string preferredBaseName)
        {
            string selectedFileName = string.IsNullOrEmpty(preferredBaseName)
                ? (convertedListBox.SelectedItem == null ? null : convertedListBox.SelectedItem.ToString())
                : preferredBaseName + ".bmp";
            LoadOutputFiles(SharedState.GetModsPath("cdn", "NOD"));
            if (selectedFileName != null)
                for (int i = 0; i < convertedListBox.Items.Count; i++)
                    if (string.Equals(convertedListBox.Items[i].ToString(), selectedFileName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        convertedListBox.SelectedIndex = i;
                        return;
                    }
            if (convertedListBox.Items.Count > 0 && convertedListBox.SelectedIndex < 0)
                convertedListBox.SelectedIndex = 0;
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
                SharedState.OnPanoFilesChanged -= PanoFilesChanged;
            }
            base.Dispose(disposing);
        }
    }
}
