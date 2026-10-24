using QFG5Extractor.Core;
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using QFG5Extractor.Core.UI;
using QFG5Extractor.Resources.Panorama;

namespace QFG5Extractor.Resources.Save
{
    public class SaveTab : UserControl
    {
        private QFG5PathField txtFilePath;
        private QFG5Button btnBrowse;
        private QFG5Button btnLoad;
        private QFG5Button btnSave;
        private DataGridView gridInventory;
        private SaveGameParser parser;
        private readonly SaveSession session;
        public event Action<string> SaveLoadRequested;

        private FlowLayoutPanel visualGrid;
        private FlowLayoutPanel catalogGrid;
        private FlowLayoutPanel catalogCategoryButtons;
        private Panel inventoryViewHost;
        private TextBox catalogSearchBox;
        private Button btnListView;
        private Button btnIconView;
        private ContextMenuStrip inventoryContextMenu;
        private ToolStripMenuItem menuDuplicateItem;
        private ToolStripMenuItem menuInsertEmptyItem;
        private ToolStripMenuItem menuDeleteItem;
        private string selectedCatalogCategory = "All";
        private readonly Dictionary<string, Button> catalogCategoryButtonMap =
            new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private string inventoryPath;
        private Panel previewPopup;
        private PictureBox previewBox;
        private Timer previewTimer;
        private System.Collections.Generic.List<Bitmap> previewFrames = new System.Collections.Generic.List<Bitmap>();
        private int previewFrameIdx = 0;
        private bool hasLoadedSave;
        private bool gridRefreshInProgress;
        private bool updatingCharacterModelControls;
        private bool updatingRoomControls;
        private int paletteRefreshVersion;
        private Label paletteStatusLabel;
        private Label hoverNameLabel;
        private NumericUpDown numericCharacterModelId;
        private ComboBox comboCharacterModelName;
        private Button btnApplyCharacterModel;
        private Label characterModelStatusLabel;
        private NumericUpDown numericRoomId;
        private ComboBox comboRoomName;
        private Button btnApplyRoom;
        private Label roomStatusLabel;
        private Label characterCoordinatesLabel;
        private Label characterRotationSizeLabel;
        private Label saveFileSizeLabel;
        private Button btnInventoryScreen;
        private Button btnCharacterScreen;
        private Button btnFlagsScreen;
        private Panel inventoryScreenPanel;
        private Panel characterScreenPanel;
        private Panel flagsScreenPanel;
        private CharacterModelPreviewControl characterModelPreview;
        private PanoramaProjectionControl roomPreview;
        private Label roomPreviewStatusLabel;
        private FlagsTab flagsScreen;
        private readonly List<PaletteItem> catalogItems = new List<PaletteItem>();

        private enum SaveEditorScreen
        {
            Inventory,
            Character,
            Flags
        }

        private sealed class PaletteSection
        {
            public string Name;
            public List<PaletteItem> Items = new List<PaletteItem>();
        }

        private sealed class PaletteItem
        {
            public string IdHex;
            public string Name;
            public string Category;
            public Image Image;
            public int Offset = -1;
        }

        public SaveTab(SaveSession session)
        {
            if (session == null) throw new ArgumentNullException("session");
            this.session = session;
            parser = new SaveGameParser(session);
            inventoryPath = Path.Combine(QFG5Theme.UIKitPath, "Inventory Graphics");
            
            previewTimer = new Timer { Interval = 100 };
            previewTimer.Tick += (s, e) => {
                if (previewFrames.Count > 0) {
                    previewFrameIdx = (previewFrameIdx + 1) % previewFrames.Count;
                    previewBox.Image = previewFrames[previewFrameIdx];
                }
            };

            InitializeComponent();
            CreatePreviewPopup();
        }

        public void AttachFlagsScreen(FlagsTab tab)
        {
            if (tab == null) throw new ArgumentNullException("tab");
            if (flagsScreen != null)
                throw new InvalidOperationException("A Flags screen is already attached.");

            flagsScreen = tab;
            tab.Dock = DockStyle.Fill;
            flagsScreenPanel.Controls.Add(tab);
            tab.RefreshFromSession();
        }

        private void CreatePreviewPopup()
        {
            previewPopup = new Panel {
                Size = new Size(160, 180),
                BackColor = QFG5Theme.ColorSurface,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };
            
            previewBox = new PictureBox {
                Dock = DockStyle.Top,
                Height = 140,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = QFG5Theme.ColorSurfaceRecessed
            };
            
            Label lblClose = new Label {
                Text = "Close",
                Dock = DockStyle.Bottom,
                Height = 25,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.BodyBoldFont,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = QFG5Theme.ColorBgDark,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            lblClose.Click += (s, e) => { previewPopup.Visible = false; previewTimer.Stop(); };

            previewPopup.Controls.Add(lblClose);
            previewPopup.Controls.Add(previewBox);
            this.Controls.Add(previewPopup);
            previewPopup.BringToFront();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;

            QFG5TableLayoutPanel mainLayout = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.RowHeight + 30));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            btnLoad = new QFG5Button { Text = "Load", Width = QFG5Layout.ButtonWidth, Height = 30 };
            btnLoad.Click += BtnLoad_Click;
            btnSave = new QFG5Button { Text = "Save", Width = QFG5Layout.ButtonWidth, Enabled = false, Height = 30 };
            btnSave.Click += BtnSave_Click;

            QFG5Button browseBtn;
            QFG5TableLayoutPanel header = QFG5Layout.CreatePathRow("Save File:", out txtFilePath, out browseBtn, btnLoad, btnSave);
            btnBrowse = browseBtn;
            btnBrowse.Click += BtnBrowse_Click;

            Panel characterModelPanel = new Panel {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorSurface
            };
            FlowLayoutPanel characterModelToolbar = new FlowLayoutPanel {
                Dock = DockStyle.Top,
                Height = 66,
                Padding = new Padding(6, 4, 6, 2),
                BackColor = QFG5Theme.ColorSurface,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };
            Label modelIdLabel = new Label {
                Text = "Model ID:",
                AutoSize = true,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                Margin = new Padding(2, 7, 4, 0)
            };
            numericCharacterModelId = new NumericUpDown {
                Minimum = 0,
                Maximum = byte.MaxValue,
                Width = 68,
                Height = 27,
                Enabled = false,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                TextAlign = HorizontalAlignment.Center,
                Margin = new Padding(0, 2, 10, 0)
            };
            numericCharacterModelId.ValueChanged += CharacterModelId_ValueChanged;
            Label modelNameLabel = new Label {
                Text = "Name:",
                AutoSize = true,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                Margin = new Padding(0, 7, 4, 0)
            };
            comboCharacterModelName = new ComboBox {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 150,
                Height = 27,
                Enabled = false,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 2, 8, 0)
            };
            foreach (CharacterModelInfo model in CharacterModelCatalog.Models)
                comboCharacterModelName.Items.Add(model);
            comboCharacterModelName.SelectedIndexChanged += CharacterModelName_SelectedIndexChanged;
            btnApplyCharacterModel = new QFG5Button {
                Text = "Apply Model",
                Width = 112,
                Height = 29,
                Enabled = false,
                Margin = new Padding(0, 1, 0, 0)
            };
            btnApplyCharacterModel.Click += ApplyCharacterModel_Click;
            characterModelToolbar.Controls.Add(modelIdLabel);
            characterModelToolbar.Controls.Add(numericCharacterModelId);
            characterModelToolbar.Controls.Add(modelNameLabel);
            characterModelToolbar.Controls.Add(comboCharacterModelName);
            characterModelToolbar.Controls.Add(btnApplyCharacterModel);
            characterModelToolbar.SizeChanged += (s, e) =>
                FitCharacterToolbar(characterModelToolbar, comboCharacterModelName,
                    btnApplyCharacterModel, 150);
            FitCharacterToolbar(characterModelToolbar, comboCharacterModelName,
                btnApplyCharacterModel, 150);
            characterModelStatusLabel = new Label {
                Text = "Load a save to change its character model.",
                Dock = DockStyle.Bottom,
                Height = 25,
                Padding = new Padding(8, 3, 0, 0),
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.BodyFont,
                BackColor = QFG5Theme.ColorSurface
            };
            characterModelPanel.Controls.Add(characterModelStatusLabel);
            characterModelPanel.Controls.Add(characterModelToolbar);

