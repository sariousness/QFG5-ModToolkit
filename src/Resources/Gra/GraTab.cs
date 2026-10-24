using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;

namespace QFG5Extractor.Resources.Gra
{
    public class GraTab : UserControl
    {
        private const string OriginalGraBackupSuffix = ".qfg5extractor.bak";
        private const string BuiltInColumnMajorResourceName =
            "QFG5Extractor.Resources.Gra.gra-column-major-defaults.txt";
        private DataGridView gridFiles;
        private ListView thumbnailView;
        private ImageList thumbnailImages;
        private Button btnListView;
        private Button btnThumbnailView;
        private Button btnToggleFavorite;
        private Button btnRestoreOriginal;
        private Button btnSaveColumnMajorList;
        private CheckBox chkFavoritesOnly;
        private CheckBox chkColumnMajor;
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
        private QFG5Button btnEditGraphic;
        private GraConverter converter;
        private Timer animTimer;
        private List<Bitmap> animFrames = new List<Bitmap>();
        private int currentFrameIdx = 0;
        private bool isPlaying = false;
        private bool thumbnailImagesLoaded;
        private bool thumbnailLoadInProgress;
        private bool suppressThumbnailSelection;
        private bool suppressDecodeRefresh;
        private bool updatingColumnMajorToggle;
        private int thumbnailLoadIndex;
        private Timer thumbnailLoadTimer;
        private readonly HashSet<string> favoriteGraFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> columnMajorGraFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<GraFile> graFiles = new List<GraFile>();
        private readonly Dictionary<string, int> thumbnailImageIndices =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Bitmap> thumbnailSourceImages = new List<Bitmap>();
        private readonly string favoritesFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QFG5Extractor", "gra-favorites.txt");
        private readonly string columnMajorOverridesFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QFG5Extractor", "gra-column-major.txt");
        private bool hasSavedColumnMajorList;

        private sealed class GraFile
        {
            public string FullPath { get; set; }
            public string RelativeFolder { get; set; }
        }

