using System;
using System.Collections.Generic;
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
        private Label lblCurrentAction;
        private Label lblFileCount;
        private Label lblFrameInfo;
        private Label lblDimensions;
        private QFG5Button btnPlayAnim;
        private QFG5Button btnLoad;
        private QFG5Button btnExportMp4;
        private QFG5Button btnExtract;
        private GraConverter converter;
        private Timer animTimer;
        private List<Bitmap> animFrames = new List<Bitmap>();
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
                }
            };
            if (!string.IsNullOrEmpty(SharedState.BaseDataPath)) {
                string graDir = Path.Combine(Path.Combine(SharedState.BaseDataPath, "cdn"), "GRA");
                if (!Directory.Exists(graDir))
                    graDir = Path.Combine(Path.Combine(SharedState.BaseDataPath, "cdn"), "gra");
                if (Directory.Exists(graDir)) {
                    txtInput.Text = graDir;
                }
            }
            SharedState.OnExtractionPathChanged += (path) => { txtOutput.Text = path; };
            if (!string.IsNullOrEmpty(SharedState.ExtractionPath)) txtOutput.Text = SharedState.ExtractionPath;
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;

            ToolTip toolTip = new ToolTip();

            QFG5TableLayoutPanel mainLayout = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // Collapsible GRA and output paths
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Center: Split view (Grid + Preview)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));  // Bottom: 2-Row Footer

            // --- Top Section: 2 Symmetrical Path Rows ---
            QFG5TableLayoutPanel topTable = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 8)
            };
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, QFG5Layout.LabelWidth)); // 100px
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));                    // Stretch
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));                    // Browse (95px)
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));                    // Load / Action (95px)

            topTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Row 0: GRA Path
            topTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Row 1: Output Path

            // Row 0: GRA Path
            Label lblInput = new Label
            {
                Text = "GRA Folder:",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            txtInput = new QFG5PathField { Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 4, 3), TabIndex = 0, AccessibleName = "GRA Folder" };
            txtInput.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) LoadFiles(txtInput.Text); };
            toolTip.SetToolTip(txtInput, "Folder containing Quest for Glory V .GRA animated sprite files (typically in cdn/GRA)");

            QFG5Button btnBrowse = new QFG5Button { Text = "Browse", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 4, 3), TabIndex = 1 };
            btnBrowse.Click += (s, e) => {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog() == DialogResult.OK) { txtInput.Text = fbd.SelectedPath; LoadFiles(fbd.SelectedPath); }
            };
            toolTip.SetToolTip(btnBrowse, "Browse for folder containing .GRA files");

            btnLoad = new QFG5Button { Text = "Load", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 0, 3), TabIndex = 2 };
            btnLoad.Click += (s, e) => LoadFiles(txtInput.Text);
            toolTip.SetToolTip(btnLoad, "Scan and load all .GRA files from the selected folder");

            topTable.Controls.Add(lblInput, 0, 0);
            topTable.Controls.Add(txtInput, 1, 0);
            topTable.Controls.Add(btnBrowse, 2, 0);
            topTable.Controls.Add(btnLoad, 3, 0);

            // Row 1: Output Path
            Label lblOutput = new Label
            {
                Text = "Output Path:",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            txtOutput = new QFG5PathField { Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 4, 3), TabIndex = 3, AccessibleName = "Output Path" };
            toolTip.SetToolTip(txtOutput, "Destination folder for extracted sprite images (.PNG/.BMP) or .MP4 animations");

            QFG5Button btnBrowseOut = new QFG5Button { Text = "Browse", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 3, 4, 3), TabIndex = 4 };
            btnBrowseOut.Click += (s, e) => {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                    if (fbd.ShowDialog() == DialogResult.OK) txtOutput.Text = fbd.SelectedPath;
            };
            toolTip.SetToolTip(btnBrowseOut, "Browse for destination output folder");

            // Empty spacer in col 3 on row 1 to maintain grid symmetry
            Panel row1Spacer = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            topTable.Controls.Add(lblOutput, 0, 1);
            topTable.Controls.Add(txtOutput, 1, 1);
            topTable.Controls.Add(btnBrowseOut, 2, 1);
            topTable.Controls.Add(row1Spacer, 3, 1);

            // --- Center Section: Split View (Resource Grid + Sprite Preview) ---
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BorderStyle = BorderStyle.None,
                SplitterWidth = 6,
                Margin = new Padding(0, 0, 0, 6)
            };
            split.Panel1.BackColor = Color.Transparent;
            split.Panel2.BackColor = Color.Transparent;
            QFG5Layout.SetSplitterRatioOnce(split, 0.58);

            // Panel 1: Grid Frame
            QFG5PreviewFrame gridFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            QFG5TableLayoutPanel listSide = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            listSide.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); // Top Header
            listSide.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // DataGridView
            listSide.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Batch Selection Bar

            Label lblGridHeader = new Label
            {
                Text = " GRA RESOURCES ",
                ForeColor = QFG5Theme.ColorGold,
                Font = QFG5Theme.SectionHeaderFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(20, 16, 12),
                Padding = new Padding(6, 0, 0, 0)
            };
            listSide.Controls.Add(lblGridHeader, 0, 0);

            gridFiles = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(16, 12, 10),
                ForeColor = QFG5Theme.ColorTextPrimary,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                GridColor = Color.FromArgb(45, 35, 28),
                MultiSelect = true,
                AutoGenerateColumns = false,
                AllowUserToResizeRows = false,
                RowHeadersWidth = 4,
                TabIndex = 5
            };
            gridFiles.DefaultCellStyle.BackColor = Color.FromArgb(16, 12, 10);
            gridFiles.DefaultCellStyle.ForeColor = QFG5Theme.ColorTextPrimary;
            gridFiles.DefaultCellStyle.SelectionBackColor = Color.FromArgb(60, 48, 32);
            gridFiles.DefaultCellStyle.SelectionForeColor = QFG5Theme.ColorGoldBright;
            gridFiles.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(28, 22, 18);
            gridFiles.ColumnHeadersDefaultCellStyle.ForeColor = QFG5Theme.ColorGold;
            gridFiles.ColumnHeadersDefaultCellStyle.Font = QFG5Theme.BodyBoldFont;
            gridFiles.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(16, 12, 10);
            gridFiles.RowHeadersDefaultCellStyle.ForeColor = QFG5Theme.ColorTextPrimary;
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
            listSide.Controls.Add(gridFiles, 0, 1);

            // Batch selection toolbar
            QFG5FlowLayoutPanel batchToolbar = new QFG5FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 16, 12),
                Padding = new Padding(4, 2, 4, 2),
                WrapContents = false
            };

            QFG5Button btnAll = new QFG5Button { Text = "PNG All", Width = 80, Height = 28, Margin = new Padding(0, 0, 4, 0), TabIndex = 6 };
            btnAll.Click += (s, e) => SetAllCheckboxes("PNG", true);
            toolTip.SetToolTip(btnAll, "Check PNG export on all rows");

            QFG5Button btnNone = new QFG5Button { Text = "PNG None", Width = 80, Height = 28, Margin = new Padding(0, 0, 8, 0), TabIndex = 7 };
            btnNone.Click += (s, e) => SetAllCheckboxes("PNG", false);
            toolTip.SetToolTip(btnNone, "Uncheck PNG export on all rows");

            QFG5Button btnMP4All = new QFG5Button { Text = "MP4 All", Width = 80, Height = 28, Margin = new Padding(0, 0, 4, 0), TabIndex = 8 };
            btnMP4All.Click += (s, e) => SetAllCheckboxes("MP4", true);
            toolTip.SetToolTip(btnMP4All, "Check MP4 export on all rows");

            QFG5Button btnMP4None = new QFG5Button { Text = "MP4 None", Width = 80, Height = 28, Margin = new Padding(0, 0, 8, 0), TabIndex = 9 };
            btnMP4None.Click += (s, e) => SetAllCheckboxes("MP4", false);
            toolTip.SetToolTip(btnMP4None, "Uncheck MP4 export on all rows");

            Label lblBatchHint = new Label
            {
                Text = "(Shift+Click checkbox to toggle selected)",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                AutoSize = true,
                Margin = new Padding(4, 7, 0, 0)
            };

            batchToolbar.Controls.Add(btnAll);
            batchToolbar.Controls.Add(btnNone);
            batchToolbar.Controls.Add(btnMP4All);
            batchToolbar.Controls.Add(btnMP4None);
            batchToolbar.Controls.Add(lblBatchHint);

            listSide.Controls.Add(batchToolbar, 0, 2);
            gridFrame.Controls.Add(listSide);
            split.Panel1.Controls.Add(gridFrame);

            // Panel 2: Preview Frame + Animation Controls
            QFG5PreviewFrame previewFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            QFG5TableLayoutPanel previewLayout = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); // Top Header
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // PictureBox
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Playbar

            Label lblPreviewHeader = new Label
            {
                Text = " SPRITE / ANIMATION PREVIEW ",
                ForeColor = QFG5Theme.ColorGold,
                Font = QFG5Theme.SectionHeaderFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(20, 16, 12),
                Padding = new Padding(6, 0, 0, 0)
            };
            QFG5Theme.ApplyHeaderStroke(lblPreviewHeader);
            previewLayout.Controls.Add(lblPreviewHeader, 0, 0);

            pbPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(16, 12, 10)
            };
            pbPreview.Paint += (s, pe) => {
                if (pbPreview.Image == null)
                {
                    string emptyMsg = gridFiles.Rows.Count == 0
                        ? "No GRA folder loaded.\n\nClick 'Load' above to scan and inspect GRA sprite files."
                        : "No GRA sprite selected.\n\nSelect a sprite from the list to preview frames and animations.";
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    using (var brush = new SolidBrush(QFG5Theme.ColorTextMuted))
                    {
                        pe.Graphics.DrawString(emptyMsg, QFG5Theme.BodyFont, brush, pbPreview.ClientRectangle, sf);
                    }
                }
            };
            toolTip.SetToolTip(pbPreview, "Preview of the selected sprite frame or animation");
            previewLayout.Controls.Add(pbPreview, 0, 1);

            // Animation playback bar
            QFG5TableLayoutPanel playbar = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.FromArgb(20, 16, 12),
                Padding = new Padding(4, 3, 6, 3),
                Margin = new Padding(0)
            };
            playbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115)); // Play Button
            playbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));  // Frame Info
            playbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Dimensions

            btnPlayAnim = new QFG5Button { Text = "▶ Play Anim", Dock = DockStyle.Fill, Height = 28, Enabled = false, TabIndex = 10 };
            btnPlayAnim.Click += (s, e) => TogglePlay();
            toolTip.SetToolTip(btnPlayAnim, "Play or pause the multi-frame sprite animation");

            lblFrameInfo = new Label
            {
                Text = "Frame: - / -",
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            lblDimensions = new Label
            {
                Text = "-",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight
            };

            playbar.Controls.Add(btnPlayAnim, 0, 0);
            playbar.Controls.Add(lblFrameInfo, 1, 0);
            playbar.Controls.Add(lblDimensions, 2, 0);

            previewLayout.Controls.Add(playbar, 0, 2);
            previewFrame.Controls.Add(previewLayout);
            split.Panel2.Controls.Add(previewFrame);

            // --- Bottom Section: 2-Row Cohesive Footer ---
            QFG5TableLayoutPanel footerTable = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0, 4, 0, 0)
            };
            footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); // Left status/info
            footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));  // Right progress / action buttons

            footerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Row 0: Status + Progress
            footerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Row 1: File Info + Actions

            // Row 0: High-Level Status + Progress Bar
            lblStatus = new Label
            {
                Text = "No GRA folder loaded",
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
                AccessibleName = "Sprite extraction progress"
            };

            footerTable.Controls.Add(lblStatus, 0, 0);
            footerTable.Controls.Add(progressBar, 1, 0);

            // Row 1: Detailed File Info + Action Buttons
            QFG5FlowLayoutPanel fileInfoPanel = new QFG5FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                WrapContents = false
            };

            lblCurrentAction = new Label
            {
                Text = "Status: Idle",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                AutoSize = true,
                Margin = new Padding(0, 6, 8, 0)
            };

            lblFileCount = new Label
            {
                Text = "(0 resources loaded)",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                AutoSize = true,
                Margin = new Padding(0, 7, 0, 0)
            };

            fileInfoPanel.Controls.Add(lblCurrentAction);
            fileInfoPanel.Controls.Add(lblFileCount);

            // Right-aligned action buttons container
            QFG5TableLayoutPanel actionButtonsPanel = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            actionButtonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44f)); // Export MP4
            actionButtonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56f)); // Extract Selected

            btnExportMp4 = new QFG5Button { Text = "Export MP4", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 1, 4, 1), TabIndex = 11 };
            btnExportMp4.Click += (s, e) => ExportMP4();
            toolTip.SetToolTip(btnExportMp4, "Export selected sprites as MP4 video animations");

            btnExtract = new QFG5Button { Text = "Extract Selected", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(2, 1, 0, 1), TabIndex = 12 };
            btnExtract.Click += (s, e) => ExtractSelected();
            toolTip.SetToolTip(btnExtract, "Extract checked sprites/animations from selected rows into destination folder");

            actionButtonsPanel.Controls.Add(btnExportMp4, 0, 0);
            actionButtonsPanel.Controls.Add(btnExtract, 1, 0);

            footerTable.Controls.Add(fileInfoPanel, 0, 1);
            footerTable.Controls.Add(actionButtonsPanel, 1, 1);

            // Assemble main layout
            mainLayout.Controls.Add(new QFG5CollapsibleSection("SPRITE FOLDERS", topTable, 110), 0, 0);
            mainLayout.Controls.Add(split, 0, 1);
            mainLayout.Controls.Add(footerTable, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private void LoadFiles(string path)
        {
            Logger.LogPerf("GraTab.LoadFiles START");
            var swGra = System.Diagnostics.Stopwatch.StartNew();

            if (animTimer != null && animTimer.Enabled) animTimer.Stop();
            isPlaying = false;
            btnPlayAnim.Text = "▶ Play Anim";
            btnPlayAnim.Enabled = false;
            lblFrameInfo.Text = "Frame: - / -";
            lblDimensions.Text = "-";

            if (pbPreview != null && pbPreview.Image != null)
            {
                pbPreview.Image = null;
            }
            if (animFrames != null) {
                foreach (var f in animFrames) f.Dispose();
                animFrames.Clear();
            }

            gridFiles.Rows.Clear();
            if (!Directory.Exists(path)) {
                lblStatus.Text = "Folder not found: " + path;
                lblCurrentAction.Text = "Status: Idle";
                lblFileCount.Text = "(0 resources loaded)";
                pbPreview.Invalidate();
                swGra.Stop();
                Logger.LogPerf($"GraTab.LoadFiles END: folder not found {path} ({swGra.ElapsedMilliseconds} ms)");
                return;
            }

            var files = new List<string>();
            foreach (var f in Directory.GetFiles(path))
            {
                string ext = Path.GetExtension(f);
                if (ext.Equals(".GRA", StringComparison.OrdinalIgnoreCase))
                    files.Add(f);
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);
            Logger.LogPerf($"GRA files discovered: {files.Count}");

            var swAnalysis = System.Diagnostics.Stopwatch.StartNew();
            int inspectedCount = 0;
            int headerReadCount = 0;
            int metadataReadCount = 0;
            int flagReadCount = 0;
            int duplicateFlagReadsAvoided = 0;
            foreach (var f in files)
            {
                inspectedCount++;
                FileInfo fi = new FileInfo(f);
                int mode = -1, cols = 0;
                string errMsg;
                headerReadCount++;
                converter.TryReadHeader(f, out mode, out cols, out errMsg);

                int cX, cY, cW, cH, nSprites;
                metadataReadCount++;
                bool metadataRead = converter.TryReadMetadata(f, out cX, out cY, out cW, out cH, out nSprites, out errMsg);
                if (!metadataRead)
                    cX = cY = cW = cH = nSprites = 0;

                int delay, flags;
                bool cmAuto = false;
                flagReadCount++;
                bool flagsRead = converter.TryReadFlags(f, out delay, out flags, out errMsg);
                if (flagsRead)
                    cmAuto = (flags & 1) != 0; // bit 0 = column-major

                // Smart heuristic overlay
                bool suggestCm;
                if (metadataRead && !(cH > cW * 4 && cW > 0))
                    duplicateFlagReadsAvoided++;
                if (converter.AnalyzeHeuristics(cW, cH, flags, metadataRead, flagsRead, out suggestCm)) {
                    if (suggestCm) cmAuto = true;
                }

                string dims = cW > 0 ? $"{cW}x{cH}" : "?";
                string offs = cX != 0 || cY != 0 ? $"{cX},{cY}" : "-";
                string spr  = nSprites > 0 ? nSprites.ToString() : "?";
                string flagStr = flags.ToString("X2");

                // Row: File, Mode, CM(auto), Cols, Dims, Offs, Sprites, Flags, PNG(false), MP4(false)
                gridFiles.Rows.Add(fi.Name, mode >= 0 ? mode.ToString() : "?", cmAuto, cols > 0 ? cols.ToString() : "?", dims, offs, spr, flagStr, false, false);
            }
            swAnalysis.Stop();
            swGra.Stop();
            double avgPerFile = inspectedCount > 0 ? (double)swAnalysis.ElapsedMilliseconds / inspectedCount : 0.0;
            Logger.LogPerf($"GRA files inspected: {inspectedCount}");
            Logger.LogPerf($"GRA file reads: header={headerReadCount}, metadata={metadataReadCount}, flags={flagReadCount}; heuristic duplicate reads avoided: metadata={inspectedCount}, flags={duplicateFlagReadsAvoided}");
            Logger.LogPerf($"GRA analysis total: {swAnalysis.ElapsedMilliseconds} ms");
            Logger.LogPerf($"GRA average analysis: {avgPerFile:F2} ms/file");
            Logger.LogPerf($"GraTab.LoadFiles END: {swGra.ElapsedMilliseconds} ms");
            lblStatus.Text = $"Loaded {gridFiles.Rows.Count} GRA files";
            lblCurrentAction.Text = "Status: Idle";
            lblFileCount.Text = $"({gridFiles.Rows.Count} resources loaded)";
            pbPreview.Invalidate();
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
                btnPlayAnim.Text = "▶ Play Anim";

                foreach (var f in animFrames) f.Dispose();
                animFrames.Clear();

                lblStatus.Text = "Decoding: " + filename + "…";
                lblCurrentAction.Text = "Decoding: " + filename;
                Application.DoEvents();
                animFrames = converter.DecodeAllFrames(path, modeOverride, cmOverride);
                if (animFrames != null && animFrames.Count > 0) {
                    currentFrameIdx = animFrames.Count - 1;
                    pbPreview.Image = animFrames[currentFrameIdx];
                    lblStatus.Text = $"Previewing: {filename} ({animFrames[0].Width}x{animFrames[0].Height}, {animFrames.Count} frames)";
                    lblCurrentAction.Text = "Previewing: " + filename;
                    lblFrameInfo.Text = $"Frame: {currentFrameIdx + 1} / {animFrames.Count}";
                    lblDimensions.Text = $"{animFrames[0].Width} x {animFrames[0].Height}";
                    btnPlayAnim.Enabled = animFrames.Count > 1;
                } else {
                    lblStatus.Text = $"Could not decode: {filename}";
                    lblCurrentAction.Text = "Decode failed";
                    lblFrameInfo.Text = "Frame: - / -";
                    lblDimensions.Text = "-";
                    btnPlayAnim.Enabled = false;
                }
                pbPreview.Invalidate();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Preview error: " + ex.Message;
                lblCurrentAction.Text = "Error";
                btnPlayAnim.Enabled = false;
                pbPreview.Invalidate();
            }
        }

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            if (animFrames == null || animFrames.Count == 0) return;
            currentFrameIdx = (currentFrameIdx + 1) % animFrames.Count;
            pbPreview.Image = animFrames[currentFrameIdx];
            lblFrameInfo.Text = $"Frame: {currentFrameIdx + 1} / {animFrames.Count}";
        }

        private void TogglePlay()
        {
            if (isPlaying) {
                animTimer.Stop();
                isPlaying = false;
                btnPlayAnim.Text = "▶ Play Anim";
            } else {
                if (animFrames != null && animFrames.Count > 1) {
                    animTimer.Start();
                    isPlaying = true;
                    btnPlayAnim.Text = "⏸ Pause";
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
                lblCurrentAction.Text = "Exporting: " + fileName;
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
            lblCurrentAction.Text = "Status: Finished";
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
                    lblCurrentAction.Text = "Exporting: " + fileName;
                    Application.DoEvents();
                    bool ok = converter.ExportToMP4(fullPath, mp4Dest, modeOverride, cmOverride);
                    if (ok) succeeded++; else { failed++; Logger.Log($"GraTab MP4: {fileName} failed"); }
                }

                if (!exportMP4 || exportPNG) {
                    // Export as BMP or PNG (when MP4 not checked OR when PNG is also checked)
                    string ext  = exportPNG ? ".png" : ".bmp";
                    string dest = Path.Combine(outDir, Path.GetFileNameWithoutExtension(fileName) + ext);
                    lblStatus.Text = "Extracting " + fileName + "…";
                    lblCurrentAction.Text = "Extracting: " + fileName;
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
            lblCurrentAction.Text = "Status: Finished";
            if (failed == 0) MessageBox.Show("Extraction complete.");
            else MessageBox.Show($"Done with {failed} error(s). Check the log for details.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (animTimer != null)
                {
                    animTimer.Stop();
                    animTimer.Dispose();
                    animTimer = null;
                }
                if (animFrames != null)
                {
                    foreach (var f in animFrames) f.Dispose();
                    animFrames.Clear();
                }
            }
            base.Dispose(disposing);
        }
    }
}
