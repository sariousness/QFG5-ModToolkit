using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace QFG5Extractor.Core
{
    public class SettingsTab : UserControl
    {
        private QFG5PathField txtBaseDataPath;
        private QFG5PathField txtExtractionPath;
        private QFG5Button btnSave;
        private QFG5Button btnStartGame;
        private QFG5Button btnRestore;
        private CheckBox chkAutoSave;
        private Label lblSettingsStatus;

        public SettingsTab()
        {
            Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            QFG5TableLayoutPanel mainLayout = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(QFG5Layout.PagePadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Title
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120)); // Main path settings
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // AutoSave checkbox
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94)); // Actions and status
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Filler

            Label lblTitle = new Label {
                Text = "APPLICATION SETTINGS",
                Font = QFG5Theme.TitleFont,
                ForeColor = QFG5Theme.ColorTextGold,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            QFG5Theme.ApplyHeaderStroke(lblTitle);
            mainLayout.Controls.Add(lblTitle, 0, 0);

            QFG5TableLayoutPanel pathRows = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            pathRows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 2; i++)
                pathRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            pathRows.Controls.Add(CreatePathRow("Game DATA Folder:", SharedState.BaseDataPath, out txtBaseDataPath), 0, 0);
            pathRows.Controls.Add(CreatePathRow("Mods / Extracted Data:", SharedState.ExtractionPath, out txtExtractionPath), 0, 1);
            ToolTip pathToolTip = new ToolTip();
            pathToolTip.SetToolTip(txtBaseDataPath, "Select the game's DATA folder itself; it can contain SPK archives or extracted cdn and hdnw folders.");
            pathToolTip.SetToolTip(txtExtractionPath, "Asset tools create matching cdn and hdnw folders beneath this destination.");
            mainLayout.Controls.Add(new QFG5CollapsibleSection("GAME AND MOD PATHS", pathRows, 120), 0, 1);
            
            chkAutoSave = new CheckBox {
                Text = "Save Settings on Change",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                AutoSize = true,
                Margin = new Padding(190, 10, 0, 0)
            };
            mainLayout.Controls.Add(chkAutoSave, 0, 2);

            QFG5TableLayoutPanel pnlBottom = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                Padding = new Padding(0)
            };
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pnlBottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            pnlBottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            
            btnSave = new QFG5Button { Text = "Save Settings", Width = 160, Height = 30, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 4) };
            btnSave.Click += (s, e) => {
                if (SaveAllSettings())
                    MessageBox.Show("Settings saved to settings.cfg.", "Success");
                else
                    if (lblSettingsStatus.Text.StartsWith("Settings could not be saved", StringComparison.Ordinal))
                        MessageBox.Show(lblSettingsStatus.Text, "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            
            btnStartGame = new QFG5Button { Text = "START GAME", Width = 160, Height = 30, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 4) };
            btnStartGame.Click += BtnStartGame_Click;

            btnRestore = new QFG5Button { Text = "RESTORE ORIGINALS", Width = 200, Height = 30, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 4) };
            btnRestore.Click += BtnRestore_Click;

            lblSettingsStatus = new Label {
                Text = "Settings are active for this session.",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlBottom.Controls.Add(btnSave, 0, 0);
            pnlBottom.Controls.Add(btnStartGame, 1, 0);
            pnlBottom.Controls.Add(btnRestore, 2, 0);
            pnlBottom.Controls.Add(lblSettingsStatus, 0, 1);
            pnlBottom.SetColumnSpan(lblSettingsStatus, 4);
            
            mainLayout.Controls.Add(pnlBottom, 0, 3);

            // Events for AutoSave
            txtBaseDataPath.TextChanged += (s, e) => { if (chkAutoSave.Checked) SaveAllSettings(); };
            txtExtractionPath.TextChanged += (s, e) => { if (chkAutoSave.Checked) SaveAllSettings(); };

            this.Controls.Add(mainLayout);
        }

        private void BtnStartGame_Click(object sender, EventArgs e)
        {
            string gameDataPath = txtBaseDataPath.Text.Trim();
            string gamePath = Directory.Exists(gameDataPath) ? Directory.GetParent(gameDataPath)?.FullName : null;
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)) {
                MessageBox.Show("Please set a valid Game DATA Folder first.");
                return;
            }

            string exePath = null;
            string[] files = Directory.GetFiles(gamePath);
            foreach (string f in files) {
                if (Path.GetFileName(f).Equals("QFG5.exe", StringComparison.OrdinalIgnoreCase)) {
                    exePath = f;
                    break;
                }
            }
            
            if (exePath == null) {
                MessageBox.Show("Could not find QFG5.exe in the specified folder.");
                return;
            }

            try {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.WorkingDirectory = gamePath;
                
                // Detect Linux/Wine
                if (System.Environment.OSVersion.Platform == PlatformID.Unix) {
                    psi.FileName = "wine";
                    psi.Arguments = "\"" + exePath + "\"";
                } else {
                    psi.FileName = exePath;
                }
                
                System.Diagnostics.Process.Start(psi);
            } catch (Exception ex) {
                MessageBox.Show("Failed to start game: " + ex.Message);
            }
        }

        private void BtnRestore_Click(object sender, EventArgs e)
        {
            string gameDataPath = txtBaseDataPath.Text.Trim();
            string gamePath = Directory.Exists(gameDataPath) ? Directory.GetParent(gameDataPath)?.FullName : null;
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)) {
                MessageBox.Show("Please set a valid Game DATA Folder first.");
                return;
            }

            string backupPath = Path.Combine(gamePath, "backup");
            if (!Directory.Exists(backupPath)) {
                MessageBox.Show("No backup folder found in the game directory. Cannot restore.");
                return;
            }

            if (MessageBox.Show("This will overwrite all modded files with original backups. Proceed?", "Restore Originals", MessageBoxButtons.YesNo) == DialogResult.No)
                return;

            try {
                int count = 0;
                foreach (string file in Directory.GetFiles(backupPath, "*.*", SearchOption.AllDirectories)) {
                    string relPath = file.Substring(backupPath.Length).TrimStart(Path.DirectorySeparatorChar);
                    string destPath = Path.Combine(gamePath, relPath);
                    string destDir = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                    File.Copy(file, destPath, true);
                    count++;
                }
                MessageBox.Show($"Successfully restored {count} files from backup.", "Restoration Complete");
            } catch (Exception ex) {
                MessageBox.Show("Restoration failed: " + ex.Message);
            }
        }

        private Control CreatePathRow(string label, string value, out QFG5PathField field)
        {
            QFG5TableLayoutPanel row = new QFG5TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

            row.Controls.Add(new Label {
                Text = label,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            field = new QFG5PathField { Dock = DockStyle.Fill, Height = 30, AccessibleName = label.Trim().TrimEnd(':') };
            field.Text = value;
            row.Controls.Add(field, 1, 0);

            QFG5Button btnBrowse = new QFG5Button { Text = "Browse", Dock = DockStyle.Fill, Height = 30 };
            var f = field;
            btnBrowse.Click += (s, e) => {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog()) {
                    if (fbd.ShowDialog() == DialogResult.OK) f.Text = fbd.SelectedPath;
                }
            };
            row.Controls.Add(btnBrowse, 2, 0);
            return row;
        }

        private bool SaveAllSettings()
        {
            string dataPath = txtBaseDataPath.Text.Trim();
            string modsPath = txtExtractionPath.Text.Trim();
            if (!string.IsNullOrEmpty(dataPath) && !SharedState.IsValidGameDataPath(dataPath))
            {
                lblSettingsStatus.Text = "Select a folder containing .SPK archives or the extracted cdn and hdnw folders.";
                lblSettingsStatus.ForeColor = Color.Firebrick;
                return false;
            }
            if (string.IsNullOrEmpty(modsPath))
            {
                lblSettingsStatus.Text = "Choose a Mods / Extracted Data folder.";
                lblSettingsStatus.ForeColor = Color.Firebrick;
                return false;
            }

            SharedState.BaseDataPath = dataPath;
            SharedState.ExtractionPath = modsPath;
            bool saved = SharedState.SaveSettings();
            if (saved)
            {
                lblSettingsStatus.Text = "Game and Mods folders saved to settings.cfg.";
                lblSettingsStatus.ForeColor = QFG5Theme.ColorGoldBright;
            }
            else
            {
                lblSettingsStatus.Text = "Settings could not be saved; selected paths remain active for this session.";
                lblSettingsStatus.ForeColor = QFG5Theme.ColorTextGold;
            }

            return saved;
        }
    }
}
