using QFG5Extractor.Core.UI;
using QFG5Extractor.Core;
using QFG5Extractor.Resources.Save;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using OpenTK;

namespace QFG5Extractor.Resources.Model
{
    public class AnmRetargetTab : UserControl
    {
        private readonly ListBox donorModelList = CreateAssetList();
        private readonly ListBox donorAnimationList = CreateAssetList();
        private readonly ListBox recipientModelList = CreateAssetList();
        private readonly ListBox recipientPoseList = CreateAssetList();
        private readonly ComboBox donorReferenceList = CreateReferenceList();
        private readonly ComboBox recipientReferenceList = CreateReferenceList();
        private readonly CheckBox preserveRecipientScaleCheckBox = new CheckBox();
        private readonly CharacterModelPreviewControl donorPreview =
            new CharacterModelPreviewControl("DONOR PREVIEW", true);
        private readonly CharacterModelPreviewControl recipientPreview =
            new CharacterModelPreviewControl("RECIPIENT PREVIEW", true);
        private readonly DataGridView mappingGrid = new DataGridView();
        private readonly SplitContainer workspaceSplit = new SplitContainer();
        private readonly Label statusLabel = new Label();
        private readonly Timer previewRefreshTimer = new Timer { Interval = 180 };
        private QFG5Button togglePartsButton;
        private readonly string staticTrackLabel = "Static (pose ANM or identity)";
        private List<string> donorParts;
        private List<string> recipientParts;
        private List<string> donorTrackLabels;
        private List<AssetListItem> availableAnimations = new List<AssetListItem>();
        private bool updatingAssetLists;
        private bool updatingOrientationOffsets;

        public AnmRetargetTab()
        {
            Dock = DockStyle.Fill;
            BackColor = QFG5Theme.ColorBgDark;
            BuildInterface();
            previewRefreshTimer.Tick += PreviewRefreshTimer_Tick;
            donorModelList.SelectedIndexChanged += DonorModelSelectionChanged;
            donorAnimationList.SelectedIndexChanged += AssetSelectionChanged;
            recipientModelList.SelectedIndexChanged += RecipientModelSelectionChanged;
            recipientPoseList.SelectedIndexChanged += AssetSelectionChanged;
            SharedState.OnBaseDataPathChanged += RefreshAssetLists;
            SharedState.OnExtractionPathChanged += RefreshAssetLists;
            SharedState.OnGameDataExtracted += RefreshAssetLists;
            RefreshAssetLists();
        }

