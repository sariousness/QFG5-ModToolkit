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
        private TextBox searchBox;
        private Button toggleSelectedButton;
        private List<string> flagNames;
        private readonly HashSet<int> checkedFlagIndices = new HashSet<int>();
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
                RowCount = 3,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.StatusRowHeight));

            QFG5TableLayoutPanel toolbar = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0)
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = QFG5Theme.BodyFont,
                Margin = new Padding(0, 4, 8, 4),
                AccessibleName = "Search flags by name or index"
            };
            searchBox.TextChanged += (s, e) => ApplySearchFilter();

            toggleSelectedButton = new QFG5Button
            {
                Text = "Toggle Selected",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 8, 3),
                Enabled = false
            };
            toggleSelectedButton.Click += (s, e) => ToggleSelectedFlags();

            Label selectionHelp = new Label
            {
                Text = "Check flags or Ctrl+click rows. Shift+click a value to set checked flags to that value.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                AutoEllipsis = true
            };

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
                MultiSelect = true,
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

            gridFlags.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "Select",
                HeaderText = "Select",
                Width = 62,
                FillWeight = 18,
                ThreeState = false,
                TrueValue = true,
                FalseValue = false
            });
            gridFlags.Columns.Add(new DataGridViewTextBoxColumn { Name = "Index", HeaderText = "Index", ReadOnly = true, Width = 60, FillWeight = 18 });
            gridFlags.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Flag Name", ReadOnly = true, Width = 300, FillWeight = 100 });
            gridFlags.Columns.Add(new DataGridViewButtonColumn {
                Name = "Value",
                HeaderText = "Value",
                Width = 90,
                FillWeight = 30,
                FlatStyle = FlatStyle.Flat,
                UseColumnTextForButtonValue = false
            });

            gridFlags.CellContentClick += GridFlags_CellContentClick;
            gridFlags.CellPainting += GridFlags_CellPainting;
            gridFlags.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (gridFlags.IsCurrentCellDirty &&
                    gridFlags.CurrentCell is DataGridViewCheckBoxCell)
                    gridFlags.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            gridFlags.CellValueChanged += GridFlags_CellValueChanged;

            QFG5PreviewFrame gridFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            gridFrame.Controls.Add(gridFlags);

            statusLabel = new Label {
                Text = "Load a QG5 save to view and edit flags.",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.BodyFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            toolbar.Controls.Add(searchBox, 0, 0);
            toolbar.Controls.Add(toggleSelectedButton, 1, 0);
            toolbar.Controls.Add(selectionHelp, 2, 0);
            mainLayout.Controls.Add(toolbar, 0, 0);
            mainLayout.Controls.Add(gridFrame, 0, 1);
            mainLayout.Controls.Add(statusLabel, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private void LoadFlagNames()
        {
            var swFlags = System.Diagnostics.Stopwatch.StartNew();
            string flagPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flags.dat");
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

        public bool TryGetFlagByName(string name, out bool value, out string error)
        {
            value = false;
            error = null;
            if (!session.IsLoaded)
            {
                error = "Load a QG5 save before applying a patch.";
                return false;
            }

            int flagIndex = FindFlagIndex(name);
            if (flagIndex < 0)
            {
                error = "The flag name '" + name + "' is not present in the loaded flag-name list.";
                return false;
            }
            if (!session.CanEditFlags)
            {
                error = session.FlagsUnavailableReason ??
                    "The save's flag data could not be located safely.";
                return false;
            }
            if (!session.TryGetFlag(flagIndex, flagNames.Count, out value))
            {
                error = "The named flag could not be read safely from this save.";
                return false;
            }

            return true;
        }

        public bool TrySetFlagByName(string name, bool value, out string error)
        {
            error = null;
            if (!TryGetFlagByName(name, out bool currentValue, out error))
                return false;

            int flagIndex = FindFlagIndex(name);
            if (currentValue != value)
                session.SetFlag(flagIndex, flagNames.Count, value);
            RefreshGrid();
            return true;
        }

        private int FindFlagIndex(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return -1;
            return flagNames.FindIndex(flagName =>
                string.Equals(flagName, name, StringComparison.OrdinalIgnoreCase));
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
                    gridFlags.Rows.Add(checkedFlagIndices.Contains(i), i, flagNames[i],
                        hasValue ? (value ? "True" : "False") : "Unknown");
                }

                gridFlags.Columns["Value"].ReadOnly = !session.CanEditFlags;
                ApplySearchFilter();
                toggleSelectedButton.Enabled = session.CanEditFlags;
            }
            finally
            {
                refreshingGrid = false;
            }
        }

        private void ApplySearchFilter()
        {
            if (gridFlags == null) return;
            string query = searchBox.Text.Trim();
            for (int i = 0; i < gridFlags.Rows.Count; i++)
            {
                DataGridViewRow row = gridFlags.Rows[i];
                string index = Convert.ToString(row.Cells["Index"].Value);
                string name = Convert.ToString(row.Cells["Name"].Value);
                row.Visible = query.Length == 0 ||
                    index.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private void GridFlags_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (refreshingGrid || e.RowIndex < 0 || e.ColumnIndex < 0 ||
                gridFlags.Columns[e.ColumnIndex].Name != "Select")
                return;

            int flagIndex = Convert.ToInt32(gridFlags.Rows[e.RowIndex].Cells["Index"].Value);
            if (Convert.ToBoolean(gridFlags.Rows[e.RowIndex].Cells["Select"].Value))
                checkedFlagIndices.Add(flagIndex);
            else
                checkedFlagIndices.Remove(flagIndex);
        }

        private void GridFlags_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (refreshingGrid || !session.CanEditFlags ||
                e.RowIndex < 0 || e.ColumnIndex < 0 ||
                gridFlags.Columns[e.ColumnIndex].Name != "Value") return;

            int flagIndex = Convert.ToInt32(gridFlags.Rows[e.RowIndex].Cells["Index"].Value);
            if (!session.TryGetFlag(flagIndex, flagNames.Count, out bool currentValue)) return;
            bool newValue = !currentValue;
            if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                HashSet<int> targets = new HashSet<int>(checkedFlagIndices);
                if (targets.Count == 0)
                    targets.Add(flagIndex);
                SetFlags(targets, newValue);
                UpdateLoadFeedback("Set " + targets.Count + " selected flag(s) to " +
                    (newValue ? "True." : "False."));
                return;
            }

            session.SetFlag(flagIndex, flagNames.Count, newValue);
            gridFlags.Rows[e.RowIndex].Cells["Value"].Value = newValue ? "True" : "False";
            UpdateLoadFeedback("Flag " + flagIndex + " set to " + (newValue ? "True." : "False."));
        }

        private void ToggleSelectedFlags()
        {
            if (!session.CanEditFlags) return;
            HashSet<int> targets = GetSelectedFlagIndices();
            if (targets.Count == 0)
            {
                UpdateLoadFeedback("Check flags or select rows before using Toggle Selected.");
                return;
            }

            foreach (int flagIndex in targets)
            {
                if (!session.TryGetFlag(flagIndex, flagNames.Count, out bool currentValue))
                    continue;
                session.SetFlag(flagIndex, flagNames.Count, !currentValue);
                UpdateFlagValueCell(flagIndex, !currentValue);
            }
            UpdateLoadFeedback("Toggled " + targets.Count + " selected flag(s).");
        }

        private HashSet<int> GetSelectedFlagIndices()
        {
            HashSet<int> targets = new HashSet<int>(checkedFlagIndices);
            foreach (DataGridViewRow row in gridFlags.SelectedRows)
            {
                if (row.IsNewRow) continue;
                targets.Add(Convert.ToInt32(row.Cells["Index"].Value));
            }
            return targets;
        }

        private void SetFlags(IEnumerable<int> flagIndices, bool value)
        {
            foreach (int flagIndex in flagIndices)
            {
                if (!session.TryGetFlag(flagIndex, flagNames.Count, out bool currentValue))
                    continue;
                if (currentValue != value)
                    session.SetFlag(flagIndex, flagNames.Count, value);
                UpdateFlagValueCell(flagIndex, value);
            }
        }

        private void UpdateFlagValueCell(int flagIndex, bool value)
        {
            foreach (DataGridViewRow row in gridFlags.Rows)
            {
                if (Convert.ToInt32(row.Cells["Index"].Value) == flagIndex)
                {
                    row.Cells["Value"].Value = value ? "True" : "False";
                    return;
                }
            }
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
