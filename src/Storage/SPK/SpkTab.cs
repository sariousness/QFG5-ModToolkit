using QFG5Extractor.Core;
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using QFG5Extractor.Core.UI;

namespace QFG5Extractor.Storage.SPK
{
    public class SpkTab : UserControl
    {
        private QFG5Button runButton;
        private QFG5Button repackButton;
        private CheckBox uppercaseOutputNamesCheckBox;
        private CheckedListBox filesListBox;
        private Label statusLabel;
        private Label auditIndicatorLabel;
        private Label lblSelectionCount;
        private QFG5ProgressBar progressBar;
        private readonly List<SpkArchiveAuditResult> auditResults = new List<SpkArchiveAuditResult>();
        private readonly List<string> auditErrors = new List<string>();

        public SpkTab()
        {
            Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;
            InitializeComponent();
            SharedState.OnBaseDataPathChanged += path => LoadSpkFiles(SharedState.BaseDataPath);
            LoadSpkFiles(SharedState.BaseDataPath);
        }

        private void InitializeComponent()
        {
            QFG5TableLayoutPanel mainLayout = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

            ToolTip toolTip = new ToolTip();

            QFG5TableLayoutPanel actionBar = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 4)
            };
            actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            auditIndicatorLabel = new Label
            {
                Text = "Audit: scanning archive entries...",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                AccessibleName = "SPK archive audit summary"
            };
            toolTip.SetToolTip(auditIndicatorLabel,
                "Missing counts archive entries without a matching file under Mods. Candidates are extensions outside the app's known dedicated workflows; this does not prove the file was not extracted.");
            actionBar.Controls.Add(auditIndicatorLabel, 0, 0);

            QFG5Button btnScan = new QFG5Button { Text = "Scan", Dock = DockStyle.Fill, Height = 30 };
            btnScan.Click += (s, e) => LoadSpkFiles(SharedState.BaseDataPath);
            toolTip.SetToolTip(btnScan, "Rescan the configured DATA folder for SPK archives");
            actionBar.Controls.Add(btnScan, 1, 0);

            QFG5Button btnAudit = new QFG5Button { Text = "Audit", Dock = DockStyle.Fill, Height = 30 };
            btnAudit.Click += (s, e) => RefreshAudit();
            toolTip.SetToolTip(btnAudit, "Recheck archive entries against files currently present under Mods");
            actionBar.Controls.Add(btnAudit, 2, 0);

            QFG5Button btnAuditDetails = new QFG5Button { Text = "Audit Details", Dock = DockStyle.Fill, Height = 30 };
            btnAuditDetails.Click += (s, e) => ShowAuditDetails();
            toolTip.SetToolTip(btnAuditDetails, "Show exact missing and candidate file paths from the SPK archives");
            actionBar.Controls.Add(btnAuditDetails, 3, 0);

            runButton = new QFG5Button { Text = "Extract", Width = 95, Height = 30 };
            runButton.Click += RunButton_Click;
            toolTip.SetToolTip(runButton, "Extract checked SPK archives into their matching folders under Mods");
            actionBar.Controls.Add(runButton, 4, 0);

            // --- Row 3: Result / Archive List Area ---
            QFG5PreviewFrame listFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            filesListBox = new CheckedListBox {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                BorderStyle = BorderStyle.None,
                CheckOnClick = true,
                IntegralHeight = false
            };
            filesListBox.ItemCheck += (s, e) =>
            {
                if (filesListBox.IsHandleCreated)
                    filesListBox.BeginInvoke((MethodInvoker)UpdateSelectionStatus);
                else
                    UpdateSelectionStatus();
            };
            listFrame.Controls.Add(filesListBox);