            Panel roomPanel = new Panel {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorSurface,
                Margin = new Padding(0)
            };
            FlowLayoutPanel roomToolbar = new FlowLayoutPanel {
                Dock = DockStyle.Top,
                Height = 66,
                Padding = new Padding(6, 4, 6, 2),
                BackColor = QFG5Theme.ColorSurface,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };
            Label roomIdLabel = new Label {
                Text = "Room ID:",
                AutoSize = true,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                Margin = new Padding(2, 7, 4, 0)
            };
            numericRoomId = new NumericUpDown {
                Minimum = 0,
                Maximum = 900,
                Width = 78,
                Height = 27,
                Enabled = false,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                TextAlign = HorizontalAlignment.Center,
                Margin = new Padding(0, 2, 10, 0)
            };
            numericRoomId.ValueChanged += RoomId_ValueChanged;
            Label roomNameLabel = new Label {
                Text = "Location:",
                AutoSize = true,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                Margin = new Padding(0, 7, 4, 0)
            };
            comboRoomName = new ComboBox {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 190,
                Height = 27,
                Enabled = false,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 2, 8, 0)
            };
            foreach (RoomLocationInfo location in RoomLocationCatalog.Locations.OrderByDescending(room => room.Id))
                comboRoomName.Items.Add(location);
            comboRoomName.SelectedIndexChanged += RoomName_SelectedIndexChanged;
            btnApplyRoom = new QFG5Button {
                Text = "Apply Room",
                Width = 112,
                Height = 29,
                Enabled = false,
                Margin = new Padding(0, 1, 0, 0)
            };
            btnApplyRoom.Click += ApplyRoom_Click;
            roomToolbar.Controls.Add(roomIdLabel);
            roomToolbar.Controls.Add(numericRoomId);
            roomToolbar.Controls.Add(roomNameLabel);
            roomToolbar.Controls.Add(comboRoomName);
            roomToolbar.Controls.Add(btnApplyRoom);
            roomToolbar.SizeChanged += (s, e) =>
                FitCharacterToolbar(roomToolbar, comboRoomName, btnApplyRoom, 190);
            FitCharacterToolbar(roomToolbar, comboRoomName, btnApplyRoom, 190);
            roomStatusLabel = new Label {
                Text = "Load a save to change its room.",
                Dock = DockStyle.Bottom,
                Height = 25,
                Padding = new Padding(8, 3, 0, 0),
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.BodyFont,
                BackColor = QFG5Theme.ColorSurface
            };
            roomPanel.Controls.Add(roomStatusLabel);
            roomPanel.Controls.Add(roomToolbar);

            TableLayoutPanel characterDiagnostics = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = QFG5Theme.ColorSurface,
                Padding = new Padding(6, 2, 6, 2),
                Margin = new Padding(0)
            };
            characterDiagnostics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            characterDiagnostics.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            characterDiagnostics.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            characterDiagnostics.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            characterCoordinatesLabel = new Label {
                Text = "Load a save to view character coordinates.",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            characterRotationSizeLabel = new Label {
                Text = "Rotation and size are read-only.",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            saveFileSizeLabel = new Label {
                Text = "Load a save to verify its file size.",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.BodyFont,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            characterDiagnostics.Controls.Add(characterCoordinatesLabel, 0, 0);
            characterDiagnostics.Controls.Add(characterRotationSizeLabel, 0, 1);
            characterDiagnostics.Controls.Add(saveFileSizeLabel, 0, 2);

            TableLayoutPanel characterDetails = new TableLayoutPanel {
                Dock = DockStyle.Top,
                Height = 288,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = QFG5Theme.ColorBgDark,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            characterDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            characterDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            characterDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            characterDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            characterModelPanel.Margin = new Padding(0);
            characterDetails.Controls.Add(characterModelPanel, 0, 0);
            characterDetails.Controls.Add(roomPanel, 0, 1);
            characterDetails.Controls.Add(characterDiagnostics, 0, 2);

            SplitContainer characterDetailsRoomSplit = new SplitContainer {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BackColor = QFG5Theme.ColorBgDark,
                SplitterWidth = 6,
                Panel1MinSize = 288,
                Panel2MinSize = 180
            };
            characterDetails.Dock = DockStyle.Fill;
            characterModelPreview = new CharacterModelPreviewControl {
                Dock = DockStyle.Fill,
                Margin = new Padding(4)
            };
            TableLayoutPanel roomPreviewLayout = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = QFG5Theme.ColorSurface,
                Padding = new Padding(6),
                Margin = new Padding(4)
            };
            roomPreviewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            roomPreviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            roomPreviewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            roomPreviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Label roomPreviewTitle = new Label {
                Text = "ROOM PREVIEW",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.SectionHeaderFont,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = QFG5Theme.ColorSurface
            };
            roomPreview = new PanoramaProjectionControl {
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            roomPreviewStatusLabel = new Label {
                Text = "Load a save to preview its room.",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true
            };
            roomPreviewLayout.Controls.Add(roomPreviewTitle, 0, 0);
            roomPreviewLayout.Controls.Add(roomPreview, 0, 1);
            roomPreviewLayout.Controls.Add(roomPreviewStatusLabel, 0, 2);
            characterDetailsRoomSplit.Panel1.Controls.Add(characterDetails);
            characterDetailsRoomSplit.Panel2.Controls.Add(roomPreviewLayout);
            QFG5Layout.SetSplitterRatioOnce(characterDetailsRoomSplit, 0.48, false);

            SplitContainer characterScreenSplit = new SplitContainer {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = QFG5Theme.ColorBgDark,
                SplitterWidth = 6,
                Panel1MinSize = 250,
                Panel2MinSize = 250
            };
            characterScreenSplit.Panel1.Controls.Add(characterDetailsRoomSplit);
            characterScreenSplit.Panel2.Controls.Add(characterModelPreview);
            QFG5Layout.SetSplitterRatioOnce(characterScreenSplit, 0.56);

            gridInventory = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                BackgroundColor = QFG5Theme.ColorBgDark,
                ForeColor = QFG5Theme.ColorTextPrimary,
                GridColor = QFG5Theme.ColorBorderOuter,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ScrollBars = ScrollBars.Both,
                Font = QFG5Theme.BodyFont,
                RowTemplate = { Height = 26 },
                ColumnHeadersHeight = 30
            };

            gridInventory.DefaultCellStyle.BackColor = QFG5Theme.ColorSurfaceRecessed;
            gridInventory.DefaultCellStyle.ForeColor = QFG5Theme.ColorTextPrimary;
            gridInventory.DefaultCellStyle.SelectionBackColor = QFG5Theme.ColorBorderHighlight;
            gridInventory.DefaultCellStyle.SelectionForeColor = QFG5Theme.ColorTextPrimary;
            gridInventory.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
            gridInventory.AlternatingRowsDefaultCellStyle.BackColor = QFG5Theme.ColorBgDark;
            gridInventory.AlternatingRowsDefaultCellStyle.ForeColor = QFG5Theme.ColorTextPrimary;
            gridInventory.ColumnHeadersDefaultCellStyle.BackColor = QFG5Theme.ColorSurface;
            gridInventory.ColumnHeadersDefaultCellStyle.ForeColor = QFG5Theme.ColorTextGold;
            gridInventory.ColumnHeadersDefaultCellStyle.Font = QFG5Theme.BodyBoldFont;
            gridInventory.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            gridInventory.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 2, 4, 2);
            gridInventory.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            gridInventory.EnableHeadersVisualStyles = false;

            gridInventory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Place", HeaderText = "Reservoir / Place", ReadOnly = true, Width = 150 });
            gridInventory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Offset", HeaderText = "Offset", ReadOnly = true, Width = 80 });
            gridInventory.Columns.Add(new DataGridViewTextBoxColumn { Name = "IdHex", HeaderText = "Item ID (Hex)", Width = 80 });
            
            var colName = new DataGridViewComboBoxColumn 
            { 
                Name = "Name", 
                HeaderText = "Item Name", 
                Width = 200,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox
            };
            foreach (var item in ItemDatabase.GetItems())
            {
                colName.Items.Add(item.Name);
            }
            gridInventory.Columns.Add(colName);

            gridInventory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Amount", Width = 80, ValueType = typeof(int) });

            gridInventory.CellValueChanged += GridInventory_CellValueChanged;
            gridInventory.CurrentCellDirtyStateChanged += (s, e) => {
                if (gridInventory.IsCurrentCellDirty) gridInventory.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            gridInventory.CellMouseDown += (s, e) => {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0) {
                    gridInventory.ClearSelection();
                    gridInventory.Rows[e.RowIndex].Selected = true;
                    gridInventory.CurrentCell = gridInventory.Rows[e.RowIndex].Cells[e.ColumnIndex >= 0 ? e.ColumnIndex : 0];
                }
            };

            inventoryContextMenu = new ContextMenuStrip();
            menuDuplicateItem = new ToolStripMenuItem("Duplicate Item Below", null, BtnInsert_Click);
            menuInsertEmptyItem = new ToolStripMenuItem("Add Empty Slot Below", null, BtnInsertEmpty_Click);
            menuDeleteItem = new ToolStripMenuItem("Delete Item", null, BtnDelete_Click);
            inventoryContextMenu.Items.AddRange(new ToolStripItem[] {
                menuDuplicateItem, menuInsertEmptyItem, new ToolStripSeparator(), menuDeleteItem
            });
            inventoryContextMenu.Opening += InventoryContextMenu_Opening;
            gridInventory.ContextMenuStrip = inventoryContextMenu;
            SplitContainer split = new SplitContainer {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = QFG5Theme.ColorBgDark,
                SplitterWidth = 6,
                Panel1MinSize = 360,
                Panel2MinSize = 300
            };
            QFG5Layout.SetSplitterRatioOnce(split, 0.62);

            Panel inventorySide = new Panel { Dock = DockStyle.Fill, BackColor = QFG5Theme.ColorBgDark };
            FlowLayoutPanel viewToolbar = new FlowLayoutPanel {
                Dock = DockStyle.Top,
                Height = 38,
                Padding = new Padding(4, 3, 4, 3),
                BackColor = QFG5Theme.ColorSurface,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            Label viewLabel = new Label {
                Text = "SAVE VIEW",
                AutoSize = true,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.BodyBoldFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(4, 5, 12, 0)
            };
            btnListView = CreateViewButton("List");
            btnIconView = CreateViewButton("Icons");
            btnListView.Click += (s, e) => SetInventoryView(false);
            btnIconView.Click += (s, e) => SetInventoryView(true);
            viewToolbar.Controls.Add(viewLabel);
            viewToolbar.Controls.Add(btnListView);
            viewToolbar.Controls.Add(btnIconView);

            QFG5PreviewFrame gridFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            gridFrame.Controls.Add(gridInventory);

            Panel inventoryIconView = new Panel { Dock = DockStyle.Fill };
            inventoryViewHost = new Panel { Dock = DockStyle.Fill };
            inventoryViewHost.Controls.Add(gridFrame);
            inventoryViewHost.Controls.Add(inventoryIconView);

            Panel pnlPaletteInfo = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = QFG5Theme.ColorSurface };
            hoverNameLabel = new Label {
                Text = "Hover over an item to see its name...",
                ForeColor = QFG5Theme.ColorTextMuted,
                Location = new Point(0, 0),
                Height = 26,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = QFG5Theme.ColorSurface
            };
            paletteStatusLabel = new Label {
                Text = "Load a save to view its items.",
                ForeColor = QFG5Theme.ColorTextMuted,
                Location = new Point(0, 26),
                Height = 26,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = QFG5Theme.ColorSurface
            };
            pnlPaletteInfo.Controls.Add(paletteStatusLabel);
            pnlPaletteInfo.Controls.Add(hoverNameLabel);
            visualGrid = new FlowLayoutPanel { 
                Dock = DockStyle.Fill, 
                AutoScroll = true, 
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                Padding = new Padding(0)
            };
            visualGrid.SizeChanged += (s, e) => UpdatePaletteItemSizes();
            inventoryIconView.Controls.Add(visualGrid);
            inventoryIconView.Controls.Add(pnlPaletteInfo);

            Panel catalogSide = new Panel { Dock = DockStyle.Fill, BackColor = QFG5Theme.ColorBgDark };
            Label catalogTitle = new Label {
                Text = "GAME ITEM CATALOG",
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(8, 0, 0, 0),
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.SectionHeaderFont,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = QFG5Theme.ColorSurface
            };
            catalogCategoryButtons = new FlowLayoutPanel {
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(4, 2, 4, 2),
                BackColor = QFG5Theme.ColorSurface,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            foreach (string category in new[] { "All", "Items", "Equipment", "Spells", "Other" })
            {
                Button categoryButton = CreateViewButton(category);
                categoryButton.AutoSize = false;
                categoryButton.Click += (s, e) => SelectCatalogCategory(category);
                catalogCategoryButtonMap.Add(category, categoryButton);
                catalogCategoryButtons.Controls.Add(categoryButton);
            }
            catalogCategoryButtons.SizeChanged += (s, e) => UpdateCatalogCategoryButtonSizes();
            catalogCategoryButtons.Layout += (s, e) => UpdateCatalogCategoryButtonSizes();
            catalogSearchBox = new TextBox {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Margin = new Padding(4)
            };
            catalogSearchBox.TextChanged += (s, e) => RenderCatalog();
            catalogGrid = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                Padding = new Padding(4),
                WrapContents = true
            };
            catalogGrid.SizeChanged += (s, e) => UpdateCatalogItemSizes();
            catalogSide.Controls.Add(catalogGrid);
            catalogSide.Controls.Add(catalogSearchBox);
            catalogSide.Controls.Add(catalogCategoryButtons);
            catalogSide.Controls.Add(catalogTitle);
            catalogSide.SizeChanged += (s, e) => UpdateCatalogCategoryButtonSizes();

            inventorySide.Controls.Add(inventoryViewHost);
            inventorySide.Controls.Add(viewToolbar);
            SetInventoryView(false);

            gridInventory.AllowDrop = true;
            gridInventory.DragEnter += CatalogDragEnter;
            gridInventory.DragOver += (s, e) => {
                e.Effect = e.Data.GetDataPresent(typeof(PaletteItem)) ? DragDropEffects.Copy : DragDropEffects.None;
            };
            gridInventory.DragDrop += GridInventory_DragDrop;

            split.Panel1.Controls.Add(inventorySide);
            split.Panel2.Controls.Add(catalogSide);

            FlowLayoutPanel screenNavigation = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                Padding = new Padding(4, 3, 4, 2),
                BackColor = QFG5Theme.ColorSurface,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            btnInventoryScreen = CreateViewButton("Inventory");
            btnInventoryScreen.Name = "InventoryScreenButton";
            btnInventoryScreen.Width = 104;
            btnInventoryScreen.Height = 30;
            btnCharacterScreen = CreateViewButton("Character");
            btnCharacterScreen.Name = "CharacterScreenButton";
            btnCharacterScreen.Width = 104;
            btnCharacterScreen.Height = 30;
            btnCharacterScreen.Click += (s, e) => SetSaveScreen(SaveEditorScreen.Character);
            btnInventoryScreen.Click += (s, e) => SetSaveScreen(SaveEditorScreen.Inventory);
            btnFlagsScreen = CreateViewButton("Flags");
            btnFlagsScreen.Name = "FlagsScreenButton";
            btnFlagsScreen.Width = 104;
            btnFlagsScreen.Height = 30;
            btnFlagsScreen.Click += (s, e) => SetSaveScreen(SaveEditorScreen.Flags);
            screenNavigation.Controls.Add(btnInventoryScreen);
            screenNavigation.Controls.Add(btnCharacterScreen);
            screenNavigation.Controls.Add(btnFlagsScreen);

            Panel saveScreenHost = new Panel {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorBgDark
            };
            inventoryScreenPanel = new Panel {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorBgDark,
                Padding = new Padding(4)
            };
            characterScreenPanel = new Panel {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorBgDark,
                Padding = new Padding(8)
            };
            flagsScreenPanel = new Panel {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorBgDark,
                Padding = new Padding(4),
                Visible = false
            };
            split.Dock = DockStyle.Fill;
            characterScreenPanel.Controls.Add(characterScreenSplit);
            inventoryScreenPanel.Controls.Add(split);
            saveScreenHost.Controls.Add(inventoryScreenPanel);
            saveScreenHost.Controls.Add(characterScreenPanel);
            saveScreenHost.Controls.Add(flagsScreenPanel);

            mainLayout.Controls.Add(new QFG5CollapsibleSection("SAVE FILE", header, QFG5Layout.RowHeight + 30), 0, 0);
            mainLayout.Controls.Add(screenNavigation, 0, 1);
            mainLayout.Controls.Add(saveScreenHost, 0, 2);
            this.Controls.Add(mainLayout);
            SetSaveScreen(SaveEditorScreen.Inventory);

            this.Load += (s, e) => {
                this.PerformLayout();
                UpdatePaletteItemSizes();
                UpdateCatalogItemSizes();
                if (IsHandleCreated)
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        UpdateCatalogCategoryButtonSizes();
                        UpdateCatalogItemSizes();
                    });
                }
            };
            PopulateCatalog();
            UpdateCatalogCategoryButtons();
        }

        private void SetSaveScreen(SaveEditorScreen screen)
        {
            if (inventoryScreenPanel == null || characterScreenPanel == null ||
                flagsScreenPanel == null || btnInventoryScreen == null ||
                btnCharacterScreen == null || btnFlagsScreen == null)
                return;

            inventoryScreenPanel.Visible = screen == SaveEditorScreen.Inventory;
            characterScreenPanel.Visible = screen == SaveEditorScreen.Character;
            flagsScreenPanel.Visible = screen == SaveEditorScreen.Flags;
            if (screen == SaveEditorScreen.Inventory)
                inventoryScreenPanel.BringToFront();
            else if (screen == SaveEditorScreen.Character)
                characterScreenPanel.BringToFront();
            else
                flagsScreenPanel.BringToFront();

            SetScreenButtonState(btnInventoryScreen, screen == SaveEditorScreen.Inventory);
            SetScreenButtonState(btnCharacterScreen, screen == SaveEditorScreen.Character);
            SetScreenButtonState(btnFlagsScreen, screen == SaveEditorScreen.Flags);
        }

        private static void FitCharacterToolbar(FlowLayoutPanel toolbar, ComboBox selector,
            Button applyButton, int preferredSelectorWidth)
        {
            int availableWidth = Math.Max(0, toolbar.ClientSize.Width - toolbar.Padding.Horizontal);
            applyButton.Width = Math.Max(82, Math.Min(112, availableWidth / 3));
            selector.Width = Math.Max(90, Math.Min(preferredSelectorWidth,
                availableWidth - applyButton.Width - 16));
        }

        private static void SetScreenButtonState(Button button, bool selected)
        {
            if (button == null) return;
            button.BackColor = selected
                ? QFG5Theme.ColorBorderHighlight
                : QFG5Theme.ColorSurfaceRecessed;
            button.ForeColor = selected
                ? QFG5Theme.ColorGoldBright
                : QFG5Theme.ColorTextPrimary;
            button.Font = selected ? QFG5Theme.BodyBoldFont : QFG5Theme.BodyFont;
            button.FlatAppearance.BorderColor = selected
                ? QFG5Theme.ColorGoldBright
                : QFG5Theme.ColorBorderOuter;
        }

        private static Button CreateViewButton(string text)
        {
            return new Button {
                Text = text,
                Width = 72,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Margin = new Padding(2, 0, 2, 0),
                Cursor = Cursors.Hand
            };
        }

        private void SetInventoryView(bool showIcons)
        {
            if (inventoryViewHost != null)
            {
                foreach (Control control in inventoryViewHost.Controls)
                    control.Visible = (showIcons && control != gridInventory.Parent) ||
                        (!showIcons && control == gridInventory.Parent);
            }
            if (btnListView != null)
            {
                btnListView.BackColor = showIcons ? QFG5Theme.ColorSurfaceRecessed : QFG5Theme.ColorBorderHighlight;
                btnIconView.BackColor = showIcons ? QFG5Theme.ColorBorderHighlight : QFG5Theme.ColorSurfaceRecessed;
            }
        }

        private void PopulateCatalog()
        {
            if (catalogGrid == null) return;
            catalogItems.Clear();
            HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ItemDatabase.ItemEntry entry in ItemDatabase.GetItems())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ID) ||
                    !seenIds.Add(entry.ID.Trim()))
                    continue;

                string idHex = entry.ID.Trim().ToUpperInvariant();
                catalogItems.Add(new PaletteItem {
                    IdHex = idHex,
                    Name = entry.Name,
                    Category = NormalizeCatalogCategory(entry.Category),
                    Image = idHex == "0000" ? null : GetItemImage(idHex, entry.Name, false)
                });
            }
            ApplySharedIconFallbacks();
            RenderCatalog();
        }

        private void ApplySharedIconFallbacks()
        {
            foreach (PaletteItem item in catalogItems)
            {
                if (item.Image != null) continue;
                string group = GetSharedIconGroup(item.Name);
                if (group == null) continue;

                PaletteItem source = catalogItems.FirstOrDefault(candidate =>
                    candidate != item &&
                    candidate.Category == item.Category &&
                    GetSharedIconGroup(candidate.Name) == group &&
                    candidate.Image != null);
                if (source == null) continue;

                item.Image = source.Image;
                Logger.LogInfo("SaveTab shared " + group + " icon for " + item.Name +
                    " (0x" + item.IdHex + ") from " + source.Name + " (0x" + source.IdHex + ").");
            }
        }

        private static string GetSharedIconGroup(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return null;
            string normalized = itemName.ToLowerInvariant();
            if (normalized.Contains("scroll")) return "scroll";
            if (normalized.Contains("amulet") || normalized.Contains("breathing")) return null;
            if (normalized.Contains("potion") || normalized.Contains("elixir") ||
                normalized.Contains("draught") || normalized.Contains("drink") ||
                normalized.Contains("water") || normalized.Contains("ale") ||
                normalized.Contains("wine"))
                return "drink";
            return null;
        }

        private void SelectCatalogCategory(string category)
        {
            selectedCatalogCategory = category;
            UpdateCatalogCategoryButtons();
            RenderCatalog();
        }

        private void UpdateCatalogCategoryButtons()
        {
            foreach (KeyValuePair<string, Button> entry in catalogCategoryButtonMap)
            {
                bool isSelected = string.Equals(entry.Key, selectedCatalogCategory, StringComparison.OrdinalIgnoreCase);
                entry.Value.BackColor = isSelected
                    ? QFG5Theme.ColorBorderHighlight
                    : QFG5Theme.ColorSurfaceRecessed;
            }
            UpdateCatalogCategoryButtonSizes();
        }

        private void UpdateCatalogCategoryButtonSizes()
        {
            if (catalogCategoryButtons == null || catalogCategoryButtonMap.Count == 0) return;
            int availableWidth = catalogCategoryButtons.ClientSize.Width -
                catalogCategoryButtons.Padding.Horizontal;
            int buttonMargins = catalogCategoryButtonMap.Values
                .Sum(button => button.Margin.Horizontal);
            int buttonWidth = Math.Max(1,
                (availableWidth - buttonMargins) / catalogCategoryButtonMap.Count);
            foreach (Button button in catalogCategoryButtonMap.Values)
                button.Width = buttonWidth;
        }

        private static string NormalizeCatalogCategory(string category)
        {
            if (string.Equals(category, "Items", StringComparison.OrdinalIgnoreCase)) return "Items";
            if (string.Equals(category, "Equipment", StringComparison.OrdinalIgnoreCase)) return "Equipment";
            if (string.Equals(category, "Spells", StringComparison.OrdinalIgnoreCase)) return "Spells";
            return "Other";
        }

        private void RenderCatalog()
        {
            foreach (Control control in catalogGrid.Controls) control.Dispose();
            catalogGrid.Controls.Clear();

            string selectedCategory = selectedCatalogCategory;
            string search = catalogSearchBox.Text.Trim();
            string[] categories = selectedCategory == "All"
                ? new[] { "Items", "Equipment", "Spells", "Other" }
                : new[] { selectedCategory };

            foreach (string category in categories)
            {
                List<PaletteItem> matches = catalogItems
                    .Where(item => item.Category == category &&
                        (search.Length == 0 ||
                         item.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         item.IdHex.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (matches.Count == 0) continue;

                Label sectionLabel = new Label {
                    Text = category.ToUpperInvariant(),
                    ForeColor = QFG5Theme.ColorTextGold,
                    Font = QFG5Theme.SectionHeaderFont,
                    Width = Math.Max(100, catalogGrid.ClientSize.Width - 30),
                    Height = 26,
                    Margin = new Padding(4, 8, 4, 2),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                catalogGrid.Controls.Add(sectionLabel);
                catalogGrid.SetFlowBreak(sectionLabel, true);

                foreach (PaletteItem item in matches)
                {
                    Panel card = new Panel {
                        Width = 96,
                        Height = 92,
                        Margin = new Padding(3),
                        BackColor = QFG5Theme.ColorSurface,
                        BorderStyle = BorderStyle.FixedSingle,
                        Tag = item,
                        Cursor = Cursors.Hand,
                        AllowDrop = false
                    };
                    PictureBox picture = new PictureBox {
                        Width = 58,
                        Height = 54,
                        Location = new Point(18, 3),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        BackColor = QFG5Theme.ColorSurfaceRecessed,
                        Image = item.Image,
                        Tag = item,
                        Cursor = Cursors.Hand
                    };
                    Label nameLabel = new Label {
                        Text = item.Name,
                        Width = 90,
                        Height = 29,
                        Location = new Point(2, 59),
                        ForeColor = QFG5Theme.ColorTextPrimary,
                        Font = QFG5Theme.BodyFont,
                        TextAlign = ContentAlignment.TopCenter,
                        AutoEllipsis = true,
                        Tag = item,
                        Cursor = Cursors.Hand
                    };
                    card.Controls.Add(picture);
                    card.Controls.Add(nameLabel);
                    AttachCatalogDrag(card);
                    AttachCatalogDrag(picture);
                    AttachCatalogDrag(nameLabel);
                    catalogGrid.Controls.Add(card);
                }
            }
            UpdateCatalogItemSizes();
        }

        private void AttachCatalogDrag(Control control)
        {
            control.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                PaletteItem item = (s as Control)?.Tag as PaletteItem;
                if (item != null) DoDragDrop(item, DragDropEffects.Copy);
            };
        }

        private void CatalogDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(PaletteItem))
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void GridInventory_DragDrop(object sender, DragEventArgs e)
        {
            PaletteItem item = e.Data.GetData(typeof(PaletteItem)) as PaletteItem;
            if (item == null) return;
            Point point = gridInventory.PointToClient(new Point(e.X, e.Y));
            DataGridView.HitTestInfo hit = gridInventory.HitTest(point.X, point.Y);
            if (hit.RowIndex < 0) return;
            ApplyCatalogItemToRow(gridInventory.Rows[hit.RowIndex], item);
        }

        private void InventoryContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            PaletteItem sourceItem = FindContextPaletteItem(inventoryContextMenu.SourceControl);
            if (sourceItem != null)
                SelectInventoryRowByOffset(sourceItem.Offset);

            int selectedIndex = GetSelectedInventoryIndex();
            DataGridViewRow selectedRow = gridInventory.SelectedRows.Count > 0
                ? gridInventory.SelectedRows[0]
                : gridInventory.CurrentRow;
            SaveGameParser.InventoryItem selectedItem = selectedRow == null
                ? null
                : selectedRow.Tag as SaveGameParser.InventoryItem;
            bool activeItem = session.IsLoaded && selectedIndex >= 0 &&
                selectedIndex < session.InventoryCount && selectedIndex < SaveGameParser.INVENTORY_POOL_SIZE &&
                (selectedItem == null || !selectedItem.IsHeader);
            bool canInsert = activeItem && session.InventoryCount < SaveGameParser.INVENTORY_POOL_SIZE;
            menuDuplicateItem.Enabled = canInsert;
            menuInsertEmptyItem.Enabled = canInsert;
            menuDeleteItem.Enabled = activeItem;
            e.Cancel = !activeItem;
        }

        private static PaletteItem FindContextPaletteItem(Control control)
        {
            while (control != null)
            {
                PaletteItem item = control.Tag as PaletteItem;
                if (item != null && item.Offset >= 0) return item;
                control = control.Parent;
            }
            return null;
        }

        private void SelectInventoryRowByOffset(int offset)
        {
            foreach (DataGridViewRow row in gridInventory.Rows)
            {
                if (row.Cells["Offset"].Value == null) continue;
                int rowOffset;
                if (!int.TryParse(row.Cells["Offset"].Value.ToString().Substring(2),
                    System.Globalization.NumberStyles.HexNumber, null, out rowOffset) ||
                    rowOffset != offset)
                    continue;
                gridInventory.ClearSelection();
                row.Selected = true;
                gridInventory.CurrentCell = row.Cells["IdHex"];
                return;
            }
        }

        private int GetSelectedInventoryIndex()
        {
            DataGridViewRow row = gridInventory.SelectedRows.Count > 0
                ? gridInventory.SelectedRows[0]
                : gridInventory.CurrentRow;
            if (row == null || row.Cells["Offset"].Value == null) return -1;
            int offset;
            if (!int.TryParse(row.Cells["Offset"].Value.ToString().Substring(2),
                System.Globalization.NumberStyles.HexNumber, null, out offset) ||
                offset < session.InventoryOffset)
                return -1;
            int relativeOffset = offset - session.InventoryOffset;
            return relativeOffset % SaveGameParser.INVENTORY_SLOT_SIZE == 0
                ? relativeOffset / SaveGameParser.INVENTORY_SLOT_SIZE
                : -1;
        }

        private void InventoryIcon_DragDrop(object sender, DragEventArgs e)
        {
            PaletteItem item = e.Data.GetData(typeof(PaletteItem)) as PaletteItem;
            Panel target = sender as Panel;
            PaletteItem current = target == null ? null : target.Tag as PaletteItem;
            if (item == null || current == null) return;
            DataGridViewRow row = gridInventory.Rows
                .Cast<DataGridViewRow>()
                .FirstOrDefault(candidate =>
                    candidate.Cells["Offset"].Value != null &&
                    Convert.ToInt32(candidate.Cells["Offset"].Value.ToString().Substring(2), 16) == current.Offset);
            if (row != null) ApplyCatalogItemToRow(row, item);
        }

        private void ApplyCatalogItemToRow(DataGridViewRow row, PaletteItem item)
        {
            if (row == null || item == null || row.ReadOnly) return;
            string offsetText = row.Cells["Offset"].Value == null ? string.Empty : row.Cells["Offset"].Value.ToString();
            int offset;
            if (offsetText.Length < 3 || !int.TryParse(offsetText.Substring(2),
                System.Globalization.NumberStyles.HexNumber, null, out offset) ||
                !session.IsActiveInventorySlot(offset))
                return;
            int currentAmount;
            if (row.Cells["Amount"].Value == null ||
                !int.TryParse(row.Cells["Amount"].Value.ToString(), out currentAmount) ||
                currentAmount < 0)
            {
                MessageBox.Show("The selected inventory slot has an invalid amount and cannot be replaced.",
                    "Invalid Inventory Slot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            gridRefreshInProgress = true;
            try
            {
                row.Cells["IdHex"].Value = item.IdHex;
                string displayName = ItemDatabase.GetName(item.IdHex);
                DataGridViewComboBoxColumn nameColumn = (DataGridViewComboBoxColumn)gridInventory.Columns["Name"];
                if (!nameColumn.Items.Contains(displayName)) nameColumn.Items.Add(displayName);
                row.Cells["Name"].Value = displayName;
                if (item.IdHex == "0000")
                    row.Cells["Amount"].Value = 0;
                else if (currentAmount == 0)
                    row.Cells["Amount"].Value = 1;
            }
            finally
            {
                gridRefreshInProgress = false;
            }

            if (!SyncInventoryRow(row))
            {
                MessageBox.Show("The item could not be applied because the inventory slot contains invalid data.",
                    "Invalid Inventory Slot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            gridInventory.ClearSelection();
            row.Selected = true;
            PopulatePalette();
        }

        private void UpdateCatalogItemSizes()
        {
            if (catalogGrid == null || catalogGrid.Controls.Count == 0) return;
            int availableWidth = Math.Max(100, catalogGrid.ClientSize.Width - 26);
            int columns = Math.Max(1, availableWidth / 104);
            int cardWidth = Math.Max(76, (availableWidth - columns * 6) / columns);
            foreach (Control control in catalogGrid.Controls)
            {
                if (control is Panel card)
                {
                    card.Width = cardWidth;
                    if (card.Controls.Count >= 2)
                    {
                        Control picture = card.Controls[0];
                        picture.Left = Math.Max(2, (cardWidth - picture.Width) / 2);
                        card.Controls[1].Width = cardWidth - 6;
                        card.Controls[1].Left = 3;
                    }
                }
                else if (control is Label label)
                {
                    label.Width = availableWidth;
                }
            }
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "QFG5 Save Games (*.QG5)|*.QG5|All Files (*.*)|*.*" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtFilePath.Text = ofd.FileName;
                    LoadSaveFile(ofd.FileName);
                }
            }
        }

        private void BtnLoad_Click(object sender, EventArgs e)
        {
            if (File.Exists(txtFilePath.Text))
            {
                LoadSaveFile(txtFilePath.Text);
            }
            else
            {
                MessageBox.Show("File does not exist: " + txtFilePath.Text, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadSaveFile(string path)
        {
            if (!CommitPendingEdits())
            {
                RestoreActivePath();
                ShowInvalidItemIdWarning("loading another save");
                return;
            }
            if (SaveLoadRequested != null) SaveLoadRequested(path);
        }

        public bool CommitPendingEdits()
        {
            if (!gridInventory.EndEdit()) return false;
            return SyncInventoryToSession();
        }

        public void RefreshFromSession()
        {
            txtFilePath.Text = session.IsLoaded ? session.FilePath : string.Empty;
            hasLoadedSave = session.IsLoaded;
            btnSave.Enabled = session.IsLoaded && !session.HasIncompleteCharacterRecord;
            RefreshCharacterControls();
            RefreshGrid();
            if (flagsScreen != null)
                flagsScreen.RefreshFromSession();
            if (hasLoadedSave) PopulatePalette();
        }

        private void RefreshCharacterControls()
        {
            RefreshCharacterModelControls();
            RefreshRoomControls();
            RefreshCharacterDiagnostics();
        }

        private void RefreshCharacterDiagnostics()
        {
            if (!session.IsLoaded)
            {
                characterCoordinatesLabel.Text = "Load a save to view character coordinates.";
                characterRotationSizeLabel.Text = "Rotation and size are read-only.";
                saveFileSizeLabel.Text = "Load a save to verify its file size.";
                return;
            }

            if (session.TryGetCharacterPlacement(out CharacterPlacementInfo placement))
            {
                characterCoordinatesLabel.Text =
                    "Position (read-only): X: " + FormatPlacementValue(placement.X, placement.XRaw) +
                    "   Y: " + FormatPlacementValue(placement.Y, placement.YRaw) +
                    "   Z: " + FormatPlacementValue(placement.Z, placement.ZRaw);
                characterRotationSizeLabel.Text =
                    "Rotation (read-only): " + FormatPlacementValue(placement.Rotation, placement.RotationRaw) +
                    "   Size (read-only): " + FormatPlacementValue(placement.Size, placement.SizeRaw);
            }
            else
            {
                string reason = session.CharacterPlacementUnavailableReason ??
                    "The character placement fields could not be read safely.";
                characterCoordinatesLabel.Text = reason;
                characterRotationSizeLabel.Text = "Rotation and size are unavailable.";
            }

            if (session.IsLengthPreserved)
            {
                saveFileSizeLabel.Text = "File size: " +
                    session.OriginalLength.ToString("N0", CultureInfo.InvariantCulture) +
                    " bytes (matches the loaded file). Save verifies the written size; this does not guarantee the game will load the save.";
            }
            else
            {
                saveFileSizeLabel.Text = "File size mismatch: loaded " +
                    session.OriginalLength.ToString("N0", CultureInfo.InvariantCulture) +
                    " bytes; current data " +
                    session.Length.ToString("N0", CultureInfo.InvariantCulture) +
                    " bytes. Saving will be refused until the lengths match.";
            }

            if (session.HasIncompleteCharacterRecord)
            {
                saveFileSizeLabel.Text = "Save is incomplete: only 'Qu' of the character room label remains. Saving is disabled.";
            }
        }

        private static string FormatPlacementValue(float value, uint rawValue)
        {
            return value.ToString("G9", CultureInfo.InvariantCulture) +
                " (raw " + rawValue.ToString(CultureInfo.InvariantCulture) + ")";
        }

        private void RefreshCharacterModelControls()
        {
            int modelId = 0;
            bool canRead = session.IsLoaded && session.TryGetCharacterModelId(out modelId);
            bool canEdit = canRead && session.CanEditCharacterData;
            updatingCharacterModelControls = true;
            numericCharacterModelId.Enabled = canEdit;
            comboCharacterModelName.Enabled = canEdit;
            btnApplyCharacterModel.Enabled = canEdit;

            if (canRead)
            {
                numericCharacterModelId.Value = modelId;
                SelectCharacterModelName(modelId);
                characterModelStatusLabel.Text = !canEdit
                    ? session.CharacterDataUnavailableReason
                    : "Current model: " + FormatCharacterModel(modelId) +
                        (session.IsDirty ? ". Unsaved changes are in memory; click Save." : ". Apply a new ID, then click Save.");
            }
            else
            {
                numericCharacterModelId.Value = 0;
                comboCharacterModelName.SelectedIndex = -1;
                characterModelStatusLabel.Text = session.IsLoaded
                    ? (session.CharacterModelUnavailableReason ?? "The character model field could not be located safely.")
                    : "Load a save to change its character model.";
            }
            updatingCharacterModelControls = false;
            if (canRead)
                characterModelPreview.SetModelId(modelId);
            else
                characterModelPreview.ClearModel(session.IsLoaded
                    ? (session.CharacterModelUnavailableReason ?? "The character model could not be read.")
                    : "Load a save to preview its character model.");
        }

        private void RefreshRoomControls()
        {
            int roomId = 0;
            bool canRead = session.IsLoaded && session.TryGetRoomId(out roomId);
            bool canEdit = canRead && session.CanEditCharacterData;
            updatingRoomControls = true;
            numericRoomId.Enabled = canEdit;
            comboRoomName.Enabled = canEdit;
            btnApplyRoom.Enabled = canEdit;

            if (canRead)
            {
                numericRoomId.Value = roomId;
                SelectRoomName(roomId);
                roomStatusLabel.Text = !canEdit
                    ? session.CharacterDataUnavailableReason
                    : "Current location: " + FormatRoomLocation(roomId) +
                        (session.IsDirty ? ". Unsaved changes are in memory; click Save." : ". Select another location, apply it, then click Save.");
            }
            else
            {
                numericRoomId.Value = 0;
                comboRoomName.SelectedIndex = -1;
                roomStatusLabel.Text = session.IsLoaded
                    ? (session.RoomUnavailableReason ?? "The room field could not be located safely.")
                    : "Load a save to change its room.";
            }
            updatingRoomControls = false;
            LoadRoomPreview(canRead ? roomId : -1);
        }

        private void SelectRoomName(int roomId)
        {
            comboRoomName.SelectedIndex = -1;
            for (int i = 0; i < comboRoomName.Items.Count; i++)
            {
                RoomLocationInfo location = comboRoomName.Items[i] as RoomLocationInfo;
                if (location != null && location.Id == roomId)
                {
                    comboRoomName.SelectedIndex = i;
                    return;
                }
            }
        }

        private static string FormatRoomLocation(int roomId)
        {
            RoomLocationInfo location;
            return RoomLocationCatalog.TryGet(roomId, out location)
                ? location.ToString()
                : roomId.ToString();
        }

        private void LoadRoomPreview(int roomId)
        {
            if (roomPreview == null || roomPreviewStatusLabel == null)
                return;
            if (roomId < 0)
            {
                roomPreview.ClearPanorama();
                roomPreviewStatusLabel.Text = "Load a save to preview its room.";
                return;
            }

            string panoramaPath = FindRoomPanoramaPath(roomId);
            if (panoramaPath == null)
            {
                roomPreview.ClearPanorama();
                roomPreviewStatusLabel.Text = "No extracted panorama found for room " +
                    roomId + ". Extract its view in Pano Editor.";
                return;
            }

            try
            {
                double startAngle = 0;
                double endAngle = 0;
                string imagePath = FindCompanionImagePath(panoramaPath);
                bool hasAngles = !string.IsNullOrEmpty(imagePath) &&
                    PanoConverter.TryReadProjectionAngles(imagePath, out startAngle, out endAngle);
                using (Bitmap bitmap = new Bitmap(panoramaPath))
                    if (hasAngles)
                        roomPreview.SetPanorama(bitmap, panoramaPath, startAngle, endAngle, true);
                    else
                        roomPreview.SetPanorama(bitmap, panoramaPath, -Math.PI, Math.PI, false);

                RoomLocationInfo location;
                string locationName = RoomLocationCatalog.TryGet(roomId, out location)
                    ? location.Name
                    : roomId.ToString();
                roomPreviewStatusLabel.Text = "Room " + roomId + " - " + locationName +
                    " | " + Path.GetFileName(panoramaPath) +
                    (hasAngles ? " | IMG angle range" : " | estimated 360-degree view");
            }
            catch (Exception ex)
            {
                roomPreview.ClearPanorama();
                roomPreviewStatusLabel.Text = "Could not preview room " + roomId + ": " + ex.Message;
                Logger.LogError("SaveTab", "Could not load room panorama " +
                    panoramaPath + ": " + ex.Message);
            }
        }

        private static string FindRoomPanoramaPath(int roomId)
        {
            string outputFolder = SharedState.GetModsPath("cdn", "NOD");
            if (!Directory.Exists(outputFolder))
                return null;

            string preferredPath = Path.Combine(outputFolder,
                (roomId * 10).ToString(CultureInfo.InvariantCulture) + ".bmp");
            if (File.Exists(preferredPath))
                return preferredPath;

            string[] files = Directory.GetFiles(outputFolder)
                .Where(path => string.Equals(Path.GetExtension(path), ".bmp",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (string path in files)
            {
                int resourceId;
                if (int.TryParse(Path.GetFileNameWithoutExtension(path), out resourceId) &&
                    resourceId >= 1000 && resourceId / 10 == roomId)
                    return path;
            }

            string directPath = Path.Combine(outputFolder,
                roomId.ToString(CultureInfo.InvariantCulture) + ".bmp");
            return File.Exists(directPath) ? directPath : null;
        }

        private static string FindCompanionImagePath(string panoramaPath)
        {
            string imageFolder = SharedState.GetModsPath("cdn", "IMG");
            if (!Directory.Exists(imageFolder))
                return null;

            string baseName = Path.GetFileNameWithoutExtension(panoramaPath);
            return Directory.GetFiles(imageFolder)
                .FirstOrDefault(path =>
                    string.Equals(Path.GetExtension(path), ".img", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Path.GetFileNameWithoutExtension(path), baseName,
                        StringComparison.OrdinalIgnoreCase));
        }

        private void SelectCharacterModelName(int modelId)
        {
            comboCharacterModelName.SelectedIndex = -1;
            for (int i = 0; i < comboCharacterModelName.Items.Count; i++)
            {
                CharacterModelInfo model = comboCharacterModelName.Items[i] as CharacterModelInfo;
                if (model != null && model.Id == modelId)
                {
                    comboCharacterModelName.SelectedIndex = i;
                    return;
                }
            }
        }

        private static string FormatCharacterModel(int modelId)
        {
            string name = CharacterModelCatalog.GetName(modelId);
            return name == null ? modelId.ToString() : modelId + " - " + name;
        }

        private void CharacterModelId_ValueChanged(object sender, EventArgs e)
        {
            if (updatingCharacterModelControls) return;
            updatingCharacterModelControls = true;
            SelectCharacterModelName((int)numericCharacterModelId.Value);
            updatingCharacterModelControls = false;
            characterModelStatusLabel.Text = "Selected model: " +
                FormatCharacterModel((int)numericCharacterModelId.Value) + ". Click Apply Model, then Save.";
            characterModelPreview.SetModelId((int)numericCharacterModelId.Value);
        }

        private void CharacterModelName_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (updatingCharacterModelControls) return;
            CharacterModelInfo model = comboCharacterModelName.SelectedItem as CharacterModelInfo;
            if (model == null) return;
            updatingCharacterModelControls = true;
            numericCharacterModelId.Value = model.Id;
            updatingCharacterModelControls = false;
            characterModelStatusLabel.Text = "Selected model: " +
                FormatCharacterModel(model.Id) + ". Click Apply Model, then Save.";
            characterModelPreview.SetModelId(model.Id);
        }

        private void ApplyCharacterModel_Click(object sender, EventArgs e)
        {
            try
            {
                int modelId = (int)numericCharacterModelId.Value;
                session.SetCharacterModelId(modelId);
                RefreshCharacterControls();
                characterModelStatusLabel.Text = "Applied model: " + FormatCharacterModel(modelId) +
                    (session.IsDirty
                        ? ". Click Save to write pending changes."
                        : ". This model is already active; no changes were made.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not change the character model: " + ex.Message,
                    "Character Model", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RefreshCharacterModelControls();
            }
        }

        private void RoomId_ValueChanged(object sender, EventArgs e)
        {
            if (updatingRoomControls) return;
            int roomId = (int)numericRoomId.Value;
            updatingRoomControls = true;
            SelectRoomName(roomId);
            updatingRoomControls = false;
            roomStatusLabel.Text = "Selected location: " + FormatRoomLocation(roomId) +
                ". Click Apply Room, then Save.";
            LoadRoomPreview(roomId);
        }

        private void RoomName_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (updatingRoomControls) return;
            RoomLocationInfo location = comboRoomName.SelectedItem as RoomLocationInfo;
            if (location == null) return;
            updatingRoomControls = true;
            numericRoomId.Value = location.Id;
            updatingRoomControls = false;
            roomStatusLabel.Text = "Selected location: " + location +
                ". Click Apply Room, then Save.";
            LoadRoomPreview(location.Id);
        }

        private void ApplyRoom_Click(object sender, EventArgs e)
        {
            try
            {
                int roomId = (int)numericRoomId.Value;
                session.SetRoomId(roomId);
                RefreshCharacterControls();
                roomStatusLabel.Text = "Applied location: " + FormatRoomLocation(roomId) +
                    (session.IsDirty
                        ? ". Click Save to write pending changes."
                        : ". This location is already active; no changes were made.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not change the room: " + ex.Message,
                    "Character Room", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RefreshRoomControls();
            }
        }

        public void RestoreActivePath()
        {
            txtFilePath.Text = session.IsLoaded ? session.FilePath : string.Empty;
        }

        private void RefreshGrid()
        {
            gridRefreshInProgress = true;
            try
            {
                gridInventory.Rows.Clear();
                var items = parser.GetInventory();
                foreach (var item in items)
                {
                    string displayName = ItemDatabase.GetName(item.IdHex);

                    int rowIndex = gridInventory.Rows.Add(
                        item.PlaceName,
                        $"0x{item.Offset:X4}",
                        item.IdHex,
                        displayName,
                        item.Amount
                    );

                    gridInventory.Rows[rowIndex].Tag = item; // Store item object for sync

                    if (item.IsHeader)
                    {
                        gridInventory.Rows[rowIndex].DefaultCellStyle.BackColor = QFG5Theme.ColorBorderOuter;
                        gridInventory.Rows[rowIndex].DefaultCellStyle.ForeColor = QFG5Theme.ColorTextPrimary;
                        gridInventory.Rows[rowIndex].ReadOnly = true;
                    }
                }
            }
            finally
            {
                gridRefreshInProgress = false;
            }
        }

        private void GridInventory_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || gridRefreshInProgress) return;
            DataGridViewRow row = gridInventory.Rows[e.RowIndex];

            if (gridInventory.Columns[e.ColumnIndex].Name == "Name")
            {
                string newName = row.Cells["Name"].Value?.ToString();
                if (!string.IsNullOrEmpty(newName))
                {
                    string newId = ItemDatabase.GetID(newName);
                    if (row.Cells["IdHex"].Value?.ToString() != newId)
                    {
                        row.Cells["IdHex"].Value = newId;
                    }
                }
            }
            else if (gridInventory.Columns[e.ColumnIndex].Name == "IdHex")
            {
                string newId = row.Cells["IdHex"].Value?.ToString()?.ToUpper();
                if (!string.IsNullOrEmpty(newId))
                {
                    string newName = ItemDatabase.GetName(newId);
                    if (row.Cells["Name"].Value?.ToString() != newName)
                    {
                        // If the name is not in the ComboBox items, we might need to add it temporarily
                        var col = (DataGridViewComboBoxColumn)gridInventory.Columns["Name"];
                        if (!col.Items.Contains(newName))
                        {
                            col.Items.Add(newName);
                        }
                        row.Cells["Name"].Value = newName;
                    }
                }
            }

            string changedColumn = gridInventory.Columns[e.ColumnIndex].Name;
            if (session.IsLoaded && (changedColumn == "Name" || changedColumn == "IdHex" || changedColumn == "Amount"))
                SyncInventoryRow(row);
            if (hasLoadedSave && (changedColumn == "Name" || changedColumn == "IdHex" || changedColumn == "Amount"))
                PopulatePalette();
        }

        private bool SyncInventoryToSession()
        {
            if (!session.IsLoaded) return true;
            foreach (DataGridViewRow row in gridInventory.Rows)
            {
                if (row.Cells["Offset"].Value != null && !SyncInventoryRow(row))
                    return false;
            }
            return true;
        }

        private bool SyncInventoryRow(DataGridViewRow row)
        {
            string offsetText = row.Cells["Offset"].Value.ToString();
            int offset = Convert.ToInt32(offsetText.Substring(2), 16);
            if (!session.IsActiveInventorySlot(offset)) return true;

            string idHex = row.Cells["IdHex"].Value == null
                ? "0000"
                : row.Cells["IdHex"].Value.ToString().ToUpper();
            if (string.IsNullOrEmpty(idHex)) idHex = "0000";
            if (idHex.Length != 4 || !Uri.IsHexDigit(idHex[0]) || !Uri.IsHexDigit(idHex[1]) ||
                !Uri.IsHexDigit(idHex[2]) || !Uri.IsHexDigit(idHex[3]))
                return false;
            int amount = 0;
            if (row.Cells["Amount"].Value != null)
            {
                if (!int.TryParse(row.Cells["Amount"].Value.ToString(), out amount) || amount < 0)
                    return false;
            }
            parser.UpdateInventoryItem(offset, idHex, amount);
            return true;
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (!CommitPendingEdits())
            {
                ShowInvalidItemIdWarning("deleting an item");
                return;
            }

            int selectedIndex = -1;
            if (gridInventory.SelectedRows.Count > 0)
                selectedIndex = gridInventory.SelectedRows[0].Index;
            else if (gridInventory.CurrentRow != null)
                selectedIndex = gridInventory.CurrentRow.Index;

            if (selectedIndex == -1) return;

            var items = parser.GetInventory();
            if (selectedIndex < 0 || selectedIndex >= items.Count) return;

            if (items[selectedIndex].IsHeader)
            {
                MessageBox.Show("Cannot delete system headers.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("Are you sure you want to delete this item?", "Confirm Delete", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                try
                {
                    parser.DeleteInventoryItem(selectedIndex);
                }
                catch (InvalidDataException ex)
                {
                    MessageBox.Show("Could not delete inventory item: " + ex.Message,
                        "Invalid Inventory", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                RefreshGrid();
                PopulatePalette();
            }
        }

        private void BtnInsert_Click(object sender, EventArgs e)
        {
            if (!CommitPendingEdits())
            {
                ShowInvalidItemIdWarning("inserting an item");
                return;
            }

            int selectedIndex = -1;
            if (gridInventory.SelectedRows.Count > 0)
                selectedIndex = gridInventory.SelectedRows[0].Index;
            else if (gridInventory.CurrentRow != null)
                selectedIndex = gridInventory.CurrentRow.Index;

            if (selectedIndex == -1) return;
            
            var items = parser.GetInventory();
            if (selectedIndex < 0 || selectedIndex >= items.Count) return;

            try
            {
                parser.InsertInventoryItem(selectedIndex);
            }
            catch (InvalidDataException ex)
            {
                MessageBox.Show("Could not insert inventory item: " + ex.Message,
                    "Invalid Inventory", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            RefreshGrid();
            PopulatePalette();
            
            // Select the newly inserted row
            if (selectedIndex + 1 < gridInventory.Rows.Count)
            {
                gridInventory.ClearSelection();
                gridInventory.Rows[selectedIndex + 1].Selected = true;
                gridInventory.FirstDisplayedScrollingRowIndex = Math.Max(0, selectedIndex - 5);
            }
        }

        private void BtnInsertEmpty_Click(object sender, EventArgs e)
        {
            if (!CommitPendingEdits())
            {
                ShowInvalidItemIdWarning("adding an empty slot");
                return;
            }

            int selectedIndex = GetSelectedInventoryIndex();
            if (selectedIndex < 0) return;

            try
            {
                parser.InsertEmptyInventoryItem(selectedIndex);
            }
            catch (InvalidDataException ex)
            {
                MessageBox.Show("Could not add an empty slot: " + ex.Message,
                    "Invalid Inventory", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RefreshGrid();
            PopulatePalette();
            SelectInventoryRow(selectedIndex + 1);
        }

        private void SelectInventoryRow(int index)
        {
            if (index < 0 || index >= gridInventory.Rows.Count) return;
            gridInventory.ClearSelection();
            gridInventory.Rows[index].Selected = true;
            gridInventory.CurrentCell = gridInventory.Rows[index].Cells["IdHex"];
            gridInventory.FirstDisplayedScrollingRowIndex = Math.Max(0, index - 5);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (!CommitPendingEdits())
                {
                    ShowInvalidItemIdWarning("saving");
                    return;
                }
                bool hadChanges = session.IsDirty;
                bool backupCreated = session.Save();
                string message;
                if (!hadChanges)
                {
                    message = "No changes to save.";
                }
                else if (backupCreated)
                {
                    message = "Saved successfully." + Environment.NewLine +
                        "Verified file size unchanged: " +
                        session.Length.ToString("N0", CultureInfo.InvariantCulture) + " bytes." +
                        Environment.NewLine +
                        "Original save backed up to:" + Environment.NewLine + session.FilePath + ".bak";
                }
                else
                {
                    message = "Saved successfully. File size verified unchanged: " +
                        session.Length.ToString("N0", CultureInfo.InvariantCulture) + " bytes." +
                        Environment.NewLine + "The existing .bak backup was left unchanged.";
                }
                RefreshCharacterControls();
                if (flagsScreen != null)
                    flagsScreen.RefreshFromSession();
                MessageBox.Show(message, "Save Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ShowInvalidItemIdWarning(string action)
        {
            MessageBox.Show("Correct the invalid item ID or amount before " + action + ".",
                "Invalid Save Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static System.Collections.Generic.Dictionary<string, Image> itemImageCache = new System.Collections.Generic.Dictionary<string, Image>();

        private void PopulatePalette()
        {
            if (!hasLoadedSave) return;

            List<PaletteSection> sections = GetPaletteSections();
            int itemCount = 0;
            foreach (PaletteSection section in sections) itemCount += section.Items.Count;

            int requestVersion = ++paletteRefreshVersion;
            Logger.LogPerf("SaveTab.PopulatePalette START");
            var swPalette = System.Diagnostics.Stopwatch.StartNew();
            SetStatus("Loading palette thumbnails...", QFG5Theme.ColorTextGold);
            System.Threading.Tasks.Task.Run(() => {
                try
                {
                    int missingImageCount = 0;
                    Image frameImg = null;
                    string framePath = Path.Combine(QFG5Theme.UIKitPath, "qfg-itemFrame.png");
                    try
                    {
                        if (File.Exists(framePath))
                        {
                            lock (itemImageCache)
                            {
                                if (!itemImageCache.ContainsKey("frame"))
                                    itemImageCache["frame"] = Image.FromFile(framePath);
                                frameImg = itemImageCache["frame"];
                            }
                        }
                        else
                        {
                            Logger.Log("SaveTab palette frame not found: " + framePath);
                        }
                    }
                    catch (Exception frameException)
                    {
                        Logger.Log("SaveTab palette frame could not be loaded: " + frameException.Message);
                    }

                    foreach (PaletteSection section in sections)
                    {
                        foreach (PaletteItem item in section.Items)
                        {
                            item.Image = GetItemImage(item.IdHex, item.Name);
                            if (item.Image == null) missingImageCount++;
                        }
                    }

                    swPalette.Stop();
                    Logger.LogPerf("SaveTab.PopulatePalette END: " + swPalette.ElapsedMilliseconds +
                        " ms (Saved items: " + itemCount + ", Missing thumbnails: " + missingImageCount + ")");

                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (requestVersion != paletteRefreshVersion || IsDisposed) return;
                            try
                            {
                                RenderPalette(sections, frameImg);
                                if (itemCount == 0)
                                    SetStatus("Palette empty: no non-empty saved slots.", QFG5Theme.ColorTextMuted);
                                else if (missingImageCount > 0)
                                    SetStatus("Palette ready: " + itemCount + " saved items; " + missingImageCount + " thumbnails unavailable.", QFG5Theme.ColorGold);
                                else
                                    SetStatus("Palette ready: " + itemCount + " saved items; all thumbnails available.", QFG5Theme.ColorGoldBright);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log("SaveTab palette rendering failed: " + ex);
                                SetStatus("Palette error: " + ex.Message, QFG5Theme.ColorTextPrimary);
                            }
                        });
                    }
                    catch (InvalidOperationException ex)
                    {
                        Logger.Log("SaveTab palette update could not be posted: " + ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    swPalette.Stop();
                    Logger.Log("SaveTab palette loading failed: " + ex);
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (requestVersion == paletteRefreshVersion && !IsDisposed)
                                SetStatus("Palette error: " + ex.Message, QFG5Theme.ColorTextPrimary);
                        });
                    }
                    catch (InvalidOperationException invokeException)
                    {
                        Logger.Log("SaveTab palette error could not be displayed: " + invokeException.Message);
                    }
                }
            });
        }

        private List<PaletteSection> GetPaletteSections()
        {
            List<PaletteSection> sections = new List<PaletteSection>();
            Dictionary<string, PaletteSection> sectionsByName =
                new Dictionary<string, PaletteSection>(StringComparer.OrdinalIgnoreCase);

            foreach (DataGridViewRow row in gridInventory.Rows)
            {
                if (row.IsNewRow) continue;
                SaveGameParser.InventoryItem sourceItem = row.Tag as SaveGameParser.InventoryItem;
                if (sourceItem != null && sourceItem.IsHeader) continue;

                string idHex = row.Cells["IdHex"].Value == null ? string.Empty : row.Cells["IdHex"].Value.ToString().Trim();
                if (idHex.Length == 0 || idHex.Equals("0000", StringComparison.OrdinalIgnoreCase)) continue;

                string placeName = row.Cells["Place"].Value == null ? string.Empty : row.Cells["Place"].Value.ToString().Trim();
                if (placeName.Length == 0) placeName = "Other";

                PaletteSection section;
                if (!sectionsByName.TryGetValue(placeName, out section))
                {
                    section = new PaletteSection { Name = placeName };
                    sectionsByName.Add(placeName, section);
                    sections.Add(section);
                }

                string itemName = row.Cells["Name"].Value == null ? string.Empty : row.Cells["Name"].Value.ToString().Trim();
                if (itemName.Length == 0) itemName = ItemDatabase.GetName(idHex);
                section.Items.Add(new PaletteItem {
                    IdHex = idHex.ToUpperInvariant(),
                    Name = itemName,
                    Category = "Saved",
                    Offset = sourceItem == null
                        ? Convert.ToInt32(row.Cells["Offset"].Value.ToString().Substring(2), 16)
                        : sourceItem.Offset
                });
            }

            return sections;
        }

        private void RenderPalette(List<PaletteSection> sections, Image frameImg)
        {
            foreach (Control control in visualGrid.Controls) control.Dispose();
            visualGrid.Controls.Clear();

            foreach (PaletteSection section in sections)
            {
                Label lblCat = new Label {
                    Text = section.Name.ToUpperInvariant(),
                    ForeColor = QFG5Theme.ColorTextGold,
                    Font = QFG5Theme.SectionHeaderFont,
                    Width = visualGrid.Width - 40,
                    Margin = new Padding(10, 12, 10, 8),
                    AutoSize = false,
                    Height = 28
                };
                visualGrid.Controls.Add(lblCat);
                visualGrid.SetFlowBreak(lblCat, true);

                foreach (PaletteItem item in section.Items)
                {
                    Panel pnl = new Panel {
                        Width = 72,
                        Height = 72,
                        Margin = new Padding(4),
                        BackgroundImage = frameImg,
                        BackgroundImageLayout = ImageLayout.Stretch,
                        Tag = item,
                        AllowDrop = true
                    };
                    pnl.DragEnter += CatalogDragEnter;
                    pnl.DragOver += (s, e) => {
                        e.Effect = e.Data.GetDataPresent(typeof(PaletteItem))
                            ? DragDropEffects.Copy
                            : DragDropEffects.None;
                    };
                    pnl.DragDrop += InventoryIcon_DragDrop;
                    pnl.ContextMenuStrip = inventoryContextMenu;
                    pnl.MouseDown += InventoryIcon_MouseDown;

                    PictureBox pb = new PictureBox
                    {
                        Width = 56,
                        Height = 56,
                        Location = new Point(8, 8),
                        BorderStyle = BorderStyle.None,
                        BackColor = Color.Transparent,
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Image = item.Image,
                        Tag = item
                    };
                    AttachInventoryIconDrop(pb, pnl);
                    pb.ContextMenuStrip = inventoryContextMenu;
                    pb.MouseDown += InventoryIcon_MouseDown;

                    pb.MouseEnter += (s, e) => {
                        if (hoverNameLabel != null) hoverNameLabel.Text = item.Name;
                    };
                    pb.MouseLeave += (s, e) => {
                        if (hoverNameLabel != null) hoverNameLabel.Text = "Hover over an item to see its name...";
                    };
                    pb.Click += (s, e) => ShowItemPreview(item.IdHex, item.Name);

                    pnl.Controls.Add(pb);
                    visualGrid.Controls.Add(pnl);
                }
            }

            UpdatePaletteItemSizes();
        }

        private void AttachInventoryIconDrop(Control control, Panel target)
        {
            control.AllowDrop = true;
            control.DragEnter += CatalogDragEnter;
            control.DragOver += (s, e) => {
                e.Effect = e.Data.GetDataPresent(typeof(PaletteItem))
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
            };
            control.DragDrop += (s, e) => InventoryIcon_DragDrop(target, e);
        }

        private void InventoryIcon_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            PaletteItem item = FindContextPaletteItem(sender as Control);
            if (item != null) SelectInventoryRowByOffset(item.Offset);
        }

        private void SetStatus(string msg, Color color) {
            if (paletteStatusLabel != null) {
                paletteStatusLabel.Text = msg;
                paletteStatusLabel.ForeColor = color;
            }
        }

        private void ShowItemPreview(string idHex, string name)
        {
            // Close old
            previewTimer.Stop();
            foreach(var f in previewFrames) f.Dispose();
            previewFrames.Clear();
            previewFrameIdx = 0;

            // Try to find matching GRA or frames
            string graPath = FindAssociatedGra(idHex, name);
            if (File.Exists(graPath)) {
                var converter = new QFG5Extractor.Resources.Gra.GraConverter();
                previewFrames = converter.DecodeAllFrames(graPath);
            }

            if (previewFrames.Count > 0) {
                previewBox.Image = previewFrames[0];
                previewTimer.Start();
            } else {
                // Just show the static icon
                previewBox.Image = GetItemImage(idHex, name);
            }

            // Position popup near cursor
            Point local = this.PointToClient(Cursor.Position);
            previewPopup.Location = new Point(Math.Max(10, local.X - 80), Math.Max(10, local.Y - 200));
            previewPopup.Visible = true;
            previewPopup.BringToFront();
        }

        private string FindAssociatedGra(string idHex, string name)
        {
            // Look for matching .gra in the same subfolders as icons
            string[] subdirs = { "Items", "Spells", "Equipment" };
            foreach (var sub in subdirs) {
                string dir = Path.Combine(inventoryPath, sub);
                if (!Directory.Exists(dir)) continue;
                
                string[] patterns = { $"{idHex}.gra", $"{name.Replace(" ", "")}.gra" };
                foreach (var p in patterns) {
                    string full = Path.Combine(dir, p);
                    if (File.Exists(full)) return full;
                }
            }
            return null;
        }

        private void UpdatePaletteItemSizes()
        {
            if (visualGrid == null || visualGrid.Controls.Count == 0) return;
            int availableWidth = visualGrid.ClientSize.Width - 25;
            int columns = availableWidth / 80;
            if (columns < 2) columns = 2; // Reduced minimum to allow scaling
            int itemWidth = availableWidth / columns;
            foreach (Control c in visualGrid.Controls) {
                if (c is Panel pnl) {
                    pnl.Width = itemWidth - 4;
                    pnl.Height = pnl.Width; 
                    if (pnl.Controls.Count > 0 && pnl.Controls[0] is PictureBox pb) {
                        int margin = pnl.Width / 8; // Adjust margin based on frame size
                        pb.Width = pnl.Width - (margin * 2);
                        pb.Height = pnl.Height - (margin * 2);
                        pb.Location = new Point(margin, margin);
                    }
                } else if (c is Label lbl) {
                    lbl.Width = availableWidth;
                }
            }
        }

        private Image GetItemImage(string idHex, string itemName = null, bool includeSharedFallback = true)
        {
            if (includeSharedFallback)
            {
                PaletteItem catalogItem = catalogItems.FirstOrDefault(item =>
                    string.Equals(item.IdHex, idHex, StringComparison.OrdinalIgnoreCase) && item.Image != null);
                if (catalogItem != null) return catalogItem.Image;
            }

            Image directImage;
            lock (itemImageCache)
                directImage = GetItemImageCached(idHex, itemName);
            if (directImage != null) return directImage;

            string group = GetSharedIconGroup(itemName);
            if (group == null) return null;
            ItemDatabase.ItemEntry itemEntry = ItemDatabase.GetItems().FirstOrDefault(item =>
                string.Equals(item.ID, idHex, StringComparison.OrdinalIgnoreCase));
            string category = itemEntry == null ? null : NormalizeCatalogCategory(itemEntry.Category);
            PaletteItem sharedImage = catalogItems.FirstOrDefault(item =>
                item.Image != null &&
                item.Category == category &&
                GetSharedIconGroup(item.Name) == group);
            return sharedImage == null ? null : sharedImage.Image;
        }

        private Image GetItemImageCached(string idHex, string itemName = null)
        {
            // Explicit user mappings remain available as overrides.
            string mappingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon_mapping.dat");
            if (File.Exists(mappingPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(mappingPath);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('|');
                        if (parts.Length == 2 && string.Equals(parts[0], idHex, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!File.Exists(parts[1]))
                            {
                                Logger.Log("SaveTab mapped item image not found: " + parts[1]);
                                break;
                            }
                            using (Image source = Image.FromFile(parts[1]))
                                return new Bitmap(source);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("SaveTab could not read item image mappings: " + ex.Message);
                }
            }

            if (string.IsNullOrEmpty(inventoryPath) || !Directory.Exists(inventoryPath)) {
                return null;
            }

            string cacheKey = idHex.ToUpper();
            if (itemImageCache.ContainsKey(cacheKey)) return itemImageCache[cacheKey];

            if (string.IsNullOrEmpty(idHex) || idHex.Length != 4 ||
                !Uri.IsHexDigit(idHex[0]) || !Uri.IsHexDigit(idHex[1]) ||
                !Uri.IsHexDigit(idHex[2]) || !Uri.IsHexDigit(idHex[3]))
                return null;

            byte lowByte = Convert.ToByte(idHex.Substring(0, 2), 16);
            byte highByte = Convert.ToByte(idHex.Substring(2, 2), 16);
            int spriteId = 0x10000 | lowByte | (highByte << 8);
            string fileName = spriteId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string[] extensions = { ".png", ".bmp" };
            string[] directories = { "Items", "Spells", "Equipment" };
            foreach (string directoryName in directories)
            {
                string directory = Path.Combine(inventoryPath, directoryName);
                foreach (string extension in extensions)
                {
                    string imagePath = Path.Combine(directory, fileName + extension);
                    if (!File.Exists(imagePath)) continue;
                    try
                    {
                        using (Image source = Image.FromFile(imagePath))
                        {
                            Image image = new Bitmap(source);
                            if (string.Equals(idHex, "298B", StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrEmpty(itemName) &&
                                 itemName.IndexOf("Tinderbox", StringComparison.OrdinalIgnoreCase) >= 0))
                                image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                            itemImageCache[cacheKey] = image;
                            return image;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("SaveTab could not load item sprite " + imagePath + ": " + ex.Message);
                    }
                }
            }
            return null;
        }
    }
}
