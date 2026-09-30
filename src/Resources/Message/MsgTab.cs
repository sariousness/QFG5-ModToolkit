using QFG5Extractor.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using QFG5Extractor.Core.UI;

namespace QFG5Extractor.Resources.Message
{
    public class MsgTab : UserControl
    {
        private QFG5PathField qgmFolderTextBox;
        private QFG5PathField outputFolderTextBox;
        private QFG5Button runButton;
        private QFG5Button repackButton;
        private Label statusLabel;
        private Label currentFileLabel;
        private QFG5ProgressBar progressBar;
        private ListBox convertedListBox;

        public MsgTab()
        {
            Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;
            InitializeComponent();

            SharedState.OnBaseDataPathChanged += UpdatePaths;
            if (!string.IsNullOrEmpty(SharedState.BaseDataPath))
            {
                UpdatePaths(SharedState.BaseDataPath);
            }
        }

        private void UpdatePaths(string path)
        {
            string qgmDir = Path.Combine(path, "cdn" + Path.DirectorySeparatorChar + "QGM");
            if (!Directory.Exists(qgmDir)) qgmDir = Path.Combine(path, "cdn" + Path.DirectorySeparatorChar + "qgm");
            if (Directory.Exists(qgmDir)) qgmFolderTextBox.Text = qgmDir;
            
            SharedState.OnExtractionPathChanged += (p) => { outputFolderTextBox.Text = p; };
            if (!string.IsNullOrEmpty(SharedState.ExtractionPath)) outputFolderTextBox.Text = SharedState.ExtractionPath;
        }

