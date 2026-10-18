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
    public class MainForm : Form
    {
        private PictureBox logoBox;
        private Label currentPageLabel;
        private readonly List<QFG5Button> tabButtons = new List<QFG5Button>();
        private int activeTabIndex = 0;

        private bool _firstPaintLogged = false;

        public MainForm()
        {
            var swMain = System.Diagnostics.Stopwatch.StartNew();
            Logger.LogStartup("MainForm constructor started");
            Logger.LogResource($"Memory before MainForm setup: {Logger.GetMemorySnapshot()}");

            var swSettings = System.Diagnostics.Stopwatch.StartNew();
            SharedState.LoadSettings();
            swSettings.Stop();
            Logger.LogStartup($"SharedState.LoadSettings: {swSettings.ElapsedMilliseconds} ms");

            Text = "QFG5 Extraction Tools";
            Width = 1320;
            Height = 860;
            MinimumSize = new Size(1024, 700);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = QFG5Theme.ColorBgDark;
            this.BackgroundImage = QFG5Theme.WindowBg;
            this.BackgroundImageLayout = ImageLayout.Stretch;

            Panel logoBar = new Panel
            {
                Location = new Point(12, 10),
                Size = new Size(this.ClientSize.Width - 24, 62),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = QFG5Theme.ColorSurface,
                BackgroundImage = QFG5Theme.LeatherTexture,
                BackgroundImageLayout = ImageLayout.Tile,
                BorderStyle = BorderStyle.FixedSingle
            };
            logoBar.Paint += (s, e) => QFG5Theme.DrawHeaderStroke(e.Graphics, logoBar.ClientRectangle);
            ToolTip toolTips = new ToolTip();
            logoBox = new PictureBox
            {
                Image = QFG5Theme.ToolkitLogo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Height = 50,
                Width = 190,
                Location = new Point(12, 5),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            logoBar.Controls.Add(logoBox);
            logoBar.Controls.Add(new Label
            {
                Text = "QFG V RESOURCE WORKSHOP",
                Location = new Point(218, 10),
                Size = new Size(360, 25),
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.TitleFont,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            });
            logoBar.Controls.Add(new Label
            {
                Text = "GAME ASSET & SAVE UTILITIES",
                Location = new Point(220, 34),
                Size = new Size(360, 18),
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            });
            currentPageLabel = new Label
            {
                Text = "SPK Archive",
                Location = new Point(logoBar.Width - 270, 15),
                Size = new Size(245, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                ForeColor = QFG5Theme.ColorGoldBright,
                Font = QFG5Theme.SectionHeaderFont,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };
            logoBar.Controls.Add(currentPageLabel);
            this.Controls.Add(logoBar);

            // Visible tabs only (Map Icons excluded from strip)
            UserControl[] pages = new UserControl[10];
            SaveSession saveSession = new SaveSession();
            SaveTab saveTab = null;
            FlagsTab flagsTab = null;
            var swTab = new System.Diagnostics.Stopwatch();

            Logger.LogStartup("Tab SpkTab: START");
            swTab.Restart();
            pages[0] = new SpkTab();
            swTab.Stop();
            Logger.LogStartup($"Tab SpkTab: END {swTab.ElapsedMilliseconds} ms");

            Logger.LogStartup("Tab ModelTab: START");
            swTab.Restart();
            pages[1] = new ModelTab();
            swTab.Stop();
            Logger.LogStartup($"Tab ModelTab: END {swTab.ElapsedMilliseconds} ms");

            Logger.LogStartup("Tab PanoTab: START");
            swTab.Restart();
            pages[2] = new PanoTab();
            swTab.Stop();
            Logger.LogStartup($"Tab PanoTab: END {swTab.ElapsedMilliseconds} ms");

            Logger.LogStartup("Tab MsgTab: START");
            swTab.Restart();
            pages[3] = new MsgTab();
            swTab.Stop();
            Logger.LogStartup($"Tab MsgTab: END {swTab.ElapsedMilliseconds} ms");

            Logger.LogStartup("Tab GraTab: START");
            swTab.Restart();
            pages[4] = new GraTab();
            swTab.Stop();
            Logger.LogStartup($"Tab GraTab: END {swTab.ElapsedMilliseconds} ms");

            Logger.LogStartup("Tab MdlViewer: START");
            swTab.Restart();
            pages[5] = new MdlViewer();
            swTab.Stop();
            Logger.LogStartup($"Tab MdlViewer: END {swTab.ElapsedMilliseconds} ms");

            Logger.LogStartup("Tab TextureEditorTab: START");
            swTab.Restart();
            pages[6] = new TextureEditorTab();
            swTab.Stop();
            Logger.LogStartup($"Tab TextureEditorTab: END {swTab.ElapsedMilliseconds} ms");

            Logger.LogStartup("Tab SaveTab: START");
            swTab.Restart();
            saveTab = new SaveTab(saveSession);
            pages[7] = saveTab;
            swTab.Stop();
            Logger.LogStartup($"Tab SaveTab: END {swTab.ElapsedMilliseconds} ms");

            Logger.LogStartup("Tab FlagsTab: START");
            swTab.Restart();
            flagsTab = new FlagsTab(saveSession);
            pages[8] = flagsTab;
            swTab.Stop();
            Logger.LogStartup($"Tab FlagsTab: END {swTab.ElapsedMilliseconds} ms");

            Logger.LogStartup("Tab SettingsTab: START");
            swTab.Restart();
            pages[9] = new SettingsTab();
            swTab.Stop();
            Logger.LogStartup($"Tab SettingsTab: END {swTab.ElapsedMilliseconds} ms");

            // Control & Memory snapshot after tabs
            int totalTabCtrls = 0, pbTabCtrls = 0, pnlTabCtrls = 0, btnTabCtrls = 0, dgvTabCtrls = 0;
            foreach (var tab in pages) Logger.CountControls(tab, ref totalTabCtrls, ref pbTabCtrls, ref pnlTabCtrls, ref btnTabCtrls, ref dgvTabCtrls);
            Logger.LogResource($"Controls across all 10 tabs: {totalTabCtrls} (PictureBoxes: {pbTabCtrls}, Panels: {pnlTabCtrls}, Buttons: {btnTabCtrls}, Grids: {dgvTabCtrls})");
            Logger.LogResource($"Memory after Tabs: {Logger.GetMemorySnapshot()}");

            string[] pageNames = new string[] {
                "SPK Archive", "MDL Model", "Pano Image", "Message", "GRA Sprite",
                "3D Viewer", "Texture Edit", "Save Editor", "Flags (Beta)", "Settings"
            };
            string[] navigationNames = new string[] {
                "SPK Archive", "MDL Model", "Pano Image", "Message (Beta)", "GRA Sprite",
                "3D Viewer", "Texture Edit (Beta)", "Save Editor", "Flags (Beta)", "Settings"
            };

            string[] iconFiles = new string[] {
                "spk_900002_f001.png", "mdl_900003_f001.png", "pano_900017_f001.png", "message_900004_f001.png",
                "gra_900001_f001.png", "model_900007_f001.png", "texedit_900005.png", "save_900016_f001.png",
                "flag_900000_f001.png", "settings_900014_f001.png"
            };

            QFG5TableLayoutPanel tabHeaders = new QFG5TableLayoutPanel {
                Location = new Point(12, 84),
                Size = new Size(192, this.ClientSize.Height - 98),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = QFG5Theme.ColorSurface,
                BackgroundImage = QFG5Theme.LeatherTexture,
                BackgroundImageLayout = ImageLayout.Tile,
                Padding = new Padding(4),
                ColumnCount = 1,
                RowCount = pages.Length + 1
            };
            tabHeaders.Paint += (s, e) => QFG5Theme.DrawHeaderStroke(e.Graphics, tabHeaders.ClientRectangle);
            tabHeaders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tabHeaders.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            for (int i = 0; i < pages.Length; i++)
            {
                tabHeaders.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / pages.Length));
            }

            Panel tabBody = new Panel {
                Location = new Point(216, 84),
                Size = new Size(this.ClientSize.Width - 228, this.ClientSize.Height - 98),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = QFG5Theme.ColorSurface,
                BackgroundImage = QFG5Theme.LeatherTexture,
                BackgroundImageLayout = ImageLayout.Tile,
                Padding = new Padding(7)
            };
            tabBody.Paint += (s, e) => QFG5Theme.DrawHeaderStroke(e.Graphics, tabBody.ClientRectangle);

            saveTab.SaveLoadRequested += (path) => CoordinateSaveLoad(path, saveSession, saveTab, flagsTab);
            flagsTab.SaveLoadRequested += (path) => CoordinateSaveLoad(path, saveSession, saveTab, flagsTab);
            flagsTab.SaveRequested += () => CoordinateFlagsSave(saveTab, flagsTab);

            Action<int> showTab = (index) => {
                if (index < 0 || index >= pages.Length) return;
                tabBody.Controls.Clear();
                UserControl c = pages[index];
                c.Dock = DockStyle.Fill;
                tabBody.Controls.Add(c);
                currentPageLabel.Text = pageNames[index];
                for (int t = 0; t < tabButtons.Count; t++) {
                    tabButtons[t].Selected = (t == index);
                }
                activeTabIndex = index;
            };

            bool navigationCollapsed = false;
            QFG5Button btnMenuToggle = new QFG5Button
            {
                Text = "◀",
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 3, 3, 5),
                Font = QFG5Theme.ButtonFont,
                ForeColor = QFG5Theme.ColorTextDark,
                Cursor = Cursors.Hand,
                AccessibleName = "Collapse navigation"
            };
            toolTips.SetToolTip(btnMenuToggle, "Collapse navigation menu");
            btnMenuToggle.Click += (s, e) => {
                navigationCollapsed = !navigationCollapsed;
                int navigationWidth = navigationCollapsed ? 58 : 192;
                tabHeaders.Width = navigationWidth;
                tabBody.Left = tabHeaders.Left + navigationWidth + 12;
                tabBody.Width = this.ClientSize.Width - tabBody.Left - 12;
                btnMenuToggle.Text = navigationCollapsed ? "▶" : "◀";
                btnMenuToggle.AccessibleName = navigationCollapsed ? "Expand navigation" : "Collapse navigation";
                toolTips.SetToolTip(btnMenuToggle, navigationCollapsed ? "Expand navigation menu" : "Collapse navigation menu");
                for (int i = 0; i < tabButtons.Count; i++)
                {
                    tabButtons[i].Text = navigationCollapsed ? string.Empty : navigationNames[i];
                    tabButtons[i].ImageOnLeft = !navigationCollapsed;
                    tabButtons[i].AccessibleName = navigationNames[i];
                    toolTips.SetToolTip(tabButtons[i], navigationNames[i]);
                }
            };
            tabHeaders.Controls.Add(btnMenuToggle, 0, 0);

            var swRibbon = System.Diagnostics.Stopwatch.StartNew();
            int iconsLoaded = 0;
            for (int i = 0; i < pages.Length; i++)
            {
                QFG5Button btnTab = new QFG5Button {
                    Text = navigationNames[i],
                    Dock = DockStyle.Fill,
                    Margin = new Padding(3, 3, 3, 4),
                    Font = QFG5Theme.BodyBoldFont,
                    ForeColor = QFG5Theme.ColorTextDark,
                    ImageOnLeft = true,
                    AutoEllipsis = true,
                    Cursor = Cursors.Hand
                };
                tabButtons.Add(btnTab);

                string iconPath = Path.Combine(QFG5Theme.UIKitPath, "Menu Graphics" + Path.DirectorySeparatorChar + iconFiles[i]);
                if (File.Exists(iconPath)) {
                    btnTab.Image = Image.FromFile(iconPath);
                    iconsLoaded++;
                }
                toolTips.SetToolTip(btnTab, navigationNames[i]);

                int index = i;
                btnTab.Click += (s, e) => showTab(index);
                tabHeaders.Controls.Add(btnTab, 0, i + 1);
            }
            swRibbon.Stop();
            Logger.LogStartup($"Navigation construction: {swRibbon.ElapsedMilliseconds} ms (buttons: {tabButtons.Count}, icons loaded: {iconsLoaded})");
            Logger.LogResource($"Memory after Ribbon: {Logger.GetMemorySnapshot()}");

            // Default to SPK Tab
            var swShowTab0 = System.Diagnostics.Stopwatch.StartNew();
            showTab(0);
            swShowTab0.Stop();
            Logger.LogStartup($"showTab(0): {swShowTab0.ElapsedMilliseconds} ms");
            Logger.LogResource($"Memory after showTab(0): {Logger.GetMemorySnapshot()}");

            Controls.Add(tabHeaders);
            Controls.Add(tabBody);

            // Texture editor still requests index 6; Settings remains last (9).
            SharedState.OnSwitchTabRequested += (idx) => {
                if (idx < 0 || idx >= pages.Length) return;
                showTab(idx);
            };

            swMain.Stop();
            Logger.LogStartup($"MainForm constructor total: {swMain.ElapsedMilliseconds} ms");

            this.Paint += (s, e) => {
                if (!_firstPaintLogged)
                {
                    _firstPaintLogged = true;
                    Logger.LogStartup($"First Paint: {Logger.StartupWatch.ElapsedMilliseconds} ms from start");
                }
            };

            this.Shown += (s, e) => {
                CenterLogo(logoBar);
                Logger.LogStartup($"Form Shown: {Logger.StartupWatch.ElapsedMilliseconds} ms from start");

                int totalShown = 0, pbShown = 0, pnlShown = 0, btnShown = 0, dgvShown = 0;
                Logger.CountControls(this, ref totalShown, ref pbShown, ref pnlShown, ref btnShown, ref dgvShown);
                Logger.LogResource($"MainForm total controls: {totalShown} (PictureBoxes: {pbShown}, Panels: {pnlShown}, Buttons: {btnShown}, Grids: {dgvShown})");
                Logger.LogResource($"Memory after MainForm shown: {Logger.GetMemorySnapshot()}");

                LogActiveTimers(pages);
                Logger.LogStartup("===== QFG5Extractor Performance Baseline END =====");
            };
        }

        private void LogActiveTimers(UserControl[] pages)
        {
            try
            {
                if (pages.Length > 5 && pages[5] is MdlViewer mv)
                {
                    var fSpin = typeof(MdlViewer).GetField("spinTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (fSpin != null && fSpin.GetValue(mv) is Timer tSpin)
                        Logger.LogResource($"Timer: MdlViewer.spinTimer interval={tSpin.Interval} enabled={tSpin.Enabled}");
                }
                if (pages.Length > 7 && pages[7] is SaveTab st)
                {
                    var fPrev = typeof(SaveTab).GetField("previewTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (fPrev != null && fPrev.GetValue(st) is Timer tPrev)
                        Logger.LogResource($"Timer: SaveTab.previewTimer interval={tPrev.Interval} enabled={tPrev.Enabled}");
                }
                if (pages.Length > 4 && pages[4] is GraTab gt)
                {
                    var fAnim = typeof(GraTab).GetField("animTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (fAnim != null && fAnim.GetValue(gt) is Timer tAnim)
                        Logger.LogResource($"Timer: GraTab.animTimer interval={tAnim.Interval} enabled={tAnim.Enabled}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogResource("Could not inspect timers: " + ex.Message);
            }
        }

        private void CoordinateSaveLoad(string path, SaveSession session, SaveTab saveTab, FlagsTab flagsTab)
        {
            if (!saveTab.CommitPendingEdits())
            {
                saveTab.RestoreActivePath();
                saveTab.ShowInvalidItemIdWarning("loading another save");
                return;
            }
            try
            {
                bool loaded = session.TryLoad(path, () =>
                {
                    DialogResult choice = MessageBox.Show(
                        this,
                        "The active save has unsaved changes. Save them before replacing the loaded save?",
                        "Unsaved Save Changes",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning);

                    if (choice == DialogResult.No) return true;
                    if (choice != DialogResult.Yes) return false;

                    try
                    {
                        session.Save();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Could not save the active save file: " + ex.Message,
                            "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                });

                if (loaded)
                {
                    saveTab.RefreshFromSession();
                    flagsTab.RefreshFromSession();
                }
                else
                {
                    saveTab.RestoreActivePath();
                }
            }
            catch (Exception ex)
            {
                saveTab.RestoreActivePath();
                MessageBox.Show(this, "Could not load save file: " + ex.Message,
                    "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CoordinateFlagsSave(SaveTab saveTab, FlagsTab flagsTab)
        {
            flagsTab.SaveActiveSession();
        }

        private void CenterLogo(Panel logoBar)
        {
            if (logoBox == null || logoBar == null) return;
            logoBox.Left = 12;
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