        public GraTab()
        {
            converter = new GraConverter();
            animTimer = new Timer { Interval = 100 };
            animTimer.Tick += AnimTimer_Tick;
            thumbnailLoadTimer = new Timer { Interval = 20 };
            thumbnailLoadTimer.Tick += ThumbnailLoadTimer_Tick;
            LoadFavoriteGraFiles();
            LoadColumnMajorGraFiles();
            InitializeComponent();
            SetGraView(false);

            SharedState.OnBaseDataPathChanged += path => LoadFiles();
            SharedState.OnGameDataExtracted += () => LoadFiles();
            SharedState.OnGraFileChanged += path => {
                if (gridFiles.SelectedRows.Count == 0) return;
                string selectedPath = GetSelectedGraPath();
                if (!string.IsNullOrEmpty(selectedPath) &&
                    string.Equals(Path.GetFullPath(path), Path.GetFullPath(selectedPath), StringComparison.OrdinalIgnoreCase))
                    UpdatePreview();
            };
            LoadFiles();
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // Source-folder hint and reload
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Center: Split view (Grid + Preview)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));  // Bottom: 2-Row Footer
            QFG5TableLayoutPanel topTable = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 4)
            };
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            topTable.Controls.Add(new Label
            {
                Text = "GRA resources load from cdn/GRA and hdn/gra in the configured Game DATA Folder.",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            btnLoad = new QFG5Button { Text = "Reload", Dock = DockStyle.Fill, Height = 30 };
            btnLoad.Click += (s, e) => LoadFiles();
            toolTip.SetToolTip(btnLoad, "Reload GRA resources from cdn/GRA and hdn/gra");
            topTable.Controls.Add(btnLoad, 1, 0);

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
                RowCount = 4
            };
            listSide.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); // Top Header
            listSide.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // View and resource actions
            listSide.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // DataGridView
            listSide.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Batch Selection Bar

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

            QFG5FlowLayoutPanel graViewToolbar = new QFG5FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(20, 16, 12),
                Padding = new Padding(3, 2, 3, 2),
                Margin = new Padding(0)
            };
            btnListView = CreateGraToolbarButton("List", 52);
            btnListView.Click += (s, e) => SetGraView(false);
            btnThumbnailView = CreateGraToolbarButton("Thumbs", 62);
            btnThumbnailView.Click += (s, e) => SetGraView(true);
            chkFavoritesOnly = new CheckBox
            {
                Text = "Favorites",
                Width = 78,
                Height = 28,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.CaptionFont,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 1, 4, 0)
            };
            chkFavoritesOnly.CheckedChanged += (s, e) => ApplyGraFavoriteFilter();
            chkColumnMajor = new CheckBox
            {
                Text = "CM",
                Width = 48,
                Height = 28,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.CaptionFont,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 1, 2, 0),
                Enabled = false
            };
            chkColumnMajor.CheckedChanged += (s, e) =>
            {
                if (!updatingColumnMajorToggle)
                    ApplyColumnMajorToSelection();
            };
            btnToggleFavorite = CreateGraToolbarButton("☆", 34);
            btnToggleFavorite.Font = new Font("Segoe UI Symbol", 15f, FontStyle.Regular);
            btnToggleFavorite.Enabled = false;
            btnToggleFavorite.Click += (s, e) => ToggleSelectedFavorite();
            btnRestoreOriginal = CreateGraToolbarButton("↻", 34);
            btnRestoreOriginal.Font = new Font("Segoe UI Symbol", 15f, FontStyle.Regular);
            btnRestoreOriginal.Enabled = false;
            btnRestoreOriginal.Click += (s, e) => RestoreSelectedGra();
            Button btnAutoCm = CreateGraToolbarButton("Auto CM", 72);
            btnAutoCm.Click += (s, e) => AutoDetectColumnMajor();
            btnSaveColumnMajorList = CreateGraToolbarButton("Save CM List", 94);
            btnSaveColumnMajorList.Click += (s, e) => SaveColumnMajorGraFiles();
            ToolTip graToolbarToolTip = new ToolTip();
            graToolbarToolTip.SetToolTip(btnListView, "Show GRA resources as a detailed list");
            graToolbarToolTip.SetToolTip(btnThumbnailView, "Show GRA resources as image thumbnails");
            graToolbarToolTip.SetToolTip(chkFavoritesOnly, "Show only favorited GRA resources");
            graToolbarToolTip.SetToolTip(chkColumnMajor, "Toggle column-major decoding for the selected resource(s)");
            graToolbarToolTip.SetToolTip(btnToggleFavorite, "Toggle favorite for the selected GRA resource");
            graToolbarToolTip.SetToolTip(btnRestoreOriginal, "Restore the selected GRA from its original backup");
            graToolbarToolTip.SetToolTip(btnAutoCm, "Detect column-major decoding for selected resources, or all visible resources");
            graToolbarToolTip.SetToolTip(btnSaveColumnMajorList, "Save the current CM selections as the GRA column-major list used on future launches");
            graViewToolbar.Controls.Add(btnListView);
            graViewToolbar.Controls.Add(btnThumbnailView);
            graViewToolbar.Controls.Add(chkFavoritesOnly);
            graViewToolbar.Controls.Add(chkColumnMajor);
            graViewToolbar.Controls.Add(btnToggleFavorite);
            graViewToolbar.Controls.Add(btnRestoreOriginal);
            graViewToolbar.Controls.Add(btnAutoCm);
            graViewToolbar.Controls.Add(btnSaveColumnMajorList);
            listSide.Controls.Add(graViewToolbar, 0, 1);

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

            gridFiles.Columns.Add("Source", "Folder");
            gridFiles.Columns["Source"].ReadOnly = true;
            gridFiles.Columns["Source"].Width = 72;

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
                {
                    if (!suppressDecodeRefresh)
                    {
                        ClearThumbnailImageCache();
                        if (thumbnailView.Visible) LoadThumbnailImages();
                        UpdatePreview();
                    }
                }
            };
            thumbnailImages = CreateThumbnailImageList();
            thumbnailView = new ListView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 12, 10),
                ForeColor = QFG5Theme.ColorTextPrimary,
                BorderStyle = BorderStyle.None,
                View = View.LargeIcon,
                Alignment = ListViewAlignment.Top,
                AutoArrange = true,
                LargeImageList = thumbnailImages,
                MultiSelect = true,
                HideSelection = false,
                FullRowSelect = true,
                Visible = false
            };
            thumbnailView.SelectedIndexChanged += (s, e) =>
            {
                if (suppressThumbnailSelection) return;
                if (thumbnailView.SelectedItems.Count > 0)
                {
                    ListViewItem selectedItem = thumbnailView.FocusedItem != null &&
                        thumbnailView.FocusedItem.Selected
                        ? thumbnailView.FocusedItem
                        : thumbnailView.SelectedItems[0];
                    GraFile file = selectedItem.Tag as GraFile;
                    DataGridViewRow row = file == null ? null : FindGridRow(file);
                    if (row != null)
                    {
                        gridFiles.ClearSelection();
                        row.Selected = true;
                    }
                }
                UpdatePreview();
            };

            Panel listContentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 12, 10),
                Margin = new Padding(0)
            };
            listContentHost.Controls.Add(gridFiles);
            listContentHost.Controls.Add(thumbnailView);
            listSide.Controls.Add(listContentHost, 0, 2);

            // Batch selection toolbar
            QFG5FlowLayoutPanel batchToolbar = new QFG5FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 16, 12),
                Padding = new Padding(4, 2, 4, 2),
                WrapContents = false,
                Margin = new Padding(0)
            };

            QFG5Button btnAll = new QFG5Button { Text = "PNG All", Width = 100, Height = 34, Margin = new Padding(0, 0, 4, 0), TabIndex = 6 };
            btnAll.Click += (s, e) => SetAllCheckboxes("PNG", true);
            toolTip.SetToolTip(btnAll, "Check PNG export on all rows");

            QFG5Button btnNone = new QFG5Button { Text = "PNG None", Width = 100, Height = 34, Margin = new Padding(0, 0, 8, 0), TabIndex = 7 };
            btnNone.Click += (s, e) => SetAllCheckboxes("PNG", false);
            toolTip.SetToolTip(btnNone, "Uncheck PNG export on all rows");

            QFG5Button btnMP4All = new QFG5Button { Text = "MP4 All", Width = 100, Height = 34, Margin = new Padding(0, 0, 4, 0), TabIndex = 8 };
            btnMP4All.Click += (s, e) => SetAllCheckboxes("MP4", true);
            toolTip.SetToolTip(btnMP4All, "Check MP4 export on all rows");

            QFG5Button btnMP4None = new QFG5Button { Text = "MP4 None", Width = 100, Height = 34, Margin = new Padding(0, 0, 8, 0), TabIndex = 9 };
            btnMP4None.Click += (s, e) => SetAllCheckboxes("MP4", false);
            toolTip.SetToolTip(btnMP4None, "Uncheck MP4 export on all rows");

            Label lblBatchHint = new Label
            {
                Text = "(Shift+Click toggles selected)",
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

            listSide.Controls.Add(batchToolbar, 0, 3);
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
            footerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350));  // Right progress / action buttons

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
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0)
            };
            actionButtonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f)); // Edit Graphic
            actionButtonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f)); // Export MP4
            actionButtonsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f)); // Extract Selected

            btnEditGraphic = new QFG5Button { Text = "Edit Graphic", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 1, 4, 1), TabIndex = 11, Enabled = false };
            btnEditGraphic.Click += (s, e) => EditSelectedGraphic();
            toolTip.SetToolTip(btnEditGraphic, "Open the currently previewed GRA graphic frame in the Texture Editor");

            btnExportMp4 = new QFG5Button { Text = "Export MP4", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(2, 1, 4, 1), TabIndex = 12 };
            btnExportMp4.Click += (s, e) => ExportMP4();
            toolTip.SetToolTip(btnExportMp4, "Export selected sprites as MP4 video animations");

            btnExtract = new QFG5Button { Text = "Extract Selected", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(2, 1, 0, 1), TabIndex = 13 };
            btnExtract.Click += (s, e) => ExtractSelected();
            toolTip.SetToolTip(btnExtract, "Extract checked sprites/animations from selected rows into destination folder");

            actionButtonsPanel.Controls.Add(btnEditGraphic, 0, 0);
            actionButtonsPanel.Controls.Add(btnExportMp4, 1, 0);
            actionButtonsPanel.Controls.Add(btnExtract, 2, 0);

            footerTable.Controls.Add(fileInfoPanel, 0, 1);
            footerTable.Controls.Add(actionButtonsPanel, 1, 1);

            // Assemble main layout
            mainLayout.Controls.Add(topTable, 0, 0);
            mainLayout.Controls.Add(split, 0, 1);
            mainLayout.Controls.Add(footerTable, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private static Button CreateGraToolbarButton(string text, int width)
        {
            return new Button
            {
                Text = text,
                Width = width,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Margin = new Padding(2, 0, 2, 0),
                Cursor = Cursors.Hand
            };
        }

        private void LoadFiles()
        {
            Logger.LogPerf("GraTab.LoadFiles START");
            var swGra = System.Diagnostics.Stopwatch.StartNew();

            if (animTimer != null && animTimer.Enabled) animTimer.Stop();
            isPlaying = false;
            btnPlayAnim.Text = "▶ Play Anim";
            btnPlayAnim.Enabled = false;
            btnEditGraphic.Enabled = false;
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
            graFiles.Clear();
            thumbnailView.Items.Clear();
            ClearThumbnailImageCache();
            AddGraFiles(graFiles, SharedState.GetAssetDataPath("cdn", "GRA"), "cdn/GRA");
            AddGraFiles(graFiles, SharedState.GetAssetDataPath("hdn", "gra"), "hdn/gra");
            if (graFiles.Count == 0) {
                string cdnPath = SharedState.GetAssetDataPath("cdn", "GRA");
                string hdnPath = SharedState.GetAssetDataPath("hdn", "gra");
                lblStatus.Text = "No GRA resources found in cdn/GRA or hdn/gra.";
                lblCurrentAction.Text = "Status: Idle";
                lblFileCount.Text = "(0 resources loaded)";
                pbPreview.Invalidate();
                swGra.Stop();
                Logger.LogPerf($"GraTab.LoadFiles END: no GRA files in {cdnPath} or {hdnPath} ({swGra.ElapsedMilliseconds} ms)");
                return;
            }

            graFiles.Sort((left, right) => {
                int nameOrder = StringComparer.OrdinalIgnoreCase.Compare(
                    Path.GetFileName(left.FullPath), Path.GetFileName(right.FullPath));
                return nameOrder != 0 ? nameOrder :
                    StringComparer.OrdinalIgnoreCase.Compare(left.RelativeFolder, right.RelativeFolder);
            });
            Logger.LogPerf($"GRA files discovered: {graFiles.Count}");

            var swAnalysis = System.Diagnostics.Stopwatch.StartNew();
            int inspectedCount = 0;
            int headerReadCount = 0;
            int metadataReadCount = 0;
            int flagReadCount = 0;
            int duplicateFlagReadsAvoided = 0;
            foreach (GraFile f in graFiles)
            {
                inspectedCount++;
                FileInfo fi = new FileInfo(f.FullPath);
                int mode = -1, cols = 0;
                string errMsg;
                headerReadCount++;
                converter.TryReadHeader(f.FullPath, out mode, out cols, out errMsg);

                int cX, cY, cW, cH, nSprites;
                metadataReadCount++;
                bool metadataRead = converter.TryReadMetadata(f.FullPath, out cX, out cY, out cW, out cH, out nSprites, out errMsg);
                if (!metadataRead)
                    cX = cY = cW = cH = nSprites = 0;

                int delay, flags;
                bool cmAuto = false;
                flagReadCount++;
                bool flagsRead = converter.TryReadFlags(f.FullPath, out delay, out flags, out errMsg);
                if (flagsRead)
                    cmAuto = (flags & 1) != 0; // bit 0 = column-major

                // Smart heuristic overlay
                bool suggestCm;
                if (metadataRead && !(cH > cW * 4 && cW > 0))
                    duplicateFlagReadsAvoided++;
                if (converter.AnalyzeHeuristics(cW, cH, flags, metadataRead, flagsRead, out suggestCm)) {
                    if (suggestCm) cmAuto = true;
                }
                if (hasSavedColumnMajorList)
                    cmAuto = columnMajorGraFiles.Contains(GetColumnMajorKey(f));

                string dims = cW > 0 ? $"{cW}x{cH}" : "?";
                string offs = cX != 0 || cY != 0 ? $"{cX},{cY}" : "-";
                string spr  = nSprites > 0 ? nSprites.ToString() : "?";
                string flagStr = flags.ToString("X2");

                // Row: File, Source, Mode, CM(auto), Cols, Dims, Offs, Sprites, Flags, PNG, MP4
                int rowIndex = gridFiles.Rows.Add(fi.Name, f.RelativeFolder,
                    mode >= 0 ? mode.ToString() : "?", cmAuto,
                    cols > 0 ? cols.ToString() : "?", dims, offs, spr, flagStr, false, false);
                gridFiles.Rows[rowIndex].Tag = f;
            }
            ApplyGraFavoriteFilter();
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
            lblFileCount.Text = "(" + (chkFavoritesOnly.Checked
                ? graFiles.Count(IsFavorite)
                : graFiles.Count) + " resources shown)";
            pbPreview.Invalidate();
        }

        private static void AddGraFiles(List<GraFile> files, string path, string relativeFolder)
        {
            if (!Directory.Exists(path)) return;
            foreach (string filePath in Directory.GetFiles(path))
            {
                if (!string.Equals(Path.GetExtension(filePath), ".GRA", StringComparison.OrdinalIgnoreCase))
                    continue;
                files.Add(new GraFile { FullPath = filePath, RelativeFolder = relativeFolder });
            }
        }

        private void ApplyGraFavoriteFilter()
        {
            if (gridFiles == null || thumbnailView == null) return;
            GraFile previouslySelected = GetSelectedGraFile();
            gridFiles.ClearSelection();
            gridFiles.CurrentCell = null;
            for (int i = 0; i < gridFiles.Rows.Count; i++)
            {
                GraFile file = gridFiles.Rows[i].Tag as GraFile;
                bool shouldBeVisible = file != null &&
                    (!chkFavoritesOnly.Checked || IsFavorite(file));
                if (gridFiles.Rows[i].Visible != shouldBeVisible)
                    gridFiles.Rows[i].Visible = shouldBeVisible;
            }

            suppressThumbnailSelection = true;
            thumbnailView.BeginUpdate();
            thumbnailView.Items.Clear();
            thumbnailLoadIndex = 0;
            foreach (GraFile file in graFiles)
            {
                if (chkFavoritesOnly.Checked && !IsFavorite(file)) continue;
                int imageIndex;
                if (!thumbnailImageIndices.TryGetValue(file.FullPath, out imageIndex)) imageIndex = 0;
                ListViewItem item = new ListViewItem(GetThumbnailLabel(file), imageIndex) { Tag = file };
                thumbnailView.Items.Add(item);
            }
            thumbnailView.EndUpdate();
            suppressThumbnailSelection = false;
            if (previouslySelected != null)
            {
                ListViewItem item = thumbnailView.Items.Cast<ListViewItem>()
                    .FirstOrDefault(candidate => ReferenceEquals(candidate.Tag, previouslySelected));
                if (item != null) item.Selected = true;
            }
            lblFileCount.Text = "(" + (chkFavoritesOnly.Checked
                ? graFiles.Count(IsFavorite)
                : graFiles.Count) + " resources shown)";
            UpdateSelectedActionButtons();
        }

        private void SetGraView(bool showThumbnails)
        {
            try
            {
                gridFiles.Visible = !showThumbnails;
                thumbnailView.Visible = showThumbnails;
                btnListView.BackColor = showThumbnails
                    ? QFG5Theme.ColorSurfaceRecessed
                    : QFG5Theme.ColorBorderHighlight;
                btnThumbnailView.BackColor = showThumbnails
                    ? QFG5Theme.ColorBorderHighlight
                    : QFG5Theme.ColorSurfaceRecessed;
                if (showThumbnails)
                {
                    GraFile selectedFile = gridFiles.SelectedRows.Count == 0
                        ? null
                        : gridFiles.SelectedRows[0].Tag as GraFile;
                    if (selectedFile != null)
                    {
                        ListViewItem item = thumbnailView.Items.Cast<ListViewItem>()
                            .FirstOrDefault(candidate => ReferenceEquals(candidate.Tag, selectedFile));
                        if (item != null) item.Selected = true;
                    }
                    LoadThumbnailImages();
                }
                else if (thumbnailLoadTimer.Enabled)
                {
                    thumbnailLoadTimer.Stop();
                    thumbnailLoadInProgress = false;
                }
                UpdatePreview();
            }
            catch (Exception ex)
            {
                thumbnailLoadTimer.Stop();
                thumbnailLoadInProgress = false;
                thumbnailView.Visible = false;
                gridFiles.Visible = true;
                lblStatus.Text = "Could not open thumbnail view: " + ex.Message;
                lblStatus.ForeColor = Color.OrangeRed;
                Logger.LogError("GraTab", "Could not switch GRA view: " + ex);
                MessageBox.Show("Could not open GRA thumbnail view: " + ex.Message,
                    "GRA Thumbnails", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadThumbnailImages()
        {
            if (thumbnailImagesLoaded || thumbnailLoadInProgress) return;
            thumbnailLoadInProgress = true;
            thumbnailLoadIndex = 0;
            if (thumbnailView.Items.Count == 0)
            {
                thumbnailLoadInProgress = false;
                thumbnailImagesLoaded = true;
                return;
            }
            lblCurrentAction.Text = "Loading thumbnails...";
            thumbnailLoadTimer.Start();
        }

        private void ThumbnailLoadTimer_Tick(object sender, EventArgs e)
        {
            int batchEnd = Math.Min(thumbnailLoadIndex + 8, thumbnailView.Items.Count);
            try
            {
                while (thumbnailLoadIndex < batchEnd)
                {
                    ListViewItem item = thumbnailView.Items[thumbnailLoadIndex++];
                    GraFile file = item.Tag as GraFile;
                    if (file == null) continue;
                    int imageIndex;
                    if (thumbnailImageIndices.TryGetValue(file.FullPath, out imageIndex))
                    {
                        item.ImageIndex = imageIndex;
                        continue;
                    }

                    DataGridViewRow row = FindGridRow(file);
                    int mode = GetSelectedMode(row);
                    bool cm = row != null && row.Cells["CM"].Value is bool value && value;
                    try
                    {
                        using (Bitmap decoded = converter.Decode(file.FullPath, mode, cm))
                        {
                            if (decoded == null) continue;
                            Bitmap thumbnail = CreateThumbnail(decoded);
                            try
                            {
                                thumbnailImages.Images.Add(thumbnail);
                                thumbnailSourceImages.Add(thumbnail);
                                imageIndex = thumbnailImages.Images.Count - 1;
                                thumbnailImageIndices[file.FullPath] = imageIndex;
                                item.ImageIndex = imageIndex;
                            }
                            catch
                            {
                                thumbnail.Dispose();
                                throw;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("GraTab thumbnail", "Could not create thumbnail for " +
                            Path.GetFileName(file.FullPath) + ": " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                thumbnailLoadTimer.Stop();
                thumbnailLoadInProgress = false;
                lblStatus.Text = "Thumbnail loading stopped: " + ex.Message;
                lblStatus.ForeColor = Color.OrangeRed;
                Logger.LogError("GraTab", "Thumbnail loading failed: " + ex);
                return;
            }

            lblCurrentAction.Text = "Loading thumbnails: " + thumbnailLoadIndex + " / " + thumbnailView.Items.Count;
            if (thumbnailLoadIndex >= thumbnailView.Items.Count)
            {
                thumbnailLoadTimer.Stop();
                thumbnailLoadInProgress = false;
                thumbnailImagesLoaded = true;
                lblCurrentAction.Text = "Status: Idle";
            }
        }

        private void ClearThumbnailImageCache()
        {
            thumbnailImageIndices.Clear();
            thumbnailImagesLoaded = false;
            thumbnailLoadInProgress = false;
            thumbnailLoadIndex = 0;
            if (thumbnailLoadTimer != null) thumbnailLoadTimer.Stop();
            if (thumbnailImages == null) return;
            if (thumbnailView != null) thumbnailView.LargeImageList = null;
            thumbnailImages.Dispose();
            foreach (Bitmap image in thumbnailSourceImages) image.Dispose();
            thumbnailSourceImages.Clear();
            thumbnailImages = CreateThumbnailImageList();
            if (thumbnailView != null) thumbnailView.LargeImageList = thumbnailImages;
        }

        private ImageList CreateThumbnailImageList()
        {
            ImageList images = new ImageList
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(96, 72),
                TransparentColor = Color.Transparent
            };
            Bitmap placeholder = CreateThumbnailPlaceholder();
            try
            {
                images.Images.Add(placeholder);
                thumbnailSourceImages.Add(placeholder);
            }
            catch
            {
                placeholder.Dispose();
                images.Dispose();
                throw;
            }
            return images;
        }

        private static Bitmap CreateThumbnailPlaceholder()
        {
            Bitmap image = new Bitmap(96, 72);
            using (Graphics graphics = Graphics.FromImage(image))
            using (SolidBrush background = new SolidBrush(Color.FromArgb(32, 25, 20)))
            using (Pen border = new Pen(Color.FromArgb(90, 70, 52)))
            {
                graphics.FillRectangle(background, 0, 0, image.Width, image.Height);
                graphics.DrawRectangle(border, 0, 0, image.Width - 1, image.Height - 1);
            }
            return image;
        }

        private Bitmap CreateThumbnail(Bitmap source)
        {
            Bitmap thumbnail = CreateThumbnailPlaceholder();
            using (Graphics graphics = Graphics.FromImage(thumbnail))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                float scale = Math.Min(
                    (float)thumbnail.Width / source.Width,
                    (float)thumbnail.Height / source.Height);
                int width = Math.Max(1, (int)(source.Width * scale));
                int height = Math.Max(1, (int)(source.Height * scale));
                int x = (thumbnail.Width - width) / 2;
                int y = (thumbnail.Height - height) / 2;
                graphics.DrawImage(source, new Rectangle(x, y, width, height));
            }
            return thumbnail;
        }

        private DataGridViewRow FindGridRow(GraFile file)
        {
            foreach (DataGridViewRow row in gridFiles.Rows)
                if (ReferenceEquals(row.Tag, file)) return row;
            return null;
        }

        private static int GetSelectedMode(DataGridViewRow row)
        {
            int mode;
            return row != null && row.Cells["Mode"].Value != null &&
                int.TryParse(row.Cells["Mode"].Value.ToString(), out mode) ? mode : -1;
        }

        private static string GetFavoriteKey(GraFile file)
        {
            return file.RelativeFolder + "/" + Path.GetFileName(file.FullPath);
        }

        private bool IsFavorite(GraFile file)
        {
            return file != null && favoriteGraFiles.Contains(GetFavoriteKey(file));
        }

        private string GetThumbnailLabel(GraFile file)
        {
            return (IsFavorite(file) ? "\u2605 " : string.Empty) +
                Path.GetFileNameWithoutExtension(file.FullPath) + "\n" + file.RelativeFolder;
        }

        private void LoadFavoriteGraFiles()
        {
            try
            {
                if (!File.Exists(favoritesFilePath)) return;
                foreach (string line in File.ReadAllLines(favoritesFilePath))
                    if (!string.IsNullOrWhiteSpace(line))
                        favoriteGraFiles.Add(line.Trim());
            }
            catch (Exception ex)
            {
                Logger.LogError("GraTab", "Could not load GRA favorites: " + ex.Message);
            }
        }

        private void SaveFavoriteGraFiles()
        {
            try
            {
                string directory = Path.GetDirectoryName(favoritesFilePath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                List<string> favorites = new List<string>(favoriteGraFiles);
                favorites.Sort(StringComparer.OrdinalIgnoreCase);
                File.WriteAllLines(favoritesFilePath, favorites.ToArray());
            }
            catch (Exception ex)
            {
                Logger.LogError("GraTab", "Could not save GRA favorites: " + ex.Message);
                MessageBox.Show("Could not save GRA favorites: " + ex.Message,
                    "GRA favorites", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadColumnMajorGraFiles()
        {
            try
            {
                IEnumerable<string> lines;
                if (File.Exists(columnMajorOverridesFilePath))
                {
                    lines = File.ReadAllLines(columnMajorOverridesFilePath);
                }
                else
                {
                    System.Reflection.Assembly assembly =
                        System.Reflection.Assembly.GetExecutingAssembly();
                    using (Stream stream = assembly.GetManifestResourceStream(BuiltInColumnMajorResourceName))
                    {
                        if (stream == null)
                            throw new InvalidDataException(
                                "The built-in GRA column-major list is missing from the application.");
                        using (StreamReader reader = new StreamReader(stream))
                            lines = reader.ReadToEnd().Split(new[] { "\r\n", "\n" },
                                StringSplitOptions.RemoveEmptyEntries);
                    }
                }

                foreach (string line in lines)
                    if (!string.IsNullOrWhiteSpace(line))
                        columnMajorGraFiles.Add(line.Trim());
                hasSavedColumnMajorList = true;
            }
            catch (Exception ex)
            {
                Logger.LogError("GraTab", "Could not load GRA column-major selections: " + ex);
                MessageBox.Show("Could not load the GRA column-major list: " + ex.Message,
                    "GRA column-major list", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveColumnMajorGraFiles()
        {
            try
            {
                columnMajorGraFiles.Clear();
                foreach (DataGridViewRow row in gridFiles.Rows)
                {
                    GraFile file = row.Tag as GraFile;
                    if (file != null && row.Cells["CM"].Value is bool enabled && enabled)
                        columnMajorGraFiles.Add(GetColumnMajorKey(file));
                }

                string directory = Path.GetDirectoryName(columnMajorOverridesFilePath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                List<string> savedFiles = new List<string>(columnMajorGraFiles);
                savedFiles.Sort(StringComparer.OrdinalIgnoreCase);
                File.WriteAllLines(columnMajorOverridesFilePath, savedFiles.ToArray());
                hasSavedColumnMajorList = true;
                MessageBox.Show(
                    "Saved " + savedFiles.Count + " GRA resource(s) with CM enabled.\n\n" +
                    "List file:\n" + columnMajorOverridesFilePath +
                    "\n\nThis saved list will be used on future launches.",
                    "GRA column-major list", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.LogError("GraTab", "Could not save GRA column-major selections: " + ex.Message);
                MessageBox.Show("Could not save the GRA column-major list: " + ex.Message,
                    "GRA column-major list", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string GetColumnMajorKey(GraFile file)
        {
            return file.RelativeFolder + "/" + Path.GetFileName(file.FullPath);
        }

        private void ToggleSelectedFavorite()
        {
            GraFile file = GetSelectedGraFile();
            if (file == null) return;
            string key = GetFavoriteKey(file);
            if (!favoriteGraFiles.Add(key)) favoriteGraFiles.Remove(key);
            SaveFavoriteGraFiles();
            ListViewItem item = thumbnailView.SelectedItems.Count > 0
                ? thumbnailView.SelectedItems[0]
                : thumbnailView.Items.Cast<ListViewItem>().FirstOrDefault(candidate => ReferenceEquals(candidate.Tag, file));
            if (item != null) item.Text = GetThumbnailLabel(file);
            if (btnToggleFavorite != null)
                btnToggleFavorite.Text = IsFavorite(file) ? "\u2605" : "\u2606";
            if (chkFavoritesOnly.Checked && !IsFavorite(file))
                ApplyGraFavoriteFilter();
            UpdateSelectedActionButtons();
        }

        private GraFile GetSelectedGraFile()
        {
            if (thumbnailView.Visible)
            {
                if (thumbnailView.SelectedItems.Count == 0) return null;
                ListViewItem selectedItem = thumbnailView.FocusedItem != null &&
                    thumbnailView.FocusedItem.Selected
                    ? thumbnailView.FocusedItem
                    : thumbnailView.SelectedItems[0];
                return selectedItem.Tag as GraFile;
            }
            return gridFiles.SelectedRows.Count == 0
                ? null
                : gridFiles.SelectedRows[0].Tag as GraFile;
        }

        private void UpdateSelectedActionButtons()
        {
            GraFile file = GetSelectedGraFile();
            bool hasSelection = file != null;
            bool singleSelection = GetSelectedGraFiles().Count == 1;
            btnToggleFavorite.Enabled = singleSelection;
            btnRestoreOriginal.Enabled = singleSelection && File.Exists(GetOriginalGraBackupPath(file.FullPath));
            btnToggleFavorite.Text = hasSelection && IsFavorite(file) ? "\u2605" : "\u2606";
            UpdateColumnMajorToggle();
        }

        private List<GraFile> GetSelectedGraFiles()
        {
            IEnumerable<GraFile> selectedFiles;
            if (thumbnailView.Visible)
            {
                selectedFiles = thumbnailView.SelectedItems.Cast<ListViewItem>()
                    .Select(item => item.Tag as GraFile);
            }
            else
            {
                selectedFiles = gridFiles.SelectedRows.Cast<DataGridViewRow>()
                    .Select(row => row.Tag as GraFile);
            }
            return selectedFiles.Where(file => file != null).Distinct().ToList();
        }

        private void UpdateColumnMajorToggle()
        {
            if (chkColumnMajor == null) return;
            List<GraFile> selectedFiles = GetSelectedGraFiles();
            updatingColumnMajorToggle = true;
            try
            {
                chkColumnMajor.Enabled = selectedFiles.Count > 0;
                chkColumnMajor.Checked = selectedFiles.Count > 0 && selectedFiles.All(file =>
                {
                    DataGridViewRow row = FindGridRow(file);
                    return row != null && row.Cells["CM"].Value is bool value && value;
                });
            }
            finally
            {
                updatingColumnMajorToggle = false;
            }
        }

        private void ApplyColumnMajorToSelection()
        {
            List<GraFile> selectedFiles = GetSelectedGraFiles();
            if (selectedFiles.Count == 0) return;

            bool setColumnMajor = !selectedFiles.All(file =>
            {
                DataGridViewRow row = FindGridRow(file);
                return row != null && row.Cells["CM"].Value is bool value && value;
            });
            suppressDecodeRefresh = true;
            try
            {
                foreach (GraFile file in selectedFiles)
                {
                    DataGridViewRow row = FindGridRow(file);
                    if (row != null) row.Cells["CM"].Value = setColumnMajor;
                }
            }
            finally
            {
                suppressDecodeRefresh = false;
            }

            ClearThumbnailImageCache();
            if (thumbnailView.Visible) LoadThumbnailImages();
            UpdatePreview();
        }

        private static string GetOriginalGraBackupPath(string graPath)
        {
            return graPath + OriginalGraBackupSuffix;
        }

        private void RestoreSelectedGra()
        {
            GraFile file = GetSelectedGraFile();
            if (file != null) RestoreOriginalGra(file);
        }

        private void RestoreOriginalGra(GraFile file)
        {
            string backupPath = GetOriginalGraBackupPath(file.FullPath);
            if (!File.Exists(backupPath))
            {
                MessageBox.Show("No original backup exists for this GRA resource.", "Restore Original",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show("Restore " + Path.GetFileName(file.FullPath) +
                " from its original backup?\n\nThe backup will be kept so you can restore it again.",
                "Restore Original GRA", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            string replacementPath = Path.Combine(Path.GetDirectoryName(file.FullPath),
                "." + Path.GetFileName(file.FullPath) + ".restore-" + Guid.NewGuid().ToString("N"));
            try
            {
                File.Copy(backupPath, replacementPath);
                File.Replace(replacementPath, file.FullPath, null);
                ClearThumbnailImageCache();
                if (thumbnailView.Visible) LoadThumbnailImages();
                if (ReferenceEquals(GetSelectedGraFile(), file)) UpdatePreview();
                DataGridViewRow row = FindGridRow(file);
                if (row != null) gridFiles.InvalidateRow(row.Index);
                MessageBox.Show("The original GRA resource has been restored.",
                    "Restore Original", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not restore the original GRA resource: " + ex.Message,
                    "Restore Original", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (File.Exists(replacementPath))
                {
                    try { File.Delete(replacementPath); }
                    catch (Exception ex) { Logger.LogError("GraTab restore temporary file", ex.Message); }
                }
            }
        }

        private string GetSelectedGraPath()
        {
            GraFile file = GetSelectedGraFile();
            return file == null ? null : file.FullPath;
        }

        private static string GetGraFilePath(DataGridViewRow row)
        {
            GraFile file = row == null ? null : row.Tag as GraFile;
            return file == null ? null : file.FullPath;
        }

        private static string GetGraOutputDirectory(DataGridViewRow row)
        {
            GraFile file = row == null ? null : row.Tag as GraFile;
            if (file == null) throw new InvalidDataException("The selected GRA resource has no source folder.");
            return Path.Combine(SharedState.ExtractionPath, file.RelativeFolder);
        }

        private void UpdatePreview()
        {
            GraFile selectedFile = GetSelectedGraFile();
            DataGridViewRow selectedRow = selectedFile == null ? null : FindGridRow(selectedFile);
            UpdateSelectedActionButtons();
            if (selectedFile == null || selectedRow == null) {
                btnEditGraphic.Enabled = false;
                if (pbPreview.Image != null) pbPreview.Image = null;
                pbPreview.Invalidate();
                return;
            }
            string filename = Path.GetFileName(selectedFile.FullPath);
            string path = selectedFile.FullPath;
            if (string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(path)) {
                btnEditGraphic.Enabled = false;
                return;
            }
            if (!File.Exists(path)) {
                btnEditGraphic.Enabled = false;
                return;
            }

            try
            {
                btnEditGraphic.Enabled = false;
                int modeOverride = GetSelectedMode(selectedRow);
                bool cmOverride = selectedRow.Cells["CM"].Value is bool b && b;

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
                    btnEditGraphic.Enabled = true;
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
                btnEditGraphic.Enabled = false;
                pbPreview.Invalidate();
            }
        }

        private void EditSelectedGraphic()
        {
            if (GetSelectedGraFile() == null || pbPreview.Image == null)
            {
                btnEditGraphic.Enabled = false;
                return;
            }

            string graPath = GetSelectedGraPath();
            if (string.IsNullOrEmpty(graPath)) {
                btnEditGraphic.Enabled = false;
                return;
            }
            DataGridViewRow selectedRow = FindGridRow(GetSelectedGraFile());
            int modeOverride = selectedRow == null ? -1 : GetSelectedMode(selectedRow);
            bool cmOverride = selectedRow != null && selectedRow.Cells["CM"].Value is bool value && value;
            SharedState.RequestEditGraFrame(
                (Bitmap)pbPreview.Image.Clone(), graPath, 0, currentFrameIdx, modeOverride, cmOverride);
            SharedState.RequestSwitchTab(6);
        }

        private void AutoDetectColumnMajor()
        {
            List<DataGridViewRow> targets = new List<DataGridViewRow>();
            if (gridFiles.Visible && gridFiles.SelectedRows.Count > 0)
                targets.AddRange(gridFiles.SelectedRows.Cast<DataGridViewRow>());
            else if (thumbnailView.Visible && thumbnailView.SelectedItems.Count > 0)
                targets.AddRange(GetSelectedGraFiles().Select(FindGridRow).Where(row => row != null));
            else
            {
                foreach (DataGridViewRow row in gridFiles.Rows)
                    if (row.Visible) targets.Add(row);
            }

            int enabledCount = 0;
            int changedCount = 0;
            suppressDecodeRefresh = true;
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    DataGridViewRow row = targets[i];
                    GraFile file = row.Tag as GraFile;
                    if (file == null) continue;
                    bool useColumnMajor = columnMajorGraFiles.Contains(GetColumnMajorKey(file));
                    if (useColumnMajor) enabledCount++;
                    bool previous = row.Cells["CM"].Value is bool value && value;
                    if (previous != useColumnMajor)
                    {
                        row.Cells["CM"].Value = useColumnMajor;
                        changedCount++;
                    }

                    lblCurrentAction.Text = "Applying verified CM list: " + (i + 1) + " / " + targets.Count;
                    if (i % 10 == 0) Application.DoEvents();
                }
            }
            finally
            {
                suppressDecodeRefresh = false;
                ClearThumbnailImageCache();
                if (thumbnailView.Visible) LoadThumbnailImages();
            }

            lblCurrentAction.Text = "Status: Idle";
            UpdatePreview();
            MessageBox.Show(
                "Applied the verified CM list to " + targets.Count + " GRA resource(s).\n" +
                "CM enabled: " + enabledCount + ". Settings changed: " + changedCount + ".",
                "Verified CM Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            List<DataGridViewRow> selectedRows = gridFiles.SelectedRows.Cast<DataGridViewRow>().ToList();
            if (selectedRows.Count == 0) {
                MessageBox.Show("Please select GRA files from the list first.");
                return;
            }
            int succeeded = 0, failed = 0;
            progressBar.Value = 0;
            int total = selectedRows.Count;
            int processed = 0;

            foreach (DataGridViewRow row in selectedRows)
            {
                string fileName = row.Cells["File"].Value.ToString();
                string fullPath = GetGraFilePath(row);
                string dest = Path.Combine(GetGraOutputDirectory(row), Path.GetFileNameWithoutExtension(fileName) + ".mp4");

                Directory.CreateDirectory(Path.GetDirectoryName(dest));
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
            ShowOutputSummary("MP4 Export Complete", succeeded, failed, GetSelectedOutputDirectories(selectedRows));
        }

        private void SetAllCheckboxes(string columnName, bool value)
        {
            foreach (DataGridViewRow row in gridFiles.Rows)
                row.Cells[columnName].Value = value;
            gridFiles.EndEdit();
        }

        private void ExtractSelected()
        {
            List<DataGridViewRow> selectedRows = gridFiles.SelectedRows.Cast<DataGridViewRow>().ToList();
            if (selectedRows.Count == 0) {
                MessageBox.Show("Please select GRA files from the list first.");
                return;
            }

            progressBar.Value = 0;
            int succeeded = 0, failed = 0;
            int total = selectedRows.Count;

            foreach (DataGridViewRow row in selectedRows) {
                string fileName = row.Cells["File"].Value.ToString();
                bool exportPNG = row.Cells["PNG"].Value is bool p && p;
                bool exportMP4 = row.Cells["MP4"].Value is bool m && m;
                string fullPath = GetGraFilePath(row);
                string outDir = GetGraOutputDirectory(row);
                Directory.CreateDirectory(outDir);

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
            ShowOutputSummary("GRA Extraction Complete", succeeded, failed,
                GetSelectedOutputDirectories(selectedRows));
        }

        private static List<string> GetSelectedOutputDirectories(IEnumerable<DataGridViewRow> rows)
        {
            HashSet<string> directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in rows)
            {
                GraFile file = row.Tag as GraFile;
                if (file != null) directories.Add(GetGraOutputDirectory(row));
            }
            return directories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void ShowOutputSummary(string title, int succeeded, int failed, List<string> outputDirectories)
        {
            string message = "Completed: " + succeeded + " succeeded, " + failed + " failed.";
            if (outputDirectories.Count > 0)
                message += "\n\nOutput folder(s):\n" + string.Join("\n", outputDirectories.ToArray());
            MessageBox.Show(message, title,
                MessageBoxButtons.OK,
                failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
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
                if (thumbnailLoadTimer != null)
                {
                    thumbnailLoadTimer.Stop();
                    thumbnailLoadTimer.Dispose();
                    thumbnailLoadTimer = null;
                }
                if (animFrames != null)
                {
                    foreach (var f in animFrames) f.Dispose();
                    animFrames.Clear();
                }
                if (thumbnailImages != null)
                {
                    if (thumbnailView != null) thumbnailView.LargeImageList = null;
                    thumbnailImages.Dispose();
                    thumbnailImages = null;
                }
                foreach (Bitmap image in thumbnailSourceImages) image.Dispose();
                thumbnailSourceImages.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
