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
        // ── Editing state (untouched semantics) ───────────────────────────────
        private Bitmap currentBitmap;
        private PictureBox canvas;
        private Color drawColor = Color.White;
        private string currentTool = "Pencil";
        private string loadedMdlPath = string.Empty;
        private float zoomLevel = 1.0f;
        private int brushSize = 1;
        private List<Point> selection = new List<Point>();

        // ── UI references ─────────────────────────────────────────────────────
        private Label lblMousePos;
        private Label lblDimensions;
        private Label lblStatus;
        private Label lblZoomPct;
        private Panel colorPreview;

        // ── Tool buttons (kept for active-state indication) ───────────────────
        private QFG5Button btnPencil;
        private QFG5Button btnEraser;
        private QFG5Button btnFill;
        private QFG5Button btnPipette;
        private QFG5Button btnMagicWand;
        private QFG5Button btnSelectColor;

        // ── TrackBar ──────────────────────────────────────────────────────────
        private TrackBar trackZoom;

        // ─────────────────────────────────────────────────────────────────────
        public TextureEditorTab()
        {
            Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;
            InitializeComponent();

            SharedState.OnEditTextureRequested += (bmp) => {
                if (currentBitmap != null) currentBitmap.Dispose();
                currentBitmap = bmp;
                UpdateDimensionsLabel();
                if (canvas != null) canvas.Invalidate();
                SetStatus("Received texture from 3D Viewer");
            };

            SharedState.OnEditModelRequested += (mdlPath) => {
                try {
                    OldMdlViewerLogic logic = new OldMdlViewerLogic();
                    var bmps = logic.getAllBMPs(mdlPath);
                    if (bmps.Count > 0) {
                        if (currentBitmap != null) currentBitmap.Dispose();
                        currentBitmap = new Bitmap(bmps[0].Width, bmps[0].Height,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        using (Graphics g = Graphics.FromImage(currentBitmap))
                            g.DrawImage(bmps[0], 0, 0);
                        loadedMdlPath = mdlPath;
                        foreach (var b in bmps) b.Dispose();
                        UpdateDimensionsLabel();
                        if (canvas != null) canvas.Invalidate();
                        SetStatus($"Loaded MDL: {Path.GetFileName(mdlPath)}");
                    }
                } catch { }
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        private void InitializeComponent()
        {
            ToolTip toolTip = new ToolTip();

            // Root: 3-row vertical layout
            QFG5TableLayoutPanel root = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(QFG5Layout.PagePadding)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));

            // ── Row 0: Header toolbar ─────────────────────────────────────────
            QFG5TableLayoutPanel headerBar = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 6),
                Padding = new Padding(0)
            };
            QFG5Theme.ApplyHeaderStroke(headerBar);
            headerBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
            headerBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21f));
            headerBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21f));
            headerBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16f));
            headerBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));

            QFG5Button btnNew     = MakeHeaderBtn("New 64x64");
            QFG5Button btnOpen    = MakeHeaderBtn("Open Image...");
            QFG5Button btnOpenMdl = MakeHeaderBtn("Open MDL...");
            QFG5Button btnSave    = MakeHeaderBtn("Save...");
            QFG5Button btnInject  = MakeHeaderBtn("Inject into MDL");
            btnInject.ForeColor = QFG5Theme.ColorGold;

            toolTip.SetToolTip(btnNew,     "Create a blank 64x64 transparent canvas");
            toolTip.SetToolTip(btnOpen,    "Open a BMP/PNG/JPG image for editing");
            toolTip.SetToolTip(btnOpenMdl, "Extract the embedded texture from a QFG5 MDL file");
            toolTip.SetToolTip(btnSave,    "Save the texture to disk as PNG or BMP");
            toolTip.SetToolTip(btnInject,  "Palette-preserving re-injection into the source MDL");

            btnNew.Click     += (s, e) => NewTexture(64, 64);
            btnOpen.Click    += BtnOpen_Click;
            btnOpenMdl.Click += BtnOpenMdl_Click;
            btnSave.Click    += BtnSave_Click;
            btnInject.Click  += BtnInject_Click;

            QFG5Button[] headerButtons = { btnNew, btnOpen, btnOpenMdl, btnSave, btnInject };
            for (int i = 0; i < headerButtons.Length; i++)
            {
                headerButtons[i].Dock = DockStyle.Fill;
                headerBar.Controls.Add(headerButtons[i], i, 0);
            }
            root.Controls.Add(headerBar, 0, 0);

            // ── Row 1: Workspace - SplitContainer ────────────────────────────
            SplitContainer workspace = new SplitContainer {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BorderStyle = BorderStyle.None,
                SplitterWidth = 6,
                Panel1MinSize = 240,
                Panel2MinSize = 220
            };
            workspace.Panel1.BackColor = Color.Transparent;
            workspace.Panel2.BackColor = Color.Transparent;
            QFG5Layout.SetSplitterRatioOnce(workspace, 0.70);

            // Panel 1: Canvas
            QFG5PreviewFrame canvasFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            canvas = new PictureBox {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Normal,
                BackColor = Color.Transparent,
                Cursor = Cursors.Cross
            };
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.Paint     += Canvas_Paint;
            canvasFrame.Controls.Add(canvas);
            workspace.Panel1.Controls.Add(canvasFrame);

            // Panel 2: Tool panel
            QFG5PreviewFrame toolFrame = new QFG5PreviewFrame { Dock = DockStyle.Fill };
            Panel toolScrollHost = new Panel {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(8)
            };
            QFG5TableLayoutPanel toolPanel = new QFG5TableLayoutPanel {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            int toolRow = 0;
            Action<string> addSection = (title) => {
                toolPanel.RowCount++;
                toolPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
                Label lbl = new Label {
                    Text = title,
                    ForeColor = QFG5Theme.ColorTextGold,
                    Font = QFG5Theme.SectionHeaderFont,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(4, 0, 0, 1),
                    Margin = new Padding(0, toolRow == 0 ? 0 : 9, 0, 3)
                };
                toolPanel.Controls.Add(lbl, 0, toolRow++);
            };
            Action<Control> addRow = (ctrl) => {
                toolPanel.RowCount++;
                toolPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                ctrl.Margin = new Padding(0, 2, 0, 2);
                toolPanel.Controls.Add(ctrl, 0, toolRow++);
            };

            // DRAW TOOLS
            addSection("DRAW TOOLS");
            btnPencil  = MakeToolBtn("Pencil");
            btnEraser  = MakeToolBtn("Eraser");
            btnFill    = MakeToolBtn("Fill");
            btnPipette = MakeToolBtn("Colour Picker");
            toolTip.SetToolTip(btnPencil,  "Draw pixels with the active colour and brush size");
            toolTip.SetToolTip(btnEraser,  "Erase pixels to transparency");
            toolTip.SetToolTip(btnFill,    "Flood-fill a region with the active colour");
            toolTip.SetToolTip(btnPipette, "Sample the colour of a clicked pixel");
            btnPencil.Click  += (s, e) => SelectTool("Pencil");
            btnEraser.Click  += (s, e) => SelectTool("Eraser");
            btnFill.Click    += (s, e) => SelectTool("Fill");
            btnPipette.Click += (s, e) => SelectTool("Pipette");
            addRow(btnPencil); addRow(btnEraser); addRow(btnFill); addRow(btnPipette);
            SelectTool("Pencil");

            // SELECT TOOLS
            addSection("SELECT TOOLS");
            btnMagicWand   = MakeToolBtn("Magic Wand");
            btnSelectColor = MakeToolBtn("Select Colour");
            QFG5Button btnClearSel = MakeToolBtn("Clear Selection");
            toolTip.SetToolTip(btnMagicWand,   "Select contiguous region of similar colour (tolerance 15)");
            toolTip.SetToolTip(btnSelectColor, "Select all pixels globally matching the clicked colour");
            toolTip.SetToolTip(btnClearSel,    "Deselect all currently selected pixels");
            btnMagicWand.Click   += (s, e) => SelectTool("MagicWand");
            btnSelectColor.Click += (s, e) => SelectTool("SelectColor");
            btnClearSel.Click    += (s, e) => { selection.Clear(); canvas.Invalidate(); SetStatus("Selection cleared"); };
            addRow(btnMagicWand); addRow(btnSelectColor); addRow(btnClearSel);

            // ADJUST
            addSection("ADJUST");
            QFG5Button btnHueSat  = MakeToolBtn("Hue / Saturation...");
            QFG5Button btnOverlay = MakeToolBtn("Add Overlay...");
            toolTip.SetToolTip(btnHueSat,  "Hue and saturation shift (whole image or selection)");
            toolTip.SetToolTip(btnOverlay, "Composite a PNG over the current texture");
            btnHueSat.Click  += (s, e) => ShowHueSatDialog();
            btnOverlay.Click += (s, e) => AddOverlay();
            addRow(btnHueSat); addRow(btnOverlay);

            // ZOOM
            addSection("ZOOM");
            QFG5TableLayoutPanel zoomRow = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Height = 32,
                Margin = new Padding(0, 2, 0, 0)
            };
            zoomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            zoomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            zoomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            QFG5Button btnZoomOut = new QFG5Button { Text = "-", Height = 28, Dock = DockStyle.Fill, Margin = new Padding(1, 0, 1, 0) };
            QFG5Button btnZoomIn  = new QFG5Button { Text = "+", Height = 28, Dock = DockStyle.Fill, Margin = new Padding(1, 0, 1, 0) };
            trackZoom = new TrackBar {
                Minimum = 1, Maximum = 16, Value = (int)zoomLevel,
                Dock = DockStyle.Fill, TickFrequency = 1,
                BackColor = QFG5Theme.ColorSurface
            };
            toolTip.SetToolTip(btnZoomOut, "Zoom out");
            toolTip.SetToolTip(btnZoomIn,  "Zoom in");
            toolTip.SetToolTip(trackZoom,  "Drag to set zoom level (1x - 16x)");
            btnZoomOut.Click += (s, e) => { if (trackZoom.Value > trackZoom.Minimum) trackZoom.Value--; };
            btnZoomIn.Click  += (s, e) => { if (trackZoom.Value < trackZoom.Maximum) trackZoom.Value++; };
            trackZoom.ValueChanged += (s, e) => { zoomLevel = trackZoom.Value; UpdateZoomLabel(); canvas.Invalidate(); };
            zoomRow.Controls.Add(btnZoomOut, 0, 0);
            zoomRow.Controls.Add(trackZoom,  1, 0);
            zoomRow.Controls.Add(btnZoomIn,  2, 0);
            addRow(zoomRow);
            lblZoomPct = new Label {
                Text = "100%",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.CaptionFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 18
            };
            addRow(lblZoomPct);

            // BRUSH SIZE
            addSection("BRUSH SIZE");
            NumericUpDown numBrush = new NumericUpDown {
                Minimum = 1, Maximum = 50, Value = brushSize,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(28, 22, 18),
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                TextAlign = HorizontalAlignment.Center,
                Height = 26,
                Margin = new Padding(0, 2, 0, 0)
            };
            numBrush.ValueChanged += (s, e) => { brushSize = (int)numBrush.Value; };
            toolTip.SetToolTip(numBrush, "Brush size in pixels (Pencil and Eraser)");
            addRow(numBrush);

            // ACTIVE COLOUR
            addSection("ACTIVE COLOUR");
            colorPreview = new Panel {
                Dock = DockStyle.Fill,
                Height = 30,
                BackColor = drawColor,
                Cursor = Cursors.Hand,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 3, 0, 5)
            };
            colorPreview.Click += (s, e) => {
                using (ColorDialog cd = new ColorDialog()) {
                    cd.Color = drawColor;
                    if (cd.ShowDialog() == DialogResult.OK) SetColor(cd.Color);
                }
            };
            toolTip.SetToolTip(colorPreview, "Click to open the colour picker");
            addRow(colorPreview);

            // PALETTE
            addSection("PALETTE");
            Color[] palette = new Color[] {
                Color.Black, Color.FromArgb(40,40,40), Color.Gray, Color.Silver, Color.White,
                Color.DarkRed, Color.Red, Color.Orange, Color.Gold, Color.Yellow,
                Color.DarkGreen, Color.Green, Color.Lime, Color.Cyan, Color.Teal,
                Color.Navy, Color.Blue, Color.RoyalBlue, Color.Purple, Color.Magenta,
                Color.Brown, Color.SaddleBrown, Color.Tan, Color.Khaki, Color.PaleGoldenrod
            };
            Panel paletteGrid = new Panel {
                Dock = DockStyle.Fill,
                Height = 102,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            int px = 0, py = 0;
            foreach (var c in palette) {
                Color captured = c;
                Panel swatch = new Panel {
                    Width = 26, Height = 26,
                    Left = px * 29 + 1,
                    Top  = py * 29 + 1,
                    BackColor = captured,
                    Cursor = Cursors.Hand,
                    BorderStyle = BorderStyle.FixedSingle
                };
                swatch.Click      += (s, e) => SetColor(((Panel)s).BackColor);
                swatch.MouseEnter += (s, e) => ((Panel)s).BorderStyle = BorderStyle.Fixed3D;
                swatch.MouseLeave += (s, e) => ((Panel)s).BorderStyle = BorderStyle.FixedSingle;
                toolTip.SetToolTip(swatch, captured.Name);
                paletteGrid.Controls.Add(swatch);
                px++;
                if (px >= 5) { px = 0; py++; }
            }
            addRow(paletteGrid);

            toolScrollHost.Controls.Add(toolPanel);
            toolFrame.Controls.Add(toolScrollHost);
            workspace.Panel2.Controls.Add(toolFrame);
            root.Controls.Add(workspace, 0, 1);

            // ── Row 2: Status footer ──────────────────────────────────────────
            QFG5TableLayoutPanel footer = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 4, 0, 0)
            };
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            QFG5TableLayoutPanel statusRow = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                Margin = new Padding(0)
            };
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            lblStatus = new Label {
                Text = "No texture loaded",
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 4, 0),
                Margin = new Padding(0)
            };
            statusRow.Controls.Add(lblStatus, 0, 0);
            footer.Controls.Add(statusRow, 0, 0);

            QFG5TableLayoutPanel infoRow = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 1,
                Margin = new Padding(0)
            };
            infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
            infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Label lblPixelCaption = new Label { Text = "Pixel:", ForeColor = QFG5Theme.ColorTextMuted, Font = QFG5Theme.CaptionFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 0, 0), Margin = new Padding(0) };
            lblMousePos = new Label { Text = "-", ForeColor = QFG5Theme.ColorTextPrimary, Font = QFG5Theme.BodyFont, Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 0, 0), Margin = new Padding(0) };
            Label lblDimsCaption = new Label { Text = "Size:", ForeColor = QFG5Theme.ColorTextMuted, Font = QFG5Theme.CaptionFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 0, 0), Margin = new Padding(0) };
            lblDimensions = new Label { Text = "-", ForeColor = QFG5Theme.ColorTextPrimary, Font = QFG5Theme.BodyFont, Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 0, 0), Margin = new Padding(0) };
            Label lblMdlCaption = new Label { Text = "MDL:", ForeColor = QFG5Theme.ColorTextMuted, Font = QFG5Theme.CaptionFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 0, 0), Margin = new Padding(0) };
            Label lblMdlName = new Label { Text = "(none)", ForeColor = QFG5Theme.ColorTextPrimary, Font = QFG5Theme.BodyFont, Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 2, 0), Margin = new Padding(0) };
            this.Tag = lblMdlName;
            infoRow.Controls.Add(lblPixelCaption, 0, 0);
            infoRow.Controls.Add(lblMousePos, 1, 0);
            infoRow.Controls.Add(lblDimsCaption, 2, 0);
            infoRow.Controls.Add(lblDimensions, 3, 0);
            infoRow.Controls.Add(lblMdlCaption, 4, 0);
            infoRow.Controls.Add(lblMdlName, 5, 0);
            footer.Controls.Add(infoRow, 0, 1);

            root.Controls.Add(footer, 0, 2);
            this.Controls.Add(root);
        }

        // ─── Control factory helpers ──────────────────────────────────────────
        private static QFG5Button MakeHeaderBtn(string text)
        {
            return new QFG5Button { Text = text, Height = 32, Margin = new Padding(3, 3, 3, 3) };
        }

        private static QFG5Button MakeToolBtn(string text)
        {
            return new QFG5Button { Text = text, Height = 30, Dock = DockStyle.Fill, Margin = new Padding(0, 1, 0, 1), Font = QFG5Theme.ButtonFont };
        }

        // ─── Tool selection with visual feedback ──────────────────────────────
        private void SelectTool(string tool)
        {
            currentTool = tool;
            Color normal   = QFG5Theme.ColorTextPrimary;
            Color selected = QFG5Theme.ColorGold;
            if (btnPencil      != null) btnPencil.ForeColor      = normal;
            if (btnEraser      != null) btnEraser.ForeColor      = normal;
            if (btnFill        != null) btnFill.ForeColor        = normal;
            if (btnPipette     != null) btnPipette.ForeColor     = normal;
            if (btnMagicWand   != null) btnMagicWand.ForeColor   = normal;
            if (btnSelectColor != null) btnSelectColor.ForeColor = normal;
            switch (tool) {
                case "Pencil":      if (btnPencil      != null) btnPencil.ForeColor      = selected; SetStatus("Tool: Pencil"); break;
                case "Eraser":      if (btnEraser      != null) btnEraser.ForeColor      = selected; SetStatus("Tool: Eraser"); break;
                case "Fill":        if (btnFill        != null) btnFill.ForeColor        = selected; SetStatus("Tool: Fill"); break;
                case "Pipette":     if (btnPipette     != null) btnPipette.ForeColor     = selected; SetStatus("Tool: Colour Picker"); break;
                case "MagicWand":   if (btnMagicWand   != null) btnMagicWand.ForeColor   = selected; SetStatus("Tool: Magic Wand"); break;
                case "SelectColor": if (btnSelectColor != null) btnSelectColor.ForeColor = selected; SetStatus("Tool: Select Colour"); break;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        private void NewTexture(int w, int h)
        {
            if (currentBitmap != null) currentBitmap.Dispose();
            currentBitmap = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(currentBitmap)) g.Clear(Color.Transparent);
            loadedMdlPath = string.Empty;
            UpdateDimensionsLabel();
            UpdateMdlLabel();
            if (canvas != null) canvas.Invalidate();
            SetStatus($"New texture ({w} x {h})");
        }

        private void UpdateDimensionsLabel()
        {
            if (lblDimensions == null) return;
            lblDimensions.Text = (currentBitmap != null) ? $"{currentBitmap.Width} x {currentBitmap.Height}" : "-";
        }

        private void UpdateMdlLabel()
        {
            var lbl = this.Tag as Label;
            if (lbl == null) return;
            lbl.Text = string.IsNullOrEmpty(loadedMdlPath) ? "(none)" : Path.GetFileName(loadedMdlPath);
        }

        private void UpdateZoomLabel()
        {
            if (lblZoomPct != null) lblZoomPct.Text = $"{(int)(zoomLevel * 100)}%";
        }

        // ─────────────────────────────────────────────────────────────────────
        //   MOUSE / DRAWING
        // ─────────────────────────────────────────────────────────────────────
        private void Canvas_MouseDown(object sender, MouseEventArgs e) { DrawAt(e.X, e.Y); }
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            UpdateMouseLabel(e.X, e.Y);
            if (e.Button == MouseButtons.Left) DrawAt(e.X, e.Y);
        }

        private void UpdateMouseLabel(int x, int y)
        {
            if (currentBitmap == null) return;
            int imgX, imgY;
            ScreenToImage(x, y, out imgX, out imgY);
            if (imgX >= 0 && imgX < currentBitmap.Width && imgY >= 0 && imgY < currentBitmap.Height)
                if (lblMousePos != null) lblMousePos.Text = $"{imgX}, {imgY}";
        }

        private void DrawAt(int x, int y)
        {
            if (currentBitmap == null) return;
            int imgX, imgY;
            ScreenToImage(x, y, out imgX, out imgY);
            if (imgX >= 0 && imgX < currentBitmap.Width && imgY >= 0 && imgY < currentBitmap.Height) {
                if (currentTool == "Pencil") {
                    using (Graphics g = Graphics.FromImage(currentBitmap))
                        g.FillRectangle(new SolidBrush(drawColor), imgX - brushSize/2, imgY - brushSize/2, brushSize, brushSize);
                } else if (currentTool == "Eraser") {
                    using (Graphics g = Graphics.FromImage(currentBitmap)) {
                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        g.FillRectangle(new SolidBrush(Color.Transparent), imgX - brushSize/2, imgY - brushSize/2, brushSize, brushSize);
                    }
                } else if (currentTool == "Fill")        FloodFill(imgX, imgY, drawColor);
                else if (currentTool == "MagicWand")     MagicWandSelect(imgX, imgY);
                else if (currentTool == "SelectColor")   SelectColorCluster(imgX, imgY);
                else if (currentTool == "Pipette")       SetColor(currentBitmap.GetPixel(imgX, imgY));
                canvas.Invalidate();
            }
        }

        private void ScreenToImage(int sx, int sy, out int imgX, out int imgY)
        {
            if (currentBitmap == null) { imgX = imgY = -1; return; }
            float canvasAspect = (float)canvas.Width  / canvas.Height;
            float imageAspect  = (float)currentBitmap.Width / currentBitmap.Height;
            float actualScale;
            if (imageAspect > canvasAspect) {
                actualScale = ((float)canvas.Width / currentBitmap.Width) * zoomLevel;
                float vOffset = (canvas.Height - currentBitmap.Height * actualScale) / 2;
                imgX = (int)(sx / actualScale);
                imgY = (int)((sy - vOffset) / actualScale);
            } else {
                actualScale = ((float)canvas.Height / currentBitmap.Height) * zoomLevel;
                float hOffset = (canvas.Width - currentBitmap.Width * actualScale) / 2;
                imgX = (int)((sx - hOffset) / actualScale);
                imgY = (int)(sy / actualScale);
            }
        }

        private void SetColor(Color c) { drawColor = c; if (colorPreview != null) colorPreview.BackColor = c; }
        private void SetStatus(string s) { if (lblStatus != null) lblStatus.Text = s; }

        // ─────────────────────────────────────────────────────────────────────
        //   CANVAS PAINT
        // ─────────────────────────────────────────────────────────────────────
        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            DrawCheckerboard(e.Graphics, canvas.ClientRectangle);

            if (currentBitmap == null) {
                e.Graphics.DrawString(
                    "No texture loaded\nUse the toolbar to open an image or MDL.",
                    QFG5Theme.BodyFont,
                    new SolidBrush(QFG5Theme.ColorTextMuted),
                    new RectangleF(0, 0, canvas.Width, canvas.Height),
                    new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                return;
            }

            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode   = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            float actualScale;
            float hOffset = 0, vOffset = 0;
            float canvasAspect = (float)canvas.Width  / canvas.Height;
            float imageAspect  = (float)currentBitmap.Width / currentBitmap.Height;

            if (imageAspect > canvasAspect) {
                actualScale = ((float)canvas.Width / currentBitmap.Width) * zoomLevel;
                vOffset = (canvas.Height - currentBitmap.Height * actualScale) / 2;
            } else {
                actualScale = ((float)canvas.Height / currentBitmap.Height) * zoomLevel;
                hOffset = (canvas.Width - currentBitmap.Width * actualScale) / 2;
            }

            e.Graphics.DrawImage(currentBitmap, new Rectangle(
                (int)hOffset, (int)vOffset,
                (int)(currentBitmap.Width  * actualScale),
                (int)(currentBitmap.Height * actualScale)));

            // Pixel grid at high zoom
            if (actualScale >= 8) {
                using (Pen gridPen = new Pen(Color.FromArgb(40, 255, 255, 255))) {
                    for (int gx = 0; gx <= currentBitmap.Width; gx++)
                        e.Graphics.DrawLine(gridPen, hOffset + gx * actualScale, vOffset, hOffset + gx * actualScale, vOffset + currentBitmap.Height * actualScale);
                    for (int gy = 0; gy <= currentBitmap.Height; gy++)
                        e.Graphics.DrawLine(gridPen, hOffset, vOffset + gy * actualScale, hOffset + currentBitmap.Width * actualScale, vOffset + gy * actualScale);
                }
            }

            // Selection overlay
            if (selection.Count > 0) {
                using (SolidBrush selBrush = new SolidBrush(Color.FromArgb(100, Color.PaleGoldenrod))) {
                    foreach (var p in selection)
                        e.Graphics.FillRectangle(selBrush, hOffset + p.X * actualScale, vOffset + p.Y * actualScale, actualScale, actualScale);
                }
            }
        }

        private static void DrawCheckerboard(Graphics g, Rectangle bounds)
        {
            const int sz = 8;
            for (int y = bounds.Y; y < bounds.Bottom; y += sz) {
                for (int x = bounds.X; x < bounds.Right; x += sz) {
                    bool dark = ((x / sz + y / sz) & 1) == 0;
                    using (SolidBrush b = new SolidBrush(dark ? Color.FromArgb(30, 30, 35) : Color.FromArgb(50, 50, 56)))
                        g.FillRectangle(b, x, y, Math.Min(sz, bounds.Right - x), Math.Min(sz, bounds.Bottom - y));
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //   SELECTION TOOLS
        // ─────────────────────────────────────────────────────────────────────
        private void MagicWandSelect(int x, int y)
        {
            Color target = currentBitmap.GetPixel(x, y);
            selection.Clear();
            bool[,] visited = new bool[currentBitmap.Width, currentBitmap.Height];
            var stack = new Stack<Point>();
            stack.Push(new Point(x, y));
            while (stack.Count > 0) {
                Point p = stack.Pop();
                if (p.X < 0 || p.X >= currentBitmap.Width || p.Y < 0 || p.Y >= currentBitmap.Height) continue;
                if (visited[p.X, p.Y]) continue;
                Color c = currentBitmap.GetPixel(p.X, p.Y);
                if (ColorDiff(c, target) < 15) {
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
            for (int ix = 0; ix < currentBitmap.Width; ix++)
                for (int iy = 0; iy < currentBitmap.Height; iy++)
                    if (ColorDiff(currentBitmap.GetPixel(ix, iy), target) < 5)
                        selection.Add(new Point(ix, iy));
            SetStatus($"Selected {selection.Count} pixels (global).");
        }

        private float ColorDiff(Color c1, Color c2)
        {
            return Math.Abs(c1.R - c2.R) + Math.Abs(c1.G - c2.G) + Math.Abs(c1.B - c2.B);
        }

        // ─────────────────────────────────────────────────────────────────────
        //   ADJUST OPERATIONS
        // ─────────────────────────────────────────────────────────────────────
        private void ShowHueSatDialog()
        {
            if (currentBitmap == null) return;
            using (Form f = new Form { Text = "Hue / Saturation", Width = 300, Height = 200, StartPosition = FormStartPosition.CenterParent }) {
                TrackBar trkHue = new TrackBar { Minimum = -180, Maximum = 180, Value = 0, Dock = DockStyle.Top };
                Label lblHue    = new Label { Text = "Hue Adjust:", Dock = DockStyle.Top };
                TrackBar trkSat = new TrackBar { Minimum = -100, Maximum = 100, Value = 0, Dock = DockStyle.Top };
                Label lblSat    = new Label { Text = "Saturation Adjust:", Dock = DockStyle.Top };
                Button btnApply = new Button { Text = "Apply", Dock = DockStyle.Bottom };
                btnApply.Click += (s, e) => { ApplyHSL(trkHue.Value, trkSat.Value / 100.0f); f.Close(); };
                f.Controls.AddRange(new Control[] { btnApply, trkSat, lblSat, trkHue, lblHue });
                f.ShowDialog();
            }
        }

        private void ApplyHSL(float hueShift, float satShift)
        {
            if (selection.Count > 0)
                foreach (var p in selection) AdjustPixelHSL(p.X, p.Y, hueShift, satShift);
            else
                for (int x = 0; x < currentBitmap.Width; x++)
                    for (int y = 0; y < currentBitmap.Height; y++)
                        AdjustPixelHSL(x, y, hueShift, satShift);
            canvas.Invalidate();
        }

        private void AdjustPixelHSL(int x, int y, float hShift, float sAdjust)
        {
            Color c = currentBitmap.GetPixel(x, y);
            if (c.A == 0) return;
            float h, s, l;
            ColorToHSL(c, out h, out s, out l);
            h = (h + hShift) % 360; if (h < 0) h += 360;
            s = Math.Min(1, Math.Max(0, s + sAdjust));
            currentBitmap.SetPixel(x, y, HSLToColor(h, s, l, c.A));
        }

        private void ColorToHSL(Color c, out float h, out float s, out float l)
        {
            float r = c.R/255f, g = c.G/255f, b = c.B/255f;
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;
            if (max == min) { h = s = 0; }
            else {
                float d = max - min;
                s = l > 0.5f ? d / (2 - max - min) : d / (max + min);
                if (max == r)      h = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g) h = (b - r) / d + 2;
                else               h = (r - g) / d + 4;
                h *= 60;
            }
        }

        private Color HSLToColor(float h, float s, float l, int alpha)
        {
            float r, g, b;
            if (s == 0) { r = g = b = l; }
            else {
                float q = l < 0.5f ? l * (1 + s) : l + s - l * s;
                float p = 2 * l - q;
                r = HueToRGB(p, q, h/360 + 1f/3);
                g = HueToRGB(p, q, h/360);
                b = HueToRGB(p, q, h/360 - 1f/3);
            }
            return Color.FromArgb(alpha, (int)(r*255), (int)(g*255), (int)(b*255));
        }

        private float HueToRGB(float p, float q, float t)
        {
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
                    using (Bitmap overlay = new Bitmap(ofd.FileName))
                    using (Graphics g = Graphics.FromImage(currentBitmap))
                        g.DrawImage(overlay, new Rectangle(0, 0, currentBitmap.Width, currentBitmap.Height));
                    canvas.Invalidate();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //   FLOOD FILL
        // ─────────────────────────────────────────────────────────────────────
        private void FloodFill(int x, int y, Color color)
        {
            Color targetColor = currentBitmap.GetPixel(x, y);
            if (targetColor.ToArgb() == color.ToArgb()) return;
            var pixels = new Stack<Point>();
            pixels.Push(new Point(x, y));
            while (pixels.Count > 0) {
                Point a = pixels.Pop();
                if (a.X < 0 || a.X >= currentBitmap.Width || a.Y < 0 || a.Y >= currentBitmap.Height) continue;
                if (currentBitmap.GetPixel(a.X, a.Y).ToArgb() == targetColor.ToArgb()) {
                    currentBitmap.SetPixel(a.X, a.Y, color);
                    pixels.Push(new Point(a.X - 1, a.Y));
                    pixels.Push(new Point(a.X + 1, a.Y));
                    pixels.Push(new Point(a.X, a.Y - 1));
                    pixels.Push(new Point(a.X, a.Y + 1));
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //   FILE OPERATIONS
        // ─────────────────────────────────────────────────────────────────────
        private void BtnOpen_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files|*.bmp;*.png;*.jpg" }) {
                if (ofd.ShowDialog() == DialogResult.OK) {
                    if (currentBitmap != null) currentBitmap.Dispose();
                    Bitmap loaded = new Bitmap(ofd.FileName);
                    currentBitmap = new Bitmap(loaded.Width, loaded.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(currentBitmap)) g.DrawImage(loaded, 0, 0);
                    loaded.Dispose();
                    loadedMdlPath = string.Empty;
                    UpdateDimensionsLabel();
                    UpdateMdlLabel();
                    canvas.Invalidate();
                    SetStatus($"Opened: {Path.GetFileName(ofd.FileName)}");
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (currentBitmap == null) { SetStatus("Nothing to save."); return; }
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "PNG Image|*.png|BMP Image|*.bmp" })
                if (sfd.ShowDialog() == DialogResult.OK) { currentBitmap.Save(sfd.FileName); SetStatus($"Saved: {Path.GetFileName(sfd.FileName)}"); }
        }

        private void BtnInject_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(loadedMdlPath) || !File.Exists(loadedMdlPath)) {
                MessageBox.Show("Please open a texture from an MDL file first to inject it.", "Texture Editor");
                return;
            }
            try {
                byte[] mdlPalette = ExtractMdlPalette(loadedMdlPath);
                byte[] pixelData  = ConvertTo8BitWithPalette(currentBitmap, mdlPalette);
                string tempBmp    = Path.Combine(Path.GetTempPath(), "qfg5_temp_inject.bmp");
                WriteGameBmp(tempBmp, mdlPalette, pixelData, currentBitmap.Width, currentBitmap.Height);
                ModelConverter.importOrExportBitmapOrMesh(tempBmp, loadedMdlPath, false, true, false, false);
                if (File.Exists(tempBmp)) File.Delete(tempBmp);
                SetStatus("Injected successfully.");
                MessageBox.Show("Texture injected successfully with palette preservation!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception ex) {
                SetStatus("Injection failed.");
                MessageBox.Show("Error injecting texture: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

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
                            using (Graphics g = Graphics.FromImage(currentBitmap)) g.DrawImage(bmps[0], 0, 0);
                            loadedMdlPath = ofd.FileName;
                            foreach (var b in bmps) b.Dispose();
                            UpdateDimensionsLabel();
                            UpdateMdlLabel();
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

        // ─────────────────────────────────────────────────────────────────────
        //   MDL INJECTION HELPERS
        // ─────────────────────────────────────────────────────────────────────
        private byte[] ExtractMdlPalette(string mdlPath)
        {
            byte[] palette = new byte[Constants.BITMAP_PALETTE_LENGTH];
            using (BinaryReader br = new BinaryReader(File.Open(mdlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) {
                br.BaseStream.Seek(Constants.MDL_HEADER_BITMAPPALETTE_ADDRESS, SeekOrigin.Begin);
                palette = br.ReadBytes(palette.Length);
            }
            return palette;
        }

        private byte[] ConvertTo8BitWithPalette(Bitmap source, byte[] rawPalette)
        {
            List<Color> paletteEntries = new List<Color>();
            for (int i = 0; i < rawPalette.Length - 3; i += 4)
                paletteEntries.Add(Color.FromArgb(rawPalette[i+2], rawPalette[i+1], rawPalette[i]));
            int w = source.Width, h = source.Height;
            byte[] pixelData = new byte[w * h];
            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    Color c = source.GetPixel(x, y);
                    if (c.A < 128) { pixelData[y * w + x] = 0; continue; }
                    int bestIndex = 0, minDiff = int.MaxValue;
                    for (int i = 0; i < paletteEntries.Count; i++) {
                        Color p = paletteEntries[i];
                        int dr = c.R - p.R, dg = c.G - p.G, db = c.B - p.B;
                        int diff = dr*dr + dg*dg + db*db;
                        if (diff < minDiff) { minDiff = diff; bestIndex = i; }
                    }
                    pixelData[y * w + x] = (byte)bestIndex;
                }
            }
            return pixelData;
        }

        private void WriteGameBmp(string path, byte[] rawPalette, byte[] pixelData, int w, int h)
        {
            using (BinaryWriter bw = new BinaryWriter(File.Open(path, FileMode.Create))) {
                bw.Write(new byte[] { 0x42, 0x4d });
                bw.Write(54 + (rawPalette.Length + 5) + pixelData.Length);
                bw.Write((ushort)0); bw.Write((ushort)0);
                bw.Write(54 + (rawPalette.Length + 5));
                bw.Write(40); bw.Write(w); bw.Write(h);
                bw.Write((ushort)1); bw.Write((ushort)8);
                bw.Write(0); bw.Write(w * h); bw.Write(2834); bw.Write(2834);
                bw.Write(0); bw.Write(0); bw.Write(0);
                bw.Write(rawPalette); bw.Write((byte)0);
                bw.Write(pixelData);
            }
        }

        private Bitmap ConvertTo8BitIndexed(Bitmap source)
        {
            int width = source.Width, height = source.Height;
            Bitmap dest = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            ColorPalette pal = dest.Palette;
            for (int i = 0; i < 256; i++) pal.Entries[i] = Color.FromArgb(i, i, i);
            dest.Palette = pal;
            BitmapData data = dest.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            byte[] pixels = new byte[data.Stride * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++) {
                    Color c = source.GetPixel(x, y);
                    int gray = (int)(c.R * 0.3 + c.G * 0.59 + c.B * 0.11);
                    pixels[y * data.Stride + x] = (byte)gray;
                }
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            dest.UnlockBits(data);
            return dest;
        }
    }
}
