using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;

namespace QFG5Extractor.Resources.Gra
{
    public class GraTab : UserControl
    {
        private QFG5PathField txtInput;
        private QFG5PathField txtOutput;
        private DataGridView gridFiles;
        private PictureBox pbPreview;
        private QFG5ProgressBar progressBar;
        private Label lblStatus;
        private GraConverter converter;
        private Timer animTimer;
        private System.Collections.Generic.List<Bitmap> animFrames = new System.Collections.Generic.List<Bitmap>();
        private int currentFrameIdx = 0;
        private bool isPlaying = false;

        public GraTab()
        {
            converter = new GraConverter();
            animTimer = new Timer { Interval = 100 };
            animTimer.Tick += AnimTimer_Tick;
            InitializeComponent();

            SharedState.OnBaseDataPathChanged += (path) => {
                string graDir = Path.Combine(Path.Combine(path, "cdn"), "GRA");
                if (!Directory.Exists(graDir))
                    graDir = Path.Combine(Path.Combine(path, "cdn"), "gra");
                if (Directory.Exists(graDir)) {
                    txtInput.Text = graDir;
                    LoadFiles(graDir);
                }
            };
            if (!string.IsNullOrEmpty(SharedState.BaseDataPath)) {
                string graDir = Path.Combine(Path.Combine(SharedState.BaseDataPath, "cdn"), "GRA");
                if (!Directory.Exists(graDir))
                    graDir = Path.Combine(Path.Combine(SharedState.BaseDataPath, "cdn"), "gra");
                if (Directory.Exists(graDir)) {
                    txtInput.Text = graDir;
                    LoadFiles(graDir);
                }
            }
            SharedState.OnExtractionPathChanged += (path) => { txtOutput.Text = path; };
            if (!string.IsNullOrEmpty(SharedState.ExtractionPath)) txtOutput.Text = SharedState.ExtractionPath;
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;

            QFG5TableLayoutPanel mainLayout = new QFG5TableLayoutPanel
            {
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

            QFG5Button btnLoad = new QFG5Button { Text = "Load", Width = QFG5Layout.ButtonWidth, Height = 30 };
            btnLoad.Click += (s, e) => LoadFiles(txtInput.Text);
            QFG5Button btnBrowse;
            QFG5TableLayoutPanel row1 = QFG5Layout.CreatePathRow("GRA Path:", out txtInput, out btnBrowse, btnLoad);
            btnBrowse.Click += (s, e) => {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog() == DialogResult.OK) { txtInput.Text = fbd.SelectedPath; LoadFiles(fbd.SelectedPath); }
            };

            QFG5Button btnExportMp4 = new QFG5Button { Text = "Export MP4", Width = 110, Height = 30 };
            btnExportMp4.Click += (s, e) => ExportMP4();
            QFG5Button btnExtract = new QFG5Button { Text = "Extract Selected", Width = 135, Height = 30 };
            btnExtract.Click += (s, e) => ExtractSelected();
            QFG5Button btnBrowseOut;
            QFG5TableLayoutPanel row2 = QFG5Layout.CreatePathRow("Output:", out txtOutput, out btnBrowseOut, btnExportMp4, btnExtract);
            btnBrowseOut.Click += (s, e) => {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog() == DialogResult.OK) txtOutput.Text = fbd.SelectedPath;
            };

            // --- Row 3: Split (list | preview) ---
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BorderStyle = BorderStyle.None
            };
            split.Panel1.BackColor = Color.Transparent;
            split.Panel2.BackColor = Color.Transparent;
            QFG5Layout.SetSplitterRatioOnce(split, 0.55);

