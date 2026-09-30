using System;
using System.Drawing;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using QFG5Extractor.Core.UI;
using QFG5Extractor.Resources.Model;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace QFG5Extractor.Core
{
    public class TextureEditorTab : UserControl
    {
        private Bitmap currentBitmap;
        private PictureBox canvas;
        private Color drawColor = Color.White;
        private string currentTool = "Pencil";
        private string loadedMdlPath = string.Empty;
        private float zoomLevel = 1.0f;
        private int brushSize = 1;
        
        // UI variables
        private Label lblMousePos;
        private Label lblDimensions;
        private Label lblStatus;
        private Panel colorPreview;
        private List<Point> selection = new List<Point>();
        private TrackBar trkZoom;
        private Label lblZoomPct;
        
        public TextureEditorTab()
        {
            Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;
            InitializeComponent();
            NewTexture(64, 64);

            SharedState.OnEditTextureRequested += (bmp) => {
                if (currentBitmap != null) currentBitmap.Dispose();
                currentBitmap = bmp;
                if (canvas != null) canvas.Invalidate();
            };

            SharedState.OnEditModelRequested += (mdlPath) => {
                try {
                    OldMdlViewerLogic logic = new OldMdlViewerLogic();
                    var bmps = logic.getAllBMPs(mdlPath);
                    if (bmps.Count > 0) {
                        if (currentBitmap != null) currentBitmap.Dispose();
                        currentBitmap = new Bitmap(bmps[0].Width, bmps[0].Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        using(Graphics g = Graphics.FromImage(currentBitmap)) { g.DrawImage(bmps[0], 0, 0); }
                        loadedMdlPath = mdlPath;
                        foreach (var b in bmps) b.Dispose();
                        if (canvas != null) canvas.Invalidate();
                        SetStatus($"Loaded MDL: {Path.GetFileName(mdlPath)}");
                    }
                } catch { }
            };
        }

        private void InitializeComponent()
        {
            QFG5TableLayoutPanel mainLayout = new QFG5TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(15) };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Editor side
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380)); // Tool/Preview side

            // --- Left Side: Editor Canvas ---
            QFG5TableLayoutPanel editorLayout = new QFG5TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            FlowLayoutPanel canvasToolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            QFG5Button btnNew = new QFG5Button { Text = "New (64x64)", Width = 100, Height = 30 };
            btnNew.Click += (s, e) => NewTexture(64, 64);
            QFG5Button btnOpen = new QFG5Button { Text = "Open", Width = 80, Height = 30 };
            btnOpen.Click += BtnOpen_Click;
            QFG5Button btnSave = new QFG5Button { Text = "Save", Width = 80, Height = 30 };
            btnSave.Click += BtnSave_Click;
            QFG5Button btnInject = new QFG5Button { Text = "Inject", Width = 80, Height = 30, ForeColor = Color.Gold };
            btnInject.Click += BtnInject_Click;
            QFG5Button btnOpenMdl = new QFG5Button { Text = "Open MDL", Width = 90, Height = 30, ForeColor = Color.LightSkyBlue };
            btnOpenMdl.Click += BtnOpenMdl_Click;

            canvasToolbar.Controls.AddRange(new Control[] { btnNew, btnOpen, btnOpenMdl, btnSave, btnInject });
            editorLayout.Controls.Add(canvasToolbar, 0, 0);

            QFG5PreviewFrame canvasFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            canvas = new PictureBox { 
                Dock = DockStyle.Fill, 
                SizeMode = PictureBoxSizeMode.Zoom, 
                BackColor = Color.FromArgb(20, 20, 25),
                Cursor = Cursors.Cross
            };
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.Paint += Canvas_Paint;
            canvasFrame.Controls.Add(canvas);
            editorLayout.Controls.Add(canvasFrame, 0, 1);

            // Bottom Palette
            FlowLayoutPanel bottomPalette = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Height = 40, WrapContents = false };
            Color[] paintColors = { Color.Black, Color.White, Color.Red, Color.Green, Color.Blue, Color.Yellow, Color.Orange, Color.Purple, Color.Cyan, Color.Magenta, Color.Brown, Color.Gray, Color.LightGray, Color.DarkRed, Color.DarkGreen, Color.DarkBlue };
            foreach(var c in paintColors) {
                Panel p = new Panel { Width = 25, Height = 25, BackColor = c, Margin = new Padding(2), BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand };
                p.Click += (s, e) => SetColor(((Panel)s).BackColor);
                bottomPalette.Controls.Add(p);
            }

            // Status Info (single status label for the tab)
            QFG5FlowLayoutPanel statusInfo = new QFG5FlowLayoutPanel { Dock = DockStyle.Fill, Height = 25 };
            lblMousePos = new Label { Text = "0, 0", ForeColor = Color.LightGray, AutoSize = true, Font = new Font("Arial", 8) };
            lblDimensions = new Label { Text = "64 x 64", ForeColor = Color.LightGray, AutoSize = true, Margin = new Padding(20, 0, 0, 0), Font = new Font("Arial", 8) };
            lblStatus = new Label { Text = "Ready", ForeColor = Color.Gold, AutoSize = true, Margin = new Padding(20, 0, 0, 0), Font = new Font("Arial", 8) };
            statusInfo.Controls.Add(lblMousePos);
            statusInfo.Controls.Add(lblDimensions);
            statusInfo.Controls.Add(lblStatus);

            QFG5TableLayoutPanel footerLayout = new QFG5TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            footerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            footerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            footerLayout.Controls.Add(bottomPalette, 0, 0);
            footerLayout.Controls.Add(statusInfo, 0, 1);

            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
            editorLayout.Controls.Add(footerLayout, 0, 2);

            // --- Right Side: Tools ---
            QFG5TableLayoutPanel toolLayout = new QFG5TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, Padding = new Padding(10, 0, 0, 0) };
            toolLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            QFG5PreviewFrame toolFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            FlowLayoutPanel toolPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true };

            Func<string, Label> section = (title) => new Label {
                Text = title,
                ForeColor = Color.Gold,
                Font = new Font(this.Font, FontStyle.Bold),
                Width = 150,
                Margin = new Padding(0, 8, 0, 4)
            };

            toolPanel.Controls.Add(section("DRAW"));
            QFG5Button btnPencil = new QFG5Button { Text = "Pencil", Width = 110, Height = 30 };
            btnPencil.Click += (s, e) => { currentTool = "Pencil"; SetStatus("Tool: Pencil"); };
            QFG5Button btnEraser = new QFG5Button { Text = "Eraser", Width = 110, Height = 30 };
            btnEraser.Click += (s, e) => { currentTool = "Eraser"; SetStatus("Tool: Eraser"); };
            QFG5Button btnFill = new QFG5Button { Text = "Fill", Width = 110, Height = 30 };
            btnFill.Click += (s, e) => { currentTool = "Fill"; SetStatus("Tool: Fill"); };
            QFG5Button btnPipette = new QFG5Button { Text = "Pipette", Width = 110, Height = 30, ForeColor = Color.Gold };
            btnPipette.Click += (s, e) => { currentTool = "Pipette"; SetStatus("Tool: Pipette (Pick color)"); };
            toolPanel.Controls.AddRange(new Control[] { btnPencil, btnEraser, btnPipette, btnFill });

            toolPanel.Controls.Add(section("SELECT"));
            QFG5Button btnMagicWand = new QFG5Button { Text = "Magic Wand", Width = 110, Height = 30 };
            btnMagicWand.Click += (s, e) => { currentTool = "MagicWand"; SetStatus("Tool: Magic Wand (Select similar colors)"); };
            QFG5Button btnSelectCluster = new QFG5Button { Text = "Select Color", Width = 110, Height = 30 };
            btnSelectCluster.Click += (s, e) => { currentTool = "SelectColor"; SetStatus("Tool: Select identical color clusters"); };
            QFG5Button btnClearDeselect = new QFG5Button { Text = "Clear Select", Width = 110, Height = 30 };
            btnClearDeselect.Click += (s, e) => { selection.Clear(); canvas.Invalidate(); SetStatus("Selection cleared"); };
            toolPanel.Controls.AddRange(new Control[] { btnMagicWand, btnSelectCluster, btnClearDeselect });

            toolPanel.Controls.Add(section("ADJUST"));
            QFG5Button btnHueSat = new QFG5Button { Text = "Hue/Sat", Width = 110, Height = 30, ForeColor = Color.LightSkyBlue };
            btnHueSat.Click += (s, e) => ShowHueSatDialog();
            QFG5Button btnOverlay = new QFG5Button { Text = "Add Overlay", Width = 110, Height = 30, ForeColor = Color.LightGreen };
            btnOverlay.Click += (s, e) => AddOverlay();
            toolPanel.Controls.AddRange(new Control[] { btnHueSat, btnOverlay });

            toolPanel.Controls.Add(new Label { Text = "ZOOM", ForeColor = Color.White, Width = 150, Margin = new Padding(0, 10, 0, 0) });
            QFG5TableLayoutPanel zoomRow = new QFG5TableLayoutPanel { Width = 150, Height = 40, ColumnCount = 3, RowCount = 1 };
            zoomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 35));
            zoomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            zoomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 35));
            QFG5Button btnZoomOut = new QFG5Button { Text = "-", Width = 30, Height = 30 };
            QFG5Button btnZoomIn = new QFG5Button { Text = "+", Width = 30, Height = 30 };
            TrackBar trackZoom = new TrackBar { Minimum = 1, Maximum = 16, Value = (int)zoomLevel, Width = 80 };
            lblZoomPct = new Label { Text = "100%", ForeColor = Color.White, Width = 40, TextAlign = ContentAlignment.MiddleCenter };

            btnZoomOut.Click += (s, e) => { if (trackZoom.Value > trackZoom.Minimum) trackZoom.Value--; };
            btnZoomIn.Click += (s, e) => { if (trackZoom.Value < trackZoom.Maximum) trackZoom.Value++; };
            trackZoom.ValueChanged += (s, e) => {
                zoomLevel = trackZoom.Value;
                lblZoomPct.Text = $"{(int)(zoomLevel * 100)}%";
                canvas.Invalidate();
            };
            zoomRow.Controls.Add(btnZoomOut, 0, 0);
            zoomRow.Controls.Add(trackZoom, 1, 0);
            zoomRow.Controls.Add(btnZoomIn, 2, 0);
            toolPanel.Controls.Add(zoomRow);
            toolPanel.Controls.Add(lblZoomPct);

            toolPanel.Controls.Add(new Label { Text = "BRUSH SIZE", ForeColor = Color.White, Width = 150, Margin = new Padding(0, 5, 0, 0) });
            NumericUpDown numBrush = new NumericUpDown { Minimum = 1, Maximum = 50, Value = brushSize, Width = 110, BackColor = Color.Black, ForeColor = Color.White };
            numBrush.ValueChanged += (s, e) => { brushSize = (int)numBrush.Value; };
            toolPanel.Controls.Add(numBrush);

            toolPanel.Controls.Add(new Label { Text = "COLOR", ForeColor = Color.Gold, Width = 150, Margin = new Padding(0, 10, 0, 0) });
            Panel colorPreview = new Panel { Width = 150, Height = 30, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
            colorPreview.Click += (s, e) => {
                using (ColorDialog cd = new ColorDialog()) if (cd.ShowDialog() == DialogResult.OK) { SetColor(cd.Color); }
            };
            toolPanel.Controls.Add(colorPreview);
            this.colorPreview = colorPreview;

            FlowLayoutPanel colorGrid = new FlowLayoutPanel { Width = 340, Height = 90, BackColor = Color.Transparent, Padding = new Padding(5) };
            Color[] palette = new Color[] {
                Color.Black, Color.FromArgb(40,40,40), Color.Gray, Color.Silver, Color.White,
                Color.DarkRed, Color.Red, Color.Orange, Color.Gold, Color.Yellow,
                Color.DarkGreen, Color.Green, Color.Lime, Color.Cyan, Color.Teal,
                Color.Navy, Color.Blue, Color.RoyalBlue, Color.Purple, Color.Magenta,
                Color.Brown, Color.SaddleBrown, Color.Tan, Color.Khaki, Color.PaleGoldenrod
            };
            foreach (var c in palette) {
                Panel p = new Panel { Width = 28, Height = 28, BackColor = c, Margin = new Padding(3), Cursor = Cursors.Hand, BorderStyle = BorderStyle.FixedSingle };
                p.Click += (s, e) => SetColor(((Panel)s).BackColor);
                p.MouseEnter += (s, e) => { ((Panel)s).BorderStyle = BorderStyle.Fixed3D; };
                p.MouseLeave += (s, e) => { ((Panel)s).BorderStyle = BorderStyle.FixedSingle; };
                colorGrid.Controls.Add(p);
            }
            toolPanel.Controls.Add(colorGrid);

            toolFrame.Controls.Add(toolPanel);
            toolLayout.Controls.Add(toolFrame, 0, 0);

            mainLayout.Controls.Add(editorLayout, 0, 0);
            mainLayout.Controls.Add(toolLayout, 1, 0);

            this.Controls.Add(mainLayout);
        }

        private int lastMouseX, lastMouseY;

        private void NewTexture(int w, int h)
        {
            if (currentBitmap != null) currentBitmap.Dispose();
            currentBitmap = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(currentBitmap)) g.Clear(Color.Transparent);
            loadedMdlPath = string.Empty;
            if (lblDimensions != null) lblDimensions.Text = $"{w} x {h}";
            canvas.Invalidate();
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e) 
        { 
            DrawAt(e.X, e.Y); 
        }
        
        private void Canvas_MouseMove(object sender, MouseEventArgs e) 
        { 
            UpdateMouseLabel(e.X, e.Y);
            if (e.Button == MouseButtons.Left) DrawAt(e.X, e.Y); 
        }

        private void UpdateMouseLabel(int x, int y)
        {
            if (currentBitmap == null) return;
            float canvasAspect = (float)canvas.Width / canvas.Height;
            float imageAspect = (float)currentBitmap.Width / currentBitmap.Height;
            int imgX, imgY;
            float actualScale;
            if (imageAspect > canvasAspect) {
                actualScale = ((float)canvas.Width / currentBitmap.Width) * zoomLevel;
                float vOffset = (canvas.Height - currentBitmap.Height * actualScale) / 2;
                imgX = (int)(x / actualScale);
                imgY = (int)((y - vOffset) / actualScale);
            } else {
                actualScale = ((float)canvas.Height / currentBitmap.Height) * zoomLevel;
                float hOffset = (canvas.Width - currentBitmap.Width * actualScale) / 2;
                imgX = (int)((x - hOffset) / actualScale);
                imgY = (int)(y / actualScale);
            }
            if (imgX >= 0 && imgX < currentBitmap.Width && imgY >= 0 && imgY < currentBitmap.Height) {
                lblMousePos.Text = $"{imgX}, {imgY}";
            }
        }

        private void DrawAt(int x, int y)
        {
            if (currentBitmap == null) return;
            
            float canvasAspect = (float)canvas.Width / canvas.Height;
            float imageAspect = (float)currentBitmap.Width / currentBitmap.Height;
            
            int imgX, imgY;
            float actualScale;
            if (imageAspect > canvasAspect) {
                actualScale = ((float)canvas.Width / currentBitmap.Width) * zoomLevel;
                float vOffset = (canvas.Height - currentBitmap.Height * actualScale) / 2;
                imgX = (int)(x / actualScale);
                imgY = (int)((y - vOffset) / actualScale);
            } else {
                actualScale = ((float)canvas.Height / currentBitmap.Height) * zoomLevel;
                float hOffset = (canvas.Width - currentBitmap.Width * actualScale) / 2;
                imgX = (int)((x - hOffset) / actualScale);
                imgY = (int)(y / actualScale);
            }

            if (imgX >= 0 && imgX < currentBitmap.Width && imgY >= 0 && imgY < currentBitmap.Height) {
                if (currentTool == "Pencil") {
                    using(Graphics g = Graphics.FromImage(currentBitmap)) {
                        g.FillRectangle(new SolidBrush(drawColor), imgX - brushSize/2, imgY - brushSize/2, brushSize, brushSize);
                    }
                }
                else if (currentTool == "Eraser") {
                    using(Graphics g = Graphics.FromImage(currentBitmap)) {
                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        g.FillRectangle(new SolidBrush(Color.Transparent), imgX - brushSize/2, imgY - brushSize/2, brushSize, brushSize);
                    }
                }
                else if (currentTool == "Fill") FloodFill(imgX, imgY, drawColor);
                else if (currentTool == "MagicWand") MagicWandSelect(imgX, imgY);
                else if (currentTool == "SelectColor") SelectColorCluster(imgX, imgY);
                else if (currentTool == "Pipette") SetColor(currentBitmap.GetPixel(imgX, imgY));
                
                canvas.Invalidate();
            }
        }

        private void SetColor(Color c) { drawColor = c; if (colorPreview != null) colorPreview.BackColor = c; }
        private void SetStatus(string s) { if (lblStatus != null) lblStatus.Text = s; }

        private void MagicWandSelect(int x, int y)
        {
            Color target = currentBitmap.GetPixel(x, y);
            selection.Clear();
            bool[,] visited = new bool[currentBitmap.Width, currentBitmap.Height];
            System.Collections.Generic.Stack<Point> stack = new System.Collections.Generic.Stack<Point>();
            stack.Push(new Point(x, y));

            while (stack.Count > 0) {
                Point p = stack.Pop();
                if (p.X < 0 || p.X >= currentBitmap.Width || p.Y < 0 || p.Y >= currentBitmap.Height) continue;
                if (visited[p.X, p.Y]) continue;
                
                Color c = currentBitmap.GetPixel(p.X, p.Y);
                if (ColorDiff(c, target) < 15) { // Tolerance
                    visited[p.X, p.Y] = true;
                    selection.Add(p);
                    stack.Push(new Point(p.X + 1, p.Y));
                    stack.Push(new Point(p.X - 1, p.Y));
                    stack.Push(new Point(p.X, p.Y + 1));
                    stack.Push(new Point(p.X, p.Y - 1));
                }
            }
            SetStatus($"Selected {selection.Count} pixels.");
        }

        private void SelectColorCluster(int x, int y)
        {
            Color target = currentBitmap.GetPixel(x, y);
            selection.Clear();
            for (int ix = 0; ix < currentBitmap.Width; ix++) {
                for (int iy = 0; iy < currentBitmap.Height; iy++) {
                    if (ColorDiff(currentBitmap.GetPixel(ix, iy), target) < 5) {
                        selection.Add(new Point(ix, iy));
                    }
                }
            }
            SetStatus($"Selected {selection.Count} pixels (global).");
        }

        private float ColorDiff(Color c1, Color c2) {
            return Math.Abs(c1.R - c2.R) + Math.Abs(c1.G - c2.G) + Math.Abs(c1.B - c2.B);
        }

        private void ShowHueSatDialog()
        {
            if (currentBitmap == null) return;
            using (Form f = new Form { Text = "Hue / Saturation", Width = 300, Height = 200, StartPosition = FormStartPosition.CenterParent }) {
                TrackBar trkHue = new TrackBar { Minimum = -180, Maximum = 180, Value = 0, Dock = DockStyle.Top };
                Label lblHue = new Label { Text = "Hue Adjust:", Dock = DockStyle.Top };
                TrackBar trkSat = new TrackBar { Minimum = -100, Maximum = 100, Value = 0, Dock = DockStyle.Top };
                Label lblSat = new Label { Text = "Saturation Adjust:", Dock = DockStyle.Top };
                Button btnApply = new Button { Text = "Apply", Dock = DockStyle.Bottom };
                
                btnApply.Click += (s, e) => {
                    ApplyHSL(trkHue.Value, trkSat.Value / 100.0f);
                    f.Close();
                };
                
                f.Controls.AddRange(new Control[] { btnApply, trkSat, lblSat, trkHue, lblHue });
                f.ShowDialog();
            }
        }

        private void ApplyHSL(float hueShift, float satShift)
        {
            if (selection.Count > 0) {
                foreach (var p in selection) AdjustPixelHSL(p.X, p.Y, hueShift, satShift);
            } else {
                for (int x = 0; x < currentBitmap.Width; x++)
                    for (int y = 0; y < currentBitmap.Height; y++)
                        AdjustPixelHSL(x, y, hueShift, satShift);
            }
            canvas.Invalidate();
        }

        private void AdjustPixelHSL(int x, int y, float hShift, float sAdjust)
        {
            Color c = currentBitmap.GetPixel(x, y);
            if (c.A == 0) return;
            // Simplified HSL adjustment
            float h, s, l;
            ColorToHSL(c, out h, out s, out l);
            h = (h + hShift) % 360; if (h < 0) h += 360;
            s = Math.Min(1, Math.Max(0, s + sAdjust));
            currentBitmap.SetPixel(x, y, HSLToColor(h, s, l, c.A));
        }

        private void ColorToHSL(Color c, out float h, out float s, out float l) {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;
            if (max == min) h = s = 0;
            else {
                float d = max - min;
                s = l > 0.5f ? d / (2 - max - min) : d / (max + min);
                if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g) h = (b - r) / d + 2;
                else h = (r - g) / d + 4;
                h *= 60;
            }
        }

        private Color HSLToColor(float h, float s, float l, int alpha) {
            float r, g, b;
            if (s == 0) r = g = b = l;
            else {
                float q = l < 0.5f ? l * (1 + s) : l + s - l * s;
                float p = 2 * l - q;
                r = HueToRGB(p, q, h / 360 + 1f/3);
                g = HueToRGB(p, q, h / 360);
                b = HueToRGB(p, q, h / 360 - 1f/3);
            }
            return Color.FromArgb(alpha, (int)(r * 255), (int)(g * 255), (int)(b * 255));
        }
        private float HueToRGB(float p, float q, float t) {
            if (t < 0) t += 1; if (t > 1) t -= 1;
            if (t < 1f/6) return p + (q - p) * 6 * t;
            if (t < 1f/2) return q;
            if (t < 2f/3) return p + (q - p) * (2f/3 - t) * 6;
            return p;
        }

        private void AddOverlay()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "PNG with transparency|*.png" }) {
                if (ofd.ShowDialog() == DialogResult.OK) {
                    using (Bitmap overlay = new Bitmap(ofd.FileName)) {
                        using (Graphics g = Graphics.FromImage(currentBitmap)) {
                            g.DrawImage(overlay, new Rectangle(0, 0, currentBitmap.Width, currentBitmap.Height));
                        }
                    }
                    canvas.Invalidate();
                }
            }
        }

        private void FloodFill(int x, int y, Color color)
        {
            Color targetColor = currentBitmap.GetPixel(x, y);
            if (targetColor.ToArgb() == color.ToArgb()) return;
            
            System.Collections.Generic.Stack<Point> pixels = new System.Collections.Generic.Stack<Point>();
            pixels.Push(new Point(x, y));
            
            while (pixels.Count > 0)
            {
                Point a = pixels.Pop();
                if (a.X < 0 || a.X >= currentBitmap.Width || a.Y < 0 || a.Y >= currentBitmap.Height) continue;
                if (currentBitmap.GetPixel(a.X, a.Y).ToArgb() == targetColor.ToArgb())
                {
                    currentBitmap.SetPixel(a.X, a.Y, color);
                    pixels.Push(new Point(a.X - 1, a.Y));
                    pixels.Push(new Point(a.X + 1, a.Y));
                    pixels.Push(new Point(a.X, a.Y - 1));
                    pixels.Push(new Point(a.X, a.Y + 1));
                }
            }
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            if (currentBitmap == null) return;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            
            float actualScale;
            float hOffset = 0, vOffset = 0;
            float canvasAspect = (float)canvas.Width / canvas.Height;
            float imageAspect = (float)currentBitmap.Width / currentBitmap.Height;
            
            if (imageAspect > canvasAspect) {
                actualScale = ((float)canvas.Width / currentBitmap.Width) * zoomLevel;
                vOffset = (canvas.Height - currentBitmap.Height * actualScale) / 2;
            } else {
                actualScale = ((float)canvas.Height / currentBitmap.Height) * zoomLevel;
                hOffset = (canvas.Width - currentBitmap.Width * actualScale) / 2;
            }

            e.Graphics.DrawImage(currentBitmap, new Rectangle((int)hOffset, (int)vOffset, (int)(currentBitmap.Width * actualScale), (int)(currentBitmap.Height * actualScale)));

            if (selection.Count > 0) {
                using (SolidBrush selectionBrush = new SolidBrush(Color.FromArgb(100, Color.PaleGoldenrod))) {
                    foreach (var p in selection) {
                        e.Graphics.FillRectangle(selectionBrush, hOffset + p.X * actualScale, vOffset + p.Y * actualScale, actualScale, actualScale);
                    }
                }
            }
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files|*.bmp;*.png;*.jpg" })
                if (ofd.ShowDialog() == DialogResult.OK) {
                    if (currentBitmap != null) currentBitmap.Dispose();
                    Bitmap loaded = new Bitmap(ofd.FileName);
                    currentBitmap = new Bitmap(loaded.Width, loaded.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using(Graphics g = Graphics.FromImage(currentBitmap)) { g.DrawImage(loaded, 0, 0); }
                    loaded.Dispose();
                    loadedMdlPath = string.Empty;
                    canvas.Invalidate();
                }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "PNG Image|*.png|BMP Image|*.bmp" })
                if (sfd.ShowDialog() == DialogResult.OK) currentBitmap.Save(sfd.FileName);
        }

        private void BtnInject_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(loadedMdlPath) || !File.Exists(loadedMdlPath)) {
                MessageBox.Show("Please open a texture from an MDL file first to inject it.", "Texture Editor");
                return;
            }
            try {
                if (string.IsNullOrEmpty(loadedMdlPath)) throw new Exception("Please open a texture from an MDL file first.");

                // 1. Extract original palette from MDL
                byte[] mdlPalette = ExtractMdlPalette(loadedMdlPath);
                
                // 2. Convert current canvas to 8-bit using the nearest match in that palette
                byte[] pixelData = ConvertTo8BitWithPalette(currentBitmap, mdlPalette);
                
                // 3. Save as Game-Compatible BMP
                string tempBmp = Path.Combine(Path.GetTempPath(), "qfg5_temp_inject.bmp");
                WriteGameBmp(tempBmp, mdlPalette, pixelData, currentBitmap.Width, currentBitmap.Height);
                
                // 4. Inject
                ModelConverter.importOrExportBitmapOrMesh(tempBmp, loadedMdlPath, false, true, false, false);
                
                if (File.Exists(tempBmp)) File.Delete(tempBmp);
                
                MessageBox.Show("Texture injected successfully with palette preservation!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception ex) {
                MessageBox.Show("Error injecting texture: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private byte[] ExtractMdlPalette(string mdlPath)
        {
            byte[] palette = new byte[Constants.BITMAP_PALETTE_LENGTH];
            using (BinaryReader br = new BinaryReader(File.Open(mdlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                br.BaseStream.Seek(Constants.MDL_HEADER_BITMAPPALETTE_ADDRESS, SeekOrigin.Begin);
                palette = br.ReadBytes(palette.Length);
            }
            return palette;
        }

        private byte[] ConvertTo8BitWithPalette(Bitmap source, byte[] rawPalette)
        {
            // rawPalette is in game format (RGBA order usually, 1019 bytes)
            // We need to convert it to a searchable list of Colors
            List<Color> paletteEntries = new List<Color>();
            for (int i = 0; i < rawPalette.Length - 3; i += 4)
            {
                // Game stores palette in BGRA or RGBA? qfg5modelBitmap.swapPalette implies swap
                // Let's look at swapPalette again: tmp[i] = pal[i+2], tmp[i+1]=pal[i+1], tmp[i+2]=pal[i]
                // That's swapping R and B.
                paletteEntries.Add(Color.FromArgb(rawPalette[i+2], rawPalette[i+1], rawPalette[i]));
            }

            int w = source.Width;
            int h = source.Height;
            byte[] pixelData = new byte[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = source.GetPixel(x, y);
                    if (c.A < 128) {
                        pixelData[y * w + x] = 0; // Assume 0 is transparency index
                        continue;
                    }

                    // Nearest color search
                    int bestIndex = 0;
                    int minDiff = int.MaxValue;
                    for (int i = 0; i < paletteEntries.Count; i++)
                    {
                        Color p = paletteEntries[i];
                        int dr = c.R - p.R;
                        int dg = c.G - p.G;
                        int db = c.B - p.B;
                        int diff = dr*dr + dg*dg + db*db;
                        if (diff < minDiff) {
                            minDiff = diff;
                            bestIndex = i;
                        }
                    }
                    pixelData[y * w + x] = (byte)bestIndex;
                }
            }
            return pixelData;
        }

        private void WriteGameBmp(string path, byte[] rawPalette, byte[] pixelData, int w, int h)
        {
            using (BinaryWriter bw = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                bw.Write(new byte[] { 0x42, 0x4d }); // 'BM'
                bw.Write(54 + (rawPalette.Length + 5) + pixelData.Length);
                bw.Write((ushort)0);
                bw.Write((ushort)0);
                bw.Write(54 + (rawPalette.Length + 5));
                bw.Write(40); // header size
                bw.Write(w);
                bw.Write(h);
                bw.Write((ushort)1);
                bw.Write((ushort)8); // 8-bit
                bw.Write(0);
                bw.Write(w * h);
                bw.Write(2834);
                bw.Write(2834);
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                bw.Write(rawPalette);
                bw.Write((byte)0); // padding
                bw.Write(pixelData);
            }
        }

        // --- MDL Integration ---
        private void BtnOpenMdl_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "MDL Files|*.mdl" }) {
                if (ofd.ShowDialog() == DialogResult.OK) {
                    try {
                        OldMdlViewerLogic logic = new OldMdlViewerLogic();
                        var bmps = logic.getAllBMPs(ofd.FileName);
                        if (bmps.Count > 0) {
                            if (currentBitmap != null) currentBitmap.Dispose();
                            currentBitmap = new Bitmap(bmps[0].Width, bmps[0].Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            using(Graphics g = Graphics.FromImage(currentBitmap)) { g.DrawImage(bmps[0], 0, 0); }
                            loadedMdlPath = ofd.FileName;
                            if (lblDimensions != null) lblDimensions.Text = $"{currentBitmap.Width} x {currentBitmap.Height}";
                            foreach (var b in bmps) b.Dispose();
                            canvas.Invalidate();
                            SetStatus($"Loaded texture from {Path.GetFileName(ofd.FileName)}");
                        } else {
                            MessageBox.Show("No embedded textures found in this MDL.");
                        }
                    } catch (Exception ex) {
                        MessageBox.Show("Error loading MDL: " + ex.Message);
                    }
                }
            }
        }

        private Bitmap ConvertTo8BitIndexed(Bitmap source)
        {
            int width = source.Width;
            int height = source.Height;
            Bitmap dest = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            
            // Build a simple 256 color palette (Grey + some colors) or just adapt from source
            ColorPalette pal = dest.Palette;
            for (int i = 0; i < 256; i++) pal.Entries[i] = Color.FromArgb(i, i, i);
            
            // Try to use common game colors for the first 16 if possible (simple heuristic)
            // But for now, we'll just do a standard grayscale palette for stability
            // Or ideally, we should extract the MDL's own palette.
            // TODO: Extract MDL palette here. For now, we'll use a standard web-safe palette.
            dest.Palette = pal;

            BitmapData data = dest.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            byte[] pixels = new byte[data.Stride * height];
            
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    Color c = source.GetPixel(x, y);
                    // Simple grayscale conversion for indexing
                    int gray = (int)(c.R * 0.3 + c.G * 0.59 + c.B * 0.11);
                    pixels[y * data.Stride + x] = (byte)gray;
                }
            }
            
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            dest.UnlockBits(data);
            return dest;
        }
    }
}