            // --- Row 4: Two-Row Cohesive Footer ---
            progressBar = new QFG5ProgressBar { Height = 22, Dock = DockStyle.Fill, AccessibleName = "SPK archive operation progress" };
            statusLabel = new Label {
                Text = "Ready — select archives to extract or repack.",
                ForeColor = QFG5Theme.ColorGold,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            repackButton = new QFG5Button { Text = "Repack All", Width = 130, Height = 30 };
            repackButton.Click += RepackButton_Click;
            toolTip.SetToolTip(repackButton, "Repack modified files from the matching Mods folder into Mods/repacked");

            uppercaseOutputNamesCheckBox = new CheckBox
            {
                Text = "Uppercase filename",
                Width = 130,
                Dock = DockStyle.Left,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.CaptionFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };
            toolTip.SetToolTip(uppercaseOutputNamesCheckBox,
                "Write repacked SPK filenames in uppercase in Mods/repacked.");

            QFG5Button btnAll = new QFG5Button { Text = "All", Width = 60, Height = 28 };
            btnAll.Click += (s, e) => {
                for (int i = 0; i < filesListBox.Items.Count; i++) filesListBox.SetItemChecked(i, true);
                UpdateSelectionStatus();
            };
            toolTip.SetToolTip(btnAll, "Select all archives in the list");

            QFG5Button btnNone = new QFG5Button { Text = "None", Width = 60, Height = 28 };
            btnNone.Click += (s, e) => {
                for (int i = 0; i < filesListBox.Items.Count; i++) filesListBox.SetItemChecked(i, false);
                UpdateSelectionStatus();
            };
            toolTip.SetToolTip(btnNone, "Deselect all archives in the list");

            lblSelectionCount = new Label {
                Text = "(0 files)",
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                AutoSize = true,
                Margin = new Padding(8, 6, 0, 0)
            };

            QFG5TableLayoutPanel footer = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Margin = new Padding(0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));

            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            // Footer Row 0: Status and progress
            footer.Controls.Add(statusLabel, 0, 0);
            footer.SetColumnSpan(statusLabel, 2);