            QFG5PreviewFrame gridFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            gridFiles = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.Black,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                GridColor = Color.FromArgb(40, 40, 40),
                MultiSelect = true
            };
            gridFiles.DefaultCellStyle.BackColor = Color.Black;
            gridFiles.DefaultCellStyle.ForeColor = Color.White;
            gridFiles.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
            gridFiles.ColumnHeadersDefaultCellStyle.ForeColor = Color.Gold;
            gridFiles.EnableHeadersVisualStyles = false;

            gridFiles.Columns.Add("File", "Filename");
            gridFiles.Columns["File"].ReadOnly = true;

            DataGridViewComboBoxColumn colMode = new DataGridViewComboBoxColumn { Name = "Mode", HeaderText = "Mode", Width = 55 };
            colMode.Items.AddRange("0", "1", "2", "3", "4");
            gridFiles.Columns.Add(colMode);

            DataGridViewCheckBoxColumn colCM = new DataGridViewCheckBoxColumn { Name = "CM", HeaderText = "CM", Width = 45 };
            gridFiles.Columns.Add(colCM);

            gridFiles.Columns.Add("Cols", "Collections");
            gridFiles.Columns["Cols"].ReadOnly = true;

            gridFiles.Columns.Add("Dims", "W x H");
            gridFiles.Columns["Dims"].ReadOnly = true;
            gridFiles.Columns["Dims"].Width = 65;

            gridFiles.Columns.Add("Offs", "X, Y");
            gridFiles.Columns["Offs"].ReadOnly = true;
            gridFiles.Columns["Offs"].Width = 60;

            gridFiles.Columns.Add("Sprites", "Sprites");
            gridFiles.Columns["Sprites"].ReadOnly = true;
            gridFiles.Columns["Sprites"].Width = 50;

            gridFiles.Columns.Add("Flags", "Flags");
            gridFiles.Columns["Flags"].ReadOnly = true;
            gridFiles.Columns["Flags"].Width = 50;

            DataGridViewCheckBoxColumn colPNG = new DataGridViewCheckBoxColumn { Name = "PNG", HeaderText = "PNG", Width = 45 };
            gridFiles.Columns.Add(colPNG);

            DataGridViewCheckBoxColumn colMP4 = new DataGridViewCheckBoxColumn { Name = "MP4", HeaderText = "MP4", Width = 45 };
            gridFiles.Columns.Add(colMP4);

            gridFiles.Columns["File"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            gridFiles.Columns["Cols"].Width = 55;
            gridFiles.Columns["Cols"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            gridFiles.SelectionChanged += (s, e) => UpdatePreview();

            gridFiles.CellMouseClick += (s, e) => {
                if (e.RowIndex < 0) return;
                string colName = null;
                if (e.ColumnIndex == gridFiles.Columns["PNG"].Index) colName = "PNG";
                else if (e.ColumnIndex == gridFiles.Columns["MP4"].Index) colName = "MP4";
                if (colName != null && (Control.ModifierKeys & Keys.Shift) == Keys.Shift) {
                    bool targetState = !(bool)(gridFiles.Rows[e.RowIndex].Cells[colName].Value ?? false);
                    foreach (DataGridViewRow row in gridFiles.SelectedRows)
                        row.Cells[colName].Value = targetState;
                    gridFiles.EndEdit();
                }
            };

            gridFiles.CurrentCellDirtyStateChanged += (s, e) => {
                if (gridFiles.IsCurrentCellDirty)
                    gridFiles.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            gridFiles.CellValueChanged += (s, e) => {
                if (e.ColumnIndex == gridFiles.Columns["Mode"].Index || e.ColumnIndex == gridFiles.Columns["CM"].Index)
                    UpdatePreview();
            };

            Panel centerWrapper = new Panel { Dock = DockStyle.Fill, Height = 40 };
            FlowLayoutPanel inner = new FlowLayoutPanel {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Location = new Point(0, 0)
            };
            QFG5Button btnAll = new QFG5Button { Text = "PNG All", Width = 100, Height = 32 };
            btnAll.Click += (s, e) => SetAllCheckboxes("PNG", true);
            QFG5Button btnNone = new QFG5Button { Text = "PNG None", Width = 100, Height = 32 };
            btnNone.Click += (s, e) => SetAllCheckboxes("PNG", false);

            QFG5Button btnMP4All = new QFG5Button { Text = "MP4 All", Width = 100, Height = 30 };
            QFG5Button btnMP4None = new QFG5Button { Text = "MP4 None", Width = 100, Height = 30 };
            btnMP4All.Click += (s, e) => SetAllCheckboxes("MP4", true);
            btnMP4None.Click += (s, e) => SetAllCheckboxes("MP4", false);
            inner.Controls.Add(btnAll);
            inner.Controls.Add(btnNone);
            inner.Controls.Add(new Label { Width = 10 });
            inner.Controls.Add(btnMP4All);
            inner.Controls.Add(btnMP4None);
            centerWrapper.Controls.Add(inner);
            centerWrapper.Resize += (s, e) => {
                inner.Location = new Point((centerWrapper.Width - inner.Width) / 2, (centerWrapper.Height - inner.Height) / 2);
            };
            inner.Location = new Point((centerWrapper.Width - inner.Width) / 2, (centerWrapper.Height - inner.Height) / 2);

            QFG5TableLayoutPanel listSide = new QFG5TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            listSide.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            listSide.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));
            listSide.Controls.Add(gridFiles, 0, 0);
            listSide.Controls.Add(centerWrapper, 0, 1);

            gridFrame.Controls.Clear();
            gridFrame.Controls.Add(listSide);

            split.Panel1.Controls.Add(gridFrame);
            QFG5TableLayoutPanel previewLayout = new QFG5TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

            QFG5PreviewFrame previewFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            pbPreview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
            previewFrame.Controls.Add(pbPreview);
            previewLayout.Controls.Add(previewFrame, 0, 0);

            Panel playPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 0) };
            QFG5Button btnPlayAnim = new QFG5Button { Text = "▶ Play Anim", Dock = DockStyle.Fill, Height = 30 };
            btnPlayAnim.Click += (s, e) => TogglePlay();
            playPanel.Controls.Add(btnPlayAnim);
            previewLayout.Controls.Add(playPanel, 0, 1);

            split.Panel2.Controls.Add(previewLayout);

            progressBar = new QFG5ProgressBar { Dock = DockStyle.Fill, Height = 25 };

            QFG5FlowLayoutPanel statusRow = new QFG5FlowLayoutPanel { Dock = DockStyle.Fill };
            lblStatus = new Label { Text = "Ready", ForeColor = Color.Gold, AutoSize = true, Margin = new Padding(0, 5, 0, 0) };
            statusRow.Controls.Add(lblStatus);

            mainLayout.Controls.Add(row1, 0, 0);
            mainLayout.Controls.Add(row2, 0, 1);
            mainLayout.Controls.Add(split, 0, 2);
            mainLayout.Controls.Add(progressBar, 0, 3);
            mainLayout.Controls.Add(statusRow, 0, 4);

            this.Controls.Add(mainLayout);
        }



        private void LoadFiles(string path)
        {
            gridFiles.Rows.Clear();
            if (!Directory.Exists(path)) {
                lblStatus.Text = "Folder not found: " + path;
                return;
            }

            var files = new System.Collections.Generic.List<string>();
            foreach (var f in Directory.GetFiles(path))
            {
                string ext = Path.GetExtension(f);
                if (ext.Equals(".GRA", StringComparison.OrdinalIgnoreCase))
                    files.Add(f);
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var f in files)
            {
                FileInfo fi = new FileInfo(f);
                int mode = -1, cols = 0;
                string errMsg;
                converter.TryReadHeader(f, out mode, out cols, out errMsg);

                int cX, cY, cW, cH, nSprites;
                if (!converter.TryReadMetadata(f, out cX, out cY, out cW, out cH, out nSprites, out errMsg))
                    cX = cY = cW = cH = nSprites = 0;

                int delay, flags;
                bool cmAuto = false;
                if (converter.TryReadFlags(f, out delay, out flags, out errMsg))
                    cmAuto = (flags & 1) != 0; // bit 0 = column-major

                // Smart heuristic overlay
                bool suggestCm;
                if (converter.AnalyzeHeuristics(f, out suggestCm)) {
                    if (suggestCm) cmAuto = true;
                }

                string dims = cW > 0 ? $"{cW}x{cH}" : "?";
                string offs = cX != 0 || cY != 0 ? $"{cX},{cY}" : "-";
                string spr  = nSprites > 0 ? nSprites.ToString() : "?";
                string flagStr = flags.ToString("X2");

                // Row: File, Mode, CM(auto), Cols, Dims, Offs, Sprites, Flags, PNG(false), MP4(false)
                gridFiles.Rows.Add(fi.Name, mode >= 0 ? mode.ToString() : "?", cmAuto, cols > 0 ? cols.ToString() : "?", dims, offs, spr, flagStr, false, false);
            }
            lblStatus.Text = $"Loaded {gridFiles.Rows.Count} GRA files";
        }

        private void UpdatePreview()
        {
            if (gridFiles.SelectedRows.Count == 0) return;
            object cellVal = gridFiles.SelectedRows[0].Cells[0].Value;
            if (cellVal == null) return;
            string filename = cellVal.ToString();
            string path = Path.Combine(txtInput.Text, filename);
            if (!File.Exists(path)) return;

            try
            {
                int modeOverride = -1;
                bool cmOverride = false;
                if (gridFiles.SelectedRows[0].Cells["Mode"].Value != null)
                    int.TryParse(gridFiles.SelectedRows[0].Cells["Mode"].Value.ToString(), out modeOverride);
                if (gridFiles.SelectedRows[0].Cells["CM"].Value is bool b)
                    cmOverride = b;

                if (pbPreview.Image != null) { pbPreview.Image = null; }
                if (animTimer != null) animTimer.Stop();
                isPlaying = false;
                
                foreach (var f in animFrames) f.Dispose();
                animFrames.Clear();

                lblStatus.Text = "Decoding: " + filename + "…";
                Application.DoEvents();
                animFrames = converter.DecodeAllFrames(path, modeOverride, cmOverride);
                if (animFrames != null && animFrames.Count > 0) {
                    currentFrameIdx = animFrames.Count - 1;
                    pbPreview.Image = animFrames[currentFrameIdx];
                    lblStatus.Text = $"Previewing: {filename} ({animFrames[0].Width}x{animFrames[0].Height}, {animFrames.Count} frames)";
                } else {
                    lblStatus.Text = $"Could not decode: {filename}";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Preview error: " + ex.Message;
            }
        }

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            if (animFrames == null || animFrames.Count == 0) return;
            currentFrameIdx = (currentFrameIdx + 1) % animFrames.Count;
            pbPreview.Image = animFrames[currentFrameIdx];
        }

        private void TogglePlay()
        {
            if (isPlaying) {
                animTimer.Stop(); isPlaying = false;
            } else {
                if (animFrames != null && animFrames.Count > 1) {
                    animTimer.Start(); isPlaying = true;
                }
            }
        }

        private void ExportMP4()
        {
            if (gridFiles.SelectedRows.Count == 0) {
                MessageBox.Show("Please select GRA files from the list first.");
                return;
            }
            string outDir = txtOutput.Text.Trim();
            if (!Directory.Exists(outDir)) { MessageBox.Show("Invalid output path."); return; }

            int succeeded = 0, failed = 0;
            progressBar.Value = 0;
            int total = gridFiles.SelectedRows.Count;
            int processed = 0;

            foreach (DataGridViewRow row in gridFiles.SelectedRows)
            {
                string fileName = row.Cells[0].Value.ToString();
                string fullPath = Path.Combine(txtInput.Text, fileName);
                string dest = Path.Combine(outDir, Path.GetFileNameWithoutExtension(fileName) + ".mp4");

                lblStatus.Text = "Exporting MP4: " + fileName + "…";
                Application.DoEvents();

                int modeOverride = -1;
                bool cmOverride = false;
                if (row.Cells["Mode"].Value != null) int.TryParse(row.Cells["Mode"].Value.ToString(), out modeOverride);
                if (row.Cells["CM"].Value is bool b) cmOverride = b;

                bool success = converter.ExportToMP4(fullPath, dest, modeOverride, cmOverride);
                if (success) succeeded++; else failed++;

                processed++;
                progressBar.Value = (int)((processed / (float)total) * 100);
            }
            lblStatus.Text = $"MP4 Export complete. {succeeded} paths done. {failed} failed.";
        }

        private void SetAllCheckboxes(string columnName, bool value)
        {
            foreach (DataGridViewRow row in gridFiles.Rows)
                row.Cells[columnName].Value = value;
            gridFiles.EndEdit();
        }

        private void ExtractSelected()
        {
            if (gridFiles.SelectedRows.Count == 0) {
                MessageBox.Show("Please select GRA files from the list first.");
                return;
            }

            string outDir = txtOutput.Text.Trim();
            if (!Directory.Exists(outDir)) {
                MessageBox.Show("Invalid output path.");
                return;
            }

            progressBar.Value = 0;
            int succeeded = 0, failed = 0;
            int total = gridFiles.SelectedRows.Count;

            foreach (DataGridViewRow row in gridFiles.SelectedRows) {
                string fileName = row.Cells[0].Value.ToString();
                bool exportPNG = row.Cells["PNG"].Value is bool p && p;
                bool exportMP4 = row.Cells["MP4"].Value is bool m && m;
                string fullPath = Path.Combine(txtInput.Text, fileName);

                int modeOverride = -1;
                bool cmOverride = false;
                if (row.Cells["Mode"].Value != null)
                    int.TryParse(row.Cells["Mode"].Value.ToString(), out modeOverride);
                if (row.Cells["CM"].Value is bool b1)
                    cmOverride = b1;

                if (exportMP4) {
                    // Export as MP4 (takes priority if both checked, or runs alongside PNG)
                    string mp4Dest = Path.Combine(outDir, Path.GetFileNameWithoutExtension(fileName) + ".mp4");
                    lblStatus.Text = "Exporting MP4: " + fileName + "…";
                    Application.DoEvents();
                    bool ok = converter.ExportToMP4(fullPath, mp4Dest, modeOverride, cmOverride);
                    if (ok) succeeded++; else { failed++; Logger.Log($"GraTab MP4: {fileName} failed"); }
                }

                if (!exportMP4 || exportPNG) {
                    // Export as BMP or PNG (when MP4 not checked OR when PNG is also checked)
                    string ext  = exportPNG ? ".png" : ".bmp";
                    string dest = Path.Combine(outDir, Path.GetFileNameWithoutExtension(fileName) + ext);
                    lblStatus.Text = "Extracting " + fileName + "…";
                    Application.DoEvents();
                    try {
                        bool ok = converter.Extract(fullPath, dest, exportPNG, modeOverride, cmOverride);
                        if (ok) succeeded++;
                        else { failed++; Logger.Log($"GraTab: {fileName} returned no bitmap"); }
                    } catch (Exception ex) {
                        failed++;
                        Logger.LogError("GraTab", $"Error extracting {fileName}: {ex.Message}");
                    }
                }

                progressBar.Value = (int)((float)(succeeded + failed) / total * 100);
            }
            lblStatus.Text = $"Done: {succeeded} extracted, {failed} failed.";
            if (failed == 0) MessageBox.Show("Extraction complete.");
            else MessageBox.Show($"Done with {failed} error(s). Check the log for details.");
        }
    }
}
