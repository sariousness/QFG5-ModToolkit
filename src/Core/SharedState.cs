using System;

namespace QFG5Extractor.Core
{
    public static class SharedState
    {
        private static string _baseDataPath = string.Empty;
        private static string _extractionPath = string.Empty;
        private static string _moddedFilesPath = string.Empty;
        private static string _gamePath = string.Empty;

        public static event Action<string> OnBaseDataPathChanged;
        public static event Action<string> OnExtractionPathChanged;
        public static event Action<string> OnModdedFilesPathChanged;
        public static event Action<string> OnGamePathChanged;
        public static event Action<System.Drawing.Bitmap> OnEditTextureRequested;
        public static event Action<string> OnEditModelRequested;
        public static event Action<int> OnSwitchTabRequested;

        public static void RequestEditTexture(System.Drawing.Bitmap bmp) => OnEditTextureRequested?.Invoke(bmp);
        public static void RequestEditModel(string mdlPath) => OnEditModelRequested?.Invoke(mdlPath);
        public static void RequestSwitchTab(int tabIndex) => OnSwitchTabRequested?.Invoke(tabIndex);

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

        public static string ModdedFilesPath
        {
            get => _moddedFilesPath;
            set
            {
                if (_moddedFilesPath != value)
                {
                    _moddedFilesPath = value;
                    OnModdedFilesPathChanged?.Invoke(_moddedFilesPath);
                }
            }
        }

        public static string GamePath
        {
            get => _gamePath;
            set
            {
                if (_gamePath != value)
                {
                    _gamePath = value;
                    OnGamePathChanged?.Invoke(_gamePath);
                }
            }
        }

        private static string SettingsFilePath => System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.cfg");

        public static void LoadSettings()
        {
            if (!System.IO.File.Exists(SettingsFilePath)) return;
            try {
                string[] lines = System.IO.File.ReadAllLines(SettingsFilePath);
                foreach (string line in lines) {
                    if (string.IsNullOrEmpty(line) || !line.Contains("=")) continue;
                    string[] parts = line.Split(new char[] { '=' }, 2);
                    string key = parts[0].Trim();
                    string val = parts[1].Trim();
                    switch (key) {
                        case "BaseDataPath": BaseDataPath = val; break;
                        case "ExtractionPath": ExtractionPath = val; break;
                        case "ModdedFilesPath": ModdedFilesPath = val; break;
                        case "GamePath": GamePath = val; break;
                    }
                }
            } catch {}
        }

        public static void SaveSettings()
        {
            try {
                System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
                lines.Add("BaseDataPath=" + BaseDataPath);
                lines.Add("ExtractionPath=" + ExtractionPath);
                lines.Add("ModdedFilesPath=" + ModdedFilesPath);
                lines.Add("GamePath=" + GamePath);
                System.IO.File.WriteAllLines(SettingsFilePath, lines.ToArray());
            } catch {}
        }
    }
}
