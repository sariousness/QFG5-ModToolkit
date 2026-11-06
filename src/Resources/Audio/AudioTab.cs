using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QFG5Extractor.Resources.Audio
{
    public class AudioTab : UserControl
    {
        private sealed class AudioScanItem
        {
            public AudioResourceInfo Info;
            public string Root;
        }

        private sealed class AudioScanResult
        {
            public readonly List<AudioScanItem> Items = new List<AudioScanItem>();
            public readonly List<string> Errors = new List<string>();
            public int ErrorCount;
        }

        private ListView audioList;
        private CheckBox favoritesOnlyCheckBox;
        private TextBox detailsText;
        private Label statusLabel;
        private QFG5Button playButton;
        private QFG5Button stopButton;
        private QFG5Button favoriteButton;
        private SoundPlayer player;
        private Process audioProcess;
        private bool disposed;
        private bool hasLoaded;
        private CancellationTokenSource scanCancellation;
        private readonly List<AudioScanItem> audioItems = new List<AudioScanItem>();
        private readonly HashSet<string> favoriteFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string favoritesFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QFG5Extractor", "audio-favorites.txt");

        public AudioTab()
        {
            Dock = DockStyle.Fill;
            BackColor = System.Drawing.Color.Transparent;
            InitializeComponent();
            LoadFavoriteFiles();
            SharedState.OnBaseDataPathChanged += OnConfiguredPathChanged;
            SharedState.OnExtractionPathChanged += OnConfiguredPathChanged;
            SharedState.OnGameDataExtracted += OnGameDataExtracted;
        }

        private void InitializeComponent()
        {
            QFG5TableLayoutPanel layout = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            Label title = new Label
            {
                Text = "AUDIO EXPLORER",
                Font = QFG5Theme.TitleFont,
                ForeColor = QFG5Theme.ColorTextGold,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            QFG5Theme.ApplyHeaderStroke(title);
            layout.Controls.Add(title, 0, 0);

            QFG5TableLayoutPanel actions = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 4)
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));

            Label sourceLabel = new Label
            {
                Text = "Extracted WAV / AUD files (including ID-named speech resources)",
                Dock = DockStyle.Fill,
                Font = QFG5Theme.CaptionFont,
                ForeColor = QFG5Theme.ColorTextMuted,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            actions.Controls.Add(sourceLabel, 0, 0);

            favoritesOnlyCheckBox = new CheckBox
            {
                Text = "Show favorites only",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.CaptionFont,
                BackColor = Color.Transparent,
                Margin = new Padding(2, 4, 0, 0),
                AutoSize = false
            };
            favoritesOnlyCheckBox.CheckedChanged += (s, e) => RenderAudioItems(null);
            actions.Controls.Add(favoritesOnlyCheckBox, 1, 0);

            QFG5Button refreshButton = new QFG5Button { Text = "Refresh", Dock = DockStyle.Fill, Height = 30 };
            refreshButton.Click += (s, e) => RefreshFiles();
            actions.Controls.Add(refreshButton, 2, 0);

            favoriteButton = new QFG5Button { Text = "\u2606", Dock = DockStyle.Fill, Height = 30, Enabled = false };
            favoriteButton.Font = new Font("Segoe UI Symbol", 14f, FontStyle.Regular);
            favoriteButton.Click += (s, e) => ToggleSelectedFavorite();
            ToolTip favoriteToolTip = new ToolTip();
            favoriteToolTip.SetToolTip(favoriteButton, "Toggle favorite for the selected audio resource");
            actions.Controls.Add(favoriteButton, 3, 0);

            playButton = new QFG5Button { Text = "Play", Dock = DockStyle.Fill, Height = 30, Enabled = false };
            playButton.Click += (s, e) => PlaySelected();
            actions.Controls.Add(playButton, 4, 0);

            stopButton = new QFG5Button { Text = "Stop", Dock = DockStyle.Fill, Height = 30, Enabled = false };
            stopButton.Click += (s, e) => StopPlayback();
            actions.Controls.Add(stopButton, 5, 0);
            layout.Controls.Add(actions, 0, 1);

            SplitContainer content = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 350,
                BackColor = QFG5Theme.ColorSurface,
                BorderStyle = BorderStyle.FixedSingle,
                Panel1MinSize = 120,
                Panel2MinSize = 100
            };

            audioList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                BorderStyle = BorderStyle.None
            };
            audioList.Columns.Add("Audio file", 200);
            audioList.Columns.Add("Duration", 90);
            audioList.Columns.Add("Header name", 180);
            audioList.Columns.Add("Resource IDs", 240);
            audioList.Columns.Add("Source folder", 180);
            audioList.Columns.Add("Format", 260);
            audioList.SelectedIndexChanged += AudioList_SelectedIndexChanged;
            content.Panel1.Controls.Add(audioList);

            detailsText = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                BorderStyle = BorderStyle.None,
                Text = "Select an audio resource to inspect its filename, encoded resource IDs, and WAV header."
            };
            content.Panel2.Controls.Add(detailsText);
            layout.Controls.Add(content, 0, 2);

            statusLabel = new Label
            {
                Text = "Open Audio Explorer to scan the configured folders.",
                Dock = DockStyle.Fill,
                Font = QFG5Theme.CaptionFont,
                ForeColor = QFG5Theme.ColorGold,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            layout.Controls.Add(statusLabel, 0, 3);
            Controls.Add(layout);
        }

        private void OnConfiguredPathChanged(string path)
        {
            InvalidateScan();
            if (Visible)
                RefreshFiles();
        }

        private void OnGameDataExtracted()
        {
            InvalidateScan();
            if (Visible)
                RefreshFiles();
        }

        private void InvalidateScan()
        {
            hasLoaded = false;
            if (scanCancellation != null)
                scanCancellation.Cancel();
        }

        public void AutoLoad()
        {
            if (!hasLoaded)
                RefreshFiles();
        }

        private async void RefreshFiles()
        {
            if (audioList == null || disposed)
                return;

            StopPlayback();
            if (scanCancellation != null)
                scanCancellation.Cancel();

            CancellationTokenSource cancellation = new CancellationTokenSource();
            scanCancellation = cancellation;
            string[] roots = GetAudioScanRoots(
                new[] { SharedState.BaseDataPath, SharedState.ExtractionPath });
            statusLabel.Text = "Scanning configured folders in the background...";

            try
            {
                AudioScanResult result = await Task.Run(
                    () => ScanFiles(roots, cancellation.Token),
                    cancellation.Token);

                if (disposed || !ReferenceEquals(scanCancellation, cancellation))
                    return;

                audioItems.Clear();
                audioItems.AddRange(result.Items);
                RenderAudioItems(null);

                if (result.ErrorCount == 0)
                {
                    statusLabel.Text = result.Items.Count == 0
                        ? "No WAV/AUD files or ID-named speech resources found under the configured DATA and Mods folders."
                        : string.Format("Loaded {0} audio resource(s).", result.Items.Count);
                }
                else
                {
                    statusLabel.Text = string.Format(
                        "Loaded {0} audio resource(s); {1} scan/read error(s). See the application log.",
                        result.Items.Count, result.ErrorCount);
                    foreach (string error in result.Errors)
                        Logger.LogError("AudioTab", error);
                    if (result.Errors.Count < result.ErrorCount)
                        Logger.LogError("AudioTab", string.Format("{0} additional scan/read errors omitted.", result.ErrorCount - result.Errors.Count));
                }

                hasLoaded = true;
                UpdatePlaybackButtons();
            }
            catch (OperationCanceledException)
            {
                // A newer refresh or path change superseded this scan.
            }
            catch (Exception ex)
            {
                if (!disposed && ReferenceEquals(scanCancellation, cancellation))
                {
                    Logger.LogError("AudioTab", "Audio scan failed: " + ex);
                    statusLabel.Text = "Audio scan failed. See the application log.";
                }
            }
            finally
            {
                if (ReferenceEquals(scanCancellation, cancellation))
                    scanCancellation = null;
                cancellation.Dispose();
            }
        }

        private static AudioScanResult ScanFiles(string[] roots, CancellationToken cancellationToken)
        {
            AudioScanResult result = new AudioScanResult();
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string root in roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                    continue;

                try
                {
                    foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string fullPath = Path.GetFullPath(path);
                        if (!visited.Add(fullPath) || !AudioResourceInspector.IsAudioCandidate(fullPath))
                            continue;

                        try
                        {
                            result.Items.Add(new AudioScanItem
                            {
                                Info = AudioResourceInspector.Inspect(fullPath),
                                Root = root
                            });
                        }
                        catch (IOException ex)
                        {
                            AddScanError(result, Path.GetFileName(path) + ": " + ex.Message);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            AddScanError(result, Path.GetFileName(path) + ": " + ex.Message);
                        }
                    }
                }
                catch (IOException ex)
                {
                    AddScanError(result, root + ": " + ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    AddScanError(result, root + ": " + ex.Message);
                }
            }

            result.Items.Sort((left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(left.Info.FileName, right.Info.FileName));
            return result;
        }

        private static string[] GetAudioScanRoots(string[] configuredRoots)
        {
            List<string> cdaRoots = new List<string>();
            List<string> hdnRoots = new List<string>();
            List<string> fallbackRoots = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string configuredRoot in configuredRoots)
            {
                if (string.IsNullOrWhiteSpace(configuredRoot) || !Directory.Exists(configuredRoot))
                    continue;

                string fullRoot = Path.GetFullPath(configuredRoot)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string leafName = Path.GetFileName(fullRoot);
                string contentRoot = fullRoot;
                string cdaPath;
                if (string.Equals(leafName, "cda", StringComparison.OrdinalIgnoreCase))
                {
                    contentRoot = Path.GetDirectoryName(fullRoot);
                    cdaPath = fullRoot;
                }
                else if (string.Equals(leafName, "aud", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetFileName(Path.GetDirectoryName(fullRoot)), "cda", StringComparison.OrdinalIgnoreCase))
                {
                    contentRoot = Path.GetDirectoryName(Path.GetDirectoryName(fullRoot));
                    cdaPath = Path.GetDirectoryName(fullRoot);
                }
                else
                {
                    cdaPath = Path.Combine(contentRoot, "cda");
                }

                if (Directory.Exists(cdaPath) && seen.Add(cdaPath))
                    cdaRoots.Add(cdaPath);

                string hdnPath = string.Equals(leafName, "hdn", StringComparison.OrdinalIgnoreCase)
                    ? fullRoot
                    : Path.Combine(contentRoot, "hdn");
                if (Directory.Exists(hdnPath) && seen.Add(hdnPath))
                    hdnRoots.Add(hdnPath);

                if (!Directory.Exists(cdaPath) && !Directory.Exists(hdnPath) && seen.Add(fullRoot))
                {
                    fallbackRoots.Add(fullRoot);
                }
            }

            List<string> roots = new List<string>(cdaRoots);
            roots.AddRange(hdnRoots);
            if (roots.Count == 0)
                roots.AddRange(fallbackRoots);
            return roots.ToArray();
        }

        private static void AddScanError(AudioScanResult result, string error)
        {
            result.ErrorCount++;
            if (result.Errors.Count < 50)
                result.Errors.Add(error);
        }

        private void RenderAudioItems(string selectedPath)
        {
            if (audioList == null)
                return;
            if (selectedPath == null)
            {
                AudioResourceInfo selected = GetSelectedInfo();
                selectedPath = selected == null ? null : selected.FullPath;
            }

            audioList.BeginUpdate();
            audioList.Items.Clear();
            foreach (AudioScanItem entry in audioItems)
            {
                if (favoritesOnlyCheckBox != null && favoritesOnlyCheckBox.Checked && !IsFavorite(entry))
                    continue;
                AddAudioItem(entry);
            }

            if (!string.IsNullOrEmpty(selectedPath))
            {
                foreach (ListViewItem item in audioList.Items)
                {
                    AudioScanItem entry = item.Tag as AudioScanItem;
                    if (entry != null && string.Equals(entry.Info.FullPath, selectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Selected = true;
                        item.Focused = true;
                        break;
                    }
                }
            }
            audioList.EndUpdate();
        }

        private void AddAudioItem(AudioScanItem entry)
        {
            AudioResourceInfo info = entry.Info;
            string root = entry.Root;
            string sourceFolder = Path.GetDirectoryName(info.FullPath);
            if (sourceFolder.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                sourceFolder = sourceFolder.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string rootName = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            ListViewItem item = new ListViewItem((IsFavorite(entry) ? "\u2605 " : "\u2606 ") + info.FileName);
            item.UseItemStyleForSubItems = false;
            item.SubItems[0].ForeColor = IsFavorite(entry) ? Color.Gold : QFG5Theme.ColorTextMuted;
            item.SubItems.Add(info.DurationSummary, QFG5Theme.ColorTextPrimary, Color.Empty, audioList.Font);
            item.SubItems.Add(string.IsNullOrEmpty(info.HeaderName) ? "--" : info.HeaderName,
                QFG5Theme.ColorTextPrimary, Color.Empty, audioList.Font);
            item.SubItems.Add(string.IsNullOrEmpty(info.ResourceIds) ? "--" : info.ResourceIds,
                QFG5Theme.ColorTextPrimary, Color.Empty, audioList.Font);
            item.SubItems.Add(rootName + Path.DirectorySeparatorChar
                + (string.IsNullOrEmpty(sourceFolder) ? "." : sourceFolder),
                QFG5Theme.ColorTextPrimary, Color.Empty, audioList.Font);
            item.SubItems.Add(info.FormatSummary, QFG5Theme.ColorTextPrimary, Color.Empty, audioList.Font);
            item.Tag = entry;
            audioList.Items.Add(item);
        }

        private void AudioList_SelectedIndexChanged(object sender, EventArgs e)
        {
            AudioResourceInfo info = GetSelectedInfo();
            if (info == null)
            {
                detailsText.Text = "Select an audio resource to inspect its filename, encoded resource IDs, and WAV header.";
            }
            else
            {
                detailsText.Text = "File: " + info.FileName + Environment.NewLine
                    + "Path: " + info.FullPath + Environment.NewLine
                    + "Duration: " + info.DurationSummary + Environment.NewLine
                    + "Resource IDs: " + (string.IsNullOrEmpty(info.ResourceIds) ? "No recognized QFG5 audio ID in the filename." : info.ResourceIds)
                    + Environment.NewLine
                    + "Header name: " + (string.IsNullOrEmpty(info.HeaderName) ? "No title/name metadata found in supported WAV headers." : info.HeaderName)
                    + Environment.NewLine
                    + "Format: " + info.FormatSummary + Environment.NewLine
                    + info.HeaderSummary;
            }
            UpdatePlaybackButtons();
        }

        private void PlaySelected()
        {
            AudioResourceInfo info = GetSelectedInfo();
            if (info == null || !info.IsPlayableWave)
            {
                statusLabel.Text = "Select a valid RIFF/WAVE audio resource before playing.";
                return;
            }

            StopPlayback();
            if (IsUnixLike())
            {
                PlayWithFfplay(info);
                return;
            }

            try
            {
                player = new SoundPlayer(info.FullPath);
                player.Load();
                player.Play();
                statusLabel.Text = "Playing " + info.FileName + ".";
                UpdatePlaybackButtons();
            }
            catch (IOException ex)
            {
                Logger.LogError("AudioTab", "Could not play " + info.FullPath + ": " + ex.Message);
                statusLabel.Text = "Could not play audio: " + ex.Message;
                StopPlayback();
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogError("AudioTab", "Could not play " + info.FullPath + ": " + ex.Message);
                statusLabel.Text = "Could not play audio: " + ex.Message;
                StopPlayback();
            }
            catch (NotSupportedException ex)
            {
                Logger.LogError("AudioTab", "Could not play " + info.FullPath + ": " + ex.Message);
                statusLabel.Text = "Could not play audio: " + ex.Message;
                StopPlayback();
            }
            catch (ArgumentException ex)
            {
                Logger.LogError("AudioTab", "Could not play " + info.FullPath + ": " + ex.Message);
                statusLabel.Text = "Could not play audio: " + ex.Message;
                StopPlayback();
            }
        }

        private void PlayWithFfplay(AudioResourceInfo info)
        {
            string ffplayPath = FindExecutable("ffplay", "ffplay.exe");
            if (ffplayPath == null)
            {
                statusLabel.Text = "In-app playback on Linux requires ffplay (install FFmpeg and add it to PATH).";
                Logger.LogError("AudioTab", "ffplay was not found on PATH or beside the application.");
                return;
            }

            Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = ffplayPath,
                Arguments = "-nodisp -autoexit -loglevel error -i " + QuoteProcessArgument(info.FullPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                    Logger.LogError("AudioTab", "ffplay: " + args.Data);
            };
            process.Exited += (sender, args) => HandlePlaybackExited(process, info.FileName);

            try
            {
                process.EnableRaisingEvents = true;
                if (!process.Start())
                    throw new InvalidOperationException("The ffplay process could not be started.");
                audioProcess = process;
                process.BeginErrorReadLine();
                statusLabel.Text = "Playing " + info.FileName + ".";
                UpdatePlaybackButtons();
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException
                || ex is System.ComponentModel.Win32Exception)
            {
                process.Dispose();
                Logger.LogError("AudioTab", "Could not start ffplay for " + info.FullPath + ": " + ex.Message);
                statusLabel.Text = "Could not start audio playback: " + ex.Message;
            }
        }

        private void HandlePlaybackExited(Process process, string fileName)
        {
            if (disposed || IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!ReferenceEquals(audioProcess, process))
                        return;

                    int exitCode = process.ExitCode;
                    audioProcess = null;
                    process.Dispose();
                    statusLabel.Text = exitCode == 0
                        ? "Finished playing " + fileName + "."
                        : "Audio playback failed (exit code " + exitCode + "). See the application log.";
                    UpdatePlaybackButtons();
                });
            }
            catch (InvalidOperationException)
            {
                // The tab can close between the process exit and its UI notification.
            }
        }

        private static bool IsUnixLike()
        {
            PlatformID platform = Environment.OSVersion.Platform;
            return platform == PlatformID.Unix || platform == PlatformID.MacOSX;
        }

        private static string FindExecutable(string executableName, string windowsName)
        {
            string localExecutable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                IsUnixLike() ? executableName : windowsName);
            if (File.Exists(localExecutable))
                return localExecutable;

            string pathEnvironment = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnvironment))
                return null;

            string[] directories = pathEnvironment.Split(Path.PathSeparator);
            foreach (string directory in directories)
            {
                if (string.IsNullOrWhiteSpace(directory))
                    continue;

                string candidate = Path.Combine(directory, IsUnixLike() ? executableName : windowsName);
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static string QuoteProcessArgument(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private void StopPlayback()
        {
            if (audioProcess != null)
            {
                Process process = audioProcess;
                audioProcess = null;
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // The player may have exited between the state check and stop.
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    Logger.LogError("AudioTab", "Could not stop ffplay: " + ex.Message);
                }
                finally
                {
                    process.Dispose();
                }
            }

            if (player != null)
            {
                player.Stop();
                player.Dispose();
                player = null;
            }
            UpdatePlaybackButtons();
        }

        private AudioResourceInfo GetSelectedInfo()
        {
            if (audioList.SelectedItems.Count == 0)
                return null;
            AudioScanItem entry = audioList.SelectedItems[0].Tag as AudioScanItem;
            return entry == null ? null : entry.Info;
        }

        private void UpdatePlaybackButtons()
        {
            if (playButton == null || stopButton == null || favoriteButton == null)
                return;
            AudioResourceInfo info = GetSelectedInfo();
            playButton.Enabled = info != null && info.IsPlayableWave && audioProcess == null && player == null;
            stopButton.Enabled = audioProcess != null || player != null;
            AudioScanItem entry = audioList.SelectedItems.Count == 0
                ? null
                : audioList.SelectedItems[0].Tag as AudioScanItem;
            favoriteButton.Enabled = entry != null;
            favoriteButton.Text = IsFavorite(entry) ? "\u2605" : "\u2606";
        }

        private static string GetFavoriteKey(AudioScanItem entry)
        {
            string root = Path.GetFullPath(entry.Root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(entry.Info.FullPath);
            string prefix = root + Path.DirectorySeparatorChar;
            string relativePath = fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(prefix.Length)
                : entry.Info.FileName;
            return Path.GetFileName(root) + "/" + relativePath.Replace('\\', '/');
        }

        private bool IsFavorite(AudioScanItem entry)
        {
            return entry != null && favoriteFiles.Contains(GetFavoriteKey(entry));
        }

        private void ToggleSelectedFavorite()
        {
            if (audioList.SelectedItems.Count == 0)
                return;
            AudioScanItem entry = audioList.SelectedItems[0].Tag as AudioScanItem;
            if (entry == null)
                return;

            string key = GetFavoriteKey(entry);
            if (!favoriteFiles.Add(key))
                favoriteFiles.Remove(key);
            SaveFavoriteFiles();
            RenderAudioItems(entry.Info.FullPath);
        }

        private void LoadFavoriteFiles()
        {
            try
            {
                if (!File.Exists(favoritesFilePath))
                    return;
                foreach (string line in File.ReadAllLines(favoritesFilePath))
                    if (!string.IsNullOrWhiteSpace(line))
                        favoriteFiles.Add(line.Trim());
            }
            catch (Exception ex)
            {
                Logger.LogError("AudioTab", "Could not load audio favorites: " + ex.Message);
            }
        }

        private void SaveFavoriteFiles()
        {
            try
            {
                string directory = Path.GetDirectoryName(favoritesFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                List<string> favorites = new List<string>(favoriteFiles);
                favorites.Sort(StringComparer.OrdinalIgnoreCase);
                File.WriteAllLines(favoritesFilePath, favorites.ToArray());
            }
            catch (Exception ex)
            {
                Logger.LogError("AudioTab", "Could not save audio favorites: " + ex.Message);
                MessageBox.Show("Could not save audio favorites: " + ex.Message,
                    "Audio favorites", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                SharedState.OnBaseDataPathChanged -= OnConfiguredPathChanged;
                SharedState.OnExtractionPathChanged -= OnConfiguredPathChanged;
                SharedState.OnGameDataExtracted -= OnGameDataExtracted;
                if (scanCancellation != null)
                {
                    scanCancellation.Cancel();
                    scanCancellation = null;
                }
                StopPlayback();
            }
            base.Dispose(disposing);
        }
    }
}
