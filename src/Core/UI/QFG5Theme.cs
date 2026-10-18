using QFG5Extractor.Core;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace QFG5Extractor.Core.UI
{
    public static class QFG5Theme
    {
        public static string UIKitPath = @"C:\Program Files\QFG5-RE\QFG5Extractor\UI Kit";
        
        public static Image ButtonIdle;
        public static Image ButtonPressed;
        public static Image WindowBg;
        public static Image LeatherTexture;
        public static Image ItemFrame;
        public static Image PreviewBox;
        public static Image GameLogo;
        public static Image ToolkitLogo;

        #region Centralized Color Tokens (QFG5 Theme)
        public static readonly Color ColorBgDark = Color.FromArgb(20, 22, 18);
        public static readonly Color ColorSurface = Color.FromArgb(43, 37, 29);
        public static readonly Color ColorSurfaceRecessed = Color.FromArgb(18, 19, 16);
        public static readonly Color ColorBorderOuter = Color.FromArgb(105, 73, 42);
        public static readonly Color ColorBorderInner = Color.FromArgb(27, 22, 17);
        public static readonly Color ColorHeaderStroke = Color.FromArgb(12, 11, 9);
        public static readonly Color ColorBorderHighlight = Color.FromArgb(116, 91, 57);
        public static readonly Color ColorGold = Color.FromArgb(194, 153, 76);
        public static readonly Color ColorGoldBright = Color.FromArgb(239, 207, 130);
        public static readonly Color ColorGoldMuted = Color.FromArgb(142, 108, 57);
        public static readonly Color ColorGoldGlow = Color.FromArgb(72, 239, 207, 130);
        public static readonly Color ColorSelected = Color.FromArgb(105, 111, 105, 49);
        public static readonly Color ColorTextPrimary = Color.FromArgb(231, 222, 202);
        public static readonly Color ColorTextGold = Color.FromArgb(218, 183, 112);
        public static readonly Color ColorTextMuted = Color.FromArgb(165, 155, 135);
        public static readonly Color ColorTextDisabled = Color.FromArgb(108, 97, 80);
        public static readonly Color ColorTextDark = Color.FromArgb(30, 24, 17);
        public static readonly Color ColorButtonDisabledOverlay = Color.FromArgb(155, 20, 18, 15);
        #endregion

        #region Centralized Typography Tokens
        public static readonly Font TitleFont = new Font("Arial", 14f, FontStyle.Bold);
        public static readonly Font SectionHeaderFont = new Font("Arial", 11f, FontStyle.Bold);
        public static readonly Font ButtonFont = new Font("Arial", 11f, FontStyle.Bold);
        public static readonly Font BodyFont = new Font("Arial", 9.5f, FontStyle.Regular);
        public static readonly Font BodyBoldFont = new Font("Arial", 9.5f, FontStyle.Bold);
        public static readonly Font CaptionFont = new Font("Arial", 8.25f, FontStyle.Regular);
        #endregion

        #region Centralized Cached Brushes & Pens
        public static readonly SolidBrush BrushGold = new SolidBrush(ColorGold);
        public static readonly SolidBrush BrushTextPrimary = new SolidBrush(ColorTextPrimary);
        public static readonly SolidBrush BrushTextMuted = new SolidBrush(ColorTextMuted);
        public static readonly SolidBrush BrushTextDisabled = new SolidBrush(ColorTextDisabled);
        public static readonly SolidBrush BrushSurfaceRecessed = new SolidBrush(ColorSurfaceRecessed);
        public static readonly SolidBrush BrushHoverGlow = new SolidBrush(ColorGoldGlow);
        public static readonly SolidBrush BrushSelected = new SolidBrush(ColorSelected);
        public static readonly SolidBrush BrushDisabledOverlay = new SolidBrush(ColorButtonDisabledOverlay);
        public static readonly Pen PenBorderOuter = new Pen(ColorBorderOuter);
        public static readonly Pen PenBorderInner = new Pen(ColorBorderInner);
        public static readonly Pen PenBorderHighlight = new Pen(ColorBorderHighlight);
        #endregion

        /// <summary>
        /// Draws a standardized subtle QFG5-style 2px recessed border.
        /// </summary>
        public static void DrawRecessedFrame(Graphics g, Rectangle bounds)
        {
            if (bounds.Width < 2 || bounds.Height < 2) return;

            // Outer subtle bronze/stone boundary (1px)
            g.DrawRectangle(PenBorderOuter, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

            if (bounds.Width > 4 && bounds.Height > 4)
            {
                // Inner shadow: top and left
                g.DrawLine(PenBorderInner, bounds.X + 1, bounds.Y + 1, bounds.Right - 2, bounds.Y + 1);
                g.DrawLine(PenBorderInner, bounds.X + 1, bounds.Y + 1, bounds.X + 1, bounds.Bottom - 2);

                // Inner highlight: bottom and right
                g.DrawLine(PenBorderHighlight, bounds.X + 1, bounds.Bottom - 2, bounds.Right - 2, bounds.Bottom - 2);
                g.DrawLine(PenBorderHighlight, bounds.Right - 2, bounds.Y + 1, bounds.Right - 2, bounds.Bottom - 2);
            }

        }

        public static void DrawHeaderStroke(Graphics graphics, Rectangle bounds)
        {
            if (bounds.Width < 2 || bounds.Height < 2) return;
            using (Pen stroke = new Pen(ColorHeaderStroke))
                graphics.DrawRectangle(stroke, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }

        public static void ApplyHeaderStroke(Control control)
        {
            if (control == null) return;
            control.Paint += (s, e) => DrawHeaderStroke(e.Graphics, control.ClientRectangle);
        }

        static QFG5Theme() {
            var swTheme = System.Diagnostics.Stopwatch.StartNew();
            var swDiscovery = System.Diagnostics.Stopwatch.StartNew();
            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
                string localKit = Path.Combine(baseDir, "UI Kit");
                string parentKit = Path.Combine(baseDir, "..", "UI Kit");
                string cwdKit = Path.Combine(Environment.CurrentDirectory, "UI Kit");

                // Priority 1: Next to executable (Standard release / zip distribution)
                if (Directory.Exists(localKit)) {
                    UIKitPath = localKit;
                }
                // Priority 2: In current working directory
                else if (Directory.Exists(cwdKit)) {
                    UIKitPath = cwdKit;
                }
                // Priority 3: In parent directory
                else if (Directory.Exists(parentKit)) {
                    UIKitPath = parentKit;
                }
                else {
                    // Priority 4: Development path (3 levels up from bin/Debug or bin/Release)
                    string devKit = null;
                    try {
                        DirectoryInfo dir = new DirectoryInfo(baseDir);
                        if (dir.Parent != null && dir.Parent.Parent != null && dir.Parent.Parent.Parent != null) {
                            devKit = Path.Combine(dir.Parent.Parent.Parent.FullName, "UI Kit");
                        }
                    } catch { }

                    if (!string.IsNullOrEmpty(devKit) && Directory.Exists(devKit)) {
                        UIKitPath = devKit;
                    } else {
                        UIKitPath = localKit;
                    }
                }
            } catch (Exception ex) {
                Logger.Log("UI Theme Init error: " + ex.Message);
            }
            swDiscovery.Stop();
            Logger.LogPerf($"QFG5Theme path discovery: {swDiscovery.ElapsedMilliseconds} ms (kit path: {UIKitPath})");
            Logger.Log("UI Theme: Using kit path: " + UIKitPath);

            ButtonIdle = LoadImage("qfg-button.png");
            ButtonPressed = LoadImage("qfg-buttonClicked.png");
            WindowBg = LoadImage("qfg-background.png");
            LeatherTexture = CreateLeatherTexture();
            ItemFrame = Path.Combine(UIKitPath, "qfg-frame.png") != null ? LoadImage("qfg-frame.png") : null;
            PreviewBox = LoadImage("qfg-previewBox.jpg");
            GameLogo = LoadImage("qfg-gameLogo.png");
            ToolkitLogo = LoadImage("qfg-toolkitLogo.png");

            swTheme.Stop();
            Logger.LogStartup($"QFG5Theme initialization: {swTheme.ElapsedMilliseconds} ms");
            Logger.LogResource($"Memory after Theme: {Logger.GetMemorySnapshot()}");
        }

        private static Image LoadImage(string filename) {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try {
                string path = Path.Combine(UIKitPath, filename);
                if (File.Exists(path)) {
                    Image img = Image.FromFile(path);
                    sw.Stop();
                    int bpp = Image.GetPixelFormatSize(img.PixelFormat);
                    long approxBytes = (long)img.Width * img.Height * Math.Max(1, bpp / 8);
                    Logger.LogPerf($"Theme image {filename} loaded: {sw.ElapsedMilliseconds} ms ({img.Width}x{img.Height}, {img.PixelFormat}, ~{approxBytes / 1024} KB decoded)");
                    return img;
                }

                sw.Stop();
                Logger.LogPerf($"Theme image {filename} missing at {path} ({sw.ElapsedMilliseconds} ms)");
            } catch (Exception ex) {
                sw.Stop();
                Logger.Log("UI Theme: Error loading " + filename + ": " + ex.Message);
            }
            return null;
        }

        private static Image CreateLeatherTexture()
        {
            const int size = 384;
            Bitmap texture = new Bitmap(size, size);
            Random random = new Random(5173);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int shade = random.Next(-3, 4);
                    int light = (int)(2 * Math.Sin((double)y / size * Math.PI));
                    texture.SetPixel(x, y, Color.FromArgb(
                        91 + shade + light,
                        59 + shade + light,
                        37 + shade + light));
                }
            }

            return texture;
        }
    }
}
