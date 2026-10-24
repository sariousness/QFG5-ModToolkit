using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QFG5Extractor.Resources.Panorama
{
    public sealed class RoomStudioTab : UserControl
    {
        private readonly ListBox screenList;
        private readonly TextBox searchBox;
        private readonly Label statusLabel;
        private readonly PanoramaProjectionControl projection;

        public RoomStudioTab()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;

            QFG5TableLayoutPanel layout = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(QFG5Layout.ContentPadding)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            QFG5TableLayoutPanel screenLayout = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = QFG5Theme.ColorSurface,
                Padding = new Padding(8)
            };
            screenLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            screenLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            screenLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            screenLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            QFG5Theme.ApplyHeaderStroke(screenLayout);

            Label listHeader = new Label
            {
                Text = "PANORAMA SCREENS",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.SectionHeaderFont,
                TextAlign = ContentAlignment.MiddleLeft
            };
            searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 0, 3)
            };
            screenList = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                BorderStyle = BorderStyle.None,
                IntegralHeight = false
            };
            screenList.SelectedIndexChanged += (s, e) => LoadSelectedScreen();
            searchBox.TextChanged += (s, e) => ReloadScreens();

            Button btnReload = new Button
            {
                Text = "Refresh Screens",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                Margin = new Padding(0, 3, 0, 0)
            };
            btnReload.Click += (s, e) => ReloadScreens();

            screenLayout.Controls.Add(listHeader, 0, 0);
            screenLayout.Controls.Add(searchBox, 0, 1);
            screenLayout.Controls.Add(screenList, 0, 2);
            screenLayout.Controls.Add(btnReload, 0, 3);
            layout.Controls.Add(screenLayout, 0, 0);

            QFG5TableLayoutPanel viewLayout = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(8, 0, 0, 0)
            };
            viewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            viewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            viewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            QFG5TableLayoutPanel viewHeader = new QFG5TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = QFG5Theme.ColorSurface,
                Padding = new Padding(8, 0, 4, 0)
            };
            viewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            viewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            QFG5Theme.ApplyHeaderStroke(viewHeader);
            viewHeader.Controls.Add(new Label
            {
                Text = "ROOM / STUDIO - PANORAMA PREVIEW",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.SectionHeaderFont,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            }, 0, 0);

            Button btnBrowse = new Button
            {
                Text = "Load BMP",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.CaptionFont,
                Margin = new Padding(2)
            };
            btnBrowse.Click += BrowsePanorama;
            viewHeader.Controls.Add(btnBrowse, 1, 0);

            projection = new PanoramaProjectionControl { Dock = DockStyle.Fill };
            statusLabel = new Label
            {
                Text = "Cylindrical preview uses IMG angle metadata when available; room ROM limits and sprites are not applied.",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            viewLayout.Controls.Add(viewHeader, 0, 0);
            viewLayout.Controls.Add(projection, 0, 1);
            viewLayout.Controls.Add(statusLabel, 0, 2);
            layout.Controls.Add(viewLayout, 1, 0);
            Controls.Add(layout);

            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(screenList, "Room views are identified by their numeric view IDs; known room names are shown where available.");
            toolTip.SetToolTip(btnReload, "Refresh the list from Mods/cdn/NOD.");
            toolTip.SetToolTip(btnBrowse, "Load a bitmap. A matching IMG file in Mods/cdn/IMG provides its panorama angle range.");

            SharedState.OnPanoFilesChanged += PanoFilesChanged;
        }

        public void AutoLoad()
        {
            ReloadScreens();
        }

        private void PanoFilesChanged(string preferredBaseName)
        {
            string preferredPath = string.IsNullOrEmpty(preferredBaseName)
                ? projection.SourcePath
                : Path.Combine(SharedState.GetModsPath("cdn", "NOD"), preferredBaseName + ".bmp");
            ReloadScreens(preferredPath);
        }

        private void ReloadScreens(string preferredPath = null)
        {
            if (screenList == null) return;
            if (string.IsNullOrEmpty(preferredPath))
                preferredPath = projection.SourcePath;

            string outputFolder = SharedState.GetModsPath("cdn", "NOD");
            string filter = searchBox.Text.Trim();
            bool scanFailed = false;
            screenList.BeginUpdate();
            try
            {
                screenList.Items.Clear();
                if (Directory.Exists(outputFolder))
                {
                    string[] files = Directory.GetFiles(outputFolder);
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    foreach (string path in files)
                        if (string.Equals(Path.GetExtension(path), ".bmp", StringComparison.OrdinalIgnoreCase) &&
                            (string.IsNullOrEmpty(filter) ||
                             Path.GetFileName(path).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             new PanoramaScreen(path).DisplayName.IndexOf(filter,
                                 StringComparison.OrdinalIgnoreCase) >= 0))
                            screenList.Items.Add(new PanoramaScreen(path));
                }
            }
            catch (Exception ex)
            {
                scanFailed = true;
                statusLabel.Text = "Could not read panorama screens: " + ex.Message;
                Logger.LogError("RoomStudioTab", "Could not enumerate panorama screens: " + ex.Message);
            }
            finally
            {
                screenList.EndUpdate();
            }

            int selectedIndex = -1;
            if (!string.IsNullOrEmpty(preferredPath))
                for (int i = 0; i < screenList.Items.Count; i++)
                    if (string.Equals(((PanoramaScreen)screenList.Items[i]).Path, preferredPath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                        break;
                    }
            if (selectedIndex < 0 && screenList.Items.Count > 0)
                selectedIndex = 0;
            if (selectedIndex >= 0)
                screenList.SelectedIndex = selectedIndex;
            else if (!scanFailed)
                statusLabel.Text = "No panorama BMPs found. Open Pano Editor to extract screens.";
        }

        private void LoadSelectedScreen()
        {
            PanoramaScreen item = screenList.SelectedItem as PanoramaScreen;
            if (item == null) return;

            try
            {
                using (Bitmap bitmap = new Bitmap(item.Path))
                    LoadPanorama(bitmap, item.Path);
                statusLabel.Text = "Loaded " + item.DisplayName + GetAngleStatus(item.Path) +
                    " Drag to pan and scroll to zoom; panning stays within the extracted view.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Could not load panorama: " + ex.Message;
                projection.ClearPanorama();
                Logger.LogError("RoomStudioTab", "Could not load panorama " + item.Path + ": " + ex.Message);
            }
        }

        private void BrowsePanorama(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Panorama images (*.bmp;*.png;*.jpg;*.jpeg)|*.bmp;*.png;*.jpg;*.jpeg|All files (*.*)|*.*",
                Title = "Load panorama screen"
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    using (Bitmap bitmap = new Bitmap(dialog.FileName))
                        LoadPanorama(bitmap, dialog.FileName);
                    statusLabel.Text = "Loaded " + Path.GetFileName(dialog.FileName) +
                        GetAngleStatus(dialog.FileName) +
                        " Drag to pan and scroll to zoom; panning stays within the extracted view.";
                }
                catch (Exception ex)
                {
                    statusLabel.Text = "Could not load panorama: " + ex.Message;
                    Logger.LogError("RoomStudioTab", "Could not load panorama " +
                        dialog.FileName + ": " + ex.Message);
                    MessageBox.Show("Could not load panorama: " + ex.Message,
                        "Room / Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadPanorama(Bitmap bitmap, string path)
        {
            double startAngle = 0;
            double endAngle = 0;
            string imgPath = FindMatchingImageFile(path);
            bool hasAngles = !string.IsNullOrEmpty(imgPath) &&
                PanoConverter.TryReadProjectionAngles(imgPath, out startAngle, out endAngle);
            if (hasAngles)
                projection.SetPanorama(bitmap, path, startAngle, endAngle, true);
            else
                projection.SetPanorama(bitmap, path, -Math.PI, Math.PI, false);
        }

        private string GetAngleStatus(string bitmapPath)
        {
            return string.IsNullOrEmpty(FindMatchingImageFile(bitmapPath))
                ? ". Using a full-panorama estimate; matching IMG metadata was not found."
                : projection.HasImageAngleRange
                    ? ". Using the cylindrical angle range from IMG metadata."
                    : ". IMG angle metadata was unavailable; using a full-panorama estimate.";
        }

        private static string FindMatchingImageFile(string bitmapPath)
        {
            string imageFolder = SharedState.GetModsPath("cdn", "IMG");
            if (!Directory.Exists(imageFolder)) return null;

            string baseName = Path.GetFileNameWithoutExtension(bitmapPath);
            foreach (string path in Directory.GetFiles(imageFolder))
                if (string.Equals(Path.GetExtension(path), ".img", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Path.GetFileNameWithoutExtension(path), baseName,
                        StringComparison.OrdinalIgnoreCase))
                    return path;
            return null;
        }

        private sealed class PanoramaScreen
        {
            public readonly string Path;
            public readonly string Name;
            public readonly string RoomName;
            public readonly int RoomId;
            public readonly string ViewId;
            public string DisplayName
            {
                get
                {
                    string description = string.IsNullOrEmpty(RoomName)
                        ? Name
                        : Name + " - Room " + RoomId + ": " + RoomName;
                    return string.IsNullOrEmpty(ViewId) ? description : description + " (view " + ViewId + ")";
                }
            }

            public PanoramaScreen(string path)
            {
                Path = path;
                Name = System.IO.Path.GetFileName(path);
                int roomId;
                string viewId;
                RoomName = GetRoomInfo(path, out roomId, out viewId);
                RoomId = roomId;
                ViewId = viewId;
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private static string GetRoomInfo(string path, out int roomId, out string viewId)
        {
            string baseName = System.IO.Path.GetFileNameWithoutExtension(path);
            int start = 0;
            while (start < baseName.Length && !char.IsDigit(baseName[start]))
                start++;
            int end = start;
            while (end < baseName.Length && char.IsDigit(baseName[end]))
                end++;

            roomId = 0;
            viewId = null;
            int resourceId;
            if (!int.TryParse(baseName.Substring(start, end - start), out resourceId))
                return string.Empty;

            RoomLocationInfo room;
            if (RoomLocationCatalog.TryGet(resourceId, out room))
            {
                roomId = resourceId;
                return room.Name;
            }

            int possibleRoomId = resourceId / 10;
            if (resourceId >= 1000 && RoomLocationCatalog.TryGet(possibleRoomId, out room))
            {
                roomId = possibleRoomId;
                viewId = resourceId.ToString();
                return room.Name;
            }

            return string.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                SharedState.OnPanoFilesChanged -= PanoFilesChanged;
            base.Dispose(disposing);
        }
    }

    internal sealed class PanoramaProjectionControl : Control
    {
        private const double DegreesToRadians = Math.PI / 180.0;
        private Bitmap source;
        private Bitmap projectionCache;
        private string sourcePath;
        private string projectionError;
        private double startAngleRadians = -Math.PI;
        private double angleSpanRadians = 2.0 * Math.PI;
        private double yawRadians;
        private double pitch;
        private int fieldOfView = 90;
        private bool hasImageAngleRange;
        private bool dragging;
        private Point previousMousePosition;
        private readonly ToolTip cameraToolTip;

        public string SourcePath
        {
            get { return sourcePath; }
        }

        public bool HasImageAngleRange
        {
            get { return hasImageAngleRange; }
        }

        public PanoramaProjectionControl()
        {
            BackColor = QFG5Theme.ColorSurfaceRecessed;
            ForeColor = QFG5Theme.ColorTextPrimary;
            TabStop = true;
            Cursor = Cursors.SizeAll;
            cameraToolTip = new ToolTip();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            cameraToolTip.SetToolTip(this,
                "Drag to pan and scroll to zoom. The camera stays within the extracted panorama.");
        }

        public void SetPanorama(Bitmap bitmap, string path)
        {
            SetPanorama(bitmap, path, -Math.PI, Math.PI, false);
        }

        public void SetPanorama(Bitmap bitmap, string path, double startAngle, double endAngle,
            bool useImageAngleRange)
        {
            if (bitmap == null) throw new ArgumentNullException("bitmap");
            if (double.IsNaN(startAngle) || double.IsInfinity(startAngle) ||
                double.IsNaN(endAngle) || double.IsInfinity(endAngle) || endAngle <= startAngle)
                throw new ArgumentOutOfRangeException("endAngle", "Panorama angles must define a finite, increasing range.");
            Bitmap replacement = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(replacement))
                graphics.DrawImageUnscaled(bitmap, 0, 0);

            if (source != null) source.Dispose();
            source = replacement;
            sourcePath = path;
            startAngleRadians = startAngle;
            angleSpanRadians = endAngle - startAngle;
            hasImageAngleRange = useImageAngleRange;
            yawRadians = startAngleRadians + angleSpanRadians / 2.0;
            pitch = 0;
            fieldOfView = 90;
            ClampCameraToPanorama();
            InvalidateProjection();
        }

        public void ClearPanorama()
        {
            if (source != null)
                source.Dispose();
            source = null;
            sourcePath = null;
            hasImageAngleRange = false;
            InvalidateProjection();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (source == null)
            {
                using (SolidBrush brush = new SolidBrush(QFG5Theme.ColorTextMuted))
                using (StringFormat format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                })
                    e.Graphics.DrawString("Load a panorama screen to view its projection.",
                        QFG5Theme.BodyFont, brush, ClientRectangle, format);
                return;
            }

            if (projectionError != null)
            {
                using (SolidBrush brush = new SolidBrush(QFG5Theme.ColorTextPrimary))
                    e.Graphics.DrawString("Projection failed: " + projectionError,
                        QFG5Theme.BodyFont, brush, new Rectangle(8, 8,
                            Math.Max(1, ClientSize.Width - 16),
                            Math.Max(1, ClientSize.Height - 16)));
                return;
            }

            double scale = Math.Min(1.0, Math.Min(
                800.0 / Math.Max(1, ClientSize.Width),
                600.0 / Math.Max(1, ClientSize.Height)));
            int renderWidth = Math.Max(1, (int)Math.Round(ClientSize.Width * scale));
            int renderHeight = Math.Max(1, (int)Math.Round(ClientSize.Height * scale));
            if (projectionCache == null || projectionCache.Width != renderWidth ||
                projectionCache.Height != renderHeight)
            {
                if (projectionCache != null) projectionCache.Dispose();
                try
                {
                    projectionCache = RenderProjection(renderWidth, renderHeight);
                }
                catch (Exception ex)
                {
                    projectionError = ex.Message;
                    Logger.LogError("RoomStudioTab", "Panorama projection failed: " + ex.Message);
                    using (SolidBrush brush = new SolidBrush(QFG5Theme.ColorTextPrimary))
                        e.Graphics.DrawString("Projection failed: " + projectionError,
                            QFG5Theme.BodyFont, brush, new Rectangle(8, 8,
                                Math.Max(1, ClientSize.Width - 16),
                                Math.Max(1, ClientSize.Height - 16)));
                    return;
                }
            }
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            e.Graphics.DrawImage(projectionCache, ClientRectangle);
            using (SolidBrush overlay = new SolidBrush(Color.FromArgb(175, 0, 0, 0)))
            using (SolidBrush text = new SolidBrush(Color.White))
            {
                e.Graphics.FillRectangle(overlay, 6, 6, 280, 22);
                e.Graphics.DrawString("FOV " + fieldOfView + " | Arc " +
                    ((int)Math.Round(angleSpanRadians / DegreesToRadians)) + " deg | Heading " +
                    ((int)Math.Round(NormalizeYaw(yawRadians / DegreesToRadians))) + " | Tilt " +
                    ((int)Math.Round(pitch)), QFG5Theme.CaptionFont, text, 11, 9);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ClampCameraToPanorama();
            InvalidateProjection();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            dragging = true;
            previousMousePosition = e.Location;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            int deltaX = e.X - previousMousePosition.X;
            int deltaY = e.Y - previousMousePosition.Y;
            previousMousePosition = e.Location;
            yawRadians -= deltaX * 0.35 * DegreesToRadians;
            pitch = Math.Max(-80, Math.Min(80, pitch + deltaY * 0.35));
            ClampCameraToPanorama();
            InvalidateProjection();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                dragging = false;
                Capture = false;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int maximumFieldOfView = GetMaximumFieldOfView();
            int minimumFieldOfView = Math.Min(5, maximumFieldOfView);
            fieldOfView = Math.Max(minimumFieldOfView, Math.Min(maximumFieldOfView,
                fieldOfView - Math.Sign(e.Delta) * 5));
            ClampCameraToPanorama();
            InvalidateProjection();
        }

        private int GetMaximumFieldOfView()
        {
            if (source == null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return 120;

            double horizontalCoverage = Math.Min(angleSpanRadians, 2.0 * Math.PI);
            double verticalCoverage = angleSpanRadians * source.Height / source.Width;
            double widthLimitedFov = horizontalCoverage / DegreesToRadians;
            double heightLimitedFov = 2.0 * Math.Atan(
                verticalCoverage * ClientSize.Width / (2.0 * ClientSize.Height)) /
                DegreesToRadians;
            return Math.Max(1, (int)Math.Floor(Math.Min(120,
                Math.Min(widthLimitedFov, heightLimitedFov))));
        }

        private void ClampCameraToPanorama()
        {
            if (source == null)
                return;

            fieldOfView = Math.Min(fieldOfView, GetMaximumFieldOfView());
            double halfFieldOfView = fieldOfView * DegreesToRadians / 2.0;
            bool wrapsAtSeam = angleSpanRadians >= 2.0 * Math.PI - 0.0001;
            if (wrapsAtSeam)
            {
                double relativeYaw = (yawRadians - startAngleRadians) % (2.0 * Math.PI);
                if (relativeYaw < 0)
                    relativeYaw += 2.0 * Math.PI;
                yawRadians = startAngleRadians + relativeYaw;
            }
            else
            {
                yawRadians = Math.Max(startAngleRadians + halfFieldOfView,
                    Math.Min(startAngleRadians + angleSpanRadians - halfFieldOfView, yawRadians));
            }

            if (ClientSize.Width > 0 && ClientSize.Height > 0)
            {
                double verticalCoverage = angleSpanRadians * source.Height / source.Width;
                double verticalHalfCoverage = Math.Atan(verticalCoverage / 2.0);
                double verticalHalfView = Math.Atan(
                    Math.Tan(halfFieldOfView) * ClientSize.Height / ClientSize.Width);
                double maximumPitch = Math.Min(80,
                    Math.Max(0, (verticalHalfCoverage - verticalHalfView) /
                        DegreesToRadians));
                pitch = Math.Max(-maximumPitch, Math.Min(maximumPitch, pitch));
            }
        }

        private void InvalidateProjection()
        {
            if (projectionCache != null)
            {
                projectionCache.Dispose();
                projectionCache = null;
            }
            projectionError = null;
            Invalidate();
        }

        private Bitmap RenderProjection(int width, int height)
        {
            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            Rectangle sourceBounds = new Rectangle(0, 0, source.Width, source.Height);
            BitmapData sourceData = source.LockBits(sourceBounds, ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            byte[] sourcePixels = new byte[source.Width * source.Height * 4];
            try
            {
                for (int y = 0; y < source.Height; y++)
                    Marshal.Copy(IntPtr.Add(sourceData.Scan0, y * sourceData.Stride),
                        sourcePixels, y * source.Width * 4, source.Width * 4);
            }
            finally
            {
                source.UnlockBits(sourceData);
            }

            byte[] outputPixels = new byte[width * height * 4];
            double horizontalScale = Math.Tan(fieldOfView * DegreesToRadians / 2.0);
            double verticalScale = horizontalScale * height / width;
            // The image is wrapped around a cylinder, so vertical scale follows its texture pixel scale.
            double verticalPanoramaHeight = angleSpanRadians * source.Height / source.Width;
            double pitchRadians = pitch * DegreesToRadians;
            double cosYaw = Math.Cos(yawRadians);
            double sinYaw = Math.Sin(yawRadians);
            double cosPitch = Math.Cos(pitchRadians);
            double sinPitch = Math.Sin(pitchRadians);
            bool wrapsAtSeam = angleSpanRadians >= 2.0 * Math.PI - 0.0001;

            for (int y = 0; y < height; y++)
            {
                double viewY = (1.0 - 2.0 * (y + 0.5) / height) * verticalScale;
                for (int x = 0; x < width; x++)
                {
                    int destination = (y * width + x) * 4;
                    outputPixels[destination + 3] = 255;
                    double viewX = (2.0 * (x + 0.5) / width - 1.0) * horizontalScale;
                    double directionX = viewX * cosYaw + sinYaw;
                    double yawDepth = -viewX * sinYaw + cosYaw;
                    double directionY = viewY * cosPitch - yawDepth * sinPitch;
                    double directionZ = viewY * sinPitch + yawDepth * cosPitch;
                    double relativeAngle = (Math.Atan2(directionX, directionZ) -
                        startAngleRadians) % (2.0 * Math.PI);
                    if (relativeAngle < 0) relativeAngle += 2.0 * Math.PI;
                    double sourceX = relativeAngle / angleSpanRadians * source.Width;
                    double verticalPosition = directionY /
                        Math.Sqrt(directionX * directionX + directionZ * directionZ);
                    double sourceY = (0.5 - verticalPosition / verticalPanoramaHeight) * source.Height;

                    if (wrapsAtSeam)
                        sourceX = (sourceX % source.Width + source.Width) % source.Width;
                    else if (sourceX < 0 || sourceX >= source.Width)
                        continue;
                    if (sourceY < 0 || sourceY >= source.Height)
                        continue;

                    int x0 = (int)Math.Floor(sourceX);
                    int x1 = wrapsAtSeam
                        ? (x0 + 1) % source.Width
                        : Math.Min(source.Width - 1, x0 + 1);
                    int y0 = (int)Math.Floor(sourceY);
                    int y1 = Math.Min(source.Height - 1, y0 + 1);
                    double fractionX = sourceX - Math.Floor(sourceX);
                    double fractionY = sourceY - y0;
                    int p00 = (y0 * source.Width + x0) * 4;
                    int p10 = (y0 * source.Width + x1) * 4;
                    int p01 = (y1 * source.Width + x0) * 4;
                    int p11 = (y1 * source.Width + x1) * 4;
                    for (int channel = 0; channel < 4; channel++)
                    {
                        double top = sourcePixels[p00 + channel] * (1.0 - fractionX)
                            + sourcePixels[p10 + channel] * fractionX;
                        double bottom = sourcePixels[p01 + channel] * (1.0 - fractionX)
                            + sourcePixels[p11 + channel] * fractionX;
                        outputPixels[destination + channel] =
                            (byte)Math.Round(top * (1.0 - fractionY) + bottom * fractionY);
                    }
                }
            }

            BitmapData resultData = result.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < height; y++)
                    Marshal.Copy(outputPixels, y * width * 4,
                        IntPtr.Add(resultData.Scan0, y * resultData.Stride), width * 4);
            }
            finally
            {
                result.UnlockBits(resultData);
            }
            return result;
        }

        private static double NormalizeYaw(double value)
        {
            value %= 360;
            if (value < 0) value += 360;
            return value;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (source != null) source.Dispose();
                if (projectionCache != null) projectionCache.Dispose();
                cameraToolTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
