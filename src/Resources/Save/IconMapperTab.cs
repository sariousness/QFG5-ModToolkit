using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using QFG5Extractor.Resources.Save;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace QFG5Extractor.Resources.Save
{
    public class IconMapperTab : UserControl
    {
        private ListBox lstItems;
        private FlowLayoutPanel pnlIcons;
        private Dictionary<string, string> mappings = new Dictionary<string, string>();
        private string mappingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon_mapping.dat");
        private string inventoryPath;

        public IconMapperTab()
        {
            InitializeComponent();
            LoadItems();
            LoadMappings();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(40, 35, 30);

            TableLayoutPanel main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            lstItems = new ListBox { Dock = DockStyle.Fill, BackColor = Color.Black, ForeColor = Color.White, Font = new Font("Consolas", 10) };
            lstItems.AllowDrop = true;
            lstItems.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.StringFormat)) e.Effect = DragDropEffects.Link; };
            lstItems.DragDrop += LstItems_DragDrop;
            lstItems.DrawMode = DrawMode.OwnerDrawFixed;
            lstItems.DrawItem += LstItems_DrawItem;

            pnlIcons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(20, 15, 10) };

            main.Controls.Add(lstItems, 0, 0);
            main.Controls.Add(pnlIcons, 1, 0);

            FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(5) };
            QFG5Button btnSave = new QFG5Button { Text = "Save Mappings", Width = 120, Height = 30 };
            btnSave.Click += (s, e) => SaveMappings();
            footer.Controls.Add(btnSave);

            QFG5Button btnLoad = new QFG5Button { Text = "Load Icons", Width = 100, Height = 30 };
            btnLoad.Click += (s, e) => LoadIcons();
            footer.Controls.Add(btnLoad);

            main.Controls.Add(footer, 0, 1);
            main.SetColumnSpan(footer, 2);

            this.Controls.Add(main);
        }

        private void LoadItems()
        {
            lstItems.Items.Clear();
            var items = ItemDatabase.GetItems();
            foreach (var it in items)
            {
                lstItems.Items.Add(it);
            }
        }

        private void LoadIcons()
        {
            if (string.IsNullOrEmpty(inventoryPath))
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog { Description = "Select 'Inventory Graphics' folder" })
                {
                    if (fbd.ShowDialog() == DialogResult.OK) inventoryPath = fbd.SelectedPath;
                    else return;
                }
            }

            pnlIcons.Controls.Clear();
            string[] subdirs = { "Items", "Spells", "Equipment" };
            foreach (var sub in subdirs)
            {
                string dir = Path.Combine(inventoryPath, sub);
                if (!Directory.Exists(dir)) continue;

                foreach (string file in Directory.GetFiles(dir, "*.*"))
                {
                    if (!file.EndsWith(".png") && !file.EndsWith(".bmp")) continue;

                    PictureBox pb = new PictureBox
                    {
                        Width = 64, Height = 64,
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Image = Image.FromFile(file),
                        Tag = file,
                        Margin = new Padding(3),
                        BorderStyle = BorderStyle.FixedSingle
                    };
                    pb.MouseDown += (s, e) => { ((PictureBox)s).DoDragDrop(file, DragDropEffects.Link); };
                    
                    ToolTip tt = new ToolTip();
                    tt.SetToolTip(pb, Path.GetFileName(file));

                    pnlIcons.Controls.Add(pb);
                }
            }
        }

        private void LstItems_DragDrop(object sender, DragEventArgs e)
        {
            string filePath = (string)e.Data.GetData(DataFormats.StringFormat);
            Point pt = lstItems.PointToClient(new Point(e.X, e.Y));
            int index = lstItems.IndexFromPoint(pt);
            if (index == ListBox.NoMatches) return;

            var item = (ItemDatabase.ItemEntry)lstItems.Items[index];
            mappings[item.ID] = filePath;
            lstItems.Invalidate();
        }

        private void LstItems_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var item = (ItemDatabase.ItemEntry)lstItems.Items[e.Index];
            bool hasMapping = mappings.ContainsKey(item.ID);

            e.DrawBackground();
            Brush textBrush = hasMapping ? Brushes.Gold : Brushes.White;
            string text = $"[{item.ID}] {item.Name}";
            if (hasMapping) text += " -> " + Path.GetFileName(mappings[item.ID]);

            e.Graphics.DrawString(text, e.Font, textBrush, e.Bounds);
            e.DrawFocusRectangle();
        }

        private void SaveMappings()
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(mappingPath))
                {
                    foreach (var kvp in mappings)
                    {
                        sw.WriteLine($"{kvp.Key}|{kvp.Value}");
                    }
                }
                MessageBox.Show("Mappings saved successfully!", "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving mappings: " + ex.Message, "Error");
            }
        }

        private void LoadMappings()
        {
            if (!File.Exists(mappingPath)) return;
            try
            {
                string[] lines = File.ReadAllLines(mappingPath);
                foreach (string line in lines)
                {
                    string[] parts = line.Split('|');
                    if (parts.Length == 2) mappings[parts[PartIndex.Zero]] = parts[PartIndex.One];
                }
            }
            catch { }
        }
        
        private static class PartIndex { public const int Zero = 0; public const int One = 1; }
    }
}
