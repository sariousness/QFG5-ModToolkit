using System;
using System.Drawing;
using System.Windows.Forms;

namespace QFG5Extractor.Core.UI
{
    /// <summary>
    /// Shared layout constants and factory helpers for release-consistent tab chrome.
    /// </summary>
    public static class QFG5Layout
    {
        public const int LabelWidth = 100;
        public const int ButtonWidth = 90;
        public const int WideButtonWidth = 110;
        public const int RowHeight = 45;
        public const int ContentPadding = 15;
        public const int StatusRowHeight = 40;
        public const int ProgressRowHeight = 35;

        /// <summary>
        /// Creates a stretch path row: Label | PathField (fill) | Browse | optional trailing actions.
        /// </summary>
        public static QFG5TableLayoutPanel CreatePathRow(
            string labelText,
            out QFG5PathField pathField,
            out QFG5Button browseButton,
            params Control[] trailingActions)
        {
            int actionCount = trailingActions != null ? trailingActions.Length : 0;
            QFG5TableLayoutPanel row = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3 + actionCount,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LabelWidth));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ButtonWidth));
            for (int i = 0; i < actionCount; i++)
            {
                int w = trailingActions[i].Width > 0 ? trailingActions[i].Width : WideButtonWidth;
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w));
            }

            Label lbl = new Label
            {
                Text = labelText,
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pathField = new QFG5PathField { Dock = DockStyle.Fill, Height = 30 };
            browseButton = new QFG5Button { Text = "Browse", Dock = DockStyle.Fill, Height = 30 };

            row.Controls.Add(lbl, 0, 0);
            row.Controls.Add(pathField, 1, 0);
            row.Controls.Add(browseButton, 2, 0);

            if (trailingActions != null)
            {
                for (int i = 0; i < trailingActions.Length; i++)
                {
                    Control c = trailingActions[i];
                    c.Dock = DockStyle.Fill;
                    if (c.Height <= 0) c.Height = 30;
                    row.Controls.Add(c, 3 + i, 0);
                }
            }

            return row;
        }

        /// <summary>
        /// Status footer row: left status label fills, optional trailing action buttons on the right.
        /// </summary>
        public static QFG5TableLayoutPanel CreateStatusFooter(Label statusLabel, params Control[] actions)
        {
            int actionCount = actions != null ? actions.Length : 0;
            QFG5TableLayoutPanel row = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1 + actionCount,
                RowCount = 1,
                Margin = new Padding(0)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < actionCount; i++)
            {
                int w = actions[i].Width > 0 ? actions[i].Width : WideButtonWidth;
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w + 8));
            }

            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            if (statusLabel.ForeColor.IsEmpty || statusLabel.ForeColor == Color.Black)
                statusLabel.ForeColor = Color.Gold;
            row.Controls.Add(statusLabel, 0, 0);

            if (actions != null)
            {
                for (int i = 0; i < actionCount; i++)
                {
                    Control c = actions[i];
                    c.Dock = DockStyle.Fill;
                    if (c.Height <= 0) c.Height = 30;
                    c.Margin = new Padding(4, 2, 0, 2);
                    row.Controls.Add(c, 1 + i, 0);
                }
            }

            return row;
        }

        /// <summary>
        /// Sets a SplitContainer distance as a fraction of width once, after handle creation.
        /// </summary>
        public static void SetSplitterRatioOnce(SplitContainer split, double ratio, bool vertical = true)
        {
            if (split == null) return;
            bool applied = false;
            EventHandler apply = null;
            apply = (s, e) =>
            {
                if (applied) return;
                int size = vertical ? split.Width : split.Height;
                if (size <= split.Panel1MinSize + split.Panel2MinSize + 10) return;
                int dist = (int)(size * ratio);
                dist = Math.Max(split.Panel1MinSize, Math.Min(dist, size - split.Panel2MinSize - split.SplitterWidth));
                try
                {
                    split.SplitterDistance = dist;
                    applied = true;
                    split.Resize -= apply;
                }
                catch { }
            };
            split.Resize += apply;
            if (split.IsHandleCreated) apply(split, EventArgs.Empty);
            else split.HandleCreated += (s, e) => apply(split, EventArgs.Empty);
        }
    }

    // Custom TableLayoutPanel that explicitly supports transparency
    public class QFG5TableLayoutPanel : TableLayoutPanel
    {
        public QFG5TableLayoutPanel()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                return cp;
            }
        }
    }

    // Custom FlowLayoutPanel that explicitly supports transparency
    public class QFG5FlowLayoutPanel : FlowLayoutPanel
    {
        public QFG5FlowLayoutPanel()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                return cp;
            }
        }
    }

    // Custom ComboBox wrapper with stroke frame
    public class QFG5ComboBox : UserControl
    {
        private ComboBox comboBox;
        public ComboBox ComboBox => comboBox;

        public QFG5ComboBox()
        {
            this.Height = 32;
            this.Padding = new Padding(3);
            this.BackColor = Color.Transparent;

            comboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 30, 25),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            this.Controls.Add(comboBox);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Strokes removed for Phase 15 polish
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.Invalidate();
        }
    }
}