        private void BuildInterface()
        {
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12),
                BackColor = QFG5Theme.ColorBgDark
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));

            Label introduction = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyBoldFont,
                Text = "ANM LAB — EXPERIMENTAL TRACK REMAPPING\r\nChoose reference clips to preserve recipient scale and translation, or disable it for raw track copying.",
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(introduction, 0, 0);

            FlowLayoutPanel retargetOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = QFG5Theme.ColorBgDark,
                Padding = new Padding(0, 4, 0, 0)
            };
            preserveRecipientScaleCheckBox.AutoSize = true;
            preserveRecipientScaleCheckBox.Checked = true;
            preserveRecipientScaleCheckBox.Text = "Preserve recipient scale / translation";
            preserveRecipientScaleCheckBox.ForeColor = QFG5Theme.ColorTextPrimary;
            preserveRecipientScaleCheckBox.Font = QFG5Theme.BodyFont;
            preserveRecipientScaleCheckBox.CheckedChanged += (s, e) => ScheduleRetargetPreview();
            donorReferenceList.Width = 190;
            recipientReferenceList.Width = 190;
            retargetOptions.Controls.Add(preserveRecipientScaleCheckBox);
            retargetOptions.Controls.Add(CreateAssetLabel("Donor reference"));
            retargetOptions.Controls.Add(donorReferenceList);
            retargetOptions.Controls.Add(CreateAssetLabel("Recipient reference"));
            retargetOptions.Controls.Add(recipientReferenceList);
            layout.Controls.Add(retargetOptions, 0, 1);
            donorReferenceList.SelectedIndexChanged += ReferenceAnimationSelectionChanged;
            recipientReferenceList.SelectedIndexChanged += ReferenceAnimationSelectionChanged;

            workspaceSplit.Dock = DockStyle.Fill;
            workspaceSplit.Orientation = Orientation.Horizontal;
            workspaceSplit.Size = new Size(1200, 500);
            workspaceSplit.SplitterDistance = 270;
            workspaceSplit.Panel1MinSize = 180;
            workspaceSplit.Panel2MinSize = 100;
            workspaceSplit.SplitterWidth = 8;

            SplitContainer donorColumnSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Size = new Size(1200, 260),
                SplitterDistance = 260,
                Panel1MinSize = 170,
                Panel2MinSize = 430,
                SplitterWidth = 8
            };

            SplitContainer donorPreviewSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Size = new Size(900, 260),
                SplitterDistance = 300,
                Panel1MinSize = 170,
                Panel2MinSize = 430,
                SplitterWidth = 8
            };
            SplitContainer recipientPreviewSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Size = new Size(600, 260),
                SplitterDistance = 300,
                Panel1MinSize = 170,
                Panel2MinSize = 170,
                SplitterWidth = 8
            };

            donorPreview.Dock = DockStyle.Fill;
            donorPreview.Margin = new Padding(3);
            recipientPreview.Dock = DockStyle.Fill;
            recipientPreview.Margin = new Padding(3);
            recipientPreviewSplit.Panel1.Controls.Add(recipientPreview);
            recipientPreviewSplit.Panel2.Controls.Add(
                CreateAssetSelector("RECIPIENT", "Model (.MDL)", recipientModelList, "Pose ANM (optional)", recipientPoseList));
            donorPreviewSplit.Panel1.Controls.Add(donorPreview);
            donorPreviewSplit.Panel2.Controls.Add(recipientPreviewSplit);
            donorColumnSplit.Panel1.Controls.Add(
                CreateAssetSelector("DONOR", "Model (.MDL)", donorModelList, "Animation (.ANM)", donorAnimationList));
            donorColumnSplit.Panel2.Controls.Add(donorPreviewSplit);
            workspaceSplit.Panel1.Controls.Add(donorColumnSplit);

            mappingGrid.Dock = DockStyle.Fill;
            mappingGrid.AllowUserToAddRows = false;
            mappingGrid.AllowUserToDeleteRows = false;
            mappingGrid.AllowUserToResizeRows = false;
            mappingGrid.EditMode = DataGridViewEditMode.EditOnEnter;
            mappingGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            mappingGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            mappingGrid.BackgroundColor = QFG5Theme.ColorSurfaceRecessed;
            mappingGrid.GridColor = QFG5Theme.ColorBorderOuter;
            mappingGrid.DefaultCellStyle.BackColor = QFG5Theme.ColorSurface;
            mappingGrid.DefaultCellStyle.ForeColor = QFG5Theme.ColorTextPrimary;
            mappingGrid.DefaultCellStyle.SelectionBackColor = QFG5Theme.ColorSelected;
            mappingGrid.DefaultCellStyle.SelectionForeColor = QFG5Theme.ColorGoldBright;
            mappingGrid.DefaultCellStyle.Font = QFG5Theme.BodyFont;
            mappingGrid.ColumnHeadersDefaultCellStyle.BackColor = QFG5Theme.ColorSurface;
            mappingGrid.ColumnHeadersDefaultCellStyle.ForeColor = QFG5Theme.ColorTextGold;
            mappingGrid.ColumnHeadersDefaultCellStyle.Font = QFG5Theme.BodyBoldFont;
            mappingGrid.RowTemplate.Height = 27;
            mappingGrid.EnableHeadersVisualStyles = false;
            mappingGrid.RowHeadersVisible = false;
            mappingGrid.DataError += MappingGrid_DataError;
            mappingGrid.CellClick += MappingGrid_CellClick;
            mappingGrid.SelectionChanged += MappingGrid_SelectionChanged;
            mappingGrid.CellValueChanged += MappingGrid_CellValueChanged;
            mappingGrid.CurrentCellDirtyStateChanged += MappingGrid_CurrentCellDirtyStateChanged;
            mappingGrid.EditingControlShowing += MappingGrid_EditingControlShowing;
            mappingGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "RecipientPart",
                HeaderText = "Recipient part",
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                FillWeight = 42
            });
            mappingGrid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "DonorTrack",
                HeaderText = "Donor track (or static)",
                SortMode = DataGridViewColumnSortMode.NotSortable,
                FillWeight = 48,
                FlatStyle = FlatStyle.Flat
            });
            mappingGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "OffsetX",
                HeaderText = "X deg",
                SortMode = DataGridViewColumnSortMode.NotSortable,
                FillWeight = 12
            });
            mappingGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "OffsetY",
                HeaderText = "Y deg",
                SortMode = DataGridViewColumnSortMode.NotSortable,
                FillWeight = 12
            });
            mappingGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "OffsetZ",
                HeaderText = "Z deg",
                SortMode = DataGridViewColumnSortMode.NotSortable,
                FillWeight = 12
            });
            workspaceSplit.Panel2.Controls.Add(mappingGrid);
            layout.Controls.Add(workspaceSplit, 0, 2);

            TableLayoutPanel footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 8,
                RowCount = 1,
                Padding = new Padding(0, 8, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            QFG5Button refreshButton = new QFG5Button
            {
                Text = "Refresh lists",
                Dock = DockStyle.Fill,
                Height = 38,
                Margin = new Padding(3)
            };
            refreshButton.Click += (s, e) =>
            {
                RefreshAssetLists();
            };
            QFG5Button loadPartsButton = new QFG5Button
            {
                Text = "Load parts",
                Dock = DockStyle.Fill,
                Height = 38,
                Margin = new Padding(3)
            };
            loadPartsButton.Click += (s, e) => LoadMappings();
            togglePartsButton = new QFG5Button
            {
                Text = "Hide parts",
                Dock = DockStyle.Fill,
                Height = 38,
                Margin = new Padding(3)
            };
            togglePartsButton.Click += (s, e) => TogglePartsPanel();
            QFG5Button exportButton = new QFG5Button
            {
                Text = "Export new ANM...",
                Dock = DockStyle.Fill,
                Height = 38,
                Margin = new Padding(3)
            };
            exportButton.Click += (s, e) => ExportAnimation();
            QFG5Button transplantButton = new QFG5Button
            {
                Text = "Transplant",
                Dock = DockStyle.Fill,
                Height = 38,
                Margin = new Padding(3)
            };
            transplantButton.Click += (s, e) => TransplantSelectedAnimation();
            QFG5Button transplantAllButton = new QFG5Button
            {
                Text = "Transplant all",
                Dock = DockStyle.Fill,
                Height = 38,
                Margin = new Padding(3)
            };
            transplantAllButton.Click += (s, e) => TransplantAllAnimations();
            QFG5Button estimateOffsetsButton = new QFG5Button
            {
                Text = "Estimate offsets",
                Dock = DockStyle.Fill,
                Height = 38,
                Margin = new Padding(3)
            };
            estimateOffsetsButton.Click += (s, e) => EstimateOrientationOffsets();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.ForeColor = QFG5Theme.ColorTextGold;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Padding = new Padding(10, 0, 0, 0);
            footer.Controls.Add(refreshButton, 0, 0);
            footer.Controls.Add(loadPartsButton, 1, 0);
            footer.Controls.Add(togglePartsButton, 2, 0);
            footer.Controls.Add(exportButton, 3, 0);
            footer.Controls.Add(transplantButton, 4, 0);
            footer.Controls.Add(transplantAllButton, 5, 0);
            footer.Controls.Add(estimateOffsetsButton, 6, 0);
            footer.Controls.Add(statusLabel, 7, 0);
            layout.Controls.Add(footer, 0, 3);
            Controls.Add(layout);
        }

        private void TogglePartsPanel()
        {
            workspaceSplit.Panel2Collapsed = !workspaceSplit.Panel2Collapsed;
            togglePartsButton.Text = workspaceSplit.Panel2Collapsed ? "Show parts" : "Hide parts";
        }

        private static ListBox CreateAssetList()
        {
            return new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont
            };
        }

        private static ComboBox CreateReferenceList()
        {
            return new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                IntegralHeight = false,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont
            };
        }

        private static Control CreateAssetSelector(
            string title, string modelLabel, ListBox modelList, string animationLabel, ListBox animationList)
        {
            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(6),
                Margin = new Padding(3),
                BackColor = QFG5Theme.ColorSurface
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            Label heading = new Label
            {
                Dock = DockStyle.Fill,
                Text = title,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.SectionHeaderFont,
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(heading, 0, 0);
            panel.Controls.Add(CreateAssetLabel(modelLabel), 0, 1);
            panel.Controls.Add(modelList, 0, 2);
            panel.Controls.Add(CreateAssetLabel(animationLabel), 0, 3);
            panel.Controls.Add(animationList, 0, 4);
            return panel;
        }

        private static Label CreateAssetLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private void RefreshAssetLists()
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(RefreshAssetLists));
                return;
            }

            updatingAssetLists = true;
            bool refreshed = false;
            string refreshError = null;
            try
            {
                string selectedDonorModel = GetSelectedPath(donorModelList);
                string selectedRecipientModel = GetSelectedPath(recipientModelList);
                string selectedRecipientPose = GetSelectedPath(recipientPoseList);
                string selectedDonorAnimation = GetSelectedPath(donorAnimationList);
                string selectedDonorReference = GetSelectedPath(donorReferenceList);
                string selectedRecipientReference = GetSelectedPath(recipientReferenceList);

                List<AssetListItem> models = GetModelAssetFiles();
                PopulateAssetList(donorModelList, models, selectedDonorModel);
                PopulateAssetList(recipientModelList, models, selectedRecipientModel);
                availableAnimations = GetAnimationAssetFiles();
                PopulateDonorAnimations(selectedDonorAnimation, selectFirstWhenMissing: true);
                PopulateRecipientPoses(selectedRecipientPose);
                PopulateReferenceAnimations(donorReferenceList,
                    GetSelectedPath(donorModelList), selectedDonorReference);
                PopulateReferenceAnimations(recipientReferenceList,
                    GetSelectedPath(recipientModelList), selectedRecipientReference);
                refreshed = true;
            }
            catch (Exception ex)
            {
                refreshError = "Could not scan model/animation files: " + ex.Message;
            }
            finally
            {
                updatingAssetLists = false;
                ClearLoadedMapping(false);
            }
            if (refreshed)
                statusLabel.Text = "Select donor MDL/ANM and recipient MDL from the lists; recipient pose ANM is optional.";
            else if (refreshError != null)
                statusLabel.Text = refreshError;
            UpdatePreviews();
        }

        private void RefreshAssetLists(string path)
        {
            RefreshAssetLists();
        }

        private static List<AssetListItem> GetAssetFiles(string archiveFolder, string assetFolder, string extension)
        {
            string directory = SharedState.GetAssetDataPath(archiveFolder, assetFolder);
            List<AssetListItem> files = new List<AssetListItem>();
            if (!Directory.Exists(directory)) return files;

            foreach (string path in Directory.GetFiles(directory))
            {
                if (string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
                    files.Add(new AssetListItem(path, Path.GetFileName(path)));
            }
            files.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
            return files;
        }

        private static List<AssetListItem> GetModelAssetFiles()
        {
            List<AssetListItem> models = GetAssetFiles("hdnw", "mdl", ".mdl");
            OldMdlViewerLogic viewerLogic = new OldMdlViewerLogic();
            foreach (AssetListItem model in models)
            {
                string modelName;
                try
                {
                    modelName = viewerLogic.getName(model.Path);
                }
                catch (Exception ex)
                {
                    Logger.LogError("ANM Lab", "Could not read model name from " +
                        Path.GetFileName(model.Path) + ": " + ex.Message);
                    modelName = "<None>";
                }
                model.Name = Path.GetFileName(model.Path) + " - [" + modelName + "]";
            }
            return models;
        }

        private static List<AssetListItem> GetAnimationAssetFiles()
        {
            List<AssetListItem> animations = GetAssetFiles("hdnw", "anm", ".anm");
            foreach (AssetListItem animation in animations)
            {
                try
                {
                    string animationName = AnmAnimationLoader.ReadAnimationName(animation.Path);
                    if (!string.IsNullOrEmpty(animationName))
                        animation.Name = Path.GetFileName(animation.Path) + " - [" + animationName + "]";
                }
                catch (Exception ex)
                {
                    Logger.LogError("ANM Lab", "Could not read animation name from " +
                        Path.GetFileName(animation.Path) + ": " + ex.Message);
                }
            }
            return animations;
        }

        private static void PopulateAssetList(
            ListBox list, List<AssetListItem> items, string selectedPath, bool selectFirstWhenMissing = false)
        {
            list.BeginUpdate();
            try
            {
                list.Items.Clear();
                int selectedIndex = -1;
                for (int i = 0; i < items.Count; i++)
                {
                    list.Items.Add(items[i]);
                    if (selectedPath != null &&
                        string.Equals(items[i].Path, selectedPath, StringComparison.OrdinalIgnoreCase))
                        selectedIndex = i;
                    else if (selectedPath == null && string.IsNullOrEmpty(items[i].Path))
                        selectedIndex = i;
                }

                if (selectedIndex < 0 && selectFirstWhenMissing && items.Count > 0)
                    selectedIndex = 0;
                list.SelectedIndex = selectedIndex;
            }
            finally
            {
                list.EndUpdate();
            }
        }

        private static string GetSelectedPath(ListBox list)
        {
            AssetListItem item = list.SelectedItem as AssetListItem;
            return item == null ? null : item.Path;
        }

        private static string GetSelectedPath(ComboBox list)
        {
            AssetListItem item = list.SelectedItem as AssetListItem;
            return item == null ? null : item.Path;
        }

        private void DonorModelSelectionChanged(object sender, EventArgs e)
        {
            if (updatingAssetLists) return;
            PopulateDonorAnimations(null, selectFirstWhenMissing: true);
            PopulateReferenceAnimations(donorReferenceList, GetSelectedPath(donorModelList), null);
            ClearLoadedMapping();
            UpdatePreviews();
        }

        private void RecipientModelSelectionChanged(object sender, EventArgs e)
        {
            if (updatingAssetLists) return;
            PopulateRecipientPoses(GetSelectedPath(recipientPoseList));
            PopulateReferenceAnimations(recipientReferenceList, GetSelectedPath(recipientModelList), null);
            ClearLoadedMapping();
            UpdatePreviews();
        }

        private void ReferenceAnimationSelectionChanged(object sender, EventArgs e)
        {
            if (!updatingAssetLists)
                ScheduleRetargetPreview();
        }

        private void PopulateDonorAnimations(string selectedPath, bool selectFirstWhenMissing)
        {
            bool wasUpdating = updatingAssetLists;
            updatingAssetLists = true;
            try
            {
                PopulateAssetList(donorAnimationList,
                    GetAnimationsForModel(GetSelectedPath(donorModelList)),
                    selectedPath ?? GetSelectedPath(donorAnimationList),
                    selectFirstWhenMissing);
            }
            finally
            {
                updatingAssetLists = wasUpdating;
            }
        }

        private void PopulateRecipientPoses(string selectedPath)
        {
            bool wasUpdating = updatingAssetLists;
            updatingAssetLists = true;
            try
            {
                List<AssetListItem> poses = GetAnimationsForModel(GetSelectedPath(recipientModelList));
                poses.Insert(0, new AssetListItem(string.Empty, "<None — identity pose>"));
                PopulateAssetList(recipientPoseList, poses, selectedPath, selectFirstWhenMissing: true);
            }
            finally
            {
                updatingAssetLists = wasUpdating;
            }
        }

        private void PopulateReferenceAnimations(ComboBox list, string modelPath, string selectedPath)
        {
            bool wasUpdating = updatingAssetLists;
            updatingAssetLists = true;
            try
            {
                List<AssetListItem> references = GetAnimationsForModel(modelPath);
                references.Insert(0, new AssetListItem(string.Empty, "<Select reference ANM>"));
                PopulateReferenceList(list, references, selectedPath);
            }
            finally
            {
                updatingAssetLists = wasUpdating;
            }
        }

        private static void PopulateReferenceList(
            ComboBox list, List<AssetListItem> items, string selectedPath)
        {
            list.BeginUpdate();
            try
            {
                list.Items.Clear();
                int selectedIndex = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    list.Items.Add(items[i]);
                    if (!string.IsNullOrEmpty(selectedPath) &&
                        string.Equals(items[i].Path, selectedPath, StringComparison.OrdinalIgnoreCase))
                        selectedIndex = i;
                }
                list.SelectedIndex = selectedIndex;
            }
            finally
            {
                list.EndUpdate();
            }
        }

        private List<AssetListItem> GetAnimationsForModel(string modelPath)
        {
            List<AssetListItem> matches = new List<AssetListItem>();
            string modelName = Path.GetFileNameWithoutExtension(modelPath ?? string.Empty);
            if (string.IsNullOrEmpty(modelName))
                return matches;
            for (int i = 0; i < modelName.Length; i++)
            {
                if (!char.IsDigit(modelName[i]))
                    return matches;
            }

            foreach (AssetListItem animation in availableAnimations)
            {
                if (Path.GetFileNameWithoutExtension(animation.Path)
                    .StartsWith(modelName, StringComparison.OrdinalIgnoreCase))
                    matches.Add(animation);
            }
            matches.Sort((left, right) => CompareAnimationSequence(left, right, modelName));
            return matches;
        }

        private static int CompareAnimationSequence(AssetListItem left, AssetListItem right, string prefix)
        {
            string leftName = Path.GetFileNameWithoutExtension(left.Path);
            string rightName = Path.GetFileNameWithoutExtension(right.Path);
            int leftSequence;
            int rightSequence;
            bool leftHasSequence = int.TryParse(leftName.Substring(prefix.Length), out leftSequence);
            bool rightHasSequence = int.TryParse(rightName.Substring(prefix.Length), out rightSequence);
            if (leftHasSequence && rightHasSequence)
                return leftSequence.CompareTo(rightSequence);
            return StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        }

        private void AssetSelectionChanged(object sender, EventArgs e)
        {
            if (!updatingAssetLists)
            {
                UpdatePreviews();
                RefreshRetargetPreview();
            }
        }

        private void UpdatePreviews()
        {
            string donorModelPath = GetSelectedPath(donorModelList);
            string donorAnimationPath = GetSelectedPath(donorAnimationList);
            if (string.IsNullOrWhiteSpace(donorModelPath))
                donorPreview.ClearModel("Select a donor MDL to preview.");
            else
                donorPreview.SetPreviewFiles(donorModelPath, donorAnimationPath);

            string recipientModelPath = GetSelectedPath(recipientModelList);
            string recipientAnimationPath = GetSelectedPath(recipientPoseList);
            if (string.IsNullOrWhiteSpace(recipientModelPath))
                recipientPreview.ClearModel("Select a recipient MDL to preview.");
            else
                recipientPreview.SetPreviewFiles(recipientModelPath, recipientAnimationPath);
            UpdateHighlightedParts();
        }

        private sealed class AssetListItem
        {
            public string Path { get; private set; }
            public string Name { get; set; }

            public AssetListItem(string path, string name)
            {
                Path = path;
                Name = name;
            }

            public override string ToString()
            {
                return Name;
            }
        }

        private void MappingGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 ||
                e.ColumnIndex != mappingGrid.Columns["DonorTrack"].Index)
                return;

            mappingGrid.BeginEdit(true);
            ComboBox combo = mappingGrid.EditingControl as ComboBox;
            if (combo != null)
                combo.DroppedDown = true;
        }

        private void MappingGrid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (mappingGrid.IsCurrentCellDirty)
                mappingGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void MappingGrid_SelectionChanged(object sender, EventArgs e)
        {
            UpdateHighlightedParts();
        }

        private void MappingGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (!updatingOrientationOffsets && e.RowIndex >= 0 &&
                (e.ColumnIndex == mappingGrid.Columns["DonorTrack"].Index ||
                 e.ColumnIndex == mappingGrid.Columns["OffsetX"].Index ||
                 e.ColumnIndex == mappingGrid.Columns["OffsetY"].Index ||
                 e.ColumnIndex == mappingGrid.Columns["OffsetZ"].Index))
                ScheduleRetargetPreview();
        }

        private void UpdateHighlightedParts()
        {
            int rowIndex = mappingGrid.CurrentCell == null
                ? -1
                : mappingGrid.CurrentCell.RowIndex;
            if (rowIndex < 0 || rowIndex >= mappingGrid.Rows.Count ||
                donorTrackLabels == null)
            {
                donorPreview.SetHighlightedTrack(-1);
                recipientPreview.SetHighlightedTrack(-1);
                return;
            }

            object value = mappingGrid.Rows[rowIndex].Cells["DonorTrack"].Value;
            string label = value == null ? null : value.ToString();
            int donorTrack = donorTrackLabels.IndexOf(label);
            recipientPreview.SetHighlightedTrack(rowIndex);
            donorPreview.SetHighlightedTrack(donorTrack);
        }

        private void RefreshRetargetPreview()
        {
            UpdateHighlightedParts();
            if (donorParts == null || recipientParts == null ||
                mappingGrid.Rows.Count != recipientParts.Count)
                return;

            try
            {
                float[,] offsets;
                int[] mapping = ReadCurrentMapping(out offsets, commitEdit: false);
                Matrix4[][] frames = AnmRetargeter.BuildPreviewFrames(
                    GetSelectedPath(donorAnimationList),
                    GetSelectedPath(recipientPoseList),
                    donorParts.Count, recipientParts.Count, mapping, offsets,
                    preserveRecipientScaleCheckBox.Checked,
                    GetSelectedPath(donorReferenceList),
                    GetSelectedPath(recipientReferenceList));
                recipientPreview.SetAnimationFrames(frames);
                statusLabel.Text = preserveRecipientScaleCheckBox.Checked
                    ? "Scale-preserving preview updated from the selected reference clips."
                    : "Raw-copy preview updated. Orientation offsets and mapping changes are not saved until you transplant.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Could not update recipient preview: " + ex.Message;
            }
        }

        private void ScheduleRetargetPreview()
        {
            previewRefreshTimer.Stop();
            previewRefreshTimer.Start();
        }

        private void PreviewRefreshTimer_Tick(object sender, EventArgs e)
        {
            previewRefreshTimer.Stop();
            RefreshRetargetPreview();
        }

        private void MappingGrid_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            ComboBox combo = e.Control as ComboBox;
            if (combo != null)
                combo.DropDownStyle = ComboBoxStyle.DropDownList;
        }

        private void MappingGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            statusLabel.Text = "Could not apply that mapping choice. Select a listed donor track or static pose.";
        }

        private void LoadMappings()
        {
            ClearLoadedMapping();
            try
            {
                string donorModelPath = GetSelectedPath(donorModelList);
                string donorAnimationPath = GetSelectedPath(donorAnimationList);
                string recipientModelPath = GetSelectedPath(recipientModelList);
                string recipientAnimationPath = GetSelectedPath(recipientPoseList);
                if (string.IsNullOrWhiteSpace(donorModelPath))
                    throw new InvalidDataException("Select a donor MDL from the list.");
                if (string.IsNullOrWhiteSpace(donorAnimationPath))
                    throw new InvalidDataException("Select a donor ANM from the list.");
                if (string.IsNullOrWhiteSpace(recipientModelPath))
                    throw new InvalidDataException("Select a recipient MDL from the list.");

                List<string> loadedDonorParts = AnmRetargeter.ReadModelParts(donorModelPath);
                List<string> loadedRecipientParts = AnmRetargeter.ReadModelParts(recipientModelPath);
                AnmRetargeter.ValidateAnimation(donorAnimationPath, loadedDonorParts.Count);
                if (!string.IsNullOrWhiteSpace(recipientAnimationPath))
                    AnmRetargeter.ValidateAnimation(recipientAnimationPath, loadedRecipientParts.Count);

                donorParts = loadedDonorParts;
                recipientParts = loadedRecipientParts;
                donorTrackLabels = new List<string>();
                for (int i = 0; i < donorParts.Count; i++)
                    donorTrackLabels.Add(i + ": " + donorParts[i]);
                mappingGrid.Rows.Clear();
                DataGridViewComboBoxColumn trackColumn =
                    (DataGridViewComboBoxColumn)mappingGrid.Columns["DonorTrack"];
                trackColumn.Items.Clear();
                trackColumn.Items.Add(staticTrackLabel);
                trackColumn.Items.AddRange(donorTrackLabels.ToArray());
                for (int i = 0; i < recipientParts.Count; i++)
                {
                    int donorIndex = FindMatchingDonorPart(recipientParts[i], donorParts);
                    string selectedTrack = donorIndex < 0
                        ? staticTrackLabel
                        : donorTrackLabels[donorIndex];
                    mappingGrid.Rows.Add(recipientParts[i], selectedTrack, "0", "0", "0");
                }

                statusLabel.Text = donorParts.Count + " donor tracks; " + recipientParts.Count +
                    " recipient parts. Exact or unique part-name matches are selected automatically; ambiguous parts stay static.";
                RefreshRetargetPreview();
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Could not load mapping: " + ex.Message;
            }
        }

        private static int FindMatchingDonorPart(string recipientPart, List<string> donorParts)
        {
            for (int i = 0; i < donorParts.Count; i++)
            {
                if (string.Equals(recipientPart, donorParts[i], StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            List<string> recipientTokens = GetPartTokens(recipientPart);
            int recipientSide = GetPartSide(recipientTokens);
            int bestIndex = -1;
            int bestScore = 0;
            bool tiedForBest = false;

            for (int i = 0; i < donorParts.Count; i++)
            {
                List<string> donorTokens = GetPartTokens(donorParts[i]);
                int donorSide = GetPartSide(donorTokens);
                if (recipientSide != 0 && donorSide != 0 && recipientSide != donorSide)
                    continue;

                int score = 0;
                bool sharesBodyPart = false;
                foreach (string token in recipientTokens)
                {
                    if (!donorTokens.Contains(token))
                        continue;
                    score++;
                    if (IsBodyPartToken(token))
                        sharesBodyPart = true;
                }

                if (!sharesBodyPart)
                    continue;
                if (score > bestScore)
                {
                    bestIndex = i;
                    bestScore = score;
                    tiedForBest = false;
                }
                else if (score == bestScore)
                {
                    tiedForBest = true;
                }
            }

            return tiedForBest ? -1 : bestIndex;
        }

        private static List<string> GetPartTokens(string partName)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrEmpty(partName))
                return tokens;

            System.Text.StringBuilder tokenBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < partName.Length; i++)
            {
                char character = partName[i];
                bool camelCaseBoundary = i > 0 && char.IsUpper(character) &&
                    (char.IsLower(partName[i - 1]) || char.IsDigit(partName[i - 1]));
                if (!char.IsLetterOrDigit(character) || camelCaseBoundary)
                    AddPartToken(tokens, tokenBuilder);
                if (char.IsLetterOrDigit(character))
                    tokenBuilder.Append(char.ToLowerInvariant(character));
            }

            AddPartToken(tokens, tokenBuilder);
            return tokens;
        }

        private static void AddPartToken(List<string> tokens, System.Text.StringBuilder tokenBuilder)
        {
            if (tokenBuilder.Length == 0)
                return;
            string token = CanonicalizePartToken(tokenBuilder.ToString());
            tokenBuilder.Length = 0;
            if (token.Length > 0 && !tokens.Contains(token))
                tokens.Add(token);
        }

        private static string CanonicalizePartToken(string token)
        {
            switch (token)
            {
                case "l": return "left";
                case "r": return "right";
                case "feet": return "foot";
                case "hand": return "hand";
                case "hands": return "hand";
                case "upperarm":
                case "forearm":
                case "bicep":
                case "tricep": return "arm";
                case "thigh":
                case "calf":
                case "shin": return "leg";
                case "skull": return "head";
                case "chest":
                case "abdomen":
                case "body": return "torso";
                case "mesh":
                case "part":
                case "piece": return string.Empty;
                default: return token;
            }
        }

        private static bool IsBodyPartToken(string token)
        {
            return token == "arm" || token == "leg" || token == "foot" ||
                token == "hand" || token == "head" || token == "torso" ||
                token == "shoulder" || token == "elbow" || token == "knee";
        }

        private static int GetPartSide(List<string> tokens)
        {
            if (tokens.Contains("left")) return -1;
            if (tokens.Contains("right")) return 1;
            return 0;
        }

        private void ClearLoadedMapping(bool updateStatus = true)
        {
            donorParts = null;
            recipientParts = null;
            donorTrackLabels = null;
            mappingGrid.Rows.Clear();
            UpdateHighlightedParts();
            if (updateStatus)
                statusLabel.Text = "Selections changed. Load parts to create a mapping.";
        }

        private void ExportAnimation()
        {
            if (donorParts == null || recipientParts == null || mappingGrid.Rows.Count != recipientParts.Count)
            {
                statusLabel.Text = "Load parts and mappings before exporting.";
                return;
            }

            try
            {
                float[,] offsets;
                int[] mapping = ReadCurrentMapping(out offsets);

                using (SaveFileDialog dialog = new SaveFileDialog
                {
                    Filter = "ANM animation (*.anm)|*.anm",
                    DefaultExt = "anm",
                    AddExtension = true,
                    FileName = Path.GetFileNameWithoutExtension(GetSelectedPath(donorAnimationList)) +
                        "_" + Path.GetFileNameWithoutExtension(GetSelectedPath(recipientModelList)) + "_retargeted.anm",
                    Title = "Export a new recipient-sized ANM"
                })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    AnmRetargeter.WriteAnimation(
                        GetSelectedPath(donorModelList), GetSelectedPath(donorAnimationList),
                        GetSelectedPath(recipientModelList), GetSelectedPath(recipientPoseList),
                        recipientParts.Count, mapping, offsets, dialog.FileName,
                        preserveRecipientScaleCheckBox.Checked,
                        GetSelectedPath(donorReferenceList),
                        GetSelectedPath(recipientReferenceList));
                    recipientPreview.SetPreviewFiles(GetSelectedPath(recipientModelList), dialog.FileName);
                }

                statusLabel.Text = "Created a new mapped ANM. Review it in the model viewer before using it in-game.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Could not export ANM: " + ex.Message;
            }
        }

        private int[] ReadCurrentMapping(out float[,] offsets, bool commitEdit = true)
        {
            if (donorParts == null || recipientParts == null ||
                mappingGrid.Rows.Count != recipientParts.Count)
                throw new InvalidDataException("Load parts and mappings before exporting.");

            if (commitEdit)
            {
                if (mappingGrid.IsCurrentCellDirty)
                    mappingGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                mappingGrid.EndEdit();
            }

            int[] mapping = new int[recipientParts.Count];
            offsets = new float[recipientParts.Count, 3];
            string[] offsetColumns = { "OffsetX", "OffsetY", "OffsetZ" };
            for (int i = 0; i < mapping.Length; i++)
            {
                object value = mappingGrid.Rows[i].Cells["DonorTrack"].Value;
                string selected = value == null ? null : value.ToString();
                for (int axis = 0; axis < 3; axis++)
                    offsets[i, axis] = ParseOrientationOffset(
                        mappingGrid.Rows[i].Cells[offsetColumns[axis]].Value,
                        recipientParts[i], offsetColumns[axis]);
                if (string.Equals(selected, staticTrackLabel, StringComparison.Ordinal))
                {
                    mapping[i] = -1;
                    continue;
                }

                int donorIndex = donorTrackLabels == null ? -1 : donorTrackLabels.IndexOf(selected);
                if (donorIndex < 0)
                    throw new InvalidDataException("Choose a donor track or static pose for recipient part " + recipientParts[i] + ".");
                mapping[i] = donorIndex;
            }
            return mapping;
        }

        private void TransplantSelectedAnimation()
        {
            try
            {
                if (donorParts == null || recipientParts == null)
                    throw new InvalidDataException("Load parts and mappings before transplanting.");

                float[,] offsets;
                int[] mapping = ReadCurrentMapping(out offsets);
                string donorModelPath = GetSelectedPath(donorModelList);
                string donorAnimationPath = GetSelectedPath(donorAnimationList);
                string recipientModelPath = GetSelectedPath(recipientModelList);
                if (string.IsNullOrWhiteSpace(donorModelPath) ||
                    string.IsNullOrWhiteSpace(donorAnimationPath) ||
                    string.IsNullOrWhiteSpace(recipientModelPath))
                    throw new InvalidDataException("Select donor and recipient assets before transplanting.");

                AnmRetargeter.ValidateAnimation(donorAnimationPath, donorParts.Count);
                string defaultDirectory = SharedState.GetAssetDataPath("hdnw", "anm");
                using (FolderBrowserDialog dialog = new FolderBrowserDialog
                {
                    Description = "Choose a folder for the new recipient ANM file.",
                    SelectedPath = Directory.Exists(defaultDirectory) ? defaultDirectory : string.Empty,
                    ShowNewFolderButton = true
                })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    if (string.IsNullOrWhiteSpace(dialog.SelectedPath) || !Directory.Exists(dialog.SelectedPath))
                        throw new DirectoryNotFoundException("Choose an existing output folder.");

                    string recipientPrefix = Path.GetFileNameWithoutExtension(recipientModelPath);
                    if (!IsNumericModelPrefix(recipientPrefix))
                        throw new InvalidDataException("The selected recipient model must have a numeric filename prefix to create sequential ANM names.");

                    int nextSequence = AnmRetargeter.FindNextAnimationSequence(dialog.SelectedPath, recipientPrefix);
                    string outputPath = AnmRetargeter.GetNextAnimationPath(
                        dialog.SelectedPath, recipientPrefix, ref nextSequence);
                    AnmRetargeter.WriteAnimation(
                        donorModelPath, donorAnimationPath, recipientModelPath,
                        GetSelectedPath(recipientPoseList), recipientParts.Count,
                        mapping, offsets, outputPath,
                        preserveRecipientScaleCheckBox.Checked,
                        GetSelectedPath(donorReferenceList),
                        GetSelectedPath(recipientReferenceList));
                    recipientPreview.SetPreviewFiles(recipientModelPath, outputPath);
                    statusLabel.Text = "Transplanted selected animation to " + Path.GetFileName(outputPath) + ".";
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Could not transplant selected animation: " + ex.Message;
            }
        }

        private void TransplantAllAnimations()
        {
            int createdCount = 0;
            try
            {
                float[,] offsets;
                int[] mapping = ReadCurrentMapping(out offsets);
                string donorModelPath = GetSelectedPath(donorModelList);
                string recipientModelPath = GetSelectedPath(recipientModelList);
                if (string.IsNullOrWhiteSpace(donorModelPath) || string.IsNullOrWhiteSpace(recipientModelPath))
                    throw new InvalidDataException("Select both donor and recipient models before transplanting animations.");

                List<AssetListItem> animations = GetAnimationsForModel(donorModelPath);
                if (animations.Count == 0)
                    throw new InvalidDataException("No ANM files match the selected donor model.");
                foreach (AssetListItem animation in animations)
                    AnmRetargeter.ValidateAnimation(animation.Path, donorParts.Count);

                string defaultDirectory = SharedState.GetAssetDataPath("hdnw", "anm");
                using (FolderBrowserDialog dialog = new FolderBrowserDialog
                {
                    Description = "Choose a folder for the new recipient ANM files.",
                    SelectedPath = Directory.Exists(defaultDirectory) ? defaultDirectory : string.Empty,
                    ShowNewFolderButton = true
                })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    if (string.IsNullOrWhiteSpace(dialog.SelectedPath) || !Directory.Exists(dialog.SelectedPath))
                        throw new DirectoryNotFoundException("Choose an existing output folder.");

                    string recipientPrefix = Path.GetFileNameWithoutExtension(recipientModelPath);
                    if (!IsNumericModelPrefix(recipientPrefix))
                        throw new InvalidDataException("The selected recipient model must have a numeric filename prefix to create sequential ANM names.");

                    int nextSequence = AnmRetargeter.FindNextAnimationSequence(dialog.SelectedPath, recipientPrefix);
                    List<string> outputPaths = new List<string>();
                    foreach (AssetListItem animation in animations)
                    {
                        string outputPath = AnmRetargeter.GetNextAnimationPath(
                            dialog.SelectedPath, recipientPrefix, ref nextSequence);
                        outputPaths.Add(outputPath);
                    }

                    string recipientPosePath = GetSelectedPath(recipientPoseList);
                    for (int i = 0; i < animations.Count; i++)
                    {
                        AnmRetargeter.WriteAnimation(
                            donorModelPath, animations[i].Path, recipientModelPath, recipientPosePath,
                            recipientParts.Count, mapping, offsets, outputPaths[i],
                            preserveRecipientScaleCheckBox.Checked,
                            GetSelectedPath(donorReferenceList),
                            GetSelectedPath(recipientReferenceList));
                        createdCount++;
                    }

                    recipientPreview.SetPreviewFiles(recipientModelPath, outputPaths[0]);
                    statusLabel.Text = "Transplanted " + animations.Count +
                        " animations to " + Path.GetFileName(dialog.SelectedPath) +
                        "; created " + Path.GetFileName(outputPaths[0]) + " through " +
                        Path.GetFileName(outputPaths[outputPaths.Count - 1]) + ".";
                }
            }

            catch (Exception ex)
            {
                statusLabel.Text = createdCount == 0
                    ? "Could not transplant all animations: " + ex.Message
                    : "Transplanted " + createdCount + " animation(s), then stopped: " + ex.Message;
            }
        }

        private void EstimateOrientationOffsets()
        {
            try
            {
                if (donorParts == null || recipientParts == null)
                    throw new InvalidDataException("Load parts before estimating orientation offsets.");

                string donorReferencePath = preserveRecipientScaleCheckBox.Checked
                    ? GetSelectedPath(donorReferenceList)
                    : GetSelectedPath(donorAnimationList);
                string recipientReferencePath = preserveRecipientScaleCheckBox.Checked
                    ? GetSelectedPath(recipientReferenceList)
                    : GetSelectedPath(recipientPoseList);
                if (string.IsNullOrWhiteSpace(donorReferencePath) ||
                    string.IsNullOrWhiteSpace(recipientReferencePath))
                    throw new InvalidDataException(
                        "Select the original donor and recipient reference ANMs first.");

                float[,] currentOffsets;
                int[] mapping = ReadCurrentMapping(out currentOffsets);
                if (currentOffsets.GetLength(0) != mapping.Length)
                    throw new InvalidDataException("The orientation offsets do not match the loaded mapping.");
                float[,] estimatedOffsets = AnmRetargeter.EstimateOrientationOffsets(
                    donorReferencePath, recipientReferencePath,
                    donorParts.Count, recipientParts.Count, mapping);

                updatingOrientationOffsets = true;
                try
                {
                    for (int row = 0; row < recipientParts.Count; row++)
                    {
                        mappingGrid.Rows[row].Cells["OffsetX"].Value =
                            estimatedOffsets[row, 0].ToString("0.##", CultureInfo.InvariantCulture);
                        mappingGrid.Rows[row].Cells["OffsetY"].Value =
                            estimatedOffsets[row, 1].ToString("0.##", CultureInfo.InvariantCulture);
                        mappingGrid.Rows[row].Cells["OffsetZ"].Value =
                            estimatedOffsets[row, 2].ToString("0.##", CultureInfo.InvariantCulture);
                    }
                }
                finally
                {
                    updatingOrientationOffsets = false;
                }

                RefreshRetargetPreview();
                statusLabel.Text = "Estimated orientation offsets from the selected reference clips. Review the preview before transplanting.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Could not estimate orientation offsets: " + ex.Message;
            }
        }

        private static bool IsNumericModelPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return false;
            for (int i = 0; i < prefix.Length; i++)
            {
                if (!char.IsDigit(prefix[i]))
                    return false;
            }
            return true;
        }

        private static float ParseOrientationOffset(object value, string partName, string axis)
        {
            string text = value == null ? "0" : value.ToString();
            float degrees;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees) ||
                float.IsNaN(degrees) || float.IsInfinity(degrees) ||
                degrees < -180.0f || degrees > 180.0f)
                throw new InvalidDataException(
                    "Enter an " + axis + " orientation offset between -180 and 180 degrees for " + partName + ".");
            return degrees;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SharedState.OnBaseDataPathChanged -= RefreshAssetLists;
                SharedState.OnExtractionPathChanged -= RefreshAssetLists;
                SharedState.OnGameDataExtracted -= RefreshAssetLists;
                previewRefreshTimer.Stop();
                previewRefreshTimer.Dispose();
                donorPreview.Dispose();
                recipientPreview.Dispose();
            }
            base.Dispose(disposing);
        }

    }

    internal static class AnmRetargeter
    {
        private const int AnimationHeaderLength = 0x2C;
        private const int BoneCountOffset = 0x18;
        private const int TrackRecordLength = 56;
        private const int MatrixDataLength = 48;

        public static int FindNextAnimationSequence(string directory, string modelPrefix)
        {
            int highestSequence = -1;
            foreach (string path in Directory.GetFiles(directory))
            {
                if (!string.Equals(Path.GetExtension(path), ".anm", StringComparison.OrdinalIgnoreCase))
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                if (!fileName.StartsWith(modelPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string suffix = fileName.Substring(modelPrefix.Length);
                int sequence;
                if (suffix.Length > 0 && int.TryParse(suffix, out sequence) && sequence > highestSequence)
                    highestSequence = sequence;
            }
            return highestSequence + 1;
        }

        public static string GetNextAnimationPath(string directory, string modelPrefix, ref int sequence)
        {
            while (sequence >= 0)
            {
                string path = Path.Combine(directory, modelPrefix + sequence.ToString("D2") + ".ANM");
                sequence++;
                if (!AnimationPathExistsIgnoreCase(path))
                    return path;
            }
            throw new InvalidOperationException("No sequential animation filename is available.");
        }

        private static bool AnimationPathExistsIgnoreCase(string path)
        {
            string directory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);
            foreach (string existingPath in Directory.GetFiles(directory))
            {
                if (string.Equals(Path.GetFileName(existingPath), fileName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static List<string> ReadModelParts(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Select an existing MDL model.", filePath);

            List<string> names = new List<string>();
            using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                if (reader.BaseStream.Length < Constants.MDL_HEADER_3DDATA_ADDRESS)
                    throw new InvalidDataException("The MDL is too short to contain its submesh table.");

                reader.BaseStream.Seek(Constants.MDL_HEADER_NUMBER_OF_SUBMESHES_ADDRESS, SeekOrigin.Begin);
                int count = reader.ReadInt32();
                if (count <= 0 || count > 4096 ||
                    Constants.MDL_HEADER_3DDATA_ADDRESS + (long)count * 4 > reader.BaseStream.Length)
                    throw new InvalidDataException("The MDL has an invalid submesh count or table.");

                reader.BaseStream.Seek(Constants.MDL_HEADER_3DDATA_ADDRESS, SeekOrigin.Begin);
                int[] addresses = new int[count];
                for (int i = 0; i < count; i++)
                    addresses[i] = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    if (addresses[i] < 0 || (long)addresses[i] + Constants.SUBMESH_NAME_LENGTH > reader.BaseStream.Length)
                        throw new InvalidDataException("The MDL contains an invalid submesh address at index " + i + ".");
                    reader.BaseStream.Seek(addresses[i], SeekOrigin.Begin);
                    names.Add(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(Constants.SUBMESH_NAME_LENGTH)).TrimEnd('\0'));
                }
            }
            return names;
        }

        public static void ValidateAnimation(string filePath, int expectedTrackCount)
        {
            AnimationDimensions dimensions = ReadDimensions(filePath);
            if (dimensions.TrackCount != expectedTrackCount)
                throw new InvalidDataException("The ANM has " + dimensions.TrackCount + " tracks, but the selected model has " + expectedTrackCount + " parts.");
        }

        public static Matrix4[][] BuildPreviewFrames(
            string donorAnimationPath, string recipientAnimationPath,
            int donorTrackCount, int recipientTrackCount, int[] mapping, float[,] offsets,
            bool preserveRecipientScale = false,
            string donorReferencePath = null, string recipientReferencePath = null)
        {
            if (mapping == null || mapping.Length != recipientTrackCount)
                throw new ArgumentException("The mapping must contain one entry for each recipient part.", "mapping");
            ValidateOrientationOffsets(offsets, recipientTrackCount);

            Matrix4[][] donorFrames = AnmAnimationLoader.LoadFrames(donorAnimationPath, donorTrackCount);
            Matrix4[][] recipientFrames = string.IsNullOrWhiteSpace(recipientAnimationPath)
                ? new[] { CreateIdentityMatrices(recipientTrackCount) }
                : AnmAnimationLoader.LoadFrames(recipientAnimationPath, recipientTrackCount);
            TrackReference[] donorReferences = null;
            TrackReference[] recipientReferences = null;
            if (preserveRecipientScale)
            {
                if (string.IsNullOrWhiteSpace(donorReferencePath) ||
                    string.IsNullOrWhiteSpace(recipientReferencePath))
                    throw new InvalidDataException(
                        "Select donor and recipient reference ANMs to preserve recipient scale.");
                Matrix4[][] donorReferenceFrames =
                    AnmAnimationLoader.LoadFrames(donorReferencePath, donorTrackCount);
                Matrix4[][] recipientReferenceFrames =
                    AnmAnimationLoader.LoadFrames(recipientReferencePath, recipientTrackCount);
                donorReferences = BuildTrackReferences(donorReferenceFrames);
                recipientReferences = BuildTrackReferences(recipientReferenceFrames);
            }
            Matrix4[][] previewFrames = new Matrix4[donorFrames.Length][];
            for (int frame = 0; frame < donorFrames.Length; frame++)
            {
                previewFrames[frame] = new Matrix4[recipientTrackCount];
                for (int target = 0; target < recipientTrackCount; target++)
                {
                    int source = mapping[target];
                    if (source < -1 || source >= donorTrackCount)
                        throw new ArgumentOutOfRangeException("mapping", "A donor track index is out of range.");
                    Matrix4 matrix = source >= 0
                        ? donorFrames[frame][source]
                        : recipientFrames[0][target];
                    if (source >= 0 && preserveRecipientScale)
                        matrix = ApplyRecipientScaleRetarget(
                            matrix, donorReferences[source], recipientReferences[target]);
                    previewFrames[frame][target] = ApplyOrientationOffset(matrix, offsets, target);
                }
            }
            return previewFrames;
        }

        private static Matrix4[] CreateIdentityMatrices(int count)
        {
            Matrix4[] matrices = new Matrix4[count];
            for (int i = 0; i < count; i++)
                matrices[i] = Matrix4.Identity;
            return matrices;
        }

        private static TrackReference[] BuildTrackReferences(Matrix4[][] frames)
        {
            if (frames == null || frames.Length == 0 || frames[0] == null)
                throw new InvalidDataException("A reference ANM contains no readable tracks.");

            int trackCount = frames[0].Length;
            TrackReference[] references = new TrackReference[trackCount];
            for (int track = 0; track < trackCount; track++)
            {
                double tx = 0;
                double ty = 0;
                double tz = 0;
                double[] axisScaleSums = new double[3];
                for (int frame = 0; frame < frames.Length; frame++)
                {
                    if (frames[frame] == null || frames[frame].Length != trackCount)
                        throw new InvalidDataException("A reference ANM has inconsistent track data.");

                    Matrix4 matrix = frames[frame][track];
                    tx += matrix.M41;
                    ty += matrix.M42;
                    tz += matrix.M43;
                    float[,] basis = ExtractRawBasis(matrix);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        double squaredLength = 0;
                        for (int row = 0; row < 3; row++)
                            squaredLength += basis[row, axis] * basis[row, axis];
                        double scale = Math.Sqrt(squaredLength);
                        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale < 0.0001)
                            throw new InvalidDataException(
                                "A reference ANM contains a zero or invalid matrix scale at track " + track + ".");
                        axisScaleSums[axis] += scale;
                    }
                }

                double frameCount = frames.Length;
                references[track] = new TrackReference(
                    (float)(tx / frameCount),
                    (float)(ty / frameCount),
                    (float)(tz / frameCount),
                    new[] {
                        (float)(axisScaleSums[0] / frameCount),
                        (float)(axisScaleSums[1] / frameCount),
                        (float)(axisScaleSums[2] / frameCount)
                    });
            }
            return references;
        }

        private static Matrix4 ApplyRecipientScaleRetarget(
            Matrix4 donorFrame, TrackReference donorReference, TrackReference recipientReference)
        {
            float[,] basis = ExtractRawBasis(donorFrame);
            for (int axis = 0; axis < 3; axis++)
            {
                double squaredLength = 0;
                for (int row = 0; row < 3; row++)
                    squaredLength += basis[row, axis] * basis[row, axis];
                double donorFrameScale = Math.Sqrt(squaredLength);
                if (double.IsNaN(donorFrameScale) || double.IsInfinity(donorFrameScale) ||
                    donorFrameScale < 0.0001)
                    throw new InvalidDataException("A target animation contains a zero or invalid matrix scale.");

                float scale = recipientReference.AxisScales[axis] / (float)donorFrameScale;
                for (int row = 0; row < 3; row++)
                    basis[row, axis] *= scale;
            }

            float donorReferenceScale =
                (donorReference.AxisScales[0] + donorReference.AxisScales[1] + donorReference.AxisScales[2]) / 3.0f;
            float recipientReferenceScale =
                (recipientReference.AxisScales[0] + recipientReference.AxisScales[1] + recipientReference.AxisScales[2]) / 3.0f;
            if (donorReferenceScale < 0.0001f)
                throw new InvalidDataException("A donor reference track has an invalid average scale.");
            float translationScale = recipientReferenceScale / donorReferenceScale;

            float tx = recipientReference.X +
                (donorFrame.M41 - donorReference.X) * translationScale;
            float ty = recipientReference.Y +
                (donorFrame.M42 - donorReference.Y) * translationScale;
            float tz = recipientReference.Z +
                (donorFrame.M43 - donorReference.Z) * translationScale;

            return new Matrix4(
                basis[0, 0], basis[1, 0], basis[2, 0], 0,
                basis[0, 1], basis[1, 1], basis[2, 1], 0,
                basis[0, 2], basis[1, 2], basis[2, 2], 0,
                tx, ty, tz, 1);
        }

        private static float[,] ExtractRawBasis(Matrix4 matrix)
        {
            return new float[,]
            {
                { matrix.M11, matrix.M21, matrix.M31 },
                { matrix.M12, matrix.M22, matrix.M32 },
                { matrix.M13, matrix.M23, matrix.M33 }
            };
        }

        private static Matrix4 ReadTrackMatrix(byte[] bytes, int trackOffset)
        {
            float tx = BitConverter.ToSingle(bytes, trackOffset);
            float ty = BitConverter.ToSingle(bytes, trackOffset + 4);
            float tz = BitConverter.ToSingle(bytes, trackOffset + 8);
            float m11 = BitConverter.ToSingle(bytes, trackOffset + 12);
            float m12 = BitConverter.ToSingle(bytes, trackOffset + 16);
            float m13 = BitConverter.ToSingle(bytes, trackOffset + 20);
            float m21 = BitConverter.ToSingle(bytes, trackOffset + 24);
            float m22 = BitConverter.ToSingle(bytes, trackOffset + 28);
            float m23 = BitConverter.ToSingle(bytes, trackOffset + 32);
            float m31 = BitConverter.ToSingle(bytes, trackOffset + 36);
            float m32 = BitConverter.ToSingle(bytes, trackOffset + 40);
            float m33 = BitConverter.ToSingle(bytes, trackOffset + 44);
            return new Matrix4(
                m11, m21, m31, 0,
                m12, m22, m32, 0,
                m13, m23, m33, 0,
                tx, ty, tz, 1);
        }

        private static void WriteTrackMatrix(byte[] bytes, int trackOffset, Matrix4 matrix)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(matrix.M41), 0, bytes, trackOffset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(matrix.M42), 0, bytes, trackOffset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(matrix.M43), 0, bytes, trackOffset + 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(matrix.M11), 0, bytes, trackOffset + 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(matrix.M21), 0, bytes, trackOffset + 16, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(matrix.M31), 0, bytes, trackOffset + 20, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(matrix.M12), 0, bytes, trackOffset + 24, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(matrix.M22), 0, bytes, trackOffset + 28, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(matrix.M32), 0, bytes, trackOffset + 32, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(matrix.M13), 0, bytes, trackOffset + 36, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(matrix.M23), 0, bytes, trackOffset + 40, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(matrix.M33), 0, bytes, trackOffset + 44, 4);
        }

        private sealed class TrackReference
        {
            public float X { get; private set; }
            public float Y { get; private set; }
            public float Z { get; private set; }
            public float[] AxisScales { get; private set; }

            public TrackReference(float x, float y, float z, float[] axisScales)
            {
                X = x;
                Y = y;
                Z = z;
                AxisScales = axisScales;
            }
        }

        public static void WriteAnimation(
            string donorModelPath, string donorAnimationPath, string recipientModelPath,
            string recipientAnimationPath, int recipientTrackCount, int[] mapping,
            float[,] offsets, string outputPath,
            bool preserveRecipientScale = false,
            string donorReferencePath = null, string recipientReferencePath = null)
        {
            AnimationDimensions donor = ReadDimensions(donorAnimationPath);
            if (recipientTrackCount <= 0 || mapping == null || mapping.Length != recipientTrackCount)
                throw new ArgumentException("The mapping must contain one entry for each recipient part.", "mapping");
            ValidateOrientationOffsets(offsets, recipientTrackCount);

            AnimationDimensions recipient = null;
            if (!string.IsNullOrWhiteSpace(recipientAnimationPath))
            {
                recipient = ReadDimensions(recipientAnimationPath);
                if (recipient.TrackCount != recipientTrackCount)
                    throw new InvalidDataException("The recipient pose ANM track count does not match the recipient model.");
            }
            TrackReference[] donorReferences = null;
            TrackReference[] recipientReferences = null;
            if (preserveRecipientScale)
            {
                if (string.IsNullOrWhiteSpace(donorReferencePath) ||
                    string.IsNullOrWhiteSpace(recipientReferencePath))
                    throw new InvalidDataException(
                        "Select donor and recipient reference ANMs to preserve recipient scale.");
                Matrix4[][] donorReferenceFrames =
                    AnmAnimationLoader.LoadFrames(donorReferencePath, donor.TrackCount);
                Matrix4[][] recipientReferenceFrames =
                    AnmAnimationLoader.LoadFrames(recipientReferencePath, recipientTrackCount);
                donorReferences = BuildTrackReferences(donorReferenceFrames);
                recipientReferences = BuildTrackReferences(recipientReferenceFrames);
            }
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("An output file path is required.", "outputPath");
            string fullOutputPath = Path.GetFullPath(outputPath);
            string[] inputPaths = {
                donorModelPath, donorAnimationPath, recipientModelPath, recipientAnimationPath,
                donorReferencePath, recipientReferencePath
            };
            foreach (string inputPath in inputPaths)
            {
                if (!string.IsNullOrWhiteSpace(inputPath) &&
                    string.Equals(fullOutputPath, Path.GetFullPath(inputPath), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Choose a new output file; source files are never overwritten.");
            }

            byte[] donorBytes = File.ReadAllBytes(donorAnimationPath);
            byte[] recipientBytes = recipient == null ? null : File.ReadAllBytes(recipientAnimationPath);
            for (int target = 0; target < recipientTrackCount; target++)
            {
                if (mapping[target] < -1 || mapping[target] >= donor.TrackCount)
                    throw new ArgumentOutOfRangeException("mapping", "A donor track index is out of range.");
            }
            long outputLength = AnimationHeaderLength + (long)donor.FrameCount * recipientTrackCount * TrackRecordLength;
            if (outputLength > int.MaxValue)
                throw new InvalidDataException("The generated ANM would exceed the supported file size.");

            byte[] output = new byte[(int)outputLength];
            Buffer.BlockCopy(donorBytes, 0, output, 0, AnimationHeaderLength);
            Buffer.BlockCopy(BitConverter.GetBytes(recipientTrackCount), 0, output, BoneCountOffset, 4);

            byte[] identityTrack = CreateIdentityTrack();
            for (int frame = 0; frame < donor.FrameCount; frame++)
            {
                for (int target = 0; target < recipientTrackCount; target++)
                {
                    int source = mapping[target];
                    if (source < -1 || source >= donor.TrackCount)
                        throw new ArgumentOutOfRangeException("mapping", "A donor track index is out of range.");

                    int targetOffset = AnimationHeaderLength + (frame * recipientTrackCount + target) * TrackRecordLength;
                    if (source >= 0)
                    {
                        int sourceOffset = AnimationHeaderLength + (frame * donor.TrackCount + source) * TrackRecordLength;
                        CopyTrack(donorBytes, sourceOffset, output, targetOffset);
                    }
                    else if (recipientBytes != null)
                    {
                        int sourceOffset = AnimationHeaderLength + target * TrackRecordLength;
                        CopyTrack(recipientBytes, sourceOffset, output, targetOffset);
                    }
                    else
                    {
                        Buffer.BlockCopy(identityTrack, 0, output, targetOffset, TrackRecordLength);
                    }

                    if (source >= 0 && preserveRecipientScale)
                    {
                        Matrix4 matrix = ReadTrackMatrix(output, targetOffset);
                        matrix = ApplyRecipientScaleRetarget(
                            matrix, donorReferences[source], recipientReferences[target]);
                        matrix = ApplyOrientationOffset(matrix, offsets, target);
                        WriteTrackMatrix(output, targetOffset, matrix);
                    }
                    else
                    {
                        ApplyOrientationOffset(output, targetOffset, offsets, target);
                    }
                }
            }

            using (FileStream stream = new FileStream(fullOutputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(output, 0, output.Length);
        }

        private static void CopyTrack(byte[] source, int sourceOffset, byte[] destination, int destinationOffset)
        {
            Buffer.BlockCopy(source, sourceOffset, destination, destinationOffset, MatrixDataLength);
            if (sourceOffset + TrackRecordLength <= source.Length)
                Buffer.BlockCopy(source, sourceOffset + MatrixDataLength, destination, destinationOffset + MatrixDataLength, TrackRecordLength - MatrixDataLength);
        }

        public static float[,] EstimateOrientationOffsets(
            string donorReferencePath, string recipientReferencePath,
            int donorTrackCount, int recipientTrackCount, int[] mapping)
        {
            if (mapping == null || mapping.Length != recipientTrackCount)
                throw new ArgumentException("The mapping must contain one entry for each recipient part.", "mapping");

            Matrix4[][] donorFrames = AnmAnimationLoader.LoadFrames(donorReferencePath, donorTrackCount);
            Matrix4[][] recipientFrames = AnmAnimationLoader.LoadFrames(recipientReferencePath, recipientTrackCount);
            if (donorFrames.Length != recipientFrames.Length)
                throw new InvalidDataException(
                    "The reference ANMs must have the same frame count; the selected clips have " +
                    donorFrames.Length + " and " + recipientFrames.Length + " frames.");

            float[,] offsets = new float[recipientTrackCount, 3];
            for (int target = 0; target < recipientTrackCount; target++)
            {
                int source = mapping[target];
                if (source < -1 || source >= donorTrackCount)
                    throw new ArgumentOutOfRangeException("mapping", "A donor track index is out of range.");
                if (source < 0)
                    continue;

                double[] sinSums = new double[3];
                double[] cosSums = new double[3];
                for (int frame = 0; frame < donorFrames.Length; frame++)
                {
                    float[,] donorBasis = ExtractBasis(donorFrames[frame][source]);
                    float[,] recipientBasis = ExtractBasis(recipientFrames[frame][target]);
                    float[,] delta = Multiply(Transpose(donorBasis), recipientBasis);
                    float[] angles = ExtractEulerAngles(delta);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        sinSums[axis] += Math.Sin(angles[axis]);
                        cosSums[axis] += Math.Cos(angles[axis]);
                    }
                }

                for (int axis = 0; axis < 3; axis++)
                    offsets[target, axis] = (float)(Math.Atan2(sinSums[axis], cosSums[axis]) * 180.0 / Math.PI);
            }
            return offsets;
        }

        private static void ValidateOrientationOffsets(float[,] offsets, int recipientTrackCount)
        {
            if (offsets == null || offsets.GetLength(0) != recipientTrackCount || offsets.GetLength(1) != 3)
                throw new ArgumentException(
                    "Orientation offsets must contain X, Y, and Z values for every recipient part.", "offsets");
            for (int track = 0; track < recipientTrackCount; track++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    float degrees = offsets[track, axis];
                    if (float.IsNaN(degrees) || float.IsInfinity(degrees) ||
                        degrees < -180.0f || degrees > 180.0f)
                        throw new ArgumentOutOfRangeException(
                            "offsets", "Orientation offsets must be between -180 and 180 degrees.");
                }
            }
        }

        private static void ApplyOrientationOffset(byte[] track, int offset, float[,] offsets, int target)
        {
            if (offsets[target, 0] == 0.0f && offsets[target, 1] == 0.0f && offsets[target, 2] == 0.0f)
                return;

            float[,] basis = new float[3, 3];
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                    basis[row, column] = BitConverter.ToSingle(track, offset + 12 + (row * 3 + column) * 4);
            }

            ApplyOrientationOffset(basis, offsets, target);
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                    Buffer.BlockCopy(BitConverter.GetBytes(basis[row, column]), 0,
                        track, offset + 12 + (row * 3 + column) * 4, 4);
            }
        }

        private static Matrix4 ApplyOrientationOffset(Matrix4 matrix, float[,] offsets, int target)
        {
            if (offsets[target, 0] == 0.0f && offsets[target, 1] == 0.0f && offsets[target, 2] == 0.0f)
                return matrix;

            float[,] basis = {
                { matrix.M11, matrix.M21, matrix.M31 },
                { matrix.M12, matrix.M22, matrix.M32 },
                { matrix.M13, matrix.M23, matrix.M33 }
            };
            ApplyOrientationOffset(basis, offsets, target);
            return new Matrix4(
                basis[0, 0], basis[1, 0], basis[2, 0], 0,
                basis[0, 1], basis[1, 1], basis[2, 1], 0,
                basis[0, 2], basis[1, 2], basis[2, 2], 0,
                matrix.M41, matrix.M42, matrix.M43, 1);
        }

        private static void ApplyOrientationOffset(float[,] basis, float[,] offsets, int target)
        {
            float[,] corrected = Multiply(basis, CreateEulerRotation(
                offsets[target, 0], offsets[target, 1], offsets[target, 2]));
            CopyMatrix(corrected, basis);
        }

        private static float[,] CreateEulerRotation(float xDegrees, float yDegrees, float zDegrees)
        {
            double x = xDegrees * Math.PI / 180.0;
            double y = yDegrees * Math.PI / 180.0;
            double z = zDegrees * Math.PI / 180.0;
            float[,] rx = {
                { 1, 0, 0 },
                { 0, (float)Math.Cos(x), (float)-Math.Sin(x) },
                { 0, (float)Math.Sin(x), (float)Math.Cos(x) }
            };
            float[,] ry = {
                { (float)Math.Cos(y), 0, (float)Math.Sin(y) },
                { 0, 1, 0 },
                { (float)-Math.Sin(y), 0, (float)Math.Cos(y) }
            };
            float[,] rz = {
                { (float)Math.Cos(z), (float)-Math.Sin(z), 0 },
                { (float)Math.Sin(z), (float)Math.Cos(z), 0 },
                { 0, 0, 1 }
            };
            return Multiply(Multiply(rx, ry), rz);
        }

        private static float[,] ExtractBasis(Matrix4 matrix)
        {
            float[,] basis = {
                { matrix.M11, matrix.M21, matrix.M31 },
                { matrix.M12, matrix.M22, matrix.M32 },
                { matrix.M13, matrix.M23, matrix.M33 }
            };

            float[] x = Normalize(new[] { basis[0, 0], basis[0, 1], basis[0, 2] });
            float[] y = new[] { basis[1, 0], basis[1, 1], basis[1, 2] };
            float projection = Dot(y, x);
            for (int axis = 0; axis < 3; axis++)
                y[axis] -= projection * x[axis];
            y = Normalize(y);

            float[] z = {
                x[1] * y[2] - x[2] * y[1],
                x[2] * y[0] - x[0] * y[2],
                x[0] * y[1] - x[1] * y[0]
            };
            float[] originalZ = { basis[2, 0], basis[2, 1], basis[2, 2] };
            if (Dot(z, originalZ) <= 0)
                throw new InvalidDataException("A reference animation contains a reflected or degenerate orientation basis.");

            for (int axis = 0; axis < 3; axis++)
            {
                basis[0, axis] = x[axis];
                basis[1, axis] = y[axis];
                basis[2, axis] = z[axis];
            }
            return basis;
        }

        private static float[] Normalize(float[] vector)
        {
            double length = Math.Sqrt(Dot(vector, vector));
            if (length < 1e-8)
                throw new InvalidDataException("A reference animation contains a degenerate orientation basis.");
            for (int axis = 0; axis < vector.Length; axis++)
                vector[axis] /= (float)length;
            return vector;
        }

        private static float Dot(float[] left, float[] right)
        {
            return left[0] * right[0] + left[1] * right[1] + left[2] * right[2];
        }

        private static float[] ExtractEulerAngles(float[,] matrix)
        {
            float y = (float)Math.Asin(Math.Max(-1.0, Math.Min(1.0, matrix[0, 2])));
            double cosY = Math.Cos(y);
            float x;
            float z;
            if (Math.Abs(cosY) > 1e-6)
            {
                x = (float)Math.Atan2(-matrix[1, 2], matrix[2, 2]);
                z = (float)Math.Atan2(-matrix[0, 1], matrix[0, 0]);
            }
            else
            {
                x = (float)Math.Atan2(matrix[2, 1], matrix[1, 1]);
                z = 0;
            }
            return new[] { x, y, z };
        }

        private static float[,] Transpose(float[,] matrix)
        {
            float[,] result = new float[3, 3];
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    result[row, column] = matrix[column, row];
            return result;
        }

        private static float[,] Multiply(float[,] left, float[,] right)
        {
            float[,] result = new float[3, 3];
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    for (int inner = 0; inner < 3; inner++)
                        result[row, column] += left[row, inner] * right[inner, column];
                }
            }
            return result;
        }

        private static void CopyMatrix(float[,] source, float[,] destination)
        {
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    destination[row, column] = source[row, column];
        }

        private static AnimationDimensions ReadDimensions(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Select an existing ANM animation.", filePath);

            using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                if (reader.BaseStream.Length < AnimationHeaderLength)
                    throw new InvalidDataException("The ANM is too short to contain its animation header.");
                reader.BaseStream.Seek(BoneCountOffset, SeekOrigin.Begin);
                int trackCount = reader.ReadInt32();
                int frameCount = reader.ReadInt32();
                if (trackCount <= 0 || frameCount <= 0)
                    throw new InvalidDataException("The ANM has an invalid track or frame count.");
                long recordCount = (long)frameCount * trackCount;
                long maximumRecordCount =
                    (long.MaxValue - AnimationHeaderLength - MatrixDataLength) / TrackRecordLength + 1;
                if (recordCount > maximumRecordCount)
                    throw new InvalidDataException("The ANM track and frame counts are too large.");
                long requiredLength = AnimationHeaderLength +
                    (recordCount - 1) * TrackRecordLength + MatrixDataLength;
                if (requiredLength > reader.BaseStream.Length)
                    throw new InvalidDataException("The ANM frame data is incomplete.");
                return new AnimationDimensions(trackCount, frameCount);
            }
        }

        private static byte[] CreateIdentityTrack()
        {
            byte[] record = new byte[TrackRecordLength];
            Buffer.BlockCopy(BitConverter.GetBytes(1.0f), 0, record, 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(1.0f), 0, record, 28, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(1.0f), 0, record, 44, 4);
            return record;
        }

        private sealed class AnimationDimensions
        {
            public int TrackCount { get; private set; }
            public int FrameCount { get; private set; }

            public AnimationDimensions(int trackCount, int frameCount)
            {
                TrackCount = trackCount;
                FrameCount = frameCount;
            }
        }
    }
}
