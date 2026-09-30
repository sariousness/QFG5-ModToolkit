using QFG5Extractor.Core;
using System;
using System.Drawing;
using System.IO;

namespace QFG5Extractor.Core.UI
{
    public static class QFG5Theme
    {
        public static string UIKitPath = @"C:\Program Files\QFG5-RE\QFG5Extractor\UI Kit";
        
        public static Image ButtonIdle;
        public static Image ButtonPressed;
        public static Image WindowBg;
        public static Image ItemFrame;
        public static Image PreviewBox;
        public static Image GameLogo;
        public static Image ToolkitLogo;

        static QFG5Theme() {
            // Priority: Local UI Kit relative to executable
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // From bin/Debug to root: up 3 levels
            string rootDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(baseDir.TrimEnd(Path.DirectorySeparatorChar))));
            UIKitPath = Path.Combine(rootDir, "UI Kit");

            if (!Directory.Exists(UIKitPath)) {
                // Try relative to project root in development environment
                string projectRootPath = "/home/vandali/Desktop/QFG5-Tool/UI Kit";
                if (Directory.Exists(projectRootPath)) {
                    UIKitPath = projectRootPath;
                } else {
                    // Fallback to absolute wine path
                    string winePath = "/home/vandali/.wine32/drive_c/Program Files/QFG5-RE/QFG5Extractor/UI Kit";
                    if (Directory.Exists(winePath)) UIKitPath = winePath;
                }
            }
            
            Logger.Log("UI Theme: Using kit path: " + UIKitPath);

            ButtonIdle = LoadImage("qfg-button.png");
            ButtonPressed = LoadImage("qfg-buttonClicked.png");
            WindowBg = LoadImage("qfg-background.png");
            ItemFrame = Path.Combine(UIKitPath, "qfg-frame.png") != null ? LoadImage("qfg-frame.png") : null;
            PreviewBox = LoadImage("qfg-previewBox.jpg");
            GameLogo = LoadImage("qfg-gameLogo.png");
            ToolkitLogo = LoadImage("qfg-toolkitLogo.png");
        }

        private static Image LoadImage(string filename) {
            try {
                string path = Path.Combine(UIKitPath, filename);
                if (File.Exists(path)) {
                    Logger.Log("UI Theme: Loading " + filename);
                    return Image.FromFile(path);
                }
                Logger.Log("UI Theme: Missing " + filename + " at " + path);
            } catch (Exception ex) {
                Logger.Log("UI Theme: Error loading " + filename + ": " + ex.Message);
            }
            return null;
        }
    }
}