            Panel progressWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 5) };
            progressWrapper.Controls.Add(progressBar);
            footer.Controls.Add(progressWrapper, 2, 0);

            // Footer Row 1: All/None Selection Controls (Left) | Repack Button (Right)
            FlowLayoutPanel selPanel = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            selPanel.Controls.Add(btnAll);
            selPanel.Controls.Add(btnNone);
            selPanel.Controls.Add(lblSelectionCount);
            footer.Controls.Add(selPanel, 0, 1);

            footer.Controls.Add(new Panel { Dock = DockStyle.Fill }, 1, 1);

            Panel repackWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 2, 0, 2) };
            repackButton.Dock = DockStyle.Right;
            repackWrapper.Controls.Add(repackButton);
            repackWrapper.Controls.Add(uppercaseOutputNamesCheckBox);
            footer.Controls.Add(repackWrapper, 2, 1);

            mainLayout.Controls.Add(actionBar, 0, 0);
            mainLayout.Controls.Add(listFrame, 0, 1);
            mainLayout.Controls.Add(footer, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private void UpdateSelectionStatus()
        {
            if (lblSelectionCount == null) return;
            int checkedCount = filesListBox.CheckedItems.Count;
            int totalCount = filesListBox.Items.Count;
            if (totalCount > 0)
                lblSelectionCount.Text = $"({checkedCount} of {totalCount} selected)";
            else
                lblSelectionCount.Text = "(0 files)";
        }

        private void LoadSpkFiles(string folder)
        {
            filesListBox.Items.Clear();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) {
                statusLabel.Text = "Data folder does not exist.";
                UpdateSelectionStatus();
                RefreshAudit();
                return;
            }

            try {
                string[] files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                    if (string.Equals(Path.GetExtension(file), ".spk", StringComparison.OrdinalIgnoreCase))
                        filesListBox.Items.Add(Path.GetFileName(file), true);
                if (filesListBox.Items.Count > 0)
                    statusLabel.Text = $"Found {filesListBox.Items.Count} SPK files.";
                else
                    statusLabel.Text = "No .SPK files found in folder.";
                UpdateSelectionStatus();
                RefreshAudit();
            } catch (Exception ex) {
                statusLabel.Text = "Error scanning folder: " + ex.Message;
                MessageBox.Show("Error scanning folder: " + ex.Message);
                RefreshAudit();
            }
        }

        private void RunButton_Click(object sender, EventArgs e)
        {
            if (filesListBox.CheckedItems.Count == 0) {
                MessageBox.Show("Please select at least one SPK file to extract.");
                return;
            }

            string sourceFolder = SharedState.BaseDataPath;
            string outDir = SharedState.ExtractionPath;
            if (!Directory.Exists(sourceFolder)) {
                MessageBox.Show("The configured Game DATA Folder does not exist.");
                return;
            }
            Directory.CreateDirectory(outDir);

            runButton.Enabled = false;
            repackButton.Enabled = false;
            progressBar.Value = 0;
            int count = 0;
            int total = filesListBox.CheckedItems.Count;

            foreach (string fileName in filesListBox.CheckedItems) {
                try {
                    string fullPath = Path.Combine(sourceFolder, fileName);
                    string target = SharedState.GetArchiveModsPath(fileName);
                    
                    statusLabel.Text = "Extracting " + fileName + "...";
                    Application.DoEvents();
                    
                    SpkConverter.Extract(fullPath, target);
                    count++;

                    progressBar.Value = (int)((float)count / total * 100);

                } catch (Exception ex) {
                    Logger.LogError("SpkTab", $"Error extracting {fileName}: {ex.Message}");
                }
            }
            
            statusLabel.Text = $"Extracted {count} files successfully.";
            if (count > 0) SharedState.NotifyGameDataExtracted();
            RefreshAudit();
            runButton.Enabled = true;
            repackButton.Enabled = true;
            MessageBox.Show("Extraction complete.");
        }

        private void RefreshAudit()
        {
            auditResults.Clear();
            auditErrors.Clear();

            string sourceFolder = SharedState.BaseDataPath;
            if (filesListBox == null || filesListBox.Items.Count == 0)
            {
                auditIndicatorLabel.Text = "Audit: no SPK archives to check.";
                auditIndicatorLabel.ForeColor = QFG5Theme.ColorTextMuted;
                return;
            }

            int missingCount = 0;
            int candidateCount = 0;
            foreach (string fileName in filesListBox.Items)
            {
                string archivePath = Path.Combine(sourceFolder, fileName);
                try
                {
                    SpkArchiveAuditResult result = SpkConverter.Audit(
                        archivePath,
                        SharedState.GetArchiveModsPath(fileName));
                    auditResults.Add(result);
                    missingCount += result.MissingFromModsCount;
                    candidateCount += result.CandidateUnhandledCount;
                }
                catch (Exception ex)
                {
                    string error = fileName + ": " + ex.Message;
                    auditErrors.Add(error);
                    Logger.LogError("SpkTab", "Error auditing " + error);
                }
            }

            auditIndicatorLabel.Text = string.Format(
                "Audit: {0} missing; {1} candidates; {2} errors",
                missingCount,
                candidateCount,
                auditErrors.Count);
            auditIndicatorLabel.ForeColor = missingCount > 0 || candidateCount > 0 || auditErrors.Count > 0
                ? QFG5Theme.ColorGold
                : QFG5Theme.ColorTextMuted;
        }

        private void ShowAuditDetails()
        {
            Form detailsForm = new Form
            {
                Text = "SPK Archive Audit Details",
                StartPosition = FormStartPosition.CenterParent,
                Width = 780,
                Height = 560,
                MinimizeBox = false,
                MaximizeBox = true
            };
            Panel filterPanel = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(6) };
            Label filterLabel = new Label
            {
                Text = "Filter archive / path:",
                AutoSize = true,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft
            };
            TextBox filterTextBox = new TextBox { Dock = DockStyle.Fill };
            RichTextBox detailsTextBox = new RichTextBox
            {
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9f),
            };
            filterPanel.Controls.Add(filterTextBox);
            filterPanel.Controls.Add(filterLabel);
            detailsForm.Controls.Add(detailsTextBox);
            detailsForm.Controls.Add(filterPanel);
            filterTextBox.TextChanged += (s, e) => detailsTextBox.Text = BuildAuditDetails(filterTextBox.Text);
            detailsTextBox.Text = BuildAuditDetails(string.Empty);
            detailsForm.ShowDialog(this);
        }

        private string BuildAuditDetails(string filter)
        {
            StringBuilder details = new StringBuilder();
            details.AppendLine("Missing from Mods");
            details.AppendLine("Archive entries with no matching extracted file under that archive's Mods folder.");
            details.AppendLine();
            AppendAuditEntries(details, true, filter);

            details.AppendLine();
            details.AppendLine("Candidate formats not covered by a dedicated tool workflow");
            details.AppendLine("Known workflow extensions: .GRA, .MDL, .ANM, .IMG, .NOD, .QGM, .FTR.");
            details.AppendLine("These entries may have been extracted as raw files. Their extensions are candidates for investigation, not proof of missing data.");
            details.AppendLine();
            AppendAuditEntries(details, false, filter);

            if (auditErrors.Count > 0)
            {
                details.AppendLine();
                details.AppendLine("Archives that could not be audited");
                int shownErrors = 0;
                foreach (string error in auditErrors)
                {
                    if (MatchesAuditFilter(error, filter))
                    {
                        details.AppendLine("- " + error);
                        shownErrors++;
                    }
                }
                if (shownErrors == 0)
                    details.AppendLine("None match this filter.");
            }

            if (auditResults.Count == 0 && auditErrors.Count == 0)
                details.AppendLine("No SPK archives are available to audit.");
            else if (auditResults.TrueForAll(result => result.MissingFromModsCount == 0 && result.CandidateUnhandledCount == 0)
                && auditErrors.Count == 0)
                details.AppendLine("No missing entries or candidate extensions were found.");
            return details.ToString();
        }

        private void AppendAuditEntries(StringBuilder details, bool missingFromMods, string filter)
        {
            int count = 0;
            foreach (SpkArchiveAuditResult result in auditResults)
            {
                foreach (SpkArchiveAuditEntry entry in result.Entries)
                {
                    if (missingFromMods ? entry.IsPresentInMods : entry.HasDedicatedToolSupport)
                        continue;
                    if (!MatchesAuditFilter(result.ArchiveFileName + " " + entry.FileName, filter))
                        continue;

                    details.AppendLine("- " + result.ArchiveFileName + ": " + entry.FileName);
                    count++;
                }
            }

            if (count == 0)
                details.AppendLine("None.");
        }

        private static bool MatchesAuditFilter(string value, string filter)
        {
            return string.IsNullOrWhiteSpace(filter)
                || value.IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RepackButton_Click(object sender, EventArgs e)
        {
            if (filesListBox.CheckedItems.Count == 0) {
                MessageBox.Show("Please select at least one SPK file to repack.");
                return;
            }

            string sourceFolder = SharedState.BaseDataPath;
            string modDir = SharedState.ExtractionPath;
            if (!Directory.Exists(sourceFolder) || !Directory.Exists(modDir)) {
                MessageBox.Show("The configured Game DATA or Mods folder is missing.");
                return;
            }

            runButton.Enabled = false;
            repackButton.Enabled = false;
            progressBar.Value = 0;
            int count = 0;
            int total = filesListBox.CheckedItems.Count;

            foreach (string fileName in filesListBox.CheckedItems) {
                try {
                    string originalPath = Path.Combine(sourceFolder, fileName);
                    string sourceDir = SharedState.GetArchiveModsPath(fileName);
                    
                    string repackDir = Path.Combine(modDir, "repacked");
                    if (!Directory.Exists(repackDir)) Directory.CreateDirectory(repackDir);
                    string outputFileName = uppercaseOutputNamesCheckBox.Checked
                        ? fileName.ToUpperInvariant()
                        : fileName;
                    string outPath = Path.Combine(repackDir, outputFileName);

                    statusLabel.Text = "Repacking " + fileName + "...";
                    Application.DoEvents();

                    SpkConverter.Repack(originalPath, sourceDir, outPath);
                    count++;
                    progressBar.Value = (int)((float)count / total * 100);
                } catch (Exception ex) {
                    Logger.LogError("SpkTab", $"Error repacking {fileName}: {ex.Message}");
                }
            }

            statusLabel.Text = $"Repacked {count} files into 'repacked' folder.";
            runButton.Enabled = true;
            repackButton.Enabled = true;
            MessageBox.Show("Repack complete. Check the Mods/repacked folder.");
        }
    }
}
