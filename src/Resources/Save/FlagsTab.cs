using QFG5Extractor.Core;
using System;
using System.Drawing;
using System.Windows.Forms;
using QFG5Extractor.Core.UI;
using System.IO;
using System.Collections.Generic;

namespace QFG5Extractor.Resources.Save
{
    public class FlagsTab : UserControl
    {
        private DataGridView gridFlags;
        private List<string> flagNames;
        private byte[] saveData;
        private string currentPath;

        public FlagsTab()
        {
            flagNames = new List<string>();
            InitializeComponent();
            LoadFlagNames();
        }

        private bool GetFlag(int index)
        {
            if (saveData == null) return false;
            // The actual offset for flags in QFG5 save games is typically further in.
            // Based on analyzing .QG5 structure:
            int startOffset = 0x125B; // Common flag start offset
            int byteOffset = startOffset + (index / 8);
            int bitOffset = index % 8;
            if (byteOffset >= saveData.Length) return false;
            return (saveData[byteOffset] & (1 << bitOffset)) != 0;
        }

        private void SetFlag(int index, bool value)
        {
            if (saveData == null) return;
            int startOffset = 0x125B;
            int byteOffset = startOffset + (index / 8);
            int bitOffset = index % 8;
            if (byteOffset >= saveData.Length) return;
            if (value)
                saveData[byteOffset] |= (byte)(1 << bitOffset);
            else
                saveData[byteOffset] &= (byte)~(1 << bitOffset);
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.RowHeight));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.StatusRowHeight));

            QFG5Button btnLoad = new QFG5Button { Text = "Load Flags", Width = 130, Height = 30 };
            btnLoad.Click += (s, e) => {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "QFG5 Save Games (*.QG5)|*.QG5|All Files (*.*)|*.*" }) {
                    if (ofd.ShowDialog() == DialogResult.OK) LoadSave(ofd.FileName);
                }
            };
            QFG5Button btnSave = new QFG5Button { Text = "Save Flags", Width = 130, Height = 30 };
            btnSave.Click += (s, e) => SaveFlags();

            QFG5TableLayoutPanel topPanel = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            topPanel.Controls.Add(btnLoad, 0, 0);
            topPanel.Controls.Add(btnSave, 1, 0);
            Label lblLoaded = new Label {
                Text = "No save loaded",
                ForeColor = Color.Gray,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            topPanel.Controls.Add(lblLoaded, 2, 0);
            this.Tag = lblLoaded;

            gridFlags = new DataGridView
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
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            gridFlags.DefaultCellStyle.BackColor = Color.Black;
            gridFlags.DefaultCellStyle.ForeColor = Color.White;
            gridFlags.DefaultCellStyle.SelectionBackColor = Color.FromArgb(100, 100, 110);
            gridFlags.DefaultCellStyle.SelectionForeColor = Color.White;
            gridFlags.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(100, 100, 110);
            gridFlags.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gridFlags.EnableHeadersVisualStyles = false;

            gridFlags.Columns.Add(new DataGridViewTextBoxColumn { Name = "Index", HeaderText = "Index", ReadOnly = true, Width = 60 });
            gridFlags.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Flag Name", ReadOnly = true, Width = 300 });
            gridFlags.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Value", HeaderText = "Set", Width = 60 });

            gridFlags.CellValueChanged += GridFlags_CellValueChanged;
            gridFlags.CurrentCellDirtyStateChanged += (s, e) => {
                if (gridFlags.IsCurrentCellDirty) gridFlags.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            QFG5PreviewFrame gridFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            gridFrame.Controls.Add(gridFlags);

            Label statusLabel = new Label {
                Text = "Load a QG5 save to edit flags.",
                ForeColor = Color.Gold,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            mainLayout.Controls.Add(topPanel, 0, 0);
            mainLayout.Controls.Add(gridFrame, 0, 1);
            mainLayout.Controls.Add(statusLabel, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private void LoadFlagNames()
        {
            string flagPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flags.dat");
            // Also check parent directories
            string[] searchPaths = { 
                flagPath, 
                "flags.dat", 
                "../flags.dat",
                "../../flags.dat",
                "../../../flags.dat",
                "../../../Resources/QG5Edit/flags.dat",
                "../../Resources/QG5Edit/flags.dat",
                "../QG5Edit/flags.dat",
                "/home/vandali/Desktop/QFG5-Tool/Resources/QG5Edit/flags.dat",
                "/home/vandali/.wine32/drive_c/Program Files/QFG5-RE/QG5Edit/flags.dat"
            };

            foreach (string p in searchPaths)
            {
                if (File.Exists(p))
                {
                    flagNames.Clear();
                    var lines = File.ReadAllLines(p);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrEmpty(line.Trim()))
                            flagNames.Add(line.Trim());
                    }
                    break;
                }
            }

            RefreshGrid();
        }

        public void LoadSave(string path)
        {
            currentPath = path;
            if (File.Exists(path))
            {
                saveData = File.ReadAllBytes(path);
                // For now, flags are just displayed. Editing is stubbed.
                RefreshGrid();
            }
        }

        private void RefreshGrid()
        {
            gridFlags.Rows.Clear();
            for (int i = 0; i < flagNames.Count; i++)
            {
                bool val = GetFlag(i);
                gridFlags.Rows.Add(i, flagNames[i], val);
            }
        }

        private void GridFlags_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || gridFlags.Columns[e.ColumnIndex].Name != "Value") return;
            int flagIndex = (int)gridFlags.Rows[e.RowIndex].Cells["Index"].Value;
            bool newVal = (bool)gridFlags.Rows[e.RowIndex].Cells["Value"].Value;
            SetFlag(flagIndex, newVal);
        }

        private void SaveFlags()
        {
            if (saveData == null || string.IsNullOrEmpty(currentPath)) return;
            try
            {
                File.WriteAllBytes(currentPath, saveData);
                MessageBox.Show("Flags saved successfully.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving flags: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
