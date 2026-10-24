using System;
using System.Drawing;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using QFG5Extractor.Core.UI;
using QFG5Extractor.Resources.Gra;
using QFG5Extractor.Resources.Model;
using QFG5Extractor.Resources.Panorama;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace QFG5Extractor.Core
{
    public class TextureEditorTab : UserControl, IMessageFilter
    {
        // ── Editing state (untouched semantics) ───────────────────────────────
        private Bitmap currentBitmap;
        private Bitmap originalBitmap;
        private readonly Stack<Bitmap> undoHistory = new Stack<Bitmap>();
        private PictureBox canvas;
        private Color drawColor = Color.White;
        private string currentTool = "Pencil";
        private string loadedMdlPath = string.Empty;
        private float zoomLevel = 1.0f;
        private float panOffsetX;
        private float panOffsetY;
        private int brushSize = 1;
        private List<Point> selection = new List<Point>();
        private Rectangle? rectangularSelection;
        private readonly List<Point> lassoPath = new List<Point>();
        private readonly Stack<int> undoGraFrameIndices = new Stack<int>();
        private readonly Stack<Dictionary<int, Bitmap>> graUndoHistory =
            new Stack<Dictionary<int, Bitmap>>();
        private Panel paletteViewport;
        private TableLayoutPanel paletteGrid;
        private QFG5TableLayoutPanel centerColumn;
        private Panel graFramePanel;
        private FlowLayoutPanel graFrameStrip;
        private TableLayoutPanel propPanel;
        private FlowLayoutPanel propThumbnailList;
        private NumericUpDown numPropScale;
        private NumericUpDown numPropRotation;
        private Bitmap selectedPropImage;
        private Point propScreenPosition;
        private bool hasPropPosition;
        private readonly List<Bitmap> loadedGraFrames = new List<Bitmap>();
        private readonly List<Bitmap> originalGraFrames = new List<Bitmap>();
        private readonly List<bool> dirtyGraFrames = new List<bool>();
        private readonly List<Button> graFrameThumbnails = new List<Button>();
        private bool spaceHeld;
        private bool isPanning;
        private bool panGestureActive;
        private bool isSelecting;
        private Point panStartPoint;
        private PointF panStartOffset;
        private Point selectionStart;
        private Point selectionCurrent;
        private bool strokeUndoCaptured;
        private string selectedProp;
        private const int MaxUndoSteps = 30;
        private const string OriginalMdlBackupSuffix = ".qfg5extractor.bak";
        private const string OriginalGraBackupSuffix = ".qfg5extractor.bak";
        private string loadedGraPath = string.Empty;
        private string loadedPanoBitmapPath = string.Empty;
        private string loadedPanoNodPath = string.Empty;
        private string loadedPanoImgPath = string.Empty;
        private int loadedGraCollectionIndex = -1;
        private int loadedGraFrameIndex = -1;

        // ── UI references ─────────────────────────────────────────────────────
        private Label lblMousePos;
        private Label lblDimensions;
        private Label lblStatus;
        private Label lblZoomPct;
        private Panel colorPreview;
        private ToolTip toolTip;
        private Button btnUndo;
        private Button btnResetOriginal;
        private Button btnInjectMdl;
        private Button btnInjectGra;
        private Button btnClearSelection;
        private Button btnHueSat;
        private Button btnOverlay;
        private Button btnProps;
        private Button btnCancelProp;
        private Button btnLassoSelect;
        private Button btnRectangleSelect;

        // ── Tool buttons (kept for active-state indication) ───────────────────
        private Button btnPencil;
        private Button btnEraser;
        private Button btnFill;
        private Button btnPipette;
        private Button btnMagicWand;
        private Button btnSelectColor;

        // ── TrackBar ──────────────────────────────────────────────────────────
        private TrackBar trackZoom;

        // ─────────────────────────────────────────────────────────────────────
        public TextureEditorTab()
        {
            Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;
            InitializeComponent();
            Application.AddMessageFilter(this);

            SharedState.OnEditTextureRequested += (bmp) => {
                try {
                    SetCurrentTexture(bmp, string.Empty, "Received image from another tab");
                } finally {
                    if (bmp != null) bmp.Dispose();
                }
            };

            SharedState.OnEditGraFrameRequested += (bmp, graPath, collectionIndex, frameIndex, modeOverride, cmOverride) => {
                try {
                    LoadGraAnimation(bmp, graPath, collectionIndex, frameIndex, modeOverride, cmOverride);
                } finally {
                    if (bmp != null) bmp.Dispose();
                }
            };

            SharedState.OnEditModelRequested += (mdlPath) => {
                try {
                    if (!File.Exists(mdlPath))
                        throw new FileNotFoundException("The selected MDL file could not be found.", mdlPath);
                    if (!LoadMdlTexture(mdlPath)) {
                        SetCurrentTexture(null, string.Empty, "Could not load texture from selected MDL");
                        MessageBox.Show("This MDL contains no readable embedded texture. If a previous injection damaged it, restore the original MDL before editing again.",
                            "Texture Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                } catch (Exception ex) {
                    SetCurrentTexture(null, string.Empty, "Could not load selected MDL");
                    MessageBox.Show("Could not open the selected model texture: " + ex.Message,
                        "Texture Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            SharedState.OnEditPanoRequested += (bmp, bmpPath, nodPath, imgPath) => {
                try {
                    SetCurrentTexture(bmp, string.Empty,
                        "Editing panorama: " + Path.GetFileName(bmpPath));
                    loadedPanoBitmapPath = bmpPath ?? string.Empty;
                    loadedPanoNodPath = nodPath ?? string.Empty;
                    loadedPanoImgPath = imgPath ?? string.Empty;
                    btnInjectMdl.Enabled = false;
                    btnInjectGra.Text = "Inject to Pano";
                    btnInjectGra.Enabled = currentBitmap != null
                        && File.Exists(loadedPanoNodPath)
                        && File.Exists(loadedPanoImgPath);
                } finally {
                    if (bmp != null) bmp.Dispose();
                }
            };
        }

        private void LoadGraAnimation(Bitmap selectedFrame, string graPath, int collectionIndex,
            int frameIndex, int modeOverride, bool cmOverride)
        {
            List<Bitmap> frames = new GraConverter().DecodeCollectionFrames(
                graPath, collectionIndex, modeOverride, cmOverride);

            if (frames.Count == 0 || frameIndex < 0 || frameIndex >= frames.Count)
            {
                foreach (Bitmap frame in frames) frame.Dispose();
                SetCurrentTexture(selectedFrame, string.Empty,
                    "Editing GRA frame: " + Path.GetFileName(graPath));
                loadedGraPath = graPath;
                loadedGraCollectionIndex = collectionIndex;
                loadedGraFrameIndex = frameIndex;
                btnInjectMdl.Enabled = false;
                btnInjectGra.Enabled = !string.IsNullOrEmpty(graPath) && currentBitmap != null;
                SetStatus("Frame loaded; the full GRA animation could not be decoded.");
                return;
            }

            SetCurrentTexture(frames[frameIndex], string.Empty,
                "Editing GRA frame: " + Path.GetFileName(graPath));
            loadedGraFrames.AddRange(frames);
            foreach (Bitmap frame in frames)
                originalGraFrames.Add((Bitmap)frame.Clone());
            for (int index = 0; index < frames.Count; index++)
                dirtyGraFrames.Add(false);
            loadedGraPath = graPath;
            loadedGraCollectionIndex = collectionIndex;
            loadedGraFrameIndex = frameIndex;
            btnInjectMdl.Enabled = false;
            btnInjectGra.Enabled = true;
            BuildGraFrameTimeline();
            UpdateGraFrameSelection();
        }

        private void ClearLoadedGraFrames()
        {
            foreach (Bitmap frame in loadedGraFrames) frame.Dispose();
            loadedGraFrames.Clear();
            foreach (Bitmap frame in originalGraFrames) frame.Dispose();
            originalGraFrames.Clear();
            dirtyGraFrames.Clear();

            foreach (Button thumbnail in graFrameThumbnails)
            {
                if (thumbnail.Image != null)
                {
                    thumbnail.Image.Dispose();
                    thumbnail.Image = null;
                }
                thumbnail.Dispose();
            }
            graFrameThumbnails.Clear();
            if (graFrameStrip != null)
                graFrameStrip.Controls.Clear();
            SetGraTimelineVisible(false);
        }

        private void BuildGraFrameTimeline()
        {
            graFrameStrip.SuspendLayout();
            try
            {
                for (int index = 0; index < loadedGraFrames.Count; index++)
                {
                    int frameIndex = index;
                    Button thumbnail = new Button
                    {
                        Width = 54,
                        Height = 56,
                        Text = (index + 1).ToString(),
                        Image = CreateFrameThumbnail(loadedGraFrames[index]),
                        ImageAlign = ContentAlignment.TopCenter,
                        TextAlign = ContentAlignment.BottomCenter,
                        TextImageRelation = TextImageRelation.ImageAboveText,
                        FlatStyle = FlatStyle.Flat,
                        BackColor = QFG5Theme.ColorSurface,
                        ForeColor = QFG5Theme.ColorTextPrimary,
                        UseVisualStyleBackColor = false,
                        Margin = new Padding(2)
                    };
                    thumbnail.FlatAppearance.BorderSize = 1;
                    thumbnail.Click += (s, e) => SelectGraFrame(frameIndex);
                    toolTip.SetToolTip(thumbnail, "Edit GRA frame " + (index + 1));
                    graFrameThumbnails.Add(thumbnail);
                    graFrameStrip.Controls.Add(thumbnail);
                }
            }
            finally
            {
                graFrameStrip.ResumeLayout();
            }
            SetGraTimelineVisible(loadedGraFrames.Count > 1);
        }

        private static Bitmap CreateFrameThumbnail(Bitmap frame)
        {
            Bitmap thumbnail = new Bitmap(44, 38, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(thumbnail))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                float scale = Math.Min(44f / frame.Width, 38f / frame.Height);
                int width = Math.Max(1, (int)(frame.Width * scale));
                int height = Math.Max(1, (int)(frame.Height * scale));
                graphics.DrawImage(frame, (44 - width) / 2, (38 - height) / 2, width, height);
            }
            return thumbnail;
        }

        private void SetGraTimelineVisible(bool visible)
        {
            if (graFramePanel == null || centerColumn == null) return;
            graFramePanel.Visible = visible;
            centerColumn.RowStyles[1].Height = visible ? 82 : 0;
        }

        private void UpdateGraFrameThumbnail(int frameIndex)
        {
            if (frameIndex < 0 || frameIndex >= loadedGraFrames.Count ||
                frameIndex >= graFrameThumbnails.Count)
                return;

            Button thumbnail = graFrameThumbnails[frameIndex];
            if (thumbnail.Image != null)
                thumbnail.Image.Dispose();
            thumbnail.Image = CreateFrameThumbnail(loadedGraFrames[frameIndex]);
        }

        private void StoreCurrentGraFrame()
        {
            if (currentBitmap == null || loadedGraFrameIndex < 0 ||
                loadedGraFrameIndex >= loadedGraFrames.Count)
                return;

            Bitmap previous = loadedGraFrames[loadedGraFrameIndex];
            loadedGraFrames[loadedGraFrameIndex] = (Bitmap)currentBitmap.Clone();
            previous.Dispose();
            UpdateGraFrameThumbnail(loadedGraFrameIndex);
        }

        private void SelectGraFrame(int frameIndex)
        {
            if (frameIndex < 0 || frameIndex >= loadedGraFrames.Count ||
                frameIndex == loadedGraFrameIndex)
                return;

            if (currentTool == "Prop")
                SelectTool("Pencil");
            StoreCurrentGraFrame();
            string graPath = loadedGraPath;
            int collectionIndex = loadedGraCollectionIndex;
            SetCurrentTexture(loadedGraFrames[frameIndex], string.Empty,
                "Editing GRA frame " + (frameIndex + 1), true);
            loadedGraPath = graPath;
            loadedGraCollectionIndex = collectionIndex;
            loadedGraFrameIndex = frameIndex;
            btnInjectMdl.Enabled = false;
            btnInjectGra.Enabled = true;
            UpdateGraFrameSelection();
        }

        private void UpdateGraFrameSelection()
        {
            for (int index = 0; index < graFrameThumbnails.Count; index++)
            {
                Button thumbnail = graFrameThumbnails[index];
                thumbnail.BackColor = index == loadedGraFrameIndex
                    ? Color.FromArgb(83, 66, 39)
                    : QFG5Theme.ColorSurface;
                thumbnail.FlatAppearance.BorderColor = index == loadedGraFrameIndex
                    ? QFG5Theme.ColorGold
                    : QFG5Theme.ColorBorderOuter;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        private void InitializeComponent()
        {
            toolTip = new ToolTip();

            // Root: actions, context options, workspace
            QFG5TableLayoutPanel root = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(QFG5Layout.PagePadding)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ── Row 0: image, edit, and output actions ──────────────────────
            FlowLayoutPanel headerBar = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 4),
                Padding = new Padding(0)
            };

            Button btnOpen    = MakeHeaderBtn("Open Image", 104);
            Button btnOpenMdl = MakeHeaderBtn("Open MDL", 104);
            btnUndo = MakeHeaderBtn("Undo", 90);
            btnResetOriginal = MakeHeaderBtn("Reset Original", 130);
            Button btnSave    = MakeHeaderBtn("Save As", 100);
            btnProps = MakeHeaderBtn("Props", 72);
            btnProps.Click += (s, e) => TogglePropPanel();
            btnInjectMdl = MakeHeaderBtn("Inject to MDL", 130);
            btnInjectMdl.ForeColor = QFG5Theme.ColorGold;
            btnInjectMdl.Enabled = false;
            btnInjectMdl.Click += BtnInject_Click;
            btnInjectGra = MakeHeaderBtn("Inject to GRA", 130);
            btnInjectGra.ForeColor = QFG5Theme.ColorGold;
            btnInjectGra.Enabled = false;
            btnInjectGra.Click += BtnInjectGra_Click;
            btnUndo.Enabled = false;
            btnResetOriginal.Enabled = false;
            btnUndo.Click += (s, e) => Undo();
            btnResetOriginal.Click += (s, e) => ResetToOriginal();

            toolTip.SetToolTip(btnOpen,    "Open a BMP/PNG/JPG image for editing");
            toolTip.SetToolTip(btnOpenMdl, "Extract the embedded texture from a QFG5 MDL file");
            toolTip.SetToolTip(btnSave,    "Save the texture to disk as PNG or BMP");
            toolTip.SetToolTip(btnProps, "Show the prop sticker gallery and placement controls");
            toolTip.SetToolTip(btnInjectMdl, "Palette-preserving re-injection into the source MDL");
            toolTip.SetToolTip(btnInjectGra, "Write the edited frame back into its source GRA file, or inject a panorama opened from Pano Editor.");
            toolTip.SetToolTip(btnUndo,    "Undo the last drawing stroke or image adjustment");
            toolTip.SetToolTip(btnResetOriginal, "Restore the original extracted texture, using the saved pre-injection MDL backup when available");

            btnOpen.Click    += BtnOpen_Click;
            btnOpenMdl.Click += BtnOpenMdl_Click;
            btnSave.Click    += BtnSave_Click;

            headerBar.Controls.AddRange(new Control[] {
                btnOpen, btnOpenMdl, btnUndo, btnResetOriginal, btnSave, btnProps, btnInjectMdl, btnInjectGra
            });
            root.Controls.Add(headerBar, 0, 0);

            // ── Row 1: flat context options bar ─────────────────────────────
            QFG5TableLayoutPanel contextBar = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 9,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 5),
                Padding = new Padding(0)
            };
            contextBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            contextBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            contextBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            contextBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            contextBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            contextBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
            contextBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            contextBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102));
            contextBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
            Label lblZoom = MakeContextLabel("Zoom");
            Button btnZoomOut = MakeHeaderBtn("-", 30);
            Button btnZoomIn = MakeHeaderBtn("+", 30);
            trackZoom = new TrackBar {
                Minimum = 1,
                Maximum = 16,
                Value = (int)zoomLevel,
                Dock = DockStyle.Fill,
                TickFrequency = 1,
                BackColor = QFG5Theme.ColorSurface,
                Margin = new Padding(0, 0, 6, 0)
            };
            lblZoomPct = MakeContextLabel("100%");
            Label lblBrushSize = MakeContextLabel("Brush Size");
            NumericUpDown numBrush = new NumericUpDown {
                Minimum = 1,
                Maximum = 50,
                Value = brushSize,
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorSurface,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                TextAlign = HorizontalAlignment.Center,
                Height = 28,
                Margin = new Padding(2, 4, 8, 2)
            };
            btnHueSat = MakeHeaderBtn("Hue/Saturation", 100);
            btnOverlay = MakeHeaderBtn("Add Overlay", 92);
            btnHueSat.Dock = DockStyle.Fill;
            btnOverlay.Dock = DockStyle.Fill;
            btnHueSat.Margin = new Padding(3, 2, 3, 2);
            btnOverlay.Margin = new Padding(3, 2, 0, 2);
            btnHueSat.Click += (s, e) => ShowHueSatDialog();
            btnOverlay.Click += (s, e) => AddOverlay();
            toolTip.SetToolTip(btnHueSat, "Hue and saturation shift (whole image or selection)");
            toolTip.SetToolTip(btnOverlay, "Composite a PNG over the current texture");
            btnZoomOut.Click += (s, e) => { if (trackZoom.Value > trackZoom.Minimum) trackZoom.Value--; };
            btnZoomIn.Click += (s, e) => { if (trackZoom.Value < trackZoom.Maximum) trackZoom.Value++; };
            trackZoom.ValueChanged += (s, e) => {
                zoomLevel = trackZoom.Value;
                UpdateZoomLabel();
                canvas.Invalidate();
            };
            numBrush.ValueChanged += (s, e) => brushSize = (int)numBrush.Value;
            toolTip.SetToolTip(trackZoom, "Set zoom level (1x - 16x)");
            toolTip.SetToolTip(numBrush, "Brush size in pixels (Pencil and Eraser)");
            contextBar.Controls.Add(lblZoom, 0, 0);
            contextBar.Controls.Add(btnZoomOut, 1, 0);
            contextBar.Controls.Add(trackZoom, 2, 0);
            contextBar.Controls.Add(btnZoomIn, 3, 0);
            contextBar.Controls.Add(lblZoomPct, 4, 0);
            contextBar.Controls.Add(lblBrushSize, 5, 0);
            contextBar.Controls.Add(numBrush, 6, 0);
            contextBar.Controls.Add(btnHueSat, 7, 0);
            contextBar.Controls.Add(btnOverlay, 8, 0);
            root.Controls.Add(contextBar, 0, 1);

            // ── Row 2: toolbox and expanded canvas workspace ────────────────
            QFG5TableLayoutPanel workspace = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            FlowLayoutPanel toolbox = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Color.Transparent,
                Padding = new Padding(2, 2, 2, 0),
                Margin = new Padding(0, 0, 5, 0)
            };
            colorPreview = new Panel {
                Width = 34,
                Height = 34,
                BackColor = drawColor,
                Cursor = Cursors.Hand,
                Margin = new Padding(2, 2, 2, 6)
            };
            colorPreview.Click += (s, e) => {
                using (ColorDialog cd = new ColorDialog()) {
                    cd.Color = drawColor;
                    if (cd.ShowDialog() == DialogResult.OK) SetColor(cd.Color);
                }
            };
            toolTip.SetToolTip(colorPreview, "Active color — click to choose a color");
            toolbox.Controls.Add(colorPreview);
            btnPencil = MakeToolIconButton("Pencil", "Draw pixels with the active colour and brush size", toolTip);
            btnEraser = MakeToolIconButton("Eraser", "Erase pixels to transparency", toolTip);
            btnFill = MakeToolIconButton("Fill", "Flood-fill a region with the active colour", toolTip);
            btnPipette = MakeToolIconButton("Pick Color", "Sample the colour of a clicked pixel", toolTip);
            btnMagicWand = MakeToolIconButton("Magic Wand", "Select a contiguous region of similar colour", toolTip);
            btnSelectColor = MakeToolIconButton("Select Color", "Select all pixels matching the clicked colour", toolTip);
            btnLassoSelect = MakeToolIconButton("Lasso Select", "Draw a freeform selection around an area", toolTip);
            btnRectangleSelect = MakeToolIconButton("Rectangle Select", "Drag to select a rectangular area", toolTip);
            btnClearSelection = MakeToolIconButton("Clear Selection", "Clear the current selection", toolTip);
            btnPencil.Click += (s, e) => SelectTool("Pencil");
            btnEraser.Click += (s, e) => SelectTool("Eraser");
            btnFill.Click += (s, e) => SelectTool("Fill");
            btnPipette.Click += (s, e) => SelectTool("Pipette");
            btnMagicWand.Click += (s, e) => SelectTool("MagicWand");
            btnSelectColor.Click += (s, e) => SelectTool("SelectColor");
            btnLassoSelect.Click += (s, e) => SelectTool("LassoSelect");
            btnRectangleSelect.Click += (s, e) => SelectTool("RectangleSelect");
            btnClearSelection.Click += (s, e) => {
                selection.Clear();
                rectangularSelection = null;
                lassoPath.Clear();
                isSelecting = false;
                canvas.Invalidate();
                SetStatus("Selection cleared");
            };
            toolbox.Controls.AddRange(new Control[] {
                btnPencil, btnEraser, btnFill, btnPipette,
                btnMagicWand, btnSelectColor, btnLassoSelect,
                btnRectangleSelect, btnClearSelection
            });
            SelectTool("Pencil");
            workspace.Controls.Add(toolbox, 0, 0);

            canvas = new PictureBox {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Normal,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                Cursor = Cursors.Cross
            };
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseUp   += Canvas_MouseUp;
            canvas.Paint     += Canvas_Paint;
            toolTip.SetToolTip(canvas, "Click to place a selected prop; hold Shift while clicking to apply it to every GRA frame. Scroll to resize a prop, or hold Shift and scroll to zoom. Hold Space and drag or use the middle mouse button to pan.");
            workspace.Controls.Add(canvas, 1, 0);

            centerColumn = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            centerColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            centerColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            centerColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            centerColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            centerColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            centerColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            centerColumn.Controls.Add(canvas, 0, 0);

            graFramePanel = new Panel {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                Margin = new Padding(0),
                Visible = false
            };
            graFrameStrip = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                Margin = new Padding(0),
                Padding = new Padding(2, 0, 2, 0)
            };
            Label lblFrames = MakePropertyLabel("GRA FRAMES");
            lblFrames.Dock = DockStyle.Top;
            lblFrames.Height = 20;
            graFramePanel.Controls.Add(graFrameStrip);
            graFramePanel.Controls.Add(lblFrames);
            centerColumn.Controls.Add(graFramePanel, 0, 1);

            propPanel = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                Margin = new Padding(0),
                Visible = false
            };
            propPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            propPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            propThumbnailList = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                Margin = new Padding(0),
                Padding = new Padding(2)
            };
            propPanel.Controls.Add(propThumbnailList, 0, 0);
            FlowLayoutPanel propControls = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = QFG5Theme.ColorSurface,
                Padding = new Padding(3, 2, 3, 1),
                Margin = new Padding(0)
            };
            propControls.Controls.Add(MakePropOptionLabel("Scale"));
            numPropScale = new NumericUpDown {
                Minimum = 0.1m,
                Maximum = 5m,
                Increment = 0.1m,
                Value = 1m,
                DecimalPlaces = 2,
                Width = 58,
                Height = 26,
                BackColor = QFG5Theme.ColorSurface,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                TextAlign = HorizontalAlignment.Center,
                Margin = new Padding(2, 1, 8, 0)
            };
            propControls.Controls.Add(numPropScale);
            toolTip.SetToolTip(numPropScale, "Scroll over the canvas to resize the selected prop.");
            Button btnAddPngProp = MakeHeaderBtn("Add PNG", 76);
            btnAddPngProp.Height = 28;
            btnAddPngProp.Margin = new Padding(3, 0, 3, 0);
            btnAddPngProp.Click += (s, e) => AddCustomPngProp();
            toolTip.SetToolTip(btnAddPngProp, "Add a transparent PNG as a custom prop.");
            propControls.Controls.Add(btnAddPngProp);
            propControls.Controls.Add(MakePropOptionLabel("Rotate"));
            numPropRotation = new NumericUpDown {
                Minimum = -180,
                Maximum = 180,
                Increment = 5,
                Value = 0,
                Width = 58,
                Height = 26,
                BackColor = QFG5Theme.ColorSurface,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                TextAlign = HorizontalAlignment.Center,
                Margin = new Padding(2, 1, 8, 0)
            };
            propControls.Controls.Add(numPropRotation);
            btnCancelProp = MakeHeaderBtn("Cancel", 64);
            btnCancelProp.Height = 28;
            btnCancelProp.Margin = new Padding(3, 0, 3, 0);
            btnCancelProp.Click += (s, e) => CancelPropPlacement();
            propControls.Controls.Add(btnCancelProp);
            propPanel.Controls.Add(propControls, 0, 1);
            numPropScale.ValueChanged += (s, e) => canvas.Invalidate();
            numPropRotation.ValueChanged += (s, e) => canvas.Invalidate();
            centerColumn.Controls.Add(propPanel, 0, 2);

            Label lblPalette = MakePropertyLabel("GAME HIGH-COLOR SAMPLES (RGB555)");
            centerColumn.Controls.Add(lblPalette, 0, 3);

            paletteViewport = new Panel {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            paletteGrid = new TableLayoutPanel {
                ColumnCount = 64,
                RowCount = 4,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
                Margin = new Padding(0),
                Padding = new Padding(2),
                BackColor = QFG5Theme.ColorSurfaceRecessed
            };
            for (int column = 0; column < paletteGrid.ColumnCount; column++)
                paletteGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
            for (int row = 0; row < paletteGrid.RowCount; row++)
                paletteGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            paletteViewport.Controls.Add(paletteGrid);
            centerColumn.Controls.Add(paletteViewport, 0, 4);
            workspace.Controls.Add(centerColumn, 1, 0);
            root.Controls.Add(workspace, 0, 2);
            lblStatus = new Label {
                Text = "No texture loaded",
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.BodyBoldFont,
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 4, 0),
                Margin = new Padding(0)
            };
            QFG5TableLayoutPanel infoRow = new QFG5TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
            infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
            infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
            infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            infoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            Label lblPixelCaption = MakeInfoLabel("Pixel:", QFG5Theme.ColorTextMuted);
            lblMousePos = MakeInfoLabel("-", QFG5Theme.ColorTextPrimary);
            Label lblDimsCaption = MakeInfoLabel("Size:", QFG5Theme.ColorTextMuted);
            lblDimensions = MakeInfoLabel("-", QFG5Theme.ColorTextPrimary);
            Label lblMdlCaption = MakeInfoLabel("MDL:", QFG5Theme.ColorTextMuted);
            Label lblMdlName = MakeInfoLabel("(none)", QFG5Theme.ColorTextPrimary);
            this.Tag = lblMdlName;
            infoRow.Controls.Add(lblStatus, 0, 0);
            infoRow.Controls.Add(lblPixelCaption, 1, 0);
            infoRow.Controls.Add(lblMousePos, 2, 0);
            infoRow.Controls.Add(lblDimsCaption, 3, 0);
            infoRow.Controls.Add(lblDimensions, 4, 0);
            infoRow.Controls.Add(lblMdlCaption, 5, 0);
            infoRow.Controls.Add(lblMdlName, 6, 0);
            centerColumn.Controls.Add(infoRow, 0, 5);
            PopulateHighColorPalette();
            PopulatePropThumbnails();
            this.Controls.Add(root);
        }

        // ─── Control factory helpers ──────────────────────────────────────────
        private static Label MakeContextLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(2, 0, 2, 0)
            };
        }

        private static Label MakePropertyLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.SectionHeaderFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };
        }

        private static Label MakeInfoLabel(string text, Color color)
        {
            return new Label
            {
                Text = text,
                ForeColor = color,
                Font = QFG5Theme.CaptionFont,
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 0, 0),
                Margin = new Padding(0)
            };
        }

        private void PopulateHighColorPalette()
        {
            if (paletteGrid == null) return;
            paletteGrid.SuspendLayout();
            paletteGrid.Controls.Clear();
            List<Color> colors = CreateHighColorSamples();
            for (int index = 0; index < colors.Count; index++)
            {
                Color color = colors[index];
                Panel swatch = new Panel {
                    Dock = DockStyle.Fill,
                    BackColor = color,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(1),
                    BorderStyle = BorderStyle.FixedSingle
                };
                swatch.Click += (s, e) => SetColor(((Panel)s).BackColor);
                int red5 = color.R >> 3;
                int green5 = color.G >> 3;
                int blue5 = color.B >> 3;
                toolTip.SetToolTip(swatch, string.Format(
                    "RGB555: {0}, {1}, {2}  (#{3:X2}{4:X2}{5:X2})",
                    red5, green5, blue5, color.R, color.G, color.B));
                paletteGrid.Controls.Add(swatch, index % paletteGrid.ColumnCount, index / paletteGrid.ColumnCount);
            }
            paletteGrid.ResumeLayout();
        }

        private static List<Color> CreateHighColorSamples()
        {
            List<Color> candidates = new List<Color>(256);
            for (int level = 0; level < 32; level++)
                candidates.Add(Color.FromArgb(ExpandRgb555(level), ExpandRgb555(level), ExpandRgb555(level)));

            for (int hueIndex = 0; hueIndex < 64; hueIndex += 2)
                candidates.Add(ToRgb555(HSLToColor(hueIndex * 360f / 64f, 0.7f, 0.5f, 255)));
            AddHueRow(candidates, 1f, 0.5f);
            AddHueRow(candidates, 0.65f, 0.68f);
            AddHueRow(candidates, 1f, 0.34f);

            List<Color> colors = new List<Color>(256);
            HashSet<int> used = new HashSet<int>();
            foreach (Color color in candidates)
                AddUniqueColor(colors, used, color);

            for (int lightIndex = 0; colors.Count < 256 && lightIndex < 8; lightIndex++)
                for (int saturationIndex = 0; colors.Count < 256 && saturationIndex < 8; saturationIndex++)
                    for (int hueIndex = 0; colors.Count < 256 && hueIndex < 64; hueIndex++)
                    {
                        float hue = hueIndex * 360f / 64f;
                        float saturation = 0.25f + saturationIndex * 0.1f;
                        float lightness = 0.18f + lightIndex * 0.09f;
                        AddUniqueColor(colors, used,
                            ToRgb555(HSLToColor(hue, saturation, lightness, 255)));
                    }
            return colors;
        }

        private static void AddUniqueColor(List<Color> colors, HashSet<int> used, Color color)
        {
            if (used.Add(color.ToArgb()))
                colors.Add(color);
        }

        private static void AddHueRow(List<Color> colors, float saturation, float lightness)
        {
            for (int hueIndex = 0; hueIndex < 64; hueIndex++)
                colors.Add(ToRgb555(HSLToColor(hueIndex * 360f / 64f,
                    saturation, lightness, 255)));
        }

        private static Color ToRgb555(Color color)
        {
            return Color.FromArgb(
                ExpandRgb555(color.R >> 3),
                ExpandRgb555(color.G >> 3),
                ExpandRgb555(color.B >> 3));
        }

        private static Label MakePropOptionLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(4, 4, 0, 0)
            };
        }

        private void PopulatePropThumbnails()
        {
            string[] props = {
                "Glasses", "Round Glasses", "Sunglasses", "Eyes", "Eyebrows",
                "Mustache", "Handlebar", "Beard", "Goatee", "Stubble",
                "Flower", "Scar", "Blush", "Fangs",
                "Halo", "Bow", "Bandana", "Headphones", "Pirate Patch", "Monocle", "Heart Glasses",
                "Star Glasses", "Wink", "Sleepy Eyes", "Heart Eyes", "Star Eyes",
                "Tears", "Freckles", "Beauty Mark", "Clown Nose", "Face Paint",
                "Rainbow Paint", "Bandage", "Earrings", "Piercing", "Mask",
                "Medical Mask", "Beak", "Sparkle Eyes", "Angry Brows", "Confetti",
                "Lightning Bolt", "Antenna", "Dragon Scales", "Butterfly", "Rosy Hearts",
                "Googly Eyes", "Anime Eyes", "Wide Eyes", "Closed Eyes", "Crossed Eyes",
                "Spiral Eyes", "Robot Eyes", "Dollar Eyes", "Dizzy Eyes", "Side-Eye",
                "Happy Eyes", "Worried Eyes", "Blue Eyes", "Green Eyes"
            };

            foreach (string prop in props)
            {
                Button thumbnail = new Button
                {
                    Width = 92,
                    Height = 68,
                    Text = prop,
                    Image = CreatePropThumbnail(prop),
                    ImageAlign = ContentAlignment.TopCenter,
                    TextAlign = ContentAlignment.BottomCenter,
                    TextImageRelation = TextImageRelation.ImageAboveText,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = QFG5Theme.ColorSurface,
                    ForeColor = QFG5Theme.ColorTextPrimary,
                    UseVisualStyleBackColor = false,
                    Margin = new Padding(2)
                };
                thumbnail.FlatAppearance.BorderColor = QFG5Theme.ColorBorderOuter;
                thumbnail.FlatAppearance.BorderSize = 1;
                thumbnail.Click += (s, e) => SelectProp(prop, thumbnail);
                toolTip.SetToolTip(thumbnail, "Select " + prop + ", scroll over the canvas to resize, and click to apply.");
                propThumbnailList.Controls.Add(thumbnail);
            }
        }

        private void TogglePropPanel()
        {
            bool show = !propPanel.Visible;
            propPanel.Visible = show;
            centerColumn.RowStyles[2].Height = show ? 132 : 0;
            if (show && currentBitmap == null)
                SetStatus("Open an image before placing props.");
        }

        private void SelectProp(string prop, Button thumbnail)
        {
            if (currentBitmap == null)
            {
                SetStatus("Open an image before placing props.");
                return;
            }

            if (selectedPropImage != null) selectedPropImage.Dispose();
            selectedProp = prop;
            Bitmap customImage = thumbnail.Tag as Bitmap;
            selectedPropImage = customImage == null
                ? CreatePropSticker(prop)
                : FitPropImage(customImage, 96, 64);
            foreach (Control control in propThumbnailList.Controls)
            {
                Button button = control as Button;
                if (button != null)
                    button.FlatAppearance.BorderColor = button == thumbnail
                        ? QFG5Theme.ColorGold
                        : QFG5Theme.ColorBorderOuter;
            }
            currentTool = "Prop";
            propScreenPosition = new Point(canvas.Width / 2, canvas.Height / 2);
            hasPropPosition = true;
            canvas.Cursor = Cursors.Cross;
            canvas.Invalidate();
            SetStatus("Move the preview; scroll to resize, click to apply, or Shift-click to apply to all GRA frames.");
        }

        private void AddCustomPngProp()
        {
            using (OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "PNG images|*.png",
                Title = "Add a custom prop"
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                Bitmap customImage = null;
                Bitmap thumbnailImage = null;
                Button thumbnail = null;
                bool addedToGallery = false;
                try
                {
                    using (Image image = Image.FromFile(dialog.FileName))
                    {
                        if (image.RawFormat.Guid != ImageFormat.Png.Guid)
                            throw new InvalidDataException("Custom props must be PNG images.");
                        customImage = new Bitmap(image);
                    }

                    string propName = Path.GetFileNameWithoutExtension(dialog.FileName);
                    thumbnailImage = CreatePropThumbnail(customImage);
                    thumbnail = new Button
                    {
                        Width = 92,
                        Height = 68,
                        Text = propName,
                        Image = thumbnailImage,
                        ImageAlign = ContentAlignment.TopCenter,
                        TextAlign = ContentAlignment.BottomCenter,
                        TextImageRelation = TextImageRelation.ImageAboveText,
                        FlatStyle = FlatStyle.Flat,
                        BackColor = QFG5Theme.ColorSurface,
                        ForeColor = QFG5Theme.ColorTextPrimary,
                        UseVisualStyleBackColor = false,
                        Margin = new Padding(2),
                        Tag = customImage
                    };
                    thumbnail.FlatAppearance.BorderColor = QFG5Theme.ColorBorderOuter;
                    thumbnail.FlatAppearance.BorderSize = 1;
                    thumbnail.Click += (s, e) => SelectProp(propName, thumbnail);
                    toolTip.SetToolTip(thumbnail,
                        "Select " + propName + ", scroll over the canvas to resize, and click to apply.");
                    propThumbnailList.Controls.Add(thumbnail);
                    addedToGallery = true;
                    customImage = null;
                    thumbnailImage = null;
                    SelectProp(propName, thumbnail);
                }
                catch (Exception ex)
                {
                    if (!addedToGallery)
                    {
                        if (thumbnail != null)
                            thumbnail.Dispose();
                        if (thumbnailImage != null)
                            thumbnailImage.Dispose();
                        if (customImage != null)
                            customImage.Dispose();
                    }
                    SetStatus("Could not load custom PNG prop.");
                    Logger.LogError("TextureEditorTab", "Could not load custom PNG prop: " + ex.Message);
                    MessageBox.Show("Could not load the PNG prop: " + ex.Message,
                        "Texture Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ApplySelectedProp(bool applyToAllGraFrames = false)
        {
            if (currentBitmap == null || selectedPropImage == null || !hasPropPosition)
            {
                SetStatus("Select a prop and position its preview on the canvas first.");
                return;
            }

            int imageX, imageY;
            ScreenToImage(propScreenPosition.X, propScreenPosition.Y, out imageX, out imageY);
            imageX = Math.Max(0, Math.Min(currentBitmap.Width - 1, imageX));
            imageY = Math.Max(0, Math.Min(currentBitmap.Height - 1, imageY));
            bool applyToAnimation = applyToAllGraFrames &&
                loadedGraFrameIndex >= 0 && loadedGraFrames.Count > 1;
            CaptureUndoStep(applyToAnimation);
            if (applyToAnimation)
            {
                double relativeX = currentBitmap.Width > 1
                    ? (double)imageX / (currentBitmap.Width - 1)
                    : 0.5;
                double relativeY = currentBitmap.Height > 1
                    ? (double)imageY / (currentBitmap.Height - 1)
                    : 0.5;
                string appliedProp = selectedProp;
                for (int frameIndex = 0; frameIndex < loadedGraFrames.Count; frameIndex++)
                {
                    Bitmap frame = frameIndex == loadedGraFrameIndex
                        ? currentBitmap
                        : loadedGraFrames[frameIndex];
                    int frameX = frame.Width > 1
                        ? (int)Math.Round(relativeX * (frame.Width - 1))
                        : 0;
                    int frameY = frame.Height > 1
                        ? (int)Math.Round(relativeY * (frame.Height - 1))
                        : 0;
                    DrawPropSticker(frame, frameX, frameY);
                    if (frameIndex != loadedGraFrameIndex)
                    {
                        dirtyGraFrames[frameIndex] = true;
                        UpdateGraFrameThumbnail(frameIndex);
                    }
                }
                StoreCurrentGraFrame();
                SetStatus("Applied " + appliedProp + " to all " +
                    loadedGraFrames.Count + " GRA frames.");
            }
            else
            {
                DrawPropSticker(currentBitmap, imageX, imageY);
                if (loadedGraFrameIndex >= 0)
                    StoreCurrentGraFrame();
                SetStatus("Applied " + selectedProp + ".");
            }
            SelectTool("Pencil");
            canvas.Invalidate();
        }

        private void CancelPropPlacement()
        {
            SelectTool("Pencil");
            SetStatus("Prop placement cancelled.");
            canvas.Invalidate();
        }

        private Bitmap CreatePropSticker(string prop)
        {
            Bitmap sticker = new Bitmap(96, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(sticker))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.ScaleTransform(2f, 2f);
                DrawPropArt(graphics, prop);
            }
            return sticker;
        }

        private static Bitmap CreatePropThumbnail(string prop)
        {
            Bitmap thumbnail = new Bitmap(48, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(thumbnail))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                DrawPropArt(graphics, prop);
            }
            return thumbnail;
        }

        private static Bitmap CreatePropThumbnail(Image image)
        {
            Bitmap thumbnail = new Bitmap(48, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(thumbnail))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(image, FitImageRectangle(image.Width, image.Height, 48, 32));
            }
            return thumbnail;
        }

        private static Bitmap FitPropImage(Image image, int width, int height)
        {
            Bitmap prop = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(prop))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(image, FitImageRectangle(image.Width, image.Height, width, height));
            }
            return prop;
        }

        private static Rectangle FitImageRectangle(int sourceWidth, int sourceHeight,
            int targetWidth, int targetHeight)
        {
            float scale = Math.Min((float)targetWidth / sourceWidth,
                (float)targetHeight / sourceHeight);
            int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            return new Rectangle((targetWidth - width) / 2, (targetHeight - height) / 2,
                width, height);
        }

        private static int ExpandRgb555(int value)
        {
            return (value << 3) | (value >> 2);
        }

        private static Button MakeHeaderBtn(string text, int width)
        {
            Button button = MakeFlatButton(text, 32, DockStyle.None, new Padding(3));
            button.Width = width;
            button.TextAlign = ContentAlignment.MiddleCenter;
            return button;
        }

        private Button MakeToolIconButton(string name, string description, ToolTip toolTip)
        {
            Button button = new Button
            {
                Width = 38,
                Height = 38,
                Margin = new Padding(0, 0, 0, 4),
                Padding = new Padding(0),
                Text = string.Empty,
                AccessibleName = name,
                FlatStyle = FlatStyle.Flat,
                BackColor = QFG5Theme.ColorSurface,
                ForeColor = QFG5Theme.ColorTextPrimary,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = QFG5Theme.ColorBorderHighlight;
            button.FlatAppearance.MouseDownBackColor = QFG5Theme.ColorGoldMuted;
            button.Paint += (s, e) => DrawToolIcon(e.Graphics, button.ClientRectangle, name, button.ForeColor);
            toolTip.SetToolTip(button, description);
            return button;
        }

        private static Button MakeFlatButton(string text, int height, DockStyle dock, Padding margin)
        {
            Button button = new Button
            {
                Text = text,
                Height = height,
                Dock = dock,
                Margin = margin,
                Padding = new Padding(6, 0, 6, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(44, 37, 32),
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private static void DrawToolIcon(Graphics graphics, Rectangle bounds, string name, Color color)
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(color, 2f))
            using (SolidBrush brush = new SolidBrush(color))
            {
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                float left = bounds.Left + 9;
                float top = bounds.Top + 9;
                float right = bounds.Right - 9;
                float bottom = bounds.Bottom - 9;
                switch (name)
                {
                    case "Pencil":
                        graphics.DrawLine(pen, left, bottom, right - 2, top + 2);
                        graphics.DrawLine(pen, left + 1, bottom - 4, left + 5, bottom);
                        graphics.DrawLine(pen, right - 5, top, right, top + 5);
                        break;
                    case "Eraser":
                        PointF[] eraser = {
                            new PointF(left, top + 8), new PointF(left + 8, top),
                            new PointF(right, bottom - 8), new PointF(right - 8, bottom)
                        };
                        graphics.DrawPolygon(pen, eraser);
                        graphics.DrawLine(pen, left + 5, bottom - 5, right - 3, top + 5);
                        break;
                    case "Fill":
                        graphics.DrawLine(pen, left + 3, top + 2, right - 2, top + 12);
                        graphics.DrawLine(pen, right - 2, top + 12, left + 10, bottom - 2);
                        graphics.DrawLine(pen, left + 10, bottom - 2, left + 3, top + 2);
                        graphics.DrawLine(pen, left + 6, top + 8, left + 13, top + 13);
                        graphics.DrawLine(pen, left + 11, bottom - 3, right - 1, bottom - 3);
                        graphics.DrawLine(pen, right - 1, bottom - 3, right - 1, bottom - 8);
                        graphics.FillEllipse(brush, right - 4, bottom - 5, 3, 3);
                        break;
                    case "Pick Color":
                        graphics.DrawLine(pen, left + 3, bottom - 3, right - 1, top + 1);
                        graphics.DrawLine(pen, left + 1, bottom - 1, left + 5, bottom - 5);
                        graphics.DrawLine(pen, right - 4, top + 4, right, top);
                        graphics.DrawEllipse(pen, left, bottom - 5, 6, 6);
                        break;
                    case "Magic Wand":
                        graphics.DrawLine(pen, left + 3, bottom - 2, right - 3, top + 2);
                        graphics.DrawLine(pen, left + 1, top + 2, left + 1, top + 7);
                        graphics.DrawLine(pen, left - 1, top + 4, left + 3, top + 4);
                        graphics.DrawLine(pen, right - 2, bottom - 8, right - 2, bottom - 4);
                        graphics.DrawLine(pen, right - 4, bottom - 6, right, bottom - 6);
                        graphics.DrawLine(pen, right - 2, top - 1, right - 2, top + 3);
                        graphics.DrawLine(pen, right - 4, top + 1, right, top + 1);
                        break;
                    case "Select Color":
                        graphics.DrawEllipse(pen, left, top + 1, right - left, bottom - top - 2);
                        graphics.DrawLine(pen, left + 2, bottom - 3, right - 3, bottom - 3);
                        break;
                    case "Lasso Select":
                        using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                        {
                            path.AddBezier(left + 1, top + 9, left - 1, top + 2, right - 3, top - 1, right - 1, top + 5);
                            path.AddBezier(right - 1, top + 5, right + 1, bottom + 1, left + 2, bottom + 2, left + 1, top + 9);
                            graphics.DrawPath(pen, path);
                        }
                        graphics.DrawLine(pen, left + 1, top + 9, left + 5, top + 5);
                        break;
                    case "Rectangle Select":
                        using (Pen dashed = new Pen(color, 1.5f))
                        {
                            dashed.DashPattern = new float[] { 2f, 2f };
                            graphics.DrawRectangle(dashed, left, top + 1, right - left, bottom - top - 2);
                        }
                        break;
                    case "Clear Selection":
                        using (Pen dashed = new Pen(color, 1.5f))
                        {
                            dashed.DashPattern = new float[] { 2f, 2f };
                            graphics.DrawRectangle(dashed, left, top, right - left, bottom - top);
                        }
                        graphics.DrawLine(pen, left + 4, top + 4, right - 4, bottom - 4);
                        graphics.DrawLine(pen, right - 4, top + 4, left + 4, bottom - 4);
                        break;
                }
            }
        }

        // ─── Tool selection with visual feedback ──────────────────────────────
        private void SelectTool(string tool)
        {
            if (tool != "Prop")
            {
                selectedProp = null;
                hasPropPosition = false;
                if (selectedPropImage != null)
                {
                    selectedPropImage.Dispose();
                    selectedPropImage = null;
                }
                if (propThumbnailList != null)
                    foreach (Control control in propThumbnailList.Controls)
                    {
                        Button thumbnail = control as Button;
                        if (thumbnail != null)
                            thumbnail.FlatAppearance.BorderColor = QFG5Theme.ColorBorderOuter;
                    }
                if (canvas != null) canvas.Invalidate();
            }
            currentTool = tool;
            Button[] buttons = {
                btnPencil, btnEraser, btnFill, btnPipette, btnMagicWand,
                btnSelectColor, btnLassoSelect, btnRectangleSelect
            };
            foreach (Button button in buttons)
            {
                if (button == null) continue;
                button.ForeColor = QFG5Theme.ColorTextPrimary;
                button.BackColor = QFG5Theme.ColorSurface;
                button.Invalidate();
            }
            Button activeButton = null;
            switch (tool) {
                case "Pencil":      activeButton = btnPencil;      SetStatus("Tool: Pencil"); break;
                case "Eraser":      activeButton = btnEraser;      SetStatus("Tool: Eraser"); break;
                case "Fill":        activeButton = btnFill;        SetStatus("Tool: Fill"); break;
                case "Pipette":     activeButton = btnPipette;     SetStatus("Tool: Colour Picker"); break;
                case "MagicWand":   activeButton = btnMagicWand;   SetStatus("Tool: Magic Wand"); break;
                case "SelectColor": activeButton = btnSelectColor; SetStatus("Tool: Select Colour"); break;
                case "LassoSelect": activeButton = btnLassoSelect; SetStatus("Tool: Lasso Select"); break;
                case "RectangleSelect": activeButton = btnRectangleSelect; SetStatus("Tool: Rectangle Select"); break;
            }
            if (activeButton != null)
            {
                activeButton.ForeColor = QFG5Theme.ColorGoldBright;
                activeButton.BackColor = Color.FromArgb(83, 66, 39);
                activeButton.Invalidate();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        private bool LoadMdlTexture(string mdlPath)
        {
            string backupPath = mdlPath + OriginalMdlBackupSuffix;
            bool hasBackup = File.Exists(backupPath);
            string snapshotPath = hasBackup ? backupPath : mdlPath;
            OldMdlViewerLogic logic = new OldMdlViewerLogic();
            List<Bitmap> bitmaps = logic.getAllBMPs(snapshotPath);
            bool loadedFromBackup = hasBackup && bitmaps.Count > 0;
            if (bitmaps.Count == 0 && hasBackup)
                bitmaps = logic.getAllBMPs(mdlPath);
            if (bitmaps.Count == 0)
                return false;

            try
            {
                string sourceMessage = loadedFromBackup
                    ? "Loaded original texture from MDL backup"
                    : hasBackup
                        ? "Loaded current MDL texture; saved original backup was unreadable"
                        : "Loaded MDL texture";
                SetCurrentTexture(bitmaps[0], mdlPath, $"{sourceMessage}: {Path.GetFileName(mdlPath)}");
            }
            finally
            {
                foreach (Bitmap bitmap in bitmaps) bitmap.Dispose();
            }
            return true;
        }

        private void SetCurrentTexture(Bitmap bitmap, string mdlPath, string status, bool preserveGraFrames = false)
        {
            if (currentTool == "Prop")
                SelectTool("Pencil");
            Bitmap replacement = null;
            if (bitmap != null)
            {
                replacement = new Bitmap(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(replacement))
                    g.DrawImage(bitmap, 0, 0);
            }

            if (currentBitmap != null) currentBitmap.Dispose();
            if (originalBitmap != null) originalBitmap.Dispose();
            currentBitmap = replacement;
            originalBitmap = replacement == null ? null : (Bitmap)replacement.Clone();
            loadedMdlPath = mdlPath ?? string.Empty;
            loadedPanoBitmapPath = string.Empty;
            loadedPanoNodPath = string.Empty;
            loadedPanoImgPath = string.Empty;
            if (!preserveGraFrames)
                ClearLoadedGraFrames();
            loadedGraPath = string.Empty;
            loadedGraCollectionIndex = -1;
            loadedGraFrameIndex = -1;
            if (btnInjectMdl != null) btnInjectMdl.Enabled = !string.IsNullOrEmpty(loadedMdlPath) && currentBitmap != null;
            if (btnInjectGra != null)
            {
                btnInjectGra.Text = "Inject to GRA";
                btnInjectGra.Enabled = false;
            }
            selection.Clear();
            rectangularSelection = null;
            lassoPath.Clear();
            isSelecting = false;
            isPanning = false;
            panGestureActive = false;
            panOffsetX = panOffsetY = 0;
            ClearUndoHistory();
            if (btnResetOriginal != null) btnResetOriginal.Enabled = originalBitmap != null;
            UpdateDimensionsLabel();
            UpdateMdlLabel();
            if (canvas != null) canvas.Invalidate();
            SetStatus(status);
        }

        private void ClearUndoHistory()
        {
            while (undoHistory.Count > 0)
                undoHistory.Pop().Dispose();
            undoGraFrameIndices.Clear();
            ClearGraUndoHistory();
            if (btnUndo != null) btnUndo.Enabled = false;
        }

        private void ClearGraUndoHistory()
        {
            while (graUndoHistory.Count > 0)
            {
                Dictionary<int, Bitmap> frameSnapshots = graUndoHistory.Pop();
                if (frameSnapshots == null)
                    continue;
                foreach (Bitmap snapshot in frameSnapshots.Values)
                    snapshot.Dispose();
            }
        }

        private void CaptureUndoStep(bool captureOtherGraFrames = false)
        {
            if (currentBitmap == null) return;
            Dictionary<int, Bitmap> frameSnapshots = null;
            if (loadedGraFrameIndex >= 0 && loadedGraFrameIndex < dirtyGraFrames.Count)
            {
                dirtyGraFrames[loadedGraFrameIndex] = true;
                frameSnapshots = new Dictionary<int, Bitmap>();
                if (captureOtherGraFrames)
                    for (int index = 0; index < loadedGraFrames.Count; index++)
                        if (index != loadedGraFrameIndex)
                            frameSnapshots.Add(index, (Bitmap)loadedGraFrames[index].Clone());
            }
            if (undoHistory.Count >= MaxUndoSteps)
            {
                Bitmap[] oldSteps = undoHistory.ToArray();
                int[] oldFrameIndices = undoGraFrameIndices.ToArray();
                Dictionary<int, Bitmap>[] oldFrameSteps = graUndoHistory.ToArray();
                undoHistory.Clear();
                undoGraFrameIndices.Clear();
                graUndoHistory.Clear();
                oldSteps[MaxUndoSteps - 1].Dispose();
                DisposeGraFrameSnapshots(oldFrameSteps[MaxUndoSteps - 1]);
                for (int i = MaxUndoSteps - 2; i >= 0; i--)
                {
                    undoHistory.Push(oldSteps[i]);
                    undoGraFrameIndices.Push(oldFrameIndices[i]);
                    graUndoHistory.Push(oldFrameSteps[i]);
                }
            }
            undoHistory.Push((Bitmap)currentBitmap.Clone());
            undoGraFrameIndices.Push(
                loadedGraFrameIndex >= 0 && loadedGraFrameIndex < loadedGraFrames.Count
                    ? loadedGraFrameIndex
                    : -1);
            graUndoHistory.Push(frameSnapshots);
            if (btnUndo != null) btnUndo.Enabled = true;
        }

        private static void DisposeGraFrameSnapshots(Dictionary<int, Bitmap> frameSnapshots)
        {
            if (frameSnapshots == null)
                return;
            foreach (Bitmap snapshot in frameSnapshots.Values)
                snapshot.Dispose();
        }

        private void Undo()
        {
            if (undoHistory.Count == 0) return;
            Bitmap previousBitmap = undoHistory.Pop();
            int actionFrameIndex = undoGraFrameIndices.Count > 0
                ? undoGraFrameIndices.Pop()
                : -1;
            Dictionary<int, Bitmap> frameSnapshots = graUndoHistory.Count > 0
                ? graUndoHistory.Pop()
                : null;
            if (actionFrameIndex < 0 ||
                actionFrameIndex == loadedGraFrameIndex)
            {
                currentBitmap.Dispose();
                currentBitmap = previousBitmap;
            }
            else if (actionFrameIndex < loadedGraFrames.Count)
            {
                Bitmap replacedFrame = loadedGraFrames[actionFrameIndex];
                loadedGraFrames[actionFrameIndex] = previousBitmap;
                replacedFrame.Dispose();
                dirtyGraFrames[actionFrameIndex] =
                    !BitmapsEqual(originalGraFrames[actionFrameIndex], previousBitmap);
                UpdateGraFrameThumbnail(actionFrameIndex);
            }
            bool activeGraFrameWasRestored = frameSnapshots != null &&
                frameSnapshots.ContainsKey(loadedGraFrameIndex);
            if (frameSnapshots != null)
            {
                foreach (KeyValuePair<int, Bitmap> snapshot in frameSnapshots)
                {
                    Bitmap previous = loadedGraFrames[snapshot.Key];
                    loadedGraFrames[snapshot.Key] = snapshot.Value;
                    previous.Dispose();
                    dirtyGraFrames[snapshot.Key] =
                        !BitmapsEqual(originalGraFrames[snapshot.Key], snapshot.Value);
                    UpdateGraFrameThumbnail(snapshot.Key);
                }
                frameSnapshots.Clear();
            }
            if (loadedGraFrameIndex >= 0 && loadedGraFrameIndex < dirtyGraFrames.Count)
            {
                if (activeGraFrameWasRestored)
                {
                    currentBitmap.Dispose();
                    currentBitmap = (Bitmap)loadedGraFrames[loadedGraFrameIndex].Clone();
                }
                dirtyGraFrames[loadedGraFrameIndex] =
                    !BitmapsEqual(originalGraFrames[loadedGraFrameIndex], currentBitmap);
                StoreCurrentGraFrame();
            }
            if (btnUndo != null) btnUndo.Enabled = undoHistory.Count > 0;
            UpdateDimensionsLabel();
            canvas.Invalidate();
            SetStatus("Last edit undone");
        }

        private void ResetToOriginal()
        {
            if (originalBitmap == null)
            {
                SetStatus("No original texture to restore");
                return;
            }
            if (currentBitmap != null) currentBitmap.Dispose();
            currentBitmap = (Bitmap)originalBitmap.Clone();
            if (loadedGraFrameIndex >= 0 && loadedGraFrameIndex < dirtyGraFrames.Count)
            {
                dirtyGraFrames[loadedGraFrameIndex] =
                    !BitmapsEqual(originalGraFrames[loadedGraFrameIndex], currentBitmap);
                StoreCurrentGraFrame();
            }
            selection.Clear();
            rectangularSelection = null;
            ClearUndoHistory();
            UpdateDimensionsLabel();
            UpdateMdlLabel();
            canvas.Invalidate();
            SetStatus("Restored freshly extracted original texture");
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
            if (zoomLevel <= 1.0f)
            {
                panOffsetX = 0;
                panOffsetY = 0;
            }
            if (canvas != null) canvas.Invalidate();
        }

        // ─────────────────────────────────────────────────────────────────────
        //   MOUSE / DRAWING
        // ─────────────────────────────────────────────────────────────────────
        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            strokeUndoCaptured = false;
            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && spaceHeld))
            {
                BeginPan(e.Location);
                return;
            }
            if (e.Button != MouseButtons.Left || currentBitmap == null) return;
            if (currentTool == "Prop")
            {
                propScreenPosition = e.Location;
                hasPropPosition = true;
                ApplySelectedProp((Control.ModifierKeys & Keys.Shift) != 0);
                return;
            }
            if (currentTool == "RectangleSelect" || currentTool == "LassoSelect")
            {
                int imageX, imageY;
                ScreenToImage(e.X, e.Y, out imageX, out imageY);
                if (imageX < 0 || imageX >= currentBitmap.Width || imageY < 0 || imageY >= currentBitmap.Height)
                    return;
                isSelecting = true;
                selection.Clear();
                rectangularSelection = null;
                selectionStart = selectionCurrent = new Point(imageX, imageY);
                lassoPath.Clear();
                if (currentTool == "LassoSelect") lassoPath.Add(selectionStart);
                canvas.Invalidate();
                return;
            }
            DrawAt(e.X, e.Y);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            UpdateMouseLabel(e.X, e.Y);
            if (panGestureActive)
            {
                if (isPanning)
                {
                    panOffsetX = panStartOffset.X + e.X - panStartPoint.X;
                    panOffsetY = panStartOffset.Y + e.Y - panStartPoint.Y;
                    canvas.Invalidate();
                }
                return;
            }
            if (currentTool == "Prop" && selectedPropImage != null)
            {
                propScreenPosition = e.Location;
                hasPropPosition = true;
                canvas.Invalidate();
                return;
            }
            if (isSelecting)
            {
                int imageX, imageY;
                ScreenToImage(e.X, e.Y, out imageX, out imageY);
                selectionCurrent = new Point(
                    Math.Max(0, Math.Min(currentBitmap.Width - 1, imageX)),
                    Math.Max(0, Math.Min(currentBitmap.Height - 1, imageY)));
                if (currentTool == "LassoSelect" &&
                    (lassoPath.Count == 0 || lassoPath[lassoPath.Count - 1] != selectionCurrent))
                    lassoPath.Add(selectionCurrent);
                canvas.Invalidate();
                return;
            }
            if (e.Button == MouseButtons.Left) DrawAt(e.X, e.Y);
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (panGestureActive)
            {
                isPanning = false;
                panGestureActive = false;
                canvas.Cursor = spaceHeld ? Cursors.SizeAll : Cursors.Cross;
                strokeUndoCaptured = false;
                return;
            }
            else if (isSelecting)
            {
                if (currentTool == "RectangleSelect")
                    CreateRectangleSelection(selectionStart, selectionCurrent);
                else
                    CreateLassoSelection();
                isSelecting = false;
                lassoPath.Clear();
                canvas.Invalidate();
                SetStatus(HasSelection() ? "Selection created" : "Selection is empty");
            }
            strokeUndoCaptured = false;
        }

        private void BeginPan(Point location)
        {
            isPanning = true;
            panGestureActive = true;
            panStartPoint = location;
            panStartOffset = new PointF(panOffsetX, panOffsetY);
            canvas.Cursor = Cursors.SizeAll;
        }

        public bool PreFilterMessage(ref Message message)
        {
            const int WM_MOUSEWHEEL = 0x020A;
            const int WM_KEYDOWN = 0x0100;
            const int WM_KEYUP = 0x0101;
            const int WM_SYSKEYDOWN = 0x0104;
            const int WM_SYSKEYUP = 0x0105;
            if (message.Msg == WM_MOUSEWHEEL)
            {
                bool pointerOverCanvasForWheel = canvas != null && canvas.Visible &&
                    canvas.RectangleToScreen(canvas.ClientRectangle).Contains(Cursor.Position);
                if (!pointerOverCanvasForWheel)
                    return false;

                int delta = unchecked((short)(message.WParam.ToInt64() >> 16));
                int steps = delta / 120;
                if (steps == 0)
                    steps = Math.Sign(delta);
                if ((Control.ModifierKeys & Keys.Shift) != 0)
                {
                    if (trackZoom == null || steps == 0)
                        return false;
                    trackZoom.Value = Math.Max(trackZoom.Minimum,
                        Math.Min(trackZoom.Maximum, trackZoom.Value + steps));
                    return true;
                }

                if (currentTool != "Prop" || selectedPropImage == null ||
                    numPropScale == null || steps == 0)
                    return false;
                decimal newScale = numPropScale.Value + steps * numPropScale.Increment;
                newScale = Math.Max(numPropScale.Minimum, Math.Min(numPropScale.Maximum, newScale));
                numPropScale.Value = newScale;
                SetStatus(string.Format(
                    "Prop size {0:0.0}x — scroll to resize, click the canvas to apply.",
                    numPropScale.Value));
                return true;
            }

            bool keyDown = message.Msg == WM_KEYDOWN || message.Msg == WM_SYSKEYDOWN;
            bool keyUp = message.Msg == WM_KEYUP || message.Msg == WM_SYSKEYUP;
            if (!keyDown && !keyUp) return false;
            if ((Keys)(int)message.WParam != Keys.Space) return false;

            bool pointerOverCanvas = canvas != null && canvas.Visible &&
                canvas.RectangleToScreen(canvas.ClientRectangle).Contains(Cursor.Position);
            if (keyDown && pointerOverCanvas)
            {
                spaceHeld = true;
                canvas.Cursor = Cursors.SizeAll;
                return true;
            }
            if (keyUp && spaceHeld)
            {
                spaceHeld = false;
                if (isPanning) isPanning = false;
                if (canvas != null) canvas.Cursor = Cursors.Cross;
                return true;
            }
            return false;
        }

        private void CreateRectangleSelection(Point start, Point end)
        {
            int left = Math.Min(start.X, end.X);
            int right = Math.Max(start.X, end.X);
            int top = Math.Min(start.Y, end.Y);
            int bottom = Math.Max(start.Y, end.Y);
            rectangularSelection = new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }

        private void CreateLassoSelection()
        {
            rectangularSelection = null;
            if (lassoPath.Count < 3)
            {
                selection.Clear();
                return;
            }

            int minY = lassoPath.Min(point => point.Y);
            int maxY = lassoPath.Max(point => point.Y);
            for (int y = minY; y <= maxY; y++)
            {
                float scanY = y + 0.5f;
                List<float> intersections = new List<float>();
                for (int i = 0, j = lassoPath.Count - 1; i < lassoPath.Count; j = i++)
                {
                    Point current = lassoPath[i];
                    Point previous = lassoPath[j];
                    if ((current.Y > scanY) == (previous.Y > scanY)) continue;
                    intersections.Add(current.X +
                        (scanY - current.Y) * (previous.X - current.X) /
                        (previous.Y - current.Y));
                }
                intersections.Sort();
                for (int i = 0; i + 1 < intersections.Count; i += 2)
                {
                    int left = Math.Max(0, (int)Math.Ceiling(intersections[i] - 0.5f));
                    int right = Math.Min(currentBitmap.Width - 1,
                        (int)Math.Floor(intersections[i + 1] - 0.5f));
                    for (int x = left; x <= right; x++)
                        selection.Add(new Point(x, y));
                }
            }
        }

        private bool HasSelection()
        {
            return rectangularSelection.HasValue || selection.Count > 0;
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
                bool editsPixels = currentTool == "Pencil" || currentTool == "Eraser" || currentTool == "Fill";
                if (editsPixels && !strokeUndoCaptured)
                {
                    CaptureUndoStep();
                    strokeUndoCaptured = true;
                }
                if (currentTool == "Pencil") {
                    using (Graphics g = Graphics.FromImage(currentBitmap))
                    using (SolidBrush brush = new SolidBrush(drawColor))
                        g.FillRectangle(brush, imgX - brushSize/2, imgY - brushSize/2, brushSize, brushSize);
                } else if (currentTool == "Eraser") {
                    using (Graphics g = Graphics.FromImage(currentBitmap)) {
                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        using (SolidBrush brush = new SolidBrush(Color.Transparent))
                            g.FillRectangle(brush, imgX - brushSize/2, imgY - brushSize/2, brushSize, brushSize);
                    }
                } else if (currentTool == "Fill")        FloodFill(imgX, imgY, drawColor);
                else if (currentTool == "MagicWand")     MagicWandSelect(imgX, imgY);
                else if (currentTool == "SelectColor")   SelectColorCluster(imgX, imgY);
                else if (currentTool == "Pipette")       SetColor(currentBitmap.GetPixel(imgX, imgY));
                canvas.Invalidate();
            }
        }

        private void DrawPropSticker(Bitmap targetBitmap, int centerX, int centerY)
        {
            if (selectedPropImage == null || numPropScale == null || numPropRotation == null)
                return;
            float width = Math.Max(8f, Math.Min(96f, targetBitmap.Width * 0.55f)) *
                (float)numPropScale.Value;
            float height = width * 2f / 3f;
            using (Graphics graphics = Graphics.FromImage(targetBitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.TranslateTransform(centerX, centerY);
                graphics.RotateTransform((float)numPropRotation.Value);
                graphics.ScaleTransform(width / 96f, height / 64f);
                graphics.DrawImage(selectedPropImage, -48, -32, 96, 64);
            }
        }

        private static void DrawPropArt(Graphics graphics, string prop)
        {
            using (SolidBrush dark = new SolidBrush(Color.FromArgb(255, 35, 27, 25)))
            using (SolidBrush light = new SolidBrush(Color.FromArgb(255, 242, 225, 190)))
            using (SolidBrush blue = new SolidBrush(Color.FromArgb(210, 95, 180, 220)))
            using (SolidBrush brown = new SolidBrush(Color.FromArgb(255, 92, 52, 34)))
            using (SolidBrush gold = new SolidBrush(Color.FromArgb(255, 245, 190, 48)))
            using (SolidBrush red = new SolidBrush(Color.FromArgb(255, 190, 50, 42)))
            using (SolidBrush pink = new SolidBrush(Color.FromArgb(230, 238, 112, 145)))
            using (SolidBrush green = new SolidBrush(Color.FromArgb(255, 54, 132, 61)))
            using (SolidBrush purple = new SolidBrush(Color.FromArgb(255, 125, 72, 190)))
            using (SolidBrush teal = new SolidBrush(Color.FromArgb(255, 45, 175, 165)))
            using (SolidBrush cyan = new SolidBrush(Color.FromArgb(255, 80, 210, 230)))
            using (SolidBrush orange = new SolidBrush(Color.FromArgb(255, 245, 135, 38)))
            using (SolidBrush white = new SolidBrush(Color.FromArgb(255, 255, 255, 245)))
            using (SolidBrush silver = new SolidBrush(Color.FromArgb(255, 185, 198, 210)))
            using (Pen outline = new Pen(Color.FromArgb(255, 35, 27, 25), 2.5f))
            using (Pen scarPen = new Pen(red.Color, 4f))
            using (Pen stemPen = new Pen(green.Color, 3f))
            {
                switch (prop)
                {
                    case "Glasses":
                        graphics.FillRectangle(dark, 2, 8, 20, 15);
                        graphics.FillRectangle(dark, 26, 8, 20, 15);
                        graphics.FillRectangle(blue, 5, 11, 14, 9);
                        graphics.FillRectangle(blue, 29, 11, 14, 9);
                        graphics.DrawLine(outline, 20, 13, 28, 13);
                        graphics.DrawLine(outline, 1, 10, 0, 7);
                        graphics.DrawLine(outline, 47, 10, 48, 7);
                        break;
                    case "Round Glasses":
                        graphics.DrawEllipse(outline, 3, 6, 19, 19);
                        graphics.DrawEllipse(outline, 26, 6, 19, 19);
                        graphics.DrawLine(outline, 21, 13, 27, 13);
                        graphics.DrawLine(outline, 3, 10, 0, 8);
                        graphics.DrawLine(outline, 45, 10, 48, 8);
                        break;
                    case "Sunglasses":
                        graphics.FillRectangle(dark, 2, 8, 21, 16);
                        graphics.FillRectangle(dark, 25, 8, 21, 16);
                        graphics.FillRectangle(blue, 5, 11, 15, 10);
                        graphics.FillRectangle(blue, 28, 11, 15, 10);
                        graphics.DrawLine(outline, 21, 13, 27, 13);
                        graphics.DrawLine(Pens.Gold, 8, 12, 14, 12);
                        graphics.DrawLine(Pens.Gold, 31, 12, 37, 12);
                        break;
                    case "Eyes":
                        graphics.DrawLine(outline, 4, 9, 20, 7);
                        graphics.DrawLine(outline, 28, 7, 44, 9);
                        graphics.FillEllipse(light, 4, 11, 17, 12);
                        graphics.FillEllipse(light, 27, 11, 17, 12);
                        graphics.FillEllipse(dark, 10, 12, 7, 10);
                        graphics.FillEllipse(dark, 32, 12, 7, 10);
                        break;
                    case "Googly Eyes":
                        graphics.FillEllipse(white, 3, 3, 20, 27);
                        graphics.FillEllipse(white, 25, 3, 20, 27);
                        graphics.DrawEllipse(outline, 3, 3, 20, 27);
                        graphics.DrawEllipse(outline, 25, 3, 20, 27);
                        graphics.FillEllipse(dark, 14, 12, 7, 10);
                        graphics.FillEllipse(dark, 28, 12, 7, 10);
                        graphics.FillEllipse(white, 15, 13, 2, 2);
                        graphics.FillEllipse(white, 29, 13, 2, 2);
                        break;
                    case "Anime Eyes":
                        graphics.FillEllipse(white, 3, 5, 19, 25);
                        graphics.FillEllipse(white, 26, 5, 19, 25);
                        graphics.FillEllipse(blue, 7, 10, 12, 17);
                        graphics.FillEllipse(blue, 29, 10, 12, 17);
                        graphics.FillEllipse(dark, 10, 15, 7, 12);
                        graphics.FillEllipse(dark, 32, 15, 7, 12);
                        graphics.FillEllipse(white, 8, 10, 6, 6);
                        graphics.FillEllipse(white, 30, 10, 6, 6);
                        graphics.DrawArc(outline, 2, 3, 21, 10, 185, 170);
                        graphics.DrawArc(outline, 25, 3, 21, 10, 185, 170);
                        break;
                    case "Wide Eyes":
                        graphics.FillEllipse(white, 2, 3, 21, 28);
                        graphics.FillEllipse(white, 25, 3, 21, 28);
                        graphics.DrawEllipse(outline, 2, 3, 21, 28);
                        graphics.DrawEllipse(outline, 25, 3, 21, 28);
                        graphics.FillEllipse(blue, 9, 11, 9, 14);
                        graphics.FillEllipse(blue, 31, 11, 9, 14);
                        graphics.FillEllipse(dark, 11, 14, 5, 9);
                        graphics.FillEllipse(dark, 33, 14, 5, 9);
                        break;
                    case "Closed Eyes":
                        graphics.DrawArc(outline, 3, 9, 19, 13, 10, 160);
                        graphics.DrawArc(outline, 26, 9, 19, 13, 10, 160);
                        graphics.DrawArc(Pens.Gray, 5, 12, 15, 9, 10, 160);
                        graphics.DrawArc(Pens.Gray, 28, 12, 15, 9, 10, 160);
                        break;
                    case "Crossed Eyes":
                        graphics.FillEllipse(white, 3, 8, 19, 18);
                        graphics.FillEllipse(white, 26, 8, 19, 18);
                        graphics.FillEllipse(dark, 16, 14, 6, 8);
                        graphics.FillEllipse(dark, 26, 14, 6, 8);
                        graphics.DrawArc(outline, 2, 5, 21, 12, 185, 170);
                        graphics.DrawArc(outline, 25, 5, 21, 12, 185, 170);
                        break;
                    case "Spiral Eyes":
                        graphics.FillEllipse(white, 3, 7, 19, 20);
                        graphics.FillEllipse(white, 26, 7, 19, 20);
                        graphics.DrawEllipse(outline, 3, 7, 19, 20);
                        graphics.DrawEllipse(outline, 26, 7, 19, 20);
                        graphics.DrawArc(Pens.Purple, 8, 12, 10, 10, 0, 300);
                        graphics.DrawArc(Pens.Purple, 31, 12, 10, 10, 0, 300);
                        graphics.FillEllipse(purple, 12, 16, 3, 3);
                        graphics.FillEllipse(purple, 35, 16, 3, 3);
                        break;
                    case "Robot Eyes":
                        graphics.FillRectangle(dark, 2, 7, 21, 20);
                        graphics.FillRectangle(dark, 25, 7, 21, 20);
                        graphics.FillRectangle(cyan, 5, 10, 15, 14);
                        graphics.FillRectangle(cyan, 28, 10, 15, 14);
                        graphics.FillEllipse(white, 9, 12, 7, 10);
                        graphics.FillEllipse(white, 32, 12, 7, 10);
                        graphics.DrawLine(Pens.Silver, 4, 5, 21, 5);
                        graphics.DrawLine(Pens.Silver, 27, 5, 44, 5);
                        break;
                    case "Dollar Eyes":
                        using (Font dollarFont = new Font("Arial", 18f, FontStyle.Bold))
                        {
                            graphics.DrawString("$", dollarFont, green, new PointF(4, 3));
                            graphics.DrawString("$", dollarFont, green, new PointF(27, 3));
                        }
                        graphics.DrawEllipse(outline, 2, 5, 21, 23);
                        graphics.DrawEllipse(outline, 25, 5, 21, 23);
                        break;
                    case "Dizzy Eyes":
                        graphics.FillEllipse(white, 3, 7, 19, 20);
                        graphics.FillEllipse(white, 26, 7, 19, 20);
                        graphics.DrawLine(outline, 7, 13, 18, 22);
                        graphics.DrawLine(outline, 18, 13, 7, 22);
                        graphics.DrawLine(outline, 30, 13, 41, 22);
                        graphics.DrawLine(outline, 41, 13, 30, 22);
                        break;
                    case "Side-Eye":
                        graphics.FillEllipse(white, 3, 9, 19, 16);
                        graphics.FillEllipse(white, 26, 9, 19, 16);
                        graphics.FillEllipse(dark, 14, 13, 6, 9);
                        graphics.FillEllipse(dark, 37, 13, 6, 9);
                        graphics.DrawLine(outline, 3, 8, 21, 6);
                        graphics.DrawLine(outline, 27, 6, 45, 8);
                        break;
                    case "Happy Eyes":
                        graphics.DrawArc(outline, 3, 8, 18, 16, 190, 160);
                        graphics.DrawArc(outline, 27, 8, 18, 16, 190, 160);
                        graphics.DrawArc(Pens.Brown, 5, 11, 14, 11, 190, 160);
                        graphics.DrawArc(Pens.Brown, 29, 11, 14, 11, 190, 160);
                        break;
                    case "Worried Eyes":
                        graphics.FillEllipse(white, 4, 10, 17, 17);
                        graphics.FillEllipse(white, 27, 10, 17, 17);
                        graphics.FillEllipse(blue, 10, 17, 7, 9);
                        graphics.FillEllipse(blue, 33, 17, 7, 9);
                        graphics.FillEllipse(dark, 12, 19, 4, 7);
                        graphics.FillEllipse(dark, 35, 19, 4, 7);
                        graphics.DrawLine(outline, 4, 9, 18, 4);
                        graphics.DrawLine(outline, 30, 4, 44, 9);
                        break;
                    case "Blue Eyes":
                    case "Green Eyes":
                        SolidBrush iris = prop == "Blue Eyes" ? blue : green;
                        graphics.FillEllipse(white, 4, 9, 17, 17);
                        graphics.FillEllipse(white, 27, 9, 17, 17);
                        graphics.FillEllipse(iris, 9, 12, 10, 13);
                        graphics.FillEllipse(iris, 32, 12, 10, 13);
                        graphics.FillEllipse(dark, 12, 15, 5, 10);
                        graphics.FillEllipse(dark, 35, 15, 5, 10);
                        graphics.FillEllipse(white, 10, 12, 3, 3);
                        graphics.FillEllipse(white, 33, 12, 3, 3);
                        break;
                    case "Eyebrows":
                        graphics.DrawArc(outline, 4, 5, 17, 10, 185, 170);
                        graphics.DrawArc(outline, 27, 5, 17, 10, 185, 170);
                        break;
                    case "Mustache":
                        graphics.FillEllipse(dark, 4, 9, 21, 17);
                        graphics.FillEllipse(dark, 23, 9, 21, 17);
                        graphics.FillEllipse(brown, 7, 12, 15, 10);
                        graphics.FillEllipse(brown, 26, 12, 15, 10);
                        break;
                    case "Handlebar":
                        graphics.FillEllipse(dark, 5, 11, 19, 13);
                        graphics.FillEllipse(dark, 24, 11, 19, 13);
                        graphics.DrawBezier(outline, 8, 17, 0, 8, 1, 27, 9, 25);
                        graphics.DrawBezier(outline, 40, 17, 48, 8, 47, 27, 39, 25);
                        break;
                    case "Beard":
                    case "Goatee":
                        if (prop == "Beard")
                        {
                            graphics.FillPolygon(dark, new[] {
                                new Point(5, 3), new Point(43, 3), new Point(42, 20),
                                new Point(34, 26), new Point(24, 31), new Point(14, 26), new Point(6, 20)
                            });
                            graphics.FillPolygon(brown, new[] {
                                new Point(9, 8), new Point(39, 8), new Point(37, 19),
                                new Point(24, 27), new Point(11, 19)
                            });
                        }
                        else
                        {
                            graphics.FillEllipse(dark, 15, 8, 18, 22);
                            graphics.FillEllipse(brown, 18, 12, 12, 13);
                        }
                        break;
                    case "Stubble":
                        for (int y = 9; y < 25; y += 5)
                            for (int x = 8 + (y % 2) * 2; x < 43; x += 7)
                                graphics.FillEllipse(dark, x, y, 2, 2);
                        break;
                    case "Crown":
                        graphics.FillPolygon(gold, new[] {
                            new Point(3, 25), new Point(7, 8), new Point(17, 17),
                            new Point(24, 4), new Point(31, 17), new Point(41, 8),
                            new Point(45, 25), new Point(43, 30), new Point(5, 30)
                        });
                        graphics.DrawRectangle(outline, 5, 24, 38, 6);
                        graphics.FillEllipse(red, 6, 7, 5, 5);
                        graphics.FillEllipse(red, 21, 2, 6, 6);
                        graphics.FillEllipse(red, 38, 7, 5, 5);
                        break;
                    case "Top Hat":
                        graphics.FillRectangle(dark, 7, 19, 34, 8);
                        graphics.FillRectangle(dark, 13, 4, 22, 16);
                        graphics.FillRectangle(red, 13, 16, 22, 4);
                        break;
                    case "Flower":
                        graphics.FillEllipse(pink, 17, 2, 14, 14);
                        graphics.FillEllipse(pink, 17, 16, 14, 14);
                        graphics.FillEllipse(pink, 9, 9, 14, 14);
                        graphics.FillEllipse(pink, 25, 9, 14, 14);
                        graphics.FillEllipse(gold, 18, 10, 12, 12);
                        graphics.DrawLine(stemPen, 24, 22, 24, 32);
                        break;
                    case "Scar":
                        graphics.DrawLine(scarPen, 9, 24, 36, 7);
                        graphics.DrawLine(Pens.LightCoral, 11, 24, 38, 7);
                        break;
                    case "Blush":
                        graphics.FillEllipse(pink, 3, 13, 15, 8);
                        graphics.FillEllipse(pink, 30, 13, 15, 8);
                        break;
                    case "Fangs":
                        graphics.FillPolygon(light, new[] { new Point(10, 7), new Point(21, 7), new Point(17, 24) });
                        graphics.FillPolygon(light, new[] { new Point(27, 7), new Point(38, 7), new Point(32, 24) });
                        graphics.DrawLine(outline, 7, 7, 41, 7);
                        break;
                    case "Cat Ears":
                        graphics.FillPolygon(dark, new[] { new Point(3, 24), new Point(8, 2), new Point(23, 18) });
                        graphics.FillPolygon(dark, new[] { new Point(25, 18), new Point(40, 2), new Point(46, 24) });
                        graphics.FillPolygon(pink, new[] { new Point(8, 18), new Point(10, 7), new Point(18, 17) });
                        graphics.FillPolygon(pink, new[] { new Point(30, 17), new Point(39, 7), new Point(41, 18) });
                        break;
                    case "Bunny Ears":
                        graphics.FillEllipse(white, 7, 0, 13, 26);
                        graphics.FillEllipse(white, 28, 0, 13, 26);
                        graphics.FillEllipse(pink, 11, 4, 5, 18);
                        graphics.FillEllipse(pink, 32, 4, 5, 18);
                        break;
                    case "Devil Horns":
                        graphics.FillPolygon(red, new[] { new Point(2, 25), new Point(6, 3), new Point(21, 17) });
                        graphics.FillPolygon(red, new[] { new Point(27, 17), new Point(42, 3), new Point(46, 25) });
                        graphics.DrawLine(Pens.LightCoral, 6, 8, 8, 20);
                        graphics.DrawLine(Pens.LightCoral, 40, 8, 42, 20);
                        break;
                    case "Halo":
                        graphics.DrawEllipse(Pens.Gold, 10, 3, 28, 10);
                        graphics.DrawArc(Pens.Yellow, 13, 4, 22, 6, 190, 160);
                        break;
                    case "Wizard Hat":
                        graphics.FillPolygon(purple, new[] { new Point(5, 26), new Point(19, 1), new Point(39, 25) });
                        graphics.FillEllipse(purple, 3, 23, 42, 8);
                        graphics.FillRectangle(gold, 14, 20, 22, 4);
                        graphics.FillEllipse(gold, 29, 9, 5, 5);
                        break;
                    case "Cowboy Hat":
                        graphics.FillEllipse(brown, 1, 20, 46, 10);
                        graphics.FillPolygon(brown, new[] { new Point(11, 23), new Point(12, 10), new Point(18, 5), new Point(31, 5), new Point(37, 10), new Point(38, 23) });
                        graphics.DrawArc(outline, 1, 20, 46, 10, 0, 180);
                        graphics.FillRectangle(gold, 12, 19, 24, 4);
                        break;
                    case "Party Hat":
                        graphics.FillPolygon(pink, new[] { new Point(9, 28), new Point(24, 1), new Point(40, 28) });
                        graphics.DrawLine(outline, 9, 28, 40, 28);
                        graphics.FillEllipse(gold, 21, 7, 6, 6);
                        graphics.FillEllipse(cyan, 16, 17, 5, 5);
                        graphics.FillEllipse(gold, 28, 20, 5, 5);
                        break;
                    case "Tiara":
                        graphics.FillPolygon(gold, new[] { new Point(4, 24), new Point(7, 9), new Point(17, 18), new Point(24, 3), new Point(31, 18), new Point(41, 9), new Point(44, 24) });
                        graphics.DrawLine(outline, 4, 25, 44, 25);
                        graphics.FillEllipse(cyan, 21, 12, 6, 6);
                        graphics.FillEllipse(pink, 8, 17, 5, 5);
                        graphics.FillEllipse(pink, 35, 17, 5, 5);
                        break;
                    case "Flower Crown":
                        graphics.DrawArc(stemPen, 4, 5, 40, 29, 190, 160);
                        graphics.FillEllipse(pink, 7, 8, 9, 9);
                        graphics.FillEllipse(gold, 10, 11, 4, 4);
                        graphics.FillEllipse(cyan, 18, 3, 9, 9);
                        graphics.FillEllipse(gold, 21, 6, 4, 4);
                        graphics.FillEllipse(pink, 30, 7, 9, 9);
                        graphics.FillEllipse(gold, 33, 10, 4, 4);
                        break;
                    case "Bow":
                        graphics.FillPolygon(pink, new[] { new Point(23, 15), new Point(4, 4), new Point(7, 25) });
                        graphics.FillPolygon(pink, new[] { new Point(25, 15), new Point(44, 4), new Point(41, 25) });
                        graphics.FillEllipse(red, 19, 11, 10, 10);
                        break;
                    case "Bandana":
                        graphics.FillPolygon(red, new[] { new Point(4, 8), new Point(24, 14), new Point(44, 8), new Point(42, 20), new Point(24, 26), new Point(6, 20) });
                        graphics.DrawLine(Pens.White, 8, 13, 40, 13);
                        graphics.FillEllipse(white, 21, 16, 6, 6);
                        break;
                    case "Headphones":
                        graphics.DrawArc(outline, 7, 0, 34, 35, 180, 180);
                        graphics.DrawArc(Pens.CornflowerBlue, 10, 3, 28, 30, 180, 180);
                        graphics.FillEllipse(blue, 3, 13, 9, 16);
                        graphics.FillEllipse(purple, 36, 13, 9, 16);
                        break;
                    case "Pirate Patch":
                        graphics.DrawLine(outline, 1, 6, 47, 25);
                        graphics.FillEllipse(dark, 5, 5, 19, 17);
                        graphics.DrawArc(Pens.Gold, 5, 5, 19, 17, 180, 180);
                        break;
                    case "Monocle":
                        graphics.DrawEllipse(Pens.Gold, 14, 6, 20, 20);
                        graphics.DrawEllipse(Pens.Gold, 16, 8, 16, 16);
                        graphics.DrawLine(Pens.Gold, 29, 24, 36, 32);
                        break;
                    case "Heart Glasses":
                        graphics.FillPolygon(pink, new[] { new Point(2, 11), new Point(11, 5), new Point(21, 12), new Point(11, 24) });
                        graphics.FillPolygon(pink, new[] { new Point(27, 12), new Point(37, 5), new Point(46, 11), new Point(37, 24) });
                        graphics.DrawLine(outline, 20, 13, 28, 13);
                        graphics.DrawLine(outline, 3, 12, 0, 9);
                        graphics.DrawLine(outline, 45, 12, 48, 9);
                        break;
                    case "Star Glasses":
                        graphics.DrawRectangle(outline, 1, 7, 21, 17);
                        graphics.DrawRectangle(outline, 26, 7, 21, 17);
                        graphics.FillPolygon(gold, new[] { new Point(11, 10), new Point(13, 15), new Point(18, 15), new Point(14, 18), new Point(15, 22), new Point(11, 20), new Point(7, 22), new Point(8, 18), new Point(5, 15), new Point(9, 15) });
                        graphics.FillPolygon(gold, new[] { new Point(36, 10), new Point(38, 15), new Point(43, 15), new Point(39, 18), new Point(40, 22), new Point(36, 20), new Point(32, 22), new Point(33, 18), new Point(29, 15), new Point(34, 15) });
                        graphics.DrawLine(outline, 21, 13, 27, 13);
                        break;
                    case "Wink":
                        graphics.FillEllipse(dark, 8, 13, 7, 8);
                        graphics.DrawArc(outline, 28, 8, 15, 12, 10, 160);
                        graphics.DrawArc(outline, 29, 12, 13, 7, 190, 150);
                        break;
                    case "Sleepy Eyes":
                        graphics.DrawArc(outline, 3, 9, 18, 12, 5, 170);
                        graphics.DrawArc(outline, 27, 9, 18, 12, 5, 170);
                        graphics.DrawLine(Pens.Gray, 5, 22, 18, 22);
                        graphics.DrawLine(Pens.Gray, 30, 22, 43, 22);
                        break;
                    case "Heart Eyes":
                        graphics.FillPolygon(red, new[] { new Point(4, 12), new Point(10, 7), new Point(16, 12), new Point(10, 21) });
                        graphics.FillPolygon(red, new[] { new Point(29, 12), new Point(35, 7), new Point(41, 12), new Point(35, 21) });
                        graphics.DrawArc(outline, 14, 22, 20, 9, 0, 180);
                        break;
                    case "Star Eyes":
                        graphics.FillPolygon(gold, new[] { new Point(11, 7), new Point(14, 14), new Point(21, 14), new Point(16, 18), new Point(18, 25), new Point(11, 21), new Point(5, 25), new Point(7, 18), new Point(2, 14), new Point(9, 14) });
                        graphics.FillPolygon(gold, new[] { new Point(36, 7), new Point(39, 14), new Point(46, 14), new Point(41, 18), new Point(43, 25), new Point(36, 21), new Point(30, 25), new Point(32, 18), new Point(27, 14), new Point(34, 14) });
                        break;
                    case "Tears":
                        graphics.FillEllipse(cyan, 8, 18, 7, 11);
                        graphics.FillEllipse(cyan, 33, 18, 7, 11);
                        graphics.FillPolygon(cyan, new[] { new Point(8, 17), new Point(11, 11), new Point(15, 17) });
                        graphics.FillPolygon(cyan, new[] { new Point(33, 17), new Point(36, 11), new Point(40, 17) });
                        break;
                    case "Freckles":
                        for (int y = 13; y <= 20; y += 5)
                            for (int x = 5 + (y % 2) * 2; x < 19; x += 6)
                            {
                                graphics.FillEllipse(brown, x, y, 2, 2);
                                graphics.FillEllipse(brown, x + 23, y, 2, 2);
                            }
                        break;
                    case "Beauty Mark":
                        graphics.FillEllipse(dark, 35, 20, 4, 4);
                        graphics.FillEllipse(white, 36, 20, 1, 1);
                        break;
                    case "Clown Nose":
                        graphics.FillEllipse(red, 17, 10, 15, 15);
                        graphics.FillEllipse(pink, 20, 12, 5, 4);
                        break;
                    case "Face Paint":
                        graphics.FillPolygon(blue, new[] { new Point(5, 7), new Point(12, 10), new Point(7, 25), new Point(1, 22) });
                        graphics.FillPolygon(red, new[] { new Point(39, 7), new Point(47, 10), new Point(42, 25), new Point(35, 22) });
                        graphics.DrawLine(Pens.White, 4, 12, 9, 14);
                        graphics.DrawLine(Pens.White, 38, 14, 44, 12);
                        break;
                    case "Rainbow Paint":
                        graphics.FillRectangle(red, 4, 6, 40, 4);
                        graphics.FillRectangle(orange, 5, 10, 38, 4);
                        graphics.FillRectangle(gold, 6, 14, 36, 4);
                        graphics.FillRectangle(green, 7, 18, 34, 4);
                        graphics.FillRectangle(blue, 8, 22, 32, 4);
                        graphics.FillRectangle(purple, 9, 26, 30, 4);
                        break;
                    case "Bandage":
                        graphics.FillPolygon(light, new[] { new Point(8, 21), new Point(29, 5), new Point(39, 16), new Point(18, 31) });
                        graphics.DrawLine(Pens.Tan, 13, 21, 18, 26);
                        graphics.DrawLine(Pens.Tan, 28, 10, 33, 15);
                        graphics.FillEllipse(brown, 21, 16, 3, 3);
                        graphics.FillEllipse(brown, 25, 19, 3, 3);
                        break;
                    case "Earrings":
                        graphics.DrawEllipse(Pens.Gold, 3, 14, 10, 15);
                        graphics.DrawEllipse(Pens.Gold, 35, 14, 10, 15);
                        graphics.FillEllipse(pink, 6, 24, 5, 5);
                        graphics.FillEllipse(cyan, 38, 24, 5, 5);
                        break;
                    case "Piercing":
                        graphics.DrawArc(Pens.Gold, 17, 18, 15, 12, 0, 180);
                        graphics.FillEllipse(silver, 21, 23, 4, 4);
                        break;
                    case "Mask":
                        graphics.FillPolygon(purple, new[] { new Point(2, 8), new Point(24, 13), new Point(46, 8), new Point(42, 25), new Point(24, 29), new Point(6, 25) });
                        graphics.FillEllipse(white, 7, 12, 12, 7);
                        graphics.FillEllipse(white, 29, 12, 12, 7);
                        graphics.DrawLine(outline, 4, 11, 0, 7);
                        graphics.DrawLine(outline, 44, 11, 48, 7);
                        break;
                    case "Medical Mask":
                        graphics.FillRectangle(blue, 8, 10, 32, 19);
                        graphics.FillEllipse(blue, 4, 10, 9, 19);
                        graphics.FillEllipse(blue, 35, 10, 9, 19);
                        graphics.DrawArc(Pens.LightBlue, 0, 10, 12, 18, 270, 180);
                        graphics.DrawArc(Pens.LightBlue, 36, 10, 12, 18, 90, 180);
                        graphics.DrawLine(Pens.White, 10, 16, 38, 16);
                        graphics.DrawLine(Pens.White, 10, 21, 38, 21);
                        graphics.DrawLine(Pens.White, 10, 26, 38, 26);
                        break;
                    case "Beak":
                        graphics.FillPolygon(orange, new[] { new Point(13, 8), new Point(35, 8), new Point(24, 27) });
                        graphics.DrawLine(Pens.Gold, 16, 12, 32, 12);
                        break;
                    case "Sparkle Eyes":
                        graphics.FillEllipse(dark, 7, 14, 8, 8);
                        graphics.FillEllipse(dark, 33, 14, 8, 8);
                        graphics.FillPolygon(gold, new[] { new Point(11, 7), new Point(13, 12), new Point(18, 13), new Point(13, 15), new Point(11, 20), new Point(9, 15), new Point(4, 13), new Point(9, 12) });
                        graphics.FillPolygon(gold, new[] { new Point(37, 7), new Point(39, 12), new Point(44, 13), new Point(39, 15), new Point(37, 20), new Point(35, 15), new Point(30, 13), new Point(35, 12) });
                        break;
                    case "Angry Brows":
                        graphics.DrawLine(outline, 3, 8, 21, 15);
                        graphics.DrawLine(outline, 27, 15, 45, 8);
                        graphics.DrawLine(Pens.Brown, 5, 9, 19, 14);
                        graphics.DrawLine(Pens.Brown, 29, 14, 43, 9);
                        break;
                    case "Confetti":
                        graphics.FillEllipse(red, 5, 4, 5, 5);
                        graphics.FillEllipse(gold, 20, 2, 5, 5);
                        graphics.FillEllipse(blue, 38, 5, 5, 5);
                        graphics.FillRectangle(green, 12, 14, 5, 3);
                        graphics.FillRectangle(pink, 29, 16, 5, 3);
                        graphics.DrawLine(Pens.Gold, 5, 26, 10, 20);
                        graphics.DrawLine(Pens.Cyan, 39, 28, 44, 22);
                        break;
                    case "Lightning Bolt":
                        graphics.FillPolygon(gold, new[] { new Point(27, 1), new Point(13, 18), new Point(23, 18), new Point(18, 31), new Point(36, 12), new Point(26, 12) });
                        graphics.DrawPolygon(Pens.Orange, new[] { new Point(27, 1), new Point(13, 18), new Point(23, 18), new Point(18, 31), new Point(36, 12), new Point(26, 12) });
                        break;
                    case "Antenna":
                        graphics.DrawLine(outline, 24, 20, 24, 5);
                        graphics.DrawLine(Pens.Silver, 24, 19, 24, 5);
                        graphics.FillEllipse(pink, 17, 0, 14, 14);
                        graphics.FillEllipse(white, 20, 3, 4, 4);
                        break;
                    case "Dragon Scales":
                        graphics.FillPolygon(green, new[] { new Point(5, 5), new Point(11, 1), new Point(17, 5), new Point(23, 1), new Point(29, 5), new Point(35, 1), new Point(43, 7), new Point(39, 22), new Point(24, 28), new Point(9, 22) });
                        for (int x = 9; x < 40; x += 8)
                        {
                            graphics.DrawArc(Pens.LightGreen, x, 6, 8, 9, 0, 180);
                            graphics.DrawArc(Pens.DarkGreen, x - 3, 14, 8, 9, 0, 180);
                        }
                        break;
                    case "Butterfly":
                        graphics.FillEllipse(pink, 5, 5, 18, 15);
                        graphics.FillEllipse(purple, 25, 5, 18, 15);
                        graphics.FillEllipse(cyan, 7, 17, 16, 12);
                        graphics.FillEllipse(gold, 25, 17, 16, 12);
                        graphics.FillRectangle(dark, 22, 10, 5, 15);
                        graphics.DrawArc(outline, 14, 1, 11, 12, 200, 120);
                        graphics.DrawArc(outline, 24, 1, 11, 12, 220, 120);
                        break;
                    case "Rosy Hearts":
                        graphics.FillEllipse(pink, 2, 15, 15, 9);
                        graphics.FillEllipse(pink, 31, 15, 15, 9);
                        graphics.FillPolygon(red, new[] { new Point(7, 13), new Point(10, 10), new Point(13, 13), new Point(10, 18) });
                        graphics.FillPolygon(red, new[] { new Point(36, 13), new Point(39, 10), new Point(42, 13), new Point(39, 18) });
                        break;
                }
            }
        }

        private void ScreenToImage(int sx, int sy, out int imgX, out int imgY)
        {
            if (currentBitmap == null) { imgX = imgY = -1; return; }
            float scale, hOffset, vOffset;
            GetCanvasTransform(out scale, out hOffset, out vOffset);
            imgX = (int)((sx - hOffset) / scale);
            imgY = (int)((sy - vOffset) / scale);
        }

        private void GetCanvasTransform(out float scale, out float hOffset, out float vOffset)
        {
            if (currentBitmap == null || canvas.Width <= 0 || canvas.Height <= 0)
            {
                scale = 1;
                hOffset = vOffset = 0;
                return;
            }
            float fitScale = Math.Min((float)canvas.Width / currentBitmap.Width,
                (float)canvas.Height / currentBitmap.Height);
            scale = fitScale * zoomLevel;
            hOffset = (canvas.Width - currentBitmap.Width * scale) / 2f + panOffsetX;
            vOffset = (canvas.Height - currentBitmap.Height * scale) / 2f + panOffsetY;
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

            float actualScale, hOffset, vOffset;
            GetCanvasTransform(out actualScale, out hOffset, out vOffset);

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
            if (rectangularSelection.HasValue)
            {
                Rectangle selected = rectangularSelection.Value;
                using (SolidBrush selBrush = new SolidBrush(Color.FromArgb(100, Color.PaleGoldenrod)))
                    e.Graphics.FillRectangle(selBrush,
                        hOffset + selected.X * actualScale,
                        vOffset + selected.Y * actualScale,
                        selected.Width * actualScale,
                        selected.Height * actualScale);
            }
            else if (selection.Count > 0) {
                using (SolidBrush selBrush = new SolidBrush(Color.FromArgb(100, Color.PaleGoldenrod))) {
                    foreach (var p in selection)
                        e.Graphics.FillRectangle(selBrush, hOffset + p.X * actualScale, vOffset + p.Y * actualScale, actualScale, actualScale);
                }
            }

            if (isSelecting && currentTool == "RectangleSelect")
            {
                float left = hOffset + Math.Min(selectionStart.X, selectionCurrent.X) * actualScale;
                float top = vOffset + Math.Min(selectionStart.Y, selectionCurrent.Y) * actualScale;
                float width = (Math.Abs(selectionCurrent.X - selectionStart.X) + 1) * actualScale;
                float height = (Math.Abs(selectionCurrent.Y - selectionStart.Y) + 1) * actualScale;
                using (SolidBrush preview = new SolidBrush(Color.FromArgb(55, Color.PaleGoldenrod)))
                using (Pen outline = new Pen(Color.PaleGoldenrod))
                {
                    e.Graphics.FillRectangle(preview, left, top, width, height);
                    e.Graphics.DrawRectangle(outline, left, top, width, height);
                }
            }
            else if (isSelecting && currentTool == "LassoSelect" && lassoPath.Count > 1)
            {
                PointF[] screenPoints = lassoPath
                    .Select(point => new PointF(hOffset + point.X * actualScale, vOffset + point.Y * actualScale))
                    .ToArray();
                using (Pen outline = new Pen(Color.PaleGoldenrod, 1.5f))
                    e.Graphics.DrawLines(outline, screenPoints);
            }

            if (currentTool == "Prop" && selectedPropImage != null && hasPropPosition)
            {
                float width = Math.Max(8f, Math.Min(96f, currentBitmap.Width * 0.55f)) *
                    (float)numPropScale.Value * actualScale;
                float height = width * 2f / 3f;
                System.Drawing.Drawing2D.GraphicsState state = e.Graphics.Save();
                e.Graphics.TranslateTransform(propScreenPosition.X, propScreenPosition.Y);
                e.Graphics.RotateTransform((float)numPropRotation.Value);
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    ColorMatrix opacity = new ColorMatrix();
                    opacity.Matrix33 = 0.72f;
                    attributes.SetColorMatrix(opacity);
                    e.Graphics.DrawImage(selectedPropImage,
                        new Rectangle(
                            (int)Math.Round(-width / 2f),
                            (int)Math.Round(-height / 2f),
                            Math.Max(1, (int)Math.Ceiling(width)),
                            Math.Max(1, (int)Math.Ceiling(height))),
                        0, 0, selectedPropImage.Width, selectedPropImage.Height,
                        GraphicsUnit.Pixel, attributes);
                }
                e.Graphics.Restore(state);
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
            rectangularSelection = null;
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
            rectangularSelection = null;
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
            Bitmap originalPreview = (Bitmap)currentBitmap.Clone();
            bool applied = false;
            Color sampleColor = GetHueSatPreviewSample();

            using (Form f = new Form {
                Text = "Hue / Saturation",
                ClientSize = new Size(360, 300),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = QFG5Theme.ColorSurface,
                ForeColor = QFG5Theme.ColorTextPrimary
            }) {
                TableLayoutPanel layout = new TableLayoutPanel {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 8,
                    Padding = new Padding(10),
                    BackColor = QFG5Theme.ColorSurface
                };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

                TrackBar trkHue = new TrackBar {
                    Minimum = -180, Maximum = 180, Value = 0, Dock = DockStyle.Fill,
                    TickFrequency = 60, Height = 42, Margin = new Padding(0)
                };
                TrackBar trkSat = new TrackBar {
                    Minimum = -100, Maximum = 100, Value = 0, Dock = DockStyle.Fill,
                    TickFrequency = 25, Height = 42, Margin = new Padding(0)
                };
                Action updateCanvasPreview = () => {
                    if (currentBitmap != null)
                        currentBitmap.Dispose();
                    currentBitmap = (Bitmap)originalPreview.Clone();
                    if (trkHue.Value != 0 || trkSat.Value != 0)
                        AdjustBitmapHSL(currentBitmap, trkHue.Value, trkSat.Value / 100f);
                    canvas.Invalidate();
                };
                Label lblHue = new Label {
                    Text = "Hue shift", Dock = DockStyle.Fill,
                    ForeColor = QFG5Theme.ColorTextGold, TextAlign = ContentAlignment.MiddleLeft
                };
                Label lblSat = new Label {
                    Text = "Saturation", Dock = DockStyle.Fill,
                    ForeColor = QFG5Theme.ColorTextGold, TextAlign = ContentAlignment.MiddleLeft
                };
                Panel hueBar = CreateHueSatColorBar(
                    trkHue, value => AdjustColorHSL(sampleColor, value, trkSat.Value / 100f));
                Panel saturationBar = CreateHueSatColorBar(
                    trkSat, value => AdjustColorHSL(sampleColor, trkHue.Value, value / 100f));

                Panel previewPanel = new Panel {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0, 4, 0, 2)
                };
                previewPanel.Paint += (s, e) => {
                    const int swatchSize = 24;
                    int top = (previewPanel.Height - swatchSize) / 2;
                    using (SolidBrush originalBrush = new SolidBrush(sampleColor))
                    using (SolidBrush resultBrush = new SolidBrush(
                        AdjustColorHSL(sampleColor, trkHue.Value, trkSat.Value / 100f)))
                    using (Pen border = new Pen(QFG5Theme.ColorTextPrimary))
                    using (SolidBrush textBrush = new SolidBrush(QFG5Theme.ColorTextPrimary)) {
                        e.Graphics.FillRectangle(originalBrush, 2, top, swatchSize, swatchSize);
                        e.Graphics.DrawRectangle(border, 2, top, swatchSize, swatchSize);
                        e.Graphics.DrawString("Original", QFG5Theme.CaptionFont, textBrush, 32, top + 5);
                        int resultX = 116;
                        e.Graphics.FillRectangle(resultBrush, resultX, top, swatchSize, swatchSize);
                        e.Graphics.DrawRectangle(border, resultX, top, swatchSize, swatchSize);
                        e.Graphics.DrawString("Preview", QFG5Theme.CaptionFont, textBrush, resultX + 30, top + 5);
                    }
                };
                trkHue.ValueChanged += (s, e) => {
                    hueBar.Invalidate();
                    previewPanel.Invalidate();
                    updateCanvasPreview();
                };
                trkSat.ValueChanged += (s, e) => {
                    hueBar.Invalidate();
                    saturationBar.Invalidate();
                    previewPanel.Invalidate();
                    updateCanvasPreview();
                };

                Button btnApply = new Button {
                    Text = "Apply", Dock = DockStyle.Fill,
                    BackColor = QFG5Theme.ColorSurfaceRecessed,
                    ForeColor = QFG5Theme.ColorTextPrimary,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(0, 4, 0, 0)
                };
                btnApply.Click += (s, e) => {
                    currentBitmap.Dispose();
                    currentBitmap = (Bitmap)originalPreview.Clone();
                    if (trkHue.Value != 0 || trkSat.Value != 0)
                        ApplyHSL(trkHue.Value, trkSat.Value / 100f);
                    applied = true;
                    f.Close();
                };

                layout.Controls.Add(lblHue, 0, 0);
                layout.Controls.Add(trkHue, 0, 1);
                layout.Controls.Add(hueBar, 0, 2);
                layout.Controls.Add(lblSat, 0, 3);
                layout.Controls.Add(trkSat, 0, 4);
                layout.Controls.Add(saturationBar, 0, 5);
                layout.Controls.Add(previewPanel, 0, 6);
                layout.Controls.Add(btnApply, 0, 7);
                f.Controls.Add(layout);
                f.FormClosed += (s, e) => {
                    if (!applied) {
                        if (currentBitmap != null)
                            currentBitmap.Dispose();
                        currentBitmap = (Bitmap)originalPreview.Clone();
                        canvas.Invalidate();
                    }
                };
                try {
                    f.ShowDialog();
                } finally {
                    originalPreview.Dispose();
                }
            }
        }

        private Panel CreateHueSatColorBar(TrackBar trackBar, Func<int, Color> getColor)
        {
            Panel bar = new Panel {
                Dock = DockStyle.Fill,
                Height = 20,
                Margin = new Padding(0, 0, 0, 2),
                BackColor = QFG5Theme.ColorSurfaceRecessed
            };
            bar.Paint += (s, e) => {
                int width = bar.ClientSize.Width;
                int height = bar.ClientSize.Height;
                if (width <= 0 || height <= 0) return;

                for (int x = 0; x < width; x++) {
                    int value = trackBar.Minimum
                        + (int)Math.Round(x * (trackBar.Maximum - trackBar.Minimum)
                            / (double)Math.Max(1, width - 1));
                    using (SolidBrush brush = new SolidBrush(getColor(value)))
                        e.Graphics.FillRectangle(brush, x, 0, 1, height);
                }

                int markerX = (int)Math.Round(
                    (trackBar.Value - trackBar.Minimum) * (width - 1)
                    / (double)Math.Max(1, trackBar.Maximum - trackBar.Minimum));
                using (Pen border = new Pen(QFG5Theme.ColorTextPrimary))
                using (Pen marker = new Pen(Color.White, 2)) {
                    e.Graphics.DrawRectangle(border, 0, 0, width - 1, height - 1);
                    e.Graphics.DrawLine(marker, markerX, 0, markerX, height - 1);
                }
            };
            return bar;
        }

        private Color GetHueSatPreviewSample()
        {
            if (rectangularSelection.HasValue) {
                Rectangle bounds = Rectangle.Intersect(
                    rectangularSelection.Value,
                    new Rectangle(0, 0, currentBitmap.Width, currentBitmap.Height));
                for (int y = bounds.Top; y < bounds.Bottom; y++)
                    for (int x = bounds.Left; x < bounds.Right; x++) {
                        Color color = currentBitmap.GetPixel(x, y);
                        if (color.A > 0) return color;
                    }
            } else if (selection.Count > 0) {
                foreach (Point point in selection)
                    if (point.X >= 0 && point.Y >= 0
                        && point.X < currentBitmap.Width && point.Y < currentBitmap.Height) {
                        Color color = currentBitmap.GetPixel(point.X, point.Y);
                        if (color.A > 0) return color;
                    }
            } else {
                for (int y = 0; y < currentBitmap.Height; y++)
                    for (int x = 0; x < currentBitmap.Width; x++) {
                        Color color = currentBitmap.GetPixel(x, y);
                        if (color.A > 0) return color;
                    }
            }
            return Color.White;
        }

        private void ApplyHSL(float hueShift, float satShift)
        {
            if (currentBitmap == null || (hueShift == 0 && satShift == 0)) return;
            CaptureUndoStep();
            AdjustBitmapHSL(currentBitmap, hueShift, satShift);
            canvas.Invalidate();
        }

        private void AdjustBitmapHSL(Bitmap bitmap, float hueShift, float satShift)
        {
            if (rectangularSelection.HasValue)
            {
                Rectangle selected = rectangularSelection.Value;
                for (int y = selected.Top; y < selected.Bottom; y++)
                    for (int x = selected.Left; x < selected.Right; x++)
                        AdjustPixelHSL(bitmap, x, y, hueShift, satShift);
            }
            else if (selection.Count > 0)
                foreach (var p in selection) AdjustPixelHSL(bitmap, p.X, p.Y, hueShift, satShift);
            else
                for (int x = 0; x < bitmap.Width; x++)
                    for (int y = 0; y < bitmap.Height; y++)
                        AdjustPixelHSL(bitmap, x, y, hueShift, satShift);
        }

        private void AdjustPixelHSL(Bitmap bitmap, int x, int y, float hShift, float sAdjust)
        {
            Color c = bitmap.GetPixel(x, y);
            if (c.A == 0) return;
            bitmap.SetPixel(x, y, AdjustColorHSL(c, hShift, sAdjust));
        }

        private Color AdjustColorHSL(Color color, float hueShift, float saturationAdjust)
        {
            float h, s, l;
            ColorToHSL(color, out h, out s, out l);
            h = (h + hueShift) % 360; if (h < 0) h += 360;
            s = Math.Min(1, Math.Max(0, s + saturationAdjust));
            return HSLToColor(h, s, l, color.A);
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

        private static Color HSLToColor(float h, float s, float l, int alpha)
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

        private static float HueToRGB(float p, float q, float t)
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
                    CaptureUndoStep();
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
                    Bitmap loaded = new Bitmap(ofd.FileName);
                    try {
                        SetCurrentTexture(loaded, string.Empty, $"Opened: {Path.GetFileName(ofd.FileName)}");
                    } finally {
                        loaded.Dispose();
                    }
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
            if (currentBitmap == null || string.IsNullOrEmpty(loadedMdlPath) || !File.Exists(loadedMdlPath)) {
                MessageBox.Show("Please open a texture from an MDL file first to inject it.", "Texture Editor");
                return;
            }
            string workingDirectory = Path.Combine(Path.GetDirectoryName(loadedMdlPath),
                ".qfg5-texture-" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(workingDirectory);
                byte[] mdlPalette = ExtractMdlPalette(loadedMdlPath);
                string textureBase = Path.Combine(workingDirectory, "texture");
                qfg5modelBitmap.exportBMPfile(loadedMdlPath, textureBase);
                List<string> bitmapPaths = GetExportedBitmapPaths(textureBase);
                string firstBitmap = bitmapPaths[0];
                List<Color> displayPalette = ExtractBitmapPalette(firstBitmap);
                byte[] pixelData = ConvertTo8BitWithPalette(currentBitmap, displayPalette);
                WriteGameBmp(firstBitmap, mdlPalette, pixelData, currentBitmap.Width, currentBitmap.Height);
                for (int index = 1; index < bitmapPaths.Count; index++)
                    WriteGameBmpPalette(bitmapPaths[index], mdlPalette);

                string tempMdl = Path.Combine(workingDirectory, "injected" + Constants.MDL_EXTENSION);
                File.Copy(loadedMdlPath, tempMdl);
                ModelConverter.importOrExportBitmapOrMesh(textureBase + Constants.BMP_EXTENSION, tempMdl, false, true, false, false);

                string validationBase = Path.Combine(workingDirectory, "validated");
                qfg5modelBitmap.exportBMPfile(tempMdl, validationBase);
                if (!File.Exists(validationBase + Constants.BMP_EXTENSION) &&
                    !File.Exists(validationBase + "-0" + Constants.BMP_EXTENSION))
                    throw new InvalidDataException("The injected model did not contain a readable texture.");

                string backupPath = loadedMdlPath + OriginalMdlBackupSuffix;
                if (!File.Exists(backupPath))
                    File.Copy(loadedMdlPath, backupPath);
                File.Replace(tempMdl, loadedMdlPath, null);
                SetStatus("Injected successfully.");
                MessageBox.Show("Texture injected successfully. A backup is available beside the MDL file as " +
                    Path.GetFileName(backupPath) + ".",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception ex) {
                SetStatus("Injection failed.");
                MessageBox.Show("Error injecting texture: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } finally {
                if (Directory.Exists(workingDirectory))
                {
                    try {
                        Directory.Delete(workingDirectory, true);
                    } catch (Exception cleanupError) {
                        MessageBox.Show("The temporary texture workspace could not be removed: " + cleanupError.Message,
                            "Texture Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        private void BtnInjectGra_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(loadedPanoNodPath))
            {
                InjectPano();
                return;
            }

            if (currentBitmap == null || string.IsNullOrEmpty(loadedGraPath) ||
                !File.Exists(loadedGraPath) || loadedGraCollectionIndex < 0 || loadedGraFrameIndex < 0)
            {
                MessageBox.Show("Open a GRA frame from the GRA tab before injecting.",
                    "Texture Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string temporaryPath = loadedGraPath + ".qfg5extractor-" + Guid.NewGuid().ToString("N") + ".tmp";
            string backupPath = loadedGraPath + OriginalGraBackupSuffix;
            try
            {
                GraConverter converter = new GraConverter();
                int replacedFrameCount;
                if (loadedGraFrames.Count > 0)
                {
                    StoreCurrentGraFrame();
                    List<bool> changedFrames = new List<bool>(loadedGraFrames.Count);
                    replacedFrameCount = 0;
                    for (int index = 0; index < loadedGraFrames.Count; index++)
                    {
                        bool changed = dirtyGraFrames[index] &&
                            !BitmapsEqual(originalGraFrames[index], loadedGraFrames[index]);
                        changedFrames.Add(changed);
                        if (changed) replacedFrameCount++;
                    }
                    if (replacedFrameCount == 0)
                    {
                        SetStatus("No GRA frame changes to inject.");
                        return;
                    }
                    converter.ReplaceFrames(loadedGraPath, temporaryPath, loadedGraCollectionIndex,
                        loadedGraFrames, changedFrames);
                }
                else
                {
                    converter.ReplaceFrame(loadedGraPath, temporaryPath, loadedGraCollectionIndex,
                        loadedGraFrameIndex, currentBitmap);
                    replacedFrameCount = 1;
                }

                if (!File.Exists(backupPath))
                    File.Copy(loadedGraPath, backupPath);
                File.Replace(temporaryPath, loadedGraPath, null);
                if (loadedGraFrames.Count > 0)
                {
                    for (int index = 0; index < loadedGraFrames.Count; index++)
                    {
                        originalGraFrames[index].Dispose();
                        originalGraFrames[index] = (Bitmap)loadedGraFrames[index].Clone();
                        dirtyGraFrames[index] = false;
                    }
                }

                SetStatus("Injected " + replacedFrameCount + " edited GRA frame(s).");
                SharedState.NotifyGraFileChanged(loadedGraPath);
                MessageBox.Show(replacedFrameCount + " edited frame(s) were written back to " +
                    Path.GetFileName(loadedGraPath) + ". An original backup is available as " +
                    Path.GetFileName(backupPath) + ".",
                    "GRA Injection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus("GRA injection failed.");
                MessageBox.Show("Could not inject the edited frame into the GRA file: " + ex.Message,
                    "GRA Injection", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("TextureEditorTab", "GRA injection failed: " + ex.Message);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception ex) {
                        Logger.LogError("TextureEditorTab", "Could not remove temporary GRA file: " + ex.Message);
                    }
                }
            }
        }

        private void InjectPano()
        {
            if (currentBitmap == null || !File.Exists(loadedPanoNodPath) ||
                !File.Exists(loadedPanoImgPath))
            {
                MessageBox.Show("The source panorama files are no longer available. Reopen the image from Pano Editor.",
                    "Panorama Injection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string baseName = Path.GetFileNameWithoutExtension(loadedPanoBitmapPath);
            string nodOutputFolder = SharedState.GetModsPath("cdn", "NOD");
            string imgOutputFolder = SharedState.GetModsPath("cdn", "IMG");
            string outNod = Path.Combine(nodOutputFolder, baseName + ".nod");
            string outImg = Path.Combine(imgOutputFolder, baseName + ".img");
            try
            {
                Directory.CreateDirectory(nodOutputFolder);
                Directory.CreateDirectory(imgOutputFolder);
                PanoConverter.importToFiles(currentBitmap, loadedPanoNodPath, loadedPanoImgPath,
                    outNod, outImg);
                string previewWarning = null;
                if (!string.IsNullOrEmpty(loadedPanoBitmapPath))
                {
                    try
                    {
                        currentBitmap.Save(loadedPanoBitmapPath, ImageFormat.Bmp);
                    }
                    catch (Exception ex)
                    {
                        previewWarning = " The panorama files were injected, but the cached BMP could not be refreshed: "
                            + ex.Message;
                        Logger.LogError("TextureEditorTab", "Could not refresh edited panorama BMP: " + ex.Message);
                    }
                }
                SharedState.NotifyPanoFileChanged(baseName);
                SetStatus(previewWarning == null
                    ? "Injected panorama: " + baseName
                    : "Injected panorama; BMP preview refresh failed.");
                MessageBox.Show("Edited panorama files were written to the Mods folders:\n" +
                    "cdn/NOD/" + baseName + ".nod\ncdn/IMG/" + baseName + ".img\n\n" +
                    "Repack CDN.SPK in the SPK tab to use these files in-game." + previewWarning,
                    "Panorama Injection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Panorama injection failed.");
                MessageBox.Show("Could not inject the edited panorama: " + ex.Message,
                    "Panorama Injection", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("TextureEditorTab", "Panorama injection failed: " + ex.Message);
            }
        }

        private static bool BitmapsEqual(Bitmap left, Bitmap right)
        {
            if (left == null || right == null || left.Size != right.Size)
                return false;
            for (int y = 0; y < left.Height; y++)
                for (int x = 0; x < left.Width; x++)
                    if (left.GetPixel(x, y).ToArgb() != right.GetPixel(x, y).ToArgb())
                        return false;
            return true;
        }

        private void BtnOpenMdl_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "MDL Files|*.mdl" }) {
                if (ofd.ShowDialog() == DialogResult.OK) {
                    try {
                        if (!LoadMdlTexture(ofd.FileName)) {
                            MessageBox.Show("No readable embedded textures found in this MDL.",
                                "Texture Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        private static List<string> GetExportedBitmapPaths(string textureBase)
        {
            List<string> paths = new List<string>();
            string firstMip = textureBase + "-0" + Constants.BMP_EXTENSION;
            if (File.Exists(firstMip))
            {
                for (int index = 0; index <= 31; index++)
                {
                    string mipPath = textureBase + "-" + index + Constants.BMP_EXTENSION;
                    if (!File.Exists(mipPath))
                        break;
                    paths.Add(mipPath);
                }
            }
            else
            {
                string bitmapPath = textureBase + Constants.BMP_EXTENSION;
                if (File.Exists(bitmapPath))
                    paths.Add(bitmapPath);
            }
            if (paths.Count == 0)
                throw new InvalidDataException("The MDL did not export a readable texture bitmap.");
            return paths;
        }

        private static List<Color> ExtractBitmapPalette(string bitmapPath)
        {
            using (Bitmap bitmap = new Bitmap(bitmapPath))
            {
                Color[] entries = bitmap.Palette.Entries;
                if (entries.Length != 256)
                    throw new InvalidDataException("The exported MDL texture does not have a 256-color palette.");
                return new List<Color>(entries);
            }
        }

        private byte[] ConvertTo8BitWithPalette(Bitmap source, IList<Color> paletteEntries)
        {
            if (paletteEntries == null || paletteEntries.Count != 256)
                throw new InvalidDataException("A complete 256-color MDL palette is required for texture injection.");
            int w = source.Width, h = source.Height;
            byte[] pixelData = new byte[w * h];
            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    Color c = source.GetPixel(x, y);
                    if (c.A < 128) { pixelData[(h - 1 - y) * w + x] = 0; continue; }
                    int bestIndex = 0, minDiff = int.MaxValue;
                    for (int i = 0; i < paletteEntries.Count; i++) {
                        Color p = paletteEntries[i];
                        int dr = c.R - p.R, dg = c.G - p.G, db = c.B - p.B;
                        int diff = dr*dr + dg*dg + db*db;
                        if (diff < minDiff) { minDiff = diff; bestIndex = i; }
                    }
                    pixelData[(h - 1 - y) * w + x] = (byte)bestIndex;
                }
            }
            return pixelData;
        }

        private static void WriteGameBmpPalette(string path, byte[] rawPalette)
        {
            byte[] bmpPalette = CreateBmpPalette(rawPalette);
            using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None)))
            {
                if (writer.BaseStream.Length < 54 + 1024)
                    throw new InvalidDataException("The exported mipmap BMP has no complete palette.");
                writer.BaseStream.Seek(54, SeekOrigin.Begin);
                writer.Write(bmpPalette);
            }
        }

        private void WriteGameBmp(string path, byte[] rawPalette, byte[] pixelData, int w, int h)
        {
            int rowStride = ((w + 3) / 4) * 4;
            int imageSize = rowStride * h;
            byte[] bmpPalette = CreateBmpPalette(rawPalette);
            using (BinaryWriter bw = new BinaryWriter(File.Open(path, FileMode.Create))) {
                bw.Write(new byte[] { 0x42, 0x4d });
                bw.Write(54 + 1024 + imageSize);
                bw.Write((ushort)0); bw.Write((ushort)0);
                bw.Write(54 + 1024);
                bw.Write(40); bw.Write(w); bw.Write(h);
                bw.Write((ushort)1); bw.Write((ushort)8);
                bw.Write(0); bw.Write(imageSize); bw.Write(2834); bw.Write(2834);
                bw.Write(256); bw.Write(0);
                bw.Write(bmpPalette);
                for (int i = 0; i < 1024 - bmpPalette.Length; i++) bw.Write((byte)0);
                for (int row = 0; row < h; row++)
                {
                    bw.Write(pixelData, row * w, w);
                    for (int padding = w; padding < rowStride; padding++) bw.Write((byte)0);
                }
            }
        }

        private static byte[] CreateBmpPalette(byte[] rawPalette)
        {
            if (rawPalette == null || rawPalette.Length != Constants.BITMAP_PALETTE_LENGTH)
                throw new InvalidDataException("The MDL palette is incomplete.");
            byte[] bmpPalette = new byte[1024];
            for (int index = 0; index + 2 < rawPalette.Length; index += 4)
            {
                bmpPalette[index] = rawPalette[index + 2];
                bmpPalette[index + 1] = rawPalette[index + 1];
                bmpPalette[index + 2] = rawPalette[index];
                if (index + 3 < rawPalette.Length)
                    bmpPalette[index + 3] = rawPalette[index + 3];
            }
            return bmpPalette;
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Application.RemoveMessageFilter(this);
                if (toolTip != null) toolTip.Dispose();
                if (currentBitmap != null) currentBitmap.Dispose();
                if (originalBitmap != null) originalBitmap.Dispose();
                if (selectedPropImage != null) selectedPropImage.Dispose();
                ClearUndoHistory();
                ClearLoadedGraFrames();
                if (propThumbnailList != null)
                {
                    foreach (Control control in propThumbnailList.Controls)
                    {
                        Button button = control as Button;
                        if (button != null && button.Image != null)
                        {
                            button.Image.Dispose();
                            button.Image = null;
                        }
                        if (button != null)
                        {
                            Bitmap customImage = button.Tag as Bitmap;
                            if (customImage != null)
                            {
                                customImage.Dispose();
                                button.Tag = null;
                            }
                        }
                    }
                }
            }
            base.Dispose(disposing);
        }
    }
}
