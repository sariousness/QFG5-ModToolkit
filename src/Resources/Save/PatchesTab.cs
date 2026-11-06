using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace QFG5Extractor.Resources.Save
{
    public sealed class PatchesTab : UserControl
    {
        private readonly FlagsTab flagsTab;
        private Button btnGetBoat;
        private Label statusLabel;

        public PatchesTab(FlagsTab flagsTab)
        {
            if (flagsTab == null) throw new ArgumentNullException("flagsTab");
            this.flagsTab = flagsTab;
            InitializeComponent();
            RefreshFromSession();
        }

        private void InitializeComponent()
        {
            Dock = DockStyle.Fill;
            BackColor = QFG5Theme.ColorBgDark;

            QFG5TableLayoutPanel layout = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Label title = new Label {
                Text = "SAVE PATCHES",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.SectionHeaderFont,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Label description = new Label {
                Text = "Only save changes with a known flag mapping are available here. Other screenshot options change game rules or have no confirmed save-field mapping.",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.BodyFont,
                TextAlign = ContentAlignment.MiddleLeft
            };

            GroupBox boatGroup = new GroupBox {
                Text = "Boat",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.BodyBoldFont,
                BackColor = QFG5Theme.ColorSurface
            };
            btnGetBoat = new QFG5Button {
                Name = "GetBoatButton",
                Text = "Get Boat",
                Width = 120,
                Height = 32,
                Enabled = false,
                Location = new Point(12, 34)
            };
            btnGetBoat.Click += BtnGetBoat_Click;
            boatGroup.Controls.Add(btnGetBoat);

            statusLabel = new Label {
                Text = "Load a QG5 save to check whether Get Boat is available.",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.BodyFont,
                TextAlign = ContentAlignment.TopLeft,
                AutoEllipsis = true
            };

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(description, 0, 1);
            layout.Controls.Add(boatGroup, 0, 2);
            layout.Controls.Add(statusLabel, 0, 3);
            Controls.Add(layout);
        }

        public void RefreshFromSession()
        {
            if (!flagsTab.TryGetFlagByName("fRowBoat", out bool hasRowboat, out string error))
            {
                btnGetBoat.Enabled = false;
                SetStatus(error, QFG5Theme.ColorGold);
                return;
            }

            btnGetBoat.Enabled = !hasRowboat;
            if (hasRowboat)
                SetStatus("The fRowBoat flag is already set. Save if this is your intended change, then leave the scene in-game.",
                    QFG5Theme.ColorTextGold);
            else
                SetStatus("Get Boat stages fRowBoat. Save the file, then leave the current scene in-game to trigger the rowboat.",
                    QFG5Theme.ColorTextMuted);
        }

        private void BtnGetBoat_Click(object sender, EventArgs e)
        {
            if (!flagsTab.TrySetFlagByName("fRowBoat", true, out string error))
            {
                btnGetBoat.Enabled = false;
                SetStatus(error, QFG5Theme.ColorGold);
                return;
            }

            btnGetBoat.Enabled = false;
            SetStatus("Get Boat staged: fRowBoat is True. Click Save, then leave the current scene in-game to trigger the rowboat.",
                QFG5Theme.ColorGoldBright);
        }

        private void SetStatus(string message, Color color)
        {
            statusLabel.Text = message;
            statusLabel.ForeColor = color;
        }
    }
}
