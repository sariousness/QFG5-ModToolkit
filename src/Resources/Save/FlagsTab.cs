using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;

namespace QFG5Extractor.Resources.Save
{
    public class FlagsTab : UserControl
    {
        private DataGridView gridFlags;
        private List<string> flagNames;
        private readonly SaveSession session;
        private Label statusLabel;
        private bool refreshingGrid;
        private string flagNamesLoadMessage;

        public FlagsTab(SaveSession session)
        {
            if (session == null) throw new ArgumentNullException("session");
            this.session = session;
            flagNames = new List<string>();
            InitializeComponent();
            LoadFlagNames();
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.StatusRowHeight));

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
            gridFlags.Columns.Add(new DataGridViewButtonColumn {
                Name = "Value",
                HeaderText = "Value",
                Width = 90,
                FlatStyle = FlatStyle.Flat,
                UseColumnTextForButtonValue = false
            });

            gridFlags.CellContentClick += GridFlags_CellContentClick;
            gridFlags.CellPainting += GridFlags_CellPainting;

            QFG5PreviewFrame gridFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            gridFrame.Controls.Add(gridFlags);

            statusLabel = new Label {
                Text = "Load a QG5 save to view and edit flags.",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.BodyFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            mainLayout.Controls.Add(gridFrame, 0, 0);
            mainLayout.Controls.Add(statusLabel, 0, 1);

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
                    bool hasValue = session.TryGetFlag(i, flagNames.Count, out bool value);
                    gridFlags.Rows.Add(i, flagNames[i],
                        hasValue ? (value ? "True" : "False") : "Unknown");
                }

                gridFlags.Columns["Value"].ReadOnly =
                    !session.CanEditFlags;
            }
            finally
            {
                refreshingGrid = false;
            }
        }

        private void GridFlags_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (refreshingGrid || !session.CanEditFlags ||
                e.RowIndex < 0 || e.ColumnIndex < 0 ||
                gridFlags.Columns[e.ColumnIndex].Name != "Value") return;
            int flagIndex = (int)gridFlags.Rows[e.RowIndex].Cells["Index"].Value;
            if (!session.TryGetFlag(flagIndex, flagNames.Count, out bool currentValue)) return;
            bool newValue = !currentValue;
            session.SetFlag(flagIndex, flagNames.Count, newValue);
            gridFlags.Rows[e.RowIndex].Cells["Value"].Value = newValue ? "True" : "False";
            UpdateLoadFeedback("Flag " + flagIndex + " set to " + (newValue ? "True." : "False."));
        }

        private void GridFlags_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 ||
                gridFlags.Columns[e.ColumnIndex].Name != "Value")
                return;

            e.Handled = true;
            Color cellColor = (e.State & DataGridViewElementStates.Selected) != 0
                ? QFG5Theme.ColorBorderHighlight
                : e.RowIndex % 2 == 0
                    ? QFG5Theme.ColorSurfaceRecessed
                    : QFG5Theme.ColorBgDark;
            using (SolidBrush cellBackground = new SolidBrush(cellColor))
                e.Graphics.FillRectangle(cellBackground, e.CellBounds);

            int width = Math.Min(76, e.CellBounds.Width - 12);
            int height = Math.Min(20, e.CellBounds.Height - 6);
            if (width <= 0 || height <= 0)
            {
                e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);
                return;
            }

            string value = Convert.ToString(e.FormattedValue);
            bool isTrue = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
            bool isKnown = isTrue || string.Equals(value, "False", StringComparison.OrdinalIgnoreCase);
            Rectangle toggleBounds = new Rectangle(
                e.CellBounds.Left + (e.CellBounds.Width - width) / 2,
                e.CellBounds.Top + (e.CellBounds.Height - height) / 2,
                width,
                height);
            Color fillColor = isTrue
                ? Color.FromArgb(44, 112, 76)
                : isKnown
                    ? Color.FromArgb(132, 70, 54)
                    : Color.FromArgb(83, 88, 96);
            Color borderColor = isTrue
                ? Color.FromArgb(92, 174, 124)
                : isKnown
                    ? Color.FromArgb(194, 116, 91)
                    : Color.FromArgb(128, 134, 144);

            using (SolidBrush fill = new SolidBrush(fillColor))
            using (Pen border = new Pen(borderColor))
            {
                e.Graphics.FillRectangle(fill, toggleBounds);
                e.Graphics.DrawRectangle(border, toggleBounds);
            }
            TextRenderer.DrawText(e.Graphics, isKnown ? (isTrue ? "True" : "False") : "Unknown",
                e.CellStyle.Font ?? Font, toggleBounds, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);
        }

        private void UpdateLoadFeedback(string operationMessage)
        {
            if (!session.IsLoaded)
            {
                if (!string.IsNullOrEmpty(operationMessage))
                    SetStatus(operationMessage, QFG5Theme.ColorGold);
                else if (flagNames.Count == 0)
                    SetStatus("Flag names unavailable. " + flagNamesLoadMessage, QFG5Theme.ColorGold);
                else
                    SetStatus(flagNames.Count + " flag names loaded. Load a save to view and edit flags.", QFG5Theme.ColorTextMuted);
                return;
            }

            if (session.HasIncompleteCharacterRecord)
            {
                SetStatus("This save is incomplete; flags are read-only and saving is disabled.",
                    QFG5Theme.ColorGold);
                return;
            }
            if (session.IsLoaded && !session.CanEditFlags)
            {
                SetStatus("Flag data could not be located safely; flags are read-only. " +
                    session.FlagsUnavailableReason, QFG5Theme.ColorGold);
                return;
            }
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
