using QFG5Extractor.Core;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using QFG5Extractor.Core.UI;

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
        private string inventoryPath;
        private Panel previewPopup;
        private PictureBox previewBox;
        private Timer previewTimer;
        private System.Collections.Generic.List<Bitmap> previewFrames = new System.Collections.Generic.List<Bitmap>();
        private int previewFrameIdx = 0;
        private bool hasLoadedSave;
        private bool gridRefreshInProgress;
        private int paletteRefreshVersion;
        private Label paletteStatusLabel;
        private Label hoverNameLabel;

        private sealed class PaletteSection
        {
            public string Name;
            public List<PaletteItem> Items = new List<PaletteItem>();
        }

        private sealed class PaletteItem
        {
            public string IdHex;
            public string Name;
            public Image Image;
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
                RowCount = 2,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.RowHeight + 30));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            btnLoad = new QFG5Button { Text = "Load", Width = QFG5Layout.ButtonWidth, Height = 30 };
            btnLoad.Click += BtnLoad_Click;
            btnSave = new QFG5Button { Text = "Save", Width = QFG5Layout.ButtonWidth, Enabled = false, Height = 30 };
            btnSave.Click += BtnSave_Click;

            QFG5Button browseBtn;
            QFG5TableLayoutPanel header = QFG5Layout.CreatePathRow("Save File:", out txtFilePath, out browseBtn, btnLoad, btnSave);
            btnBrowse = browseBtn;
            btnBrowse.Click += BtnBrowse_Click;

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

            // Context menu for insertion/deletion
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Insert Item Below", null, BtnInsert_Click);
            menu.Items.Add("Delete Item", null, BtnDelete_Click);
            gridInventory.ContextMenuStrip = menu;
            SplitContainer split = new SplitContainer {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = QFG5Theme.ColorBgDark,
                SplitterWidth = 6,
                Panel1MinSize = 420,
                Panel2MinSize = 280
            };
            QFG5Layout.SetSplitterRatioOnce(split, 0.62);
            
            QFG5PreviewFrame gridFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            gridFrame.Controls.Add(gridInventory);

            // Container for visual inventory + hover name
            Panel pnlVisualSide = new Panel { Dock = DockStyle.Fill };
            Panel pnlPaletteInfo = new Panel {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = QFG5Theme.ColorSurface
            };
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
            visualGrid.SizeChanged += (s, e) => {
                UpdatePaletteItemSizes();
            };
            
            pnlVisualSide.Controls.Add(visualGrid);
            pnlVisualSide.Controls.Add(pnlPaletteInfo);

            gridInventory.AllowDrop = true;
            gridInventory.DragOver += (s, e) => {
                e.Effect = DragDropEffects.Copy;
            };
            gridInventory.DragDrop += (s, e) => {
                Point p = gridInventory.PointToClient(new Point(e.X, e.Y));
                var hit = gridInventory.HitTest(p.X, p.Y);
                if (hit.RowIndex >= 0) {
                    string[] data = ((string)e.Data.GetData(typeof(string))).Split('|');
                    string idHex = data[0];
                    string name = data[1];
                    
                    gridInventory.Rows[hit.RowIndex].Cells["IdHex"].Value = idHex;
                    gridInventory.Rows[hit.RowIndex].Cells["Name"].Value = name;
                    gridInventory.EndEdit();
                }
            };

            gridInventory.SelectionChanged += (s, e) => {
                // Removed UpdateVisualSelection as we now show all items palette
            };

            split.Panel1.Controls.Add(gridFrame);
            split.Panel2.Controls.Add(pnlVisualSide);

            mainLayout.Controls.Add(new QFG5CollapsibleSection("SAVE FILE", header, QFG5Layout.RowHeight + 30), 0, 0);
            mainLayout.Controls.Add(split, 0, 1);

            this.Controls.Add(mainLayout);

            this.Load += (s, e) => {
                this.PerformLayout();
                UpdatePaletteItemSizes();
            };
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
            btnSave.Enabled = session.IsLoaded;
            RefreshGrid();
            if (hasLoadedSave) PopulatePalette();
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
                int.TryParse(row.Cells["Amount"].Value.ToString(), out amount);
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

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (!CommitPendingEdits())
                {
                    ShowInvalidItemIdWarning("saving");
                    return;
                }
                session.Save();
                MessageBox.Show("Saved successfully.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ShowInvalidItemIdWarning(string action)
        {
            MessageBox.Show("Correct the invalid item ID before " + action + ".",
                "Invalid Item ID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                section.Items.Add(new PaletteItem { IdHex = idHex.ToUpperInvariant(), Name = itemName });
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
                        BackgroundImageLayout = ImageLayout.Stretch
                    };

                    PictureBox pb = new PictureBox
                    {
                        Width = 56,
                        Height = 56,
                        Location = new Point(8, 8),
                        BorderStyle = BorderStyle.None,
                        BackColor = Color.Transparent,
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Image = item.Image,
                        Tag = item.IdHex + "|" + item.Name
                    };

                    pb.MouseDown += (s, e) => {
                        if (e.Button == MouseButtons.Left)
                            pb.DoDragDrop(pb.Tag, DragDropEffects.Copy);
                    };
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

        private void SetStatus(string msg, Color color) {
            if (paletteStatusLabel != null) {
                paletteStatusLabel.Text = msg;
                paletteStatusLabel.ForeColor = color;
            }
        }

        private System.Collections.Generic.Dictionary<string, string[]> dirCache = new System.Collections.Generic.Dictionary<string, string[]>();

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

        private Image GetItemImage(string idHex, string name)
        {
            lock (itemImageCache)
            {
                return GetItemImageCached(idHex, name);
            }
        }

        private Image GetItemImageCached(string idHex, string name)
        {
            // Priority 1: Manual Mapping
            try {
                string mappingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon_mapping.dat");
                if (File.Exists(mappingPath)) {
                    string[] lines = File.ReadAllLines(mappingPath);
                    foreach (string line in lines) {
                        string[] parts = line.Split('|');
                        if (parts.Length == 2 && parts[0] == idHex.ToUpper() && File.Exists(parts[1])) {
                            return Image.FromFile(parts[1]);
                        }
                    }
                }
            } catch { }

            if (string.IsNullOrEmpty(inventoryPath) || !Directory.Exists(inventoryPath)) {
                return null;
            }

            string cacheKey = idHex.ToUpper();
            if (itemImageCache.ContainsKey(cacheKey)) return itemImageCache[cacheKey];

            var itemEntry = ItemDatabase.GetItems().Find(it => it.ID == idHex.ToUpper());
            string indexFileName = "";
            
            if (itemEntry != null && itemEntry.CategoryIndex >= 0) {
                int baseVal = 0;
                if (itemEntry.Category == "Equipment") baseVal = 101101;
                else if (itemEntry.Category == "Items") baseVal = 101129;
                else if (itemEntry.Category == "Spells") baseVal = 101500;
                
                if (baseVal > 0) indexFileName = (baseVal + itemEntry.CategoryIndex).ToString();
            }

            string[] subdirs = { "Items", "Spells", "Equipment" };
            foreach (var sub in subdirs)
            {
                string dir = Path.Combine(inventoryPath, sub);
                if (!Directory.Exists(dir)) continue;

                int idDec = -1;
                bool isNumeric = int.TryParse(idHex, System.Globalization.NumberStyles.Integer, null, out idDec);
                if (!isNumeric) {
                    try {
                        if (idHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                            idDec = Convert.ToInt32(idHex.Substring(2), 16);
                        else
                            idDec = Convert.ToInt32(idHex, 16);
                    } catch { }
                }
                
                string idDecStr = idDec.ToString();
                // User requirement: Priority for category-indexed .png
                string[] patterns = { 
                    $"{indexFileName}.png", $"{indexFileName}.bmp",
                    $"{idDecStr}.png", $"{idHex}.png", $"{name}.png",
                    $"{idDecStr}.bmp", $"{idHex}.bmp", $"{name}.bmp"
                };

                foreach (var p in patterns)
                {
                    string full = Path.Combine(dir, p);
                    if (File.Exists(full)) {
                        try { 
                            Image img = Image.FromFile(full);
                            itemImageCache[cacheKey] = img;
                            return img; 
                        } catch { }
                    }
                    
                    // Case-insensitive/Fuzzy fallback
                    try {
                        if (!dirCache.ContainsKey(dir)) dirCache[dir] = Directory.GetFiles(dir, "*.*");
                        string[] matches = dirCache[dir];
                        foreach(var m in matches) {
                            string fn = Path.GetFileName(m).ToLower();
                            if (fn.Contains(idDecStr.ToLower() + ".") || fn.Contains(idHex.ToLower() + ".") || fn.Contains(name.ToLower().Replace(" ", ""))) {
                                Image img = Image.FromFile(m);
                                itemImageCache[cacheKey] = img;
                                return img;
                            }
                        }
                    } catch { }
                }
            }
            return null;
        }
    }
}
