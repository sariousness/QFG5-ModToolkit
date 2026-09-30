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
        private QFG5PathField txtModdedPath;
        private QFG5PathField txtGamePath;
        private QFG5Button btnSave;
        private QFG5Button btnStartGame;
        private QFG5Button btnRestore;
        private CheckBox chkAutoSave;

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
                RowCount = 7,
                Padding = new Padding(30)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Title
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // Data Path
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // Extraction Path
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // Modded Path
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // Game Path
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // AutoSave checkbox
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Filler

            Label lblTitle = new Label {
                Text = "APPLICATION SETTINGS",
                Font = new Font("Arial", 16, FontStyle.Bold),
                ForeColor = Color.Gold,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            mainLayout.Controls.Add(lblTitle, 0, 0);

            // Data Path
            mainLayout.Controls.Add(CreatePathRow("Game Data Path:", SharedState.BaseDataPath, out txtBaseDataPath), 0, 1);
            // Extraction Path
            mainLayout.Controls.Add(CreatePathRow("Extraction Path:", SharedState.ExtractionPath, out txtExtractionPath), 0, 2);
            // Modded Path
            mainLayout.Controls.Add(CreatePathRow("Modded Path:", SharedState.ModdedFilesPath, out txtModdedPath), 0, 3);
            // Game Path
            mainLayout.Controls.Add(CreatePathRow("Game Location:", SharedState.GamePath, out txtGamePath), 0, 4);
            
            chkAutoSave = new CheckBox { Text = "Save Settings on Change", ForeColor = Color.White, AutoSize = true, Margin = new Padding(130, 10, 0, 0) };
            mainLayout.Controls.Add(chkAutoSave, 0, 5);

            Panel pnlBottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 25, 0, 0) };
            
            btnSave = new QFG5Button { Text = "Save Settings", Width = 150, Height = 30, Location = new Point(0, 0) };
            btnSave.Click += (s, e) => {
                SaveAllSettings();
                MessageBox.Show("Settings saved and applied.", "Success");
            };
            
            btnStartGame = new QFG5Button { Text = "START GAME", Width = 150, Height = 30, Location = new Point(160, 0) };
            btnStartGame.Click += BtnStartGame_Click;

            btnRestore = new QFG5Button { Text = "RESTORE ORIGINALS", Width = 150, Height = 30, Location = new Point(320, 0) };
            btnRestore.Click += BtnRestore_Click;

            pnlBottom.Controls.Add(btnSave);
            pnlBottom.Controls.Add(btnStartGame);
            pnlBottom.Controls.Add(btnRestore);
            
            mainLayout.Controls.Add(pnlBottom, 0, 6);

            // Events for AutoSave
            txtBaseDataPath.TextChanged += (s, e) => { if(chkAutoSave.Checked) SaveAllSettings(); };
            txtExtractionPath.TextChanged += (s, e) => { if(chkAutoSave.Checked) SaveAllSettings(); };
            txtModdedPath.TextChanged += (s, e) => { if(chkAutoSave.Checked) SaveAllSettings(); };
            txtGamePath.TextChanged += (s, e) => { if(chkAutoSave.Checked) SaveAllSettings(); };

            this.Controls.Add(mainLayout);
        }

        private void BtnStartGame_Click(object sender, EventArgs e)
        {
            string gamePath = txtGamePath.Text;
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)) {
                MessageBox.Show("Please specify a valid Game Location first.");
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
            string gamePath = txtGamePath.Text;
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)) {
                MessageBox.Show("Please specify a valid Game Location first.");
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
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

            row.Controls.Add(new Label { Text = label, ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            field = new QFG5PathField { Dock = DockStyle.Fill, Height = 30 };
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

        private void SaveAllSettings()
        {
            SharedState.BaseDataPath = txtBaseDataPath.Text;
            SharedState.ExtractionPath = txtExtractionPath.Text;
            SharedState.ModdedFilesPath = txtModdedPath.Text;
            SharedState.GamePath = txtGamePath.Text;
            SharedState.SaveSettings();
        }
    }
}
