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
        private readonly SaveSession session;
        private QFG5Button btnSaveFlags;
        private Label lblLoadedSave;
        private Label statusLabel;
        private bool refreshingGrid;
        private string flagNamesLoadMessage;

        public event Action<string> SaveLoadRequested;
        public event Action SaveRequested;

        public FlagsTab(SaveSession session)
        {
            if (session == null) throw new ArgumentNullException("session");
            this.session = session;
            flagNames = new List<string>();
            InitializeComponent();
            LoadFlagNames();
        }

        private bool GetFlag(int index)
        {
            if (!session.IsLoaded) return false;
            // The actual offset for flags in QFG5 save games is typically further in.
            // Based on analyzing .QG5 structure:
            int startOffset = 0x125B; // Common flag start offset
            int byteOffset = startOffset + (index / 8);
            int bitOffset = index % 8;
            if (byteOffset >= session.Length) return false;
            return (session.GetByte(byteOffset) & (1 << bitOffset)) != 0;
        }

        private void SetFlag(int index, bool value)
        {
            if (!session.IsLoaded) return;
            int startOffset = 0x125B;
            int byteOffset = startOffset + (index / 8);
            int bitOffset = index % 8;
            if (byteOffset >= session.Length) return;
            if (value)
                session.SetByte(byteOffset, (byte)(session.GetByte(byteOffset) | (1 << bitOffset)));
            else
                session.SetByte(byteOffset, (byte)(session.GetByte(byteOffset) & ~(1 << bitOffset)));
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
            btnSaveFlags = new QFG5Button { Text = "Save Flags", Width = 130, Height = 30, Enabled = false };
            btnSaveFlags.Click += (s, e) => {
                if (SaveRequested != null) SaveRequested();
            };

            QFG5TableLayoutPanel topPanel = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            QFG5Theme.ApplyHeaderStroke(topPanel);
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            topPanel.Controls.Add(btnLoad, 0, 0);
            topPanel.Controls.Add(btnSaveFlags, 1, 0);
            lblLoadedSave = new Label {
                Text = "No save loaded",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.BodyFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                AutoEllipsis = true
            };
            topPanel.Controls.Add(lblLoadedSave, 2, 0);

            gridFlags = new DataGridView
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
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = QFG5Theme.BodyFont,
                RowTemplate = { Height = 26 },
                ColumnHeadersHeight = 30
            };

            gridFlags.DefaultCellStyle.BackColor = QFG5Theme.ColorSurfaceRecessed;
            gridFlags.DefaultCellStyle.ForeColor = QFG5Theme.ColorTextPrimary;
            gridFlags.DefaultCellStyle.SelectionBackColor = QFG5Theme.ColorBorderHighlight;
            gridFlags.DefaultCellStyle.SelectionForeColor = QFG5Theme.ColorTextPrimary;
            gridFlags.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
            gridFlags.AlternatingRowsDefaultCellStyle.BackColor = QFG5Theme.ColorBgDark;
            gridFlags.AlternatingRowsDefaultCellStyle.ForeColor = QFG5Theme.ColorTextPrimary;
            gridFlags.ColumnHeadersDefaultCellStyle.BackColor = QFG5Theme.ColorSurface;
            gridFlags.ColumnHeadersDefaultCellStyle.ForeColor = QFG5Theme.ColorTextGold;
            gridFlags.ColumnHeadersDefaultCellStyle.Font = QFG5Theme.BodyBoldFont;
            gridFlags.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            gridFlags.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 2, 4, 2);
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

            statusLabel = new Label {
                Text = "Load a QG5 save to view and edit flags.",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.BodyFont,
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
            var swFlags = System.Diagnostics.Stopwatch.StartNew();
            string flagPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flags.dat");
            // Also check parent directories
            string[] searchPaths = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../Resources/QG5Edit/flags.dat"),
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

            int probed = 0;
            string foundPath = null;
            flagNames.Clear();
            flagNamesLoadMessage = null;
            foreach (string p in searchPaths)
            {
                probed++;
                if (File.Exists(p))
                {
                    try
                    {
                        var lines = File.ReadAllLines(p);
                        List<string> loadedNames = new List<string>();
                        foreach (var line in lines)
                        {
                            if (!string.IsNullOrEmpty(line.Trim()))
                                loadedNames.Add(line.Trim());
                        }

                        if (loadedNames.Count > 0)
                        {
                            foundPath = p;
                            flagNames.AddRange(loadedNames);
                            break;
                        }

                        flagNamesLoadMessage = "The flag-name file contains no usable names: " + p;
                        Logger.Log("FlagsTab: " + flagNamesLoadMessage);
                    }
                    catch (Exception ex)
                    {
                        flagNamesLoadMessage = "Could not read flag-name file " + p + ": " + ex.Message;
                        Logger.Log("FlagsTab: " + flagNamesLoadMessage);
                    }
                }
            }

            if (flagNames.Count == 0 && string.IsNullOrEmpty(flagNamesLoadMessage))
            {
                flagNamesLoadMessage = "No flags.dat file was found in the configured locations.";
                Logger.Log("FlagsTab: " + flagNamesLoadMessage);
            }

            RefreshGrid();
            UpdateLoadFeedback(null);
            swFlags.Stop();
            Logger.LogPerf($"FlagsTab.LoadFlagNames: {swFlags.ElapsedMilliseconds} ms (probed {probed}/{searchPaths.Length} paths, found: {foundPath ?? "none"}, names: {flagNames.Count})");
        }

        public void LoadSave(string path)
        {
            if (SaveLoadRequested != null) SaveLoadRequested(path);
        }

        public void RefreshFromSession()
        {
            RefreshGrid();
            UpdateLoadFeedback(null);
        }

        private void RefreshGrid()
        {
            refreshingGrid = true;
            try
            {
                gridFlags.Rows.Clear();
                for (int i = 0; i < flagNames.Count; i++)
                {
                    bool val = GetFlag(i);
                    gridFlags.Rows.Add(i, flagNames[i], val);
                }

                gridFlags.Columns["Value"].ReadOnly = !session.IsLoaded;
                btnSaveFlags.Enabled = session.IsLoaded && !string.IsNullOrEmpty(session.FilePath);
            }
            finally
            {
                refreshingGrid = false;
            }
        }

        private void GridFlags_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (refreshingGrid || !session.IsLoaded || e.RowIndex < 0 || e.ColumnIndex < 0 ||
                gridFlags.Columns[e.ColumnIndex].Name != "Value") return;
            int flagIndex = (int)gridFlags.Rows[e.RowIndex].Cells["Index"].Value;
            bool newVal = (bool)gridFlags.Rows[e.RowIndex].Cells["Value"].Value;
            SetFlag(flagIndex, newVal);
        }

        public void SaveActiveSession()
        {
            if (!session.IsLoaded || string.IsNullOrEmpty(session.FilePath)) return;
            try
            {
                session.Save();
                MessageBox.Show("Flags saved successfully.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateLoadFeedback("Flags saved successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving flags: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateLoadFeedback("Error saving flags: " + ex.Message);
            }
        }

        private void UpdateLoadFeedback(string operationMessage)
        {
            if (!session.IsLoaded)
            {
                lblLoadedSave.Text = "No save loaded";
                lblLoadedSave.ForeColor = QFG5Theme.ColorTextMuted;

                if (!string.IsNullOrEmpty(operationMessage))
                    SetStatus(operationMessage, QFG5Theme.ColorGold);
                else if (flagNames.Count == 0)
                    SetStatus("Flag names unavailable. " + flagNamesLoadMessage, QFG5Theme.ColorGold);
                else
                    SetStatus(flagNames.Count + " flag names loaded. Load a save to view and edit flags.", QFG5Theme.ColorTextMuted);
                return;
            }

            lblLoadedSave.Text = "Loaded: " + session.FilePath;
            lblLoadedSave.ForeColor = QFG5Theme.ColorTextGold;
            if (!string.IsNullOrEmpty(operationMessage))
                SetStatus(operationMessage, QFG5Theme.ColorGoldBright);
            else if (flagNames.Count == 0)
                SetStatus("Save loaded, but flag names are unavailable. " + flagNamesLoadMessage, QFG5Theme.ColorGold);
            else
                SetStatus("Save loaded. " + flagNames.Count + " flags available.", QFG5Theme.ColorTextGold);
        }

        private void SetStatus(string message, Color color)
        {
            statusLabel.Text = message;
            statusLabel.ForeColor = color;
        }
    }
}
