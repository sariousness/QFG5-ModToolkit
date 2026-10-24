using System;
using System.Collections.Generic;
using System.IO;

namespace QFG5Extractor.Core
{
    public static class SharedState
    {
        private static string _baseDataPath = string.Empty;
        private static string _extractionPath = string.Empty;

        public static event Action<string> OnBaseDataPathChanged;
        public static event Action<string> OnExtractionPathChanged;
        public static event Action OnGameDataExtracted;
        public static event Action<string> OnGraFileChanged;
        public static event Action<System.Drawing.Bitmap> OnEditTextureRequested;
        public static event Action<System.Drawing.Bitmap, string, int, int, int, bool> OnEditGraFrameRequested;
        public static event Action<string> OnEditModelRequested;
        public static event Action<System.Drawing.Bitmap, string, string, string> OnEditPanoRequested;
        public static event Action<int> OnSwitchTabRequested;
        public static event Action<string> OnPanoFilesChanged;

        public static void RequestEditTexture(System.Drawing.Bitmap bmp) => OnEditTextureRequested?.Invoke(bmp);
        public static void RequestEditGraFrame(System.Drawing.Bitmap bmp, string graPath, int collectionIndex,
            int frameIndex, int modeOverride = -1, bool cmOverride = false) =>
            OnEditGraFrameRequested?.Invoke(bmp, graPath, collectionIndex, frameIndex, modeOverride, cmOverride);
        public static void RequestEditModel(string mdlPath) => OnEditModelRequested?.Invoke(mdlPath);
        public static void RequestEditPano(System.Drawing.Bitmap bmp, string bmpPath,
            string nodPath, string imgPath) =>
            OnEditPanoRequested?.Invoke(bmp, bmpPath, nodPath, imgPath);
        public static void RequestSwitchTab(int tabIndex) => OnSwitchTabRequested?.Invoke(tabIndex);
        public static void NotifyGameDataExtracted() => OnGameDataExtracted?.Invoke();
        public static void NotifyGraFileChanged(string graPath) => OnGraFileChanged?.Invoke(graPath);
        public static void NotifyPanoFileChanged(string baseName) => OnPanoFilesChanged?.Invoke(baseName);

        public static string BaseDataPath
        {
            get => _baseDataPath;
            set
            {
                if (_baseDataPath != value)
                {
                    _baseDataPath = value;
                    OnBaseDataPathChanged?.Invoke(_baseDataPath);
                }
            }
        }

        public static string ExtractionPath
        {
            get => _extractionPath;
            set
            {
                if (_extractionPath != value)
                {
                    _extractionPath = value;
                    OnExtractionPathChanged?.Invoke(_extractionPath);
                }
            }
        }

        public static string GamePath
        {
            get => string.IsNullOrEmpty(BaseDataPath) ? string.Empty : Directory.GetParent(BaseDataPath)?.FullName ?? string.Empty;
        }

        public static string GetGameDataPath(params string[] segments)
        {
            return CombineExistingPath(BaseDataPath, segments);
        }

        public static string GetAssetDataPath(params string[] segments)
        {
            string gamePath = GetGameDataPath(segments);
            return Directory.Exists(gamePath) && Directory.GetFiles(gamePath).Length > 0
                ? gamePath
                : GetModsPath(segments);
        }

        public static string GetModsPath(params string[] segments)
        {
            return CombineExistingPath(ExtractionPath, segments);
        }

        public static string GetArchiveModsPath(string archiveFileName)
        {
            string archiveName = Path.GetFileNameWithoutExtension(archiveFileName);
            return GetModsPath(archiveName.ToLowerInvariant());
        }

        public static string GameInstallPath => GamePath;

        private static string SettingsFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.cfg");

        public static void LoadSettings()
        {
            Dictionary<string, string> settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    foreach (string line in File.ReadAllLines(SettingsFilePath))
                    {
                        if (string.IsNullOrWhiteSpace(line) || !line.Contains("=")) continue;
                        string[] parts = line.Split(new char[] { '=' }, 2);
                        settings[parts[0].Trim()] = parts[1].Trim();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Settings", "Could not load settings.cfg: " + ex.Message);
                }
            }

            string configuredDataPath;
            if (settings.TryGetValue("BaseDataPath", out configuredDataPath) && IsValidGameDataPath(configuredDataPath))
                BaseDataPath = configuredDataPath;
            else
                BaseDataPath = FindStandardGameDataPath() ?? string.Empty;

            string configuredModsPath;
            if (!settings.TryGetValue("ExtractionPath", out configuredModsPath) || string.IsNullOrWhiteSpace(configuredModsPath))
            {
                settings.TryGetValue("ModdedFilesPath", out configuredModsPath);
            }
            ExtractionPath = string.IsNullOrWhiteSpace(configuredModsPath) ? GetDefaultModsPath() : configuredModsPath;
        }

        public static bool SaveSettings()
        {
            try
            {
                List<string> lines = new List<string>();
                lines.Add("BaseDataPath=" + BaseDataPath);
                lines.Add("ExtractionPath=" + ExtractionPath);
                File.WriteAllLines(SettingsFilePath, lines.ToArray());
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Settings", "Could not save settings.cfg: " + ex.Message);
            }
            return false;
        }

        public static bool IsValidGameDataPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return false;

            if (Directory.Exists(CombineExistingPath(path, new[] { "cdn" }))
                && Directory.Exists(CombineExistingPath(path, new[] { "hdnw" })))
                return true;

            foreach (string file in Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetExtension(file), ".spk", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string FindStandardGameDataPath()
        {
            List<string> candidates = new List<string> { @"C:\SIERRA\QFG5\DATA" };
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFilesX86))
                candidates.Add(Path.Combine(programFilesX86, "GOG.com", "Quest for Glory Pack", "Quest for Glory 5", "DATA"));
            if (!string.IsNullOrEmpty(programFiles))
                candidates.Add(Path.Combine(programFiles, "GOG.com", "Quest for Glory Pack", "Quest for Glory 5", "DATA"));

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                candidates.Add(Path.Combine(userProfile, ".wine", "drive_c", "SIERRA", "QFG5", "DATA"));
                candidates.Add(Path.Combine(userProfile, ".wine", "drive_c", "Program Files (x86)", "GOG.com", "Quest for Glory Pack", "Quest for Glory 5", "DATA"));
                candidates.Add(Path.Combine(userProfile, "Quest for Glory 5", "DATA"));
            }

            foreach (string candidate in candidates)
            {
                if (IsValidGameDataPath(candidate)) return candidate;
            }
            return null;
        }

        private static string GetDefaultModsPath()
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(documents)) documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(documents)) documents = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(documents, "QFG5 Mods");
        }

        private static string CombineExistingPath(string root, string[] segments)
        {
            if (string.IsNullOrEmpty(root)) return string.Empty;

            string current = root;
            for (int i = 0; i < segments.Length; i++)
            {
                if (!Directory.Exists(current))
                {
                    for (; i < segments.Length; i++) current = Path.Combine(current, segments[i]);
                    break;
                }

                string matchingDirectory = Array.Find(
                    Directory.GetDirectories(current),
                    directory => string.Equals(Path.GetFileName(directory), segments[i], StringComparison.OrdinalIgnoreCase));
                current = matchingDirectory ?? Path.Combine(current, segments[i]);
            }
            return current;
        }
    }
}
