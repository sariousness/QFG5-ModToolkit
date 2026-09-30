using QFG5Extractor.Viewers.Model;
using QFG5Extractor.Resources.Save;
using QFG5Extractor.Storage.SPK;
using QFG5Extractor.Resources.Model;
using QFG5Extractor.Resources.Message;
using QFG5Extractor.Resources.Panorama;
using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using QFG5Extractor.Resources.Gra;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;

namespace QFG5Extractor.Core
{
    public class QFG5TabPanel : Panel
    {
        private bool isHovered = false;
        private bool isSelected = false;
        private QFG5Button linkedButton;

        public QFG5TabPanel(QFG5Button btn) {
            this.linkedButton = btn;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        }

        public bool Selected
        {
            get => isSelected;
            set
            {
                if (isSelected == value) return;
                isSelected = value;
                if (linkedButton != null) linkedButton.Selected = value;
                this.Invalidate();
                foreach (Control c in this.Controls) if (c is PictureBox) c.Invalidate();
            }
        }

        public void ManualHover(bool hovered) {
            this.isHovered = hovered;
            if (linkedButton != null) linkedButton.ManualHover(hovered);
            this.Invalidate();
            foreach (Control c in this.Controls) if (c is PictureBox) c.Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) {
            isHovered = true;
            if (linkedButton != null) linkedButton.ManualHover(true);
            this.Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e) {
            isHovered = false;
            if (linkedButton != null) linkedButton.ManualHover(false);
            this.Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            if (isSelected) {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(55, 218, 165, 32))) {
                    e.Graphics.FillRectangle(brush, this.ClientRectangle);
                }
                using (Pen pen = new Pen(Color.FromArgb(200, 218, 165, 32), 2)) {
                    e.Graphics.DrawRectangle(pen, 1, 1, this.Width - 3, this.Height - 3);
                }
            }
            else if (isHovered) {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(35, 255, 252, 210))) {
                    e.Graphics.FillRectangle(brush, this.ClientRectangle);
                }
            }
        }

        public void RegisterIcon(PictureBox pb) {
            pb.Paint += (s, e) => {
                if (isHovered || isSelected) {
                    using (SolidBrush pbBrush = new SolidBrush(Color.FromArgb(isSelected ? 180 : 150, Color.PaleGoldenrod))) {
                        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        e.Graphics.FillEllipse(pbBrush, 0, 0, pb.Width, pb.Height);
                    }
                }
            };
        }
    }

    public class MainForm : Form
    {
        private PictureBox logoBox;
        private readonly List<QFG5Button> tabButtons = new List<QFG5Button>();
        private readonly List<QFG5TabPanel> tabPanels = new List<QFG5TabPanel>();
        private int activeTabIndex = 0;

        public MainForm()
        {
            SharedState.LoadSettings();
            Text = "QFG5 Extraction Tools";
            Width = 1200;
            Height = 800;
            MinimumSize = new Size(1000, 650);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Black;

            Panel logoBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = Color.Transparent
            };
            logoBox = new PictureBox
            {
                Image = QFG5Theme.ToolkitLogo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Height = 56,
                Width = 360,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.None
            };
            logoBar.Controls.Add(logoBox);
            logoBar.Resize += (s, e) => CenterLogo(logoBar);
            this.Controls.Add(logoBar);

            // Visible tabs only (Map Icons excluded from strip)
            UserControl[] pages = new UserControl[] {
                new SpkTab(),
                new ModelTab(),
                new PanoTab(),
                new MsgTab(),
                new GraTab(),
                new MdlViewer(),
                new TextureEditorTab(),
                new SaveTab(),
                new FlagsTab(),
                new SettingsTab()
            };

            string[] pageNames = new string[] {
                "SPK Archive", "MDL Model", "Pano Image", "Message", "GRA Sprite",
                "3D Viewer", "Texture Edit", "Save Editor", "Flags (Beta)", "Settings"
            };

            string[] iconFiles = new string[] {
                "spk_900002_f001.png", "mdl_900003_f001.png", "pano_900017_f001.png", "message_900004_f001.png",
                "gra_900001_f001.png", "model_900007_f001.png", "texedit_900005.png", "save_900016_f001.png",
                "flag_900000_f001.png", "settings_900014_f001.png"
            };

            QFG5TableLayoutPanel tabHeaders = new QFG5TableLayoutPanel {
                Location = new Point(30, 12),
                Size = new Size(this.ClientSize.Width - 60, 130),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent,
                ColumnCount = pages.Length,
                RowCount = 1
            };
            float colPct = 100f / pages.Length;
            for (int i = 0; i < pages.Length; i++) {
                tabHeaders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, colPct));
            }

            Panel tabBody = new Panel {
                Location = new Point(30, 148),
                Size = new Size(this.ClientSize.Width - 60, this.ClientSize.Height - 148 - 80),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent,
                Padding = new Padding(6)
            };

            ((SaveTab)pages[7]).SaveFileLoaded += (path) => {
                ((FlagsTab)pages[8]).LoadSave(path);
            };

            Action<int> showTab = (index) => {
                if (index < 0 || index >= pages.Length) return;
                tabBody.Controls.Clear();
                UserControl c = pages[index];
                c.Dock = DockStyle.Fill;
                tabBody.Controls.Add(c);
                for (int t = 0; t < tabPanels.Count; t++) {
                    tabPanels[t].Selected = (t == index);
                }
                activeTabIndex = index;
            };

            for (int i = 0; i < pages.Length; i++)
            {
                QFG5Button btnTab = new QFG5Button {
                    Text = pageNames[i],
                    Height = 28,
                    Dock = DockStyle.Bottom,
                    Font = new Font("Arial", 8, FontStyle.Bold),
                    AutoEllipsis = true
                };
                tabButtons.Add(btnTab);

                QFG5TabPanel pnlTab = new QFG5TabPanel(btnTab) {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(3),
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                tabPanels.Add(pnlTab);

                string iconPath = Path.Combine(QFG5Theme.UIKitPath, "Menu Graphics" + Path.DirectorySeparatorChar + iconFiles[i]);
                if (File.Exists(iconPath)) {
                    PictureBox pbIcon = new PictureBox {
                        Image = Image.FromFile(iconPath),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Width = 56,
                        Height = 56,
                        BackColor = Color.Transparent,
                        Cursor = Cursors.Hand
                    };

                    pnlTab.SizeChanged += (s, e) => {
                        pbIcon.Left = (pnlTab.Width - 56) / 2;
                        pbIcon.Top = Math.Max(2, (pnlTab.Height - btnTab.Height - 56) / 2);
                    };

                    pbIcon.MouseEnter += (s, e) => pnlTab.ManualHover(true);
                    pbIcon.MouseLeave += (s, e) => pnlTab.ManualHover(false);
                    pbIcon.Click += (s, e) => btnTab.PerformClick();

                    pnlTab.RegisterIcon(pbIcon);
                    pnlTab.Controls.Add(pbIcon);
                }

                int index = i;
                btnTab.Click += (s, e) => showTab(index);

                pnlTab.Controls.Add(btnTab);
                tabHeaders.Controls.Add(pnlTab, i, 0);
            }

            // Default to SPK Tab
            showTab(0);

            Controls.Add(tabHeaders);
            Controls.Add(tabBody);

            // Texture editor still requests index 6; Settings remains last (9).
            SharedState.OnSwitchTabRequested += (idx) => {
                if (idx < 0 || idx >= pages.Length) return;
                showTab(idx);
            };

            this.Shown += (s, e) => CenterLogo(logoBar);
        }

        private void CenterLogo(Panel logoBar)
        {
            if (logoBox == null || logoBar == null) return;
            logoBox.Left = Math.Max(0, (logoBar.Width - logoBox.Width) / 2);
            logoBox.Top = Math.Max(0, (logoBar.Height - logoBox.Height) / 2);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(this.BackColor);

            Image bg = QFG5Theme.WindowBg;
            if (bg != null) {
                e.Graphics.DrawImage(bg, 0, 0, this.ClientSize.Width, this.ClientSize.Height);
            }
        }
    }
}