        private void InitializeComponent()
        {
            QFG5TableLayoutPanel mainLayout = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.RowHeight));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.RowHeight));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.ProgressRowHeight));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, QFG5Layout.StatusRowHeight));

            runButton = new QFG5Button { Text = "Extract All", Width = 105, Height = 30 };
            runButton.Click += RunButtonClick;
            repackButton = new QFG5Button { Text = "Repack All", Width = 105, Height = 30 };
            repackButton.Click += RepackButtonClick;

            QFG5Button btnBrowseOut;
            QFG5TableLayoutPanel outRow = QFG5Layout.CreatePathRow("Output Path:", out outputFolderTextBox, out btnBrowseOut, runButton, repackButton);
            btnBrowseOut.Click += (s, e) => outputFolderTextBox.Text = BrowseFolder(outputFolderTextBox.Text);

            QFG5Button btnBrowseQgm;
            QFG5TableLayoutPanel qgmRow = QFG5Layout.CreatePathRow("QGM Path:", out qgmFolderTextBox, out btnBrowseQgm);
            btnBrowseQgm.Click += (s, e) => qgmFolderTextBox.Text = BrowseFolder(qgmFolderTextBox.Text);

            msgSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BorderStyle = BorderStyle.None
            };
            QFG5Layout.SetSplitterRatioOnce(msgSplit, 0.35);

            QFG5PreviewFrame listFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            convertedListBox = new ListBox { Dock = DockStyle.Fill, BackColor = Color.Black, ForeColor = Color.White, BorderStyle = BorderStyle.None };
            listFrame.Controls.Add(convertedListBox);
            msgSplit.Panel1.Controls.Add(listFrame);

            QFG5PreviewFrame editorFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            RichTextBox txtEditor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 20),
                ForeColor = Color.WhiteSmoke,
                Font = new System.Drawing.Font("Courier New", 9f),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                ReadOnly = false,
                WordWrap = true
            };

            Panel btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 45, BackColor = Color.Transparent };
            QFG5Button btnSaveEdits = new QFG5Button {
                Text = "Save Changes",
                Width = 160,
                Height = 30,
                Enabled = false,
                Anchor = AnchorStyles.None
            };
            btnPanel.Controls.Add(btnSaveEdits);
            btnPanel.Resize += (s, e) => {
                btnSaveEdits.Left = Math.Max(0, (btnPanel.Width - btnSaveEdits.Width) / 2);
                btnSaveEdits.Top = Math.Max(0, (btnPanel.Height - btnSaveEdits.Height) / 2);
            };

            txtEditor.TextChanged += (s, e) => btnSaveEdits.Enabled = true;

            btnSaveEdits.Click += (s, e) => {
                if (convertedListBox.SelectedItem == null) return;
                string selectedFile = convertedListBox.SelectedItem.ToString();
                string fullPath = System.IO.Path.Combine(outputFolderTextBox.Text.Trim(), selectedFile);
                try {
                    System.IO.File.WriteAllText(fullPath, txtEditor.Text);
                    statusLabel.Text = $"Saved: {selectedFile}";
                    btnSaveEdits.Enabled = false;
                } catch (Exception ex) {
                    statusLabel.Text = $"Save Failed: {ex.Message}";
                }
            };

            editorFrame.Controls.Add(txtEditor);
            editorFrame.Controls.Add(btnPanel);
            msgSplit.Panel2.Controls.Add(editorFrame);

            convertedListBox.SelectedIndexChanged += (s, e) => {
                if (convertedListBox.SelectedItem == null) return;
                string selectedFile = convertedListBox.SelectedItem.ToString();
                string outFolder = outputFolderTextBox.Text.Trim();
                string fullPath = System.IO.Path.Combine(outFolder, selectedFile);
                if (System.IO.File.Exists(fullPath)) {
                    try { txtEditor.Text = System.IO.File.ReadAllText(fullPath); btnSaveEdits.Enabled = false; }
                    catch (Exception ex) { txtEditor.Text = $"[Error reading file: {ex.Message}]"; }
                } else {
                    txtEditor.Text = $"[File not found: {fullPath}]";
                }
            };

            progressBar = new QFG5ProgressBar { Dock = DockStyle.Fill, Height = 25 };

            QFG5FlowLayoutPanel statusRow = new QFG5FlowLayoutPanel { Dock = DockStyle.Fill };
            currentFileLabel = new Label { Text = "Ready", ForeColor = Color.White, AutoSize = true, Margin = new Padding(0, 5, 20, 0) };
            statusLabel = new Label { Text = "Select output and click Run.", ForeColor = Color.Gold, AutoSize = true, Margin = new Padding(0, 5, 0, 0) };
            statusRow.Controls.Add(currentFileLabel);
            statusRow.Controls.Add(statusLabel);

            mainLayout.Controls.Add(outRow, 0, 0);
            mainLayout.Controls.Add(qgmRow, 0, 1);
            mainLayout.Controls.Add(msgSplit, 0, 2);
            mainLayout.Controls.Add(progressBar, 0, 3);
            mainLayout.Controls.Add(statusRow, 0, 4);

            this.Controls.Add(mainLayout);
        }

        private static string BrowseFolder(string currentPath)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                if (!string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath)) dialog.SelectedPath = currentPath;
                if (dialog.ShowDialog() == DialogResult.OK) return dialog.SelectedPath;
            }
            return currentPath;
        }

        private void RunButtonClick(object sender, EventArgs e)
        {
            string qgmFolder = qgmFolderTextBox.Text.Trim();
            string outputFolder = outputFolderTextBox.Text.Trim();

            if (!Directory.Exists(qgmFolder) || !Directory.Exists(outputFolder))
            {
                MessageBox.Show("Please ensure both paths exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            runButton.Enabled = false;
            convertedListBox.Items.Clear();
            SetStatus("Running...", Color.RoyalBlue);

            int converted = 0;
            int failed = 0;

            try
            {
                string[] allFiles = Directory.GetFiles(qgmFolder, "*.qgm");
                if (allFiles.Length == 0) allFiles = Directory.GetFiles(qgmFolder, "*.QGM");

                if (allFiles.Length == 0)
                {
                    SetStatus("No QGM files found.", Color.Orange);
                    runButton.Enabled = true;
                    return;
                }

                progressBar.Value = 0;
                int total = allFiles.Length;
                
                for (int i = 0; i < total; i++)
                {
                    string file = allFiles[i];
                    string baseName = Path.GetFileNameWithoutExtension(file);
                    currentFileLabel.Text = "Extracting: " + baseName;
                    Application.DoEvents();

                    try
                    {
                        MsgConverter.ExtractMsg(file, Path.Combine(outputFolder, baseName + ".txt"));
                        converted++;
                        convertedListBox.Items.Add(baseName + ".txt");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("MsgTab", ex.Message);
                        failed++;
                    }
                    progressBar.Value = (int)((float)(i + 1) / total * 100);
                }
                SetStatus($"Done. Extracted {converted} files.", Color.ForestGreen);
                MessageBox.Show($"Done. Extracted {converted} files.", "Message Tab", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Error: " + ex.Message, Color.Firebrick);
                Logger.LogError("MsgTab", ex.Message);
            }
            finally { runButton.Enabled = true; }
        }

        private void RepackButtonClick(object sender, EventArgs e)
        {
            MessageBox.Show("Repack functionality for Message tab is under development.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private SplitContainer msgSplit;

        private void SetStatus(string text, Color color) { statusLabel.Text = text; statusLabel.ForeColor = color; }
    }
}
