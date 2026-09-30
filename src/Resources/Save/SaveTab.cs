using QFG5Extractor.Core;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Threading.Tasks;
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
        public event Action<string> SaveFileLoaded;

        private FlowLayoutPanel visualGrid;
        private string inventoryPath;
        private Panel previewPopup;
        private PictureBox previewBox;
        private Timer previewTimer;
        private System.Collections.Generic.List<Bitmap> previewFrames = new System.Collections.Generic.List<Bitmap>();
        private int previewFrameIdx = 0;

        public SaveTab()
        {
            parser = new SaveGameParser();
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
                BackColor = Color.FromArgb(40, 35, 30),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };
            
            previewBox = new PictureBox {
                Dock = DockStyle.Top,
                Height = 140,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };
            
            Label lblClose = new Label {
                Text = "Close",
                Dock = DockStyle.Bottom,
                Height = 25,
                ForeColor = Color.Gold,
                TextAlign = ContentAlignment.MiddleCenter,
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.RowHeight));
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
                BackgroundColor = Color.Black,
                ForeColor = Color.White,
                GridColor = Color.FromArgb(40, 40, 40),
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };

            gridInventory.DefaultCellStyle.BackColor = Color.Black;
            gridInventory.DefaultCellStyle.ForeColor = Color.White;
            gridInventory.DefaultCellStyle.SelectionBackColor = Color.FromArgb(100, 100, 110);
            gridInventory.DefaultCellStyle.SelectionForeColor = Color.White;
            gridInventory.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(100, 100, 110);
            gridInventory.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
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
            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, BackColor = Color.Black };
            QFG5Layout.SetSplitterRatioOnce(split, 0.45);
            
            QFG5PreviewFrame gridFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            gridFrame.Controls.Add(gridInventory);

            // Container for visual inventory + hover name
            Panel pnlVisualSide = new Panel { Dock = DockStyle.Fill };
            Label lblHoverName = new Label { 
                Text = "Hover over an item to see its name...", 
                ForeColor = Color.Gold, 
                Dock = DockStyle.Top, 
                Height = 30, 
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(30, 25, 20)
            };
            this.Tag = lblHoverName;

            visualGrid = new FlowLayoutPanel { 
                Dock = DockStyle.Fill, 
                AutoScroll = true, 
                BackColor = Color.FromArgb(20, 15, 10),
                Padding = new Padding(0)
            };
            visualGrid.SizeChanged += (s, e) => {
                UpdatePaletteItemSizes();
            };
            
            pnlVisualSide.Controls.Add(visualGrid);
            pnlVisualSide.Controls.Add(lblHoverName);
            
            PopulatePalette();

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

            mainLayout.Controls.Add(header, 0, 0);
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
            try
            {
                parser.LoadSave(path);
                RefreshGrid();
                btnSave.Enabled = true;
                if (SaveFileLoaded != null) SaveFileLoaded(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading save file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSave.Enabled = false;
            }
        }

        private void RefreshGrid()
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
                    gridInventory.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightGray;
                    gridInventory.Rows[rowIndex].ReadOnly = true;
                }
            }
        }

        private void GridInventory_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
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
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
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
                parser.DeleteInventoryItem(selectedIndex);
                RefreshGrid();
            }
        }

        private void BtnInsert_Click(object sender, EventArgs e)
        {
            int selectedIndex = -1;
            if (gridInventory.SelectedRows.Count > 0)
                selectedIndex = gridInventory.SelectedRows[0].Index;
            else if (gridInventory.CurrentRow != null)
                selectedIndex = gridInventory.CurrentRow.Index;

            if (selectedIndex == -1) return;
            
            var items = parser.GetInventory();
            if (selectedIndex < 0 || selectedIndex >= items.Count) return;

            parser.InsertInventoryItem(selectedIndex);
            RefreshGrid();
            
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
            gridInventory.EndEdit(); // Commit current cell

            foreach (DataGridViewRow row in gridInventory.Rows)
            {
                if (row.Cells["Offset"].Value == null) continue;
                string offsetStr = row.Cells["Offset"].Value.ToString();
                int offset = Convert.ToInt32(offsetStr.Substring(2), 16);
                string idHex = row.Cells["IdHex"].Value?.ToString().ToUpper() ?? "0000";
                int amount = 0;
                if (row.Cells["Amount"].Value != null)
                    int.TryParse(row.Cells["Amount"].Value.ToString(), out amount);

                parser.UpdateInventoryItem(offset, idHex, amount);
            }

            try
            {
                parser.SaveSave();
                MessageBox.Show("Saved successfully.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static System.Collections.Generic.Dictionary<string, Image> itemImageCache = new System.Collections.Generic.Dictionary<string, Image>();

        private void PopulatePalette()
        {
            SetStatus("Loading Inventory Palette...", Color.LightBlue);
            System.Threading.Tasks.Task.Run(() => {
                var allItems = ItemDatabase.GetItems();
                Image frameImg = null;
                try { 
                    string framePath = Path.Combine(QFG5Theme.UIKitPath, "qfg-itemFrame.png");
                    if (!itemImageCache.ContainsKey("frame")) itemImageCache["frame"] = Image.FromFile(framePath);
                    frameImg = itemImageCache["frame"];
                } catch { }

                string[] categories = { "Equipment", "Spells", "Items", "Other" };
                
                foreach (string cat in categories)
                {
                    var catItems = allItems.FindAll(i => 
                        (cat == "Other" && i.Category != "Equipment" && i.Category != "Spells" && i.Category != "Items") || 
                        (i.Category == cat)
                    );
                    
                    if (catItems.Count == 0) continue;

                    this.Invoke((MethodInvoker)delegate {
                        // Force a new row for each category by adding a full-width spacer if needed or just a clear break
                        Label lblCat = new Label { 
                            Text = cat.ToUpper(), 
                            ForeColor = Color.Gold, 
                            Font = new Font("Arial", 10, FontStyle.Bold), 
                            Width = visualGrid.Width - 40, 
                            Margin = new Padding(10, 20, 10, 10), 
                            AutoSize = false,
                            Height = 30
                        };
                        visualGrid.Controls.Add(lblCat);
                        visualGrid.SetFlowBreak(lblCat, true);
                    });

                    foreach (var item in catItems)
                    {
                        if (item.ID == "0000") continue;

                        Image itemImg = GetItemImage(item.ID, item.Name);
                        
                        this.Invoke((MethodInvoker)delegate {
                            Panel pnl = new Panel { 
                                Width = 72, Height = 72, Margin = new Padding(4),
                                BackgroundImage = frameImg,
                                BackgroundImageLayout = ImageLayout.Stretch
                            };
                            
                            PictureBox pb = new PictureBox
                            {
                                Width = 56, Height = 56,
                                Location = new Point(8, 8),
                                BorderStyle = BorderStyle.None,
                                BackColor = Color.Transparent,
                                SizeMode = PictureBoxSizeMode.Zoom,
                                Image = itemImg,
                                Tag = $"{item.ID}|{item.Name}"
                            };

                            pb.MouseDown += (s, e) => {
                                if (e.Button == MouseButtons.Left) {
                                    pb.DoDragDrop(pb.Tag, DragDropEffects.Copy);
                                }
                            };
                            
                            pb.MouseEnter += (s, e) => {
                                if (this.Tag is Label l) l.Text = item.Name;
                            };
                            pb.MouseLeave += (s, e) => {
                                if (this.Tag is Label l) l.Text = "Hover over an item to see its name...";
                            };

                            pb.Click += (s, e) => {
                                ShowItemPreview(item.ID, item.Name);
                            };

                            pnl.Controls.Add(pb);
                            visualGrid.Controls.Add(pnl);
                        });
                    }
                }
                
                this.Invoke((MethodInvoker)delegate {
                    UpdatePaletteItemSizes();
                    SetStatus("Ready", Color.Gold);
                });
            });
        }

        private void SetStatus(string msg, Color color) {
            if (this.Tag is Label l) {
                l.Text = msg;
                l.ForeColor = color;
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
