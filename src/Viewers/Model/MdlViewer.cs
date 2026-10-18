using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using QFG5Extractor.Resources.Model;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using QFG5Extractor.Viewers.Model;


namespace QFG5Extractor.Viewers.Model
{
    public class MdlViewer : UserControl
    {
        private TextBox txtMDLFolder;
        private QFG5Button btnBrowse;
        private ListBox lstMDLFiles;
        private Label lblNameTitle;
        private Label lblModelName;
        private QFG5Button btnExport;
        private QFG5Button btnLog;
        private QFG5Button btnToggleMode;
        private TableLayoutPanel mainLayout;
        private TextBox txtANMFolder;
        private QFG5Button btnBrowseANM;
        private QFG5ComboBox cmbAnimations;
        private QFG5ComboBox cmbTextures;
        private QFG5Button btnRotate90;
        private QFG5Button btnRotateAxis;
        private QFG5Button btnResetRotation;
        private QFG5Button btnBuildIndex;
        private List<string> availableAnimations = new List<string>();
        private List<string> availableTextures = new List<string>();
        private CheckBox chkAutoMultiTex;

        private OldMdlViewerLogic viewerLogic;
        private List<MeshData> currentMeshes;
        private enum RenderMode { Texture2D, Wireframe3D, Textured3D }
        private RenderMode currentMode = RenderMode.Wireframe3D; // Quicker load default
        private float rotX = 0, rotY = 0;
        private float panX = 0, panY = 0;
        private int rotOffset90X = 270, rotOffset90Y = 0; // 0, 90, 180, 270 degrees
        private Point lastMousePos;
        private Timer spinTimer;
        private bool isSpinning = false; // Changed from true
        private bool isExploded = false;


        // OpenTK variables
        private GLControl glControl; // Moved from above
        private Panel viewportEmptyState;
        private List<int> currentTextureIds = new List<int>();
        private int fallbackTextureId = -1;
        private float meshScale = 1.0f; 
        private QFG5Extractor.Resources.Model.Vector3 meshCenter = new QFG5Extractor.Resources.Model.Vector3(0, 0, 0); // Moved from above
        private bool showAxis = true;
        private float zoomLevel = 1.0f; // Replaces trkZoom.Value
        private int uvRotation = 0; // 0=0deg, 1=90deg, 2=180deg, 3=270deg

        // Skeleton assembly variables
        private List<Bitmap> currentBitmaps = new List<Bitmap>();
        private Matrix4[] currentSkeleton = null;
        private Matrix4[][] loadedAnimationFrames = null; // [frame][bone]
        private int currentAnimFrame = 0;
        private bool isAnimPlaying = false;
        private bool animHalfSpeed = false;
        private int animTickAccumulator = 0;
        private Label lblAnimFrame;
        private QFG5Button btnPlayAnim;
        private QFG5Button btnAnimHalfSpeed;
        private ModelDatabase _db;
        private string _dbPath;
        private string loadedAnmName = "<None>";
        public MdlViewer()
        {
            var swViewerTotal = System.Diagnostics.Stopwatch.StartNew();
            this.BackColor = Color.Transparent;
            viewerLogic = new OldMdlViewerLogic();
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "model_db.json");
            if (!File.Exists(_dbPath)) {
                string devPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "model_db.json");
                if (File.Exists(devPath)) _dbPath = Path.GetFullPath(devPath);
            }
            var swDb = System.Diagnostics.Stopwatch.StartNew();
            _db = new ModelDatabase(_dbPath);
            swDb.Stop();
            Logger.LogPerf($"MdlViewer model_db.json: {swDb.ElapsedMilliseconds} ms");

            var swInitComp = System.Diagnostics.Stopwatch.StartNew();
            InitializeComponent();
            swInitComp.Stop();
            Logger.LogPerf($"MdlViewer InitializeComponent (with GLControl creation): {swInitComp.ElapsedMilliseconds} ms");
            
            SharedState.OnBaseDataPathChanged += (path) => {
                string mdlDir = Path.Combine(path, "hdnw" + Path.DirectorySeparatorChar + "mdl");
                string anmDir = Path.Combine(path, "hdnw" + Path.DirectorySeparatorChar + "anm");
                
                if (Directory.Exists(mdlDir)) {
                    txtMDLFolder.Text = mdlDir;
                    LoadFilesInFolder(mdlDir);
                }
                if (Directory.Exists(anmDir)) {
                    txtANMFolder.Text = anmDir;
                }
            };

            spinTimer = new Timer { Interval = 33 }; // ~30 FPS for spin + ANM playback
            spinTimer.Tick += (s, e) => {
                bool needsRedraw = false;

                if (isAnimPlaying && loadedAnimationFrames != null && loadedAnimationFrames.Length > 0)
                {
                    animTickAccumulator++;
                    int ticksPerFrame = animHalfSpeed ? 2 : 1;
                    if (animTickAccumulator >= ticksPerFrame)
                    {
                        animTickAccumulator = 0;
                        currentAnimFrame = (currentAnimFrame + 1) % loadedAnimationFrames.Length;
                        ApplyCurrentAnimFrame(recenter: true);
                        needsRedraw = true;
                    }
                }

                if (currentMode != RenderMode.Texture2D && isSpinning && Control.MouseButtons == MouseButtons.None)
                {
                    rotY += 0.02f;
                    needsRedraw = true;
                }

                if (needsRedraw && glControl != null) glControl.Invalidate();
            };
            this.VisibleChanged += (s, e) => UpdateSpinTimer();
            this.ParentChanged += (s, e) => UpdateSpinTimer();
            UpdateSpinTimer();

            this.DoubleBuffered = true;
            swViewerTotal.Stop();
            Logger.LogPerf($"MdlViewer constructor total: {swViewerTotal.ElapsedMilliseconds} ms");
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.Transparent;

            mainLayout = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(8)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // --- Header: Path Management ---
            TableLayoutPanel headerTable = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8, 4, 8, 4),
                BackColor = Color.Transparent
            };
            headerTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            headerTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            // Inline path rows (TextBox themed, matching viewer style)
            TableLayoutPanel mdlRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            mdlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            mdlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mdlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            txtMDLFolder = CreateThemedTextBox();
            txtMDLFolder.KeyDown += TxtMDLFolder_KeyDown;
            btnBrowse = new QFG5Button { Text = "Browse", Dock = DockStyle.Fill, Height = 30 };
            btnBrowse.Click += BtnBrowse_Click;
            mdlRow.Controls.Add(new Label { Text = "MDL Folder", ForeColor = QFG5Theme.ColorTextPrimary, Font = QFG5Theme.BodyBoldFont, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
            mdlRow.Controls.Add(txtMDLFolder, 1, 0);
            mdlRow.Controls.Add(btnBrowse, 2, 0);

            TableLayoutPanel anmRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            anmRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            anmRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            anmRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            txtANMFolder = CreateThemedTextBox();
            btnBrowseANM = new QFG5Button { Text = "Browse", Dock = DockStyle.Fill, Height = 30 };
            btnBrowseANM.Click += BtnBrowseANM_Click;
            anmRow.Controls.Add(new Label { Text = "ANM Folder", ForeColor = QFG5Theme.ColorTextPrimary, Font = QFG5Theme.BodyBoldFont, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
            anmRow.Controls.Add(txtANMFolder, 1, 0);
            anmRow.Controls.Add(btnBrowseANM, 2, 0);

            headerTable.Controls.Add(mdlRow, 0, 0);
            headerTable.Controls.Add(anmRow, 0, 1);
            QFG5CollapsibleSection folderSection =
                new QFG5CollapsibleSection("MODEL & ANIMATION FOLDERS", headerTable, 118);
            mainLayout.Controls.Add(folderSection, 0, 0);
            mainLayout.SetColumnSpan(folderSection, 2);

            // --- Left Panel: Sidebar ---
            QFG5PreviewFrame sidebar = new QFG5PreviewFrame { Dock = DockStyle.Fill, Padding = new Padding(5) };
            TableLayoutPanel sidebarLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            sidebarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));

            Panel listPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorSurface,
                Padding = new Padding(6),
                Margin = new Padding(2)
            };
            QFG5Theme.ApplyHeaderStroke(listPanel);
            Label listLabel = new Label
            {
                Text = "MODEL FILES",
                Dock = DockStyle.Top,
                Height = 25,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.SectionHeaderFont,
                TextAlign = ContentAlignment.MiddleLeft
            };
            listLabel.Paint += (s, e) => QFG5Theme.DrawHeaderStroke(e.Graphics, listLabel.ClientRectangle);
            lstMDLFiles = new ListBox {
                Dock = DockStyle.Fill,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                BorderStyle = BorderStyle.None,
                IntegralHeight = false
            };
            lstMDLFiles.SelectedIndexChanged += LstMDLFiles_SelectedIndexChanged;
            listPanel.Controls.Add(lstMDLFiles);
            listPanel.Controls.Add(listLabel);

            TableLayoutPanel animPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = QFG5Theme.ColorSurface,
                Padding = new Padding(6),
                Margin = new Padding(2)
            };
            QFG5Theme.ApplyHeaderStroke(animPanel);
            animPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            animPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            animPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            animPanel.Controls.Add(new Label
            {
                Text = "ANIMATION",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.SectionHeaderFont,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            cmbAnimations = new QFG5ComboBox { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2) };
            cmbAnimations.ComboBox.SelectedIndexChanged += CmbAnimations_SelectedIndexChanged;
            animPanel.Controls.Add(cmbAnimations, 0, 1);

            TableLayoutPanel animPlayRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            animPlayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            animPlayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            animPlayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            btnPlayAnim = new QFG5Button { Text = "Play", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 2, 3, 2) };
            btnPlayAnim.Click += BtnPlayAnim_Click;
            btnAnimHalfSpeed = new QFG5Button { Text = "1×", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(2, 2, 3, 2) };
            btnAnimHalfSpeed.Click += (s, e) => {
                animHalfSpeed = !animHalfSpeed;
                btnAnimHalfSpeed.Text = animHalfSpeed ? "½×" : "1×";
                animTickAccumulator = 0;
            };
            lblAnimFrame = new Label {
                Text = "Frame: -",
                ForeColor = QFG5Theme.ColorTextMuted,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Padding = new Padding(5, 0, 0, 0)
            };
            animPlayRow.Controls.Add(btnPlayAnim, 0, 0);
            animPlayRow.Controls.Add(btnAnimHalfSpeed, 1, 0);
            animPlayRow.Controls.Add(lblAnimFrame, 2, 0);
            animPanel.Controls.Add(animPlayRow, 0, 2);

            TableLayoutPanel texPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = QFG5Theme.ColorSurface,
                Padding = new Padding(6),
                Margin = new Padding(2)
            };
            QFG5Theme.ApplyHeaderStroke(texPanel);
            texPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            texPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            texPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            texPanel.Controls.Add(new Label
            {
                Text = "TEXTURE",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.SectionHeaderFont,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            cmbTextures = new QFG5ComboBox { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2) };
            cmbTextures.ComboBox.SelectedIndexChanged += (s, e) => {
                if (glControl != null && cmbTextures.ComboBox.SelectedIndex >= 0 && lstMDLFiles.SelectedItem != null)
                    LoadMDL(Path.Combine(txtMDLFolder.Text, ((MdlListItem)lstMDLFiles.SelectedItem).FileName));
                if (glControl != null) glControl.Invalidate();
            };
            texPanel.Controls.Add(cmbTextures, 0, 1);
            chkAutoMultiTex = new CheckBox {
                Text = "Auto-link Multi-Textures",
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.CaptionFont,
                Checked = true,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(2, 2, 0, 0),
                BackColor = Color.Transparent
            };
            chkAutoMultiTex.CheckedChanged += (s, e) => {
                if (glControl != null && lstMDLFiles.SelectedItem != null) {
                    LoadMDL(Path.Combine(txtMDLFolder.Text, ((MdlListItem)lstMDLFiles.SelectedItem).FileName));
                    glControl.Invalidate();
                }
            };
            texPanel.Controls.Add(chkAutoMultiTex, 0, 2);

            TableLayoutPanel toolsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = QFG5Theme.ColorSurface,
                Padding = new Padding(6, 3, 6, 4),
                Margin = new Padding(2)
            };
            QFG5Theme.ApplyHeaderStroke(toolsPanel);
            toolsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            toolsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            toolsPanel.Controls.Add(new Label
            {
                Text = "TOOLS",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.CaptionFont,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            TableLayoutPanel utilPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            utilPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            utilPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            btnBuildIndex = new QFG5Button { Text = "Sync DB", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 1, 2, 1) };
            btnBuildIndex.Click += BtnBuildIndex_Click;
            QFG5Button btnRunAllLog = new QFG5Button { Text = "Log All", Dock = DockStyle.Fill, Height = 30, Margin = new Padding(2, 1, 0, 1) };
            btnRunAllLog.Click += BtnRunAllLog_Click;
            utilPanel.Controls.Add(btnBuildIndex, 0, 0);
            utilPanel.Controls.Add(btnRunAllLog, 1, 0);
            toolsPanel.Controls.Add(utilPanel, 0, 1);

            sidebarLayout.Controls.Add(listPanel, 0, 0);
            sidebarLayout.Controls.Add(animPanel, 0, 1);
            sidebarLayout.Controls.Add(texPanel, 0, 2);
            sidebarLayout.Controls.Add(toolsPanel, 0, 3);
            sidebar.Controls.Add(sidebarLayout);
            mainLayout.Controls.Add(sidebar, 0, 1);

            // --- Right Panel: Viewport ---
            Panel viewportPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 4, 8, 4), BackColor = Color.Transparent };
            TableLayoutPanel viewportLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            viewportLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            viewportLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            viewportLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            viewportLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            viewportLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            viewportPanel.Controls.Add(viewportLayout);
            mainLayout.Controls.Add(viewportPanel, 1, 1);

            TableLayoutPanel viewHeader = new TableLayoutPanel {
                Dock = DockStyle.Top,
                Height = 42,
                ColumnCount = 2,
                BackColor = QFG5Theme.ColorSurface
            };
            viewHeader.Paint += (s, e) => QFG5Theme.DrawHeaderStroke(e.Graphics, viewHeader.ClientRectangle);
            viewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            viewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));

            FlowLayoutPanel nameFlow = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            lblNameTitle = new Label { Text = "ACTIVE MODEL", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = QFG5Theme.ColorTextMuted, Font = QFG5Theme.CaptionFont, Padding = new Padding(4, 12, 0, 0) };
            lblModelName = new Label { Text = "<None>", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Font = QFG5Theme.BodyBoldFont, ForeColor = QFG5Theme.ColorTextGold, Padding = new Padding(8, 10, 0, 0) };
            nameFlow.Controls.AddRange(new Control[] { lblNameTitle, lblModelName });

            FlowLayoutPanel modeFlow = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            btnToggleMode = new QFG5Button { Text = "Wireframe3D", Width = 108, Height = 30, Margin = new Padding(4, 2, 0, 0) };
            btnToggleMode.Click += BtnToggleMode_Click;
            QFG5Button btnRecenter = new QFG5Button { Text = "Recenter", Width = 82, Height = 30, Margin = new Padding(4, 2, 0, 0) };
            btnRecenter.Click += (s, e) => {
                panX = 0; panY = 0; rotX = 0; rotY = 0; zoomLevel = 1.0f;
                glControl.Invalidate();
            };
            Label lblMode = new Label { Text = "VIEW MODE", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = QFG5Theme.ColorTextMuted, Font = QFG5Theme.CaptionFont, Padding = new Padding(0, 12, 0, 0) };
            modeFlow.Controls.Add(btnToggleMode);
            modeFlow.Controls.Add(lblMode);
            modeFlow.Controls.Add(btnRecenter);

            viewHeader.Controls.Add(nameFlow, 0, 0);
            viewHeader.Controls.Add(modeFlow, 1, 0);

            Panel glContainer = new Panel {
                Dock = DockStyle.Fill,
                Padding = new Padding(3),
                BackColor = QFG5Theme.ColorSurfaceRecessed
            };
            glContainer.Paint += (s, e) => QFG5Theme.DrawRecessedFrame(e.Graphics, glContainer.ClientRectangle);
            glControl = new GLControl(new OpenTK.Graphics.GraphicsMode(32, 24, 0, 8));
            glControl.Dock = DockStyle.Fill;
            glControl.Paint += GlControl_Paint;
            glControl.Resize += GlControl_Resize;
            glControl.Load += (s, e) => {
                var swGl = System.Diagnostics.Stopwatch.StartNew();
                GL.ClearColor(Color.FromArgb(22, 24, 21));
                GL.Enable(EnableCap.DepthTest);
                GL.Enable(EnableCap.CullFace);
                GL.CullFace(CullFaceMode.Back);
                GL.ShadeModel(ShadingModel.Smooth);
                GL.Enable(EnableCap.ColorMaterial);
                GL.ColorMaterial(MaterialFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);
                GL.Light(LightName.Light0, LightParameter.Position, new float[] { 0.4f, 1.0f, 0.8f, 0.0f });
                GL.Light(LightName.Light0, LightParameter.Diffuse, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                GL.Light(LightName.Light0, LightParameter.Ambient, new float[] { 0.35f, 0.35f, 0.35f, 1.0f });
                GL.Light(LightName.Light0, LightParameter.Specular, new float[] { 0.2f, 0.2f, 0.2f, 1.0f });
                GL.Enable(EnableCap.Light0);
                SetupViewport();
                swGl.Stop();
                Logger.LogPerf($"OpenGL context initialization (glControl.Load): {swGl.ElapsedMilliseconds} ms");
            };

            this.VisibleChanged += (s, e) => {
                if (this.Visible && glControl != null) {
                    if (glControl.IsHandleCreated) {
                        glControl.MakeCurrent();
                        SetupViewport();
                        glControl.Update();
                        glControl.Invalidate();
                    }
                }
            };

            glControl.HandleCreated += (s, e) => {
                glControl.MakeCurrent();
                SetupViewport();
                glControl.Update();
                glControl.Invalidate();

                Timer startupTimer = new Timer { Interval = 100 };
                startupTimer.Tick += (st, se) => {
                    startupTimer.Stop();
                    if (glControl != null && glControl.Visible) {
                        glControl.MakeCurrent();
                        SetupViewport();
                        glControl.Update();
                        glControl.Invalidate();
                    }
                    startupTimer.Dispose();
                };
                startupTimer.Start();
            };

            this.ParentChanged += (s, e) => {
                if (this.ParentForm != null) {
                    this.ParentForm.Shown += (fs, fe) => {
                        if (glControl != null) {
                            glControl.MakeCurrent();
                            SetupViewport();
                            glControl.Update();
                            glControl.Invalidate();
                        }
                    };
                }
            };

            glControl.MouseDown += GlControl_MouseDown;
            glControl.MouseMove += GlControl_MouseMove;
            glControl.MouseWheel += GlControl_MouseWheel;

            FlowLayoutPanel overlayToolbar = new FlowLayoutPanel {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = QFG5Theme.ColorSurface,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(4, 2, 4, 2),
                Margin = new Padding(0)
            };
            overlayToolbar.Paint += (s, e) => QFG5Theme.DrawHeaderStroke(e.Graphics, overlayToolbar.ClientRectangle);

            ToolTip tt = new ToolTip();

            btnResetRotation = new QFG5Button { Text = "Reset", Width = 54, Height = 30, Margin = new Padding(0, 0, 4, 0) };
            tt.SetToolTip(btnResetRotation, "Reset View (Home)");
            btnResetRotation.Click += (s, e) => {
                rotX = 0; rotY = 0; rotOffset90X = 270; rotOffset90Y = 0; uvRotation = 0;
                panX = 0; panY = 0;
                zoomLevel = 1.0f;
                if (glControl != null) glControl.Invalidate();
            };

            btnRotate90 = new QFG5Button { Text = "Rotate X", Width = 64, Height = 30, Margin = new Padding(0, 0, 4, 0) };
            tt.SetToolTip(btnRotate90, "Rotate 90° (X Axis)");
            btnRotate90.Click += (s, e) => { rotOffset90X = (rotOffset90X + 90) % 360; if (glControl != null) glControl.Invalidate(); };

            btnRotateAxis = new QFG5Button { Text = "Rotate Y", Width = 64, Height = 30, Margin = new Padding(0, 0, 4, 0) };
            tt.SetToolTip(btnRotateAxis, "Rotate 90° (Y Axis)");
            btnRotateAxis.Click += (s, e) => { rotOffset90Y = (rotOffset90Y + 90) % 360; if (glControl != null) glControl.Invalidate(); };

            QFG5Button btnToggleSpinShort = new QFG5Button { Text = "Auto Spin", Width = 68, Height = 30, Margin = new Padding(0, 0, 4, 0) };
            tt.SetToolTip(btnToggleSpinShort, "Toggle Auto-Spin");
            btnToggleSpinShort.Click += (s, e) => { isSpinning = !isSpinning; UpdateSpinTimer(); };

            QFG5Button btnExplodeShort = new QFG5Button { Text = "Explode", Width = 62, Height = 30, Margin = new Padding(0, 0, 4, 0) };
            tt.SetToolTip(btnExplodeShort, "Toggle Exploded View");
            btnExplodeShort.Click += (s, e) => { isExploded = !isExploded; if (glControl != null) glControl.Invalidate(); };

            QFG5Button btnToggleAxis = new QFG5Button { Text = "Axis", Width = 42, Height = 30, Margin = new Padding(0, 0, 4, 0) };
            tt.SetToolTip(btnToggleAxis, "Toggle Coordinate Axis");
            btnToggleAxis.Click += (s, e) => { showAxis = !showAxis; if (glControl != null) glControl.Invalidate(); };

            QFG5Button btnScreenshot = new QFG5Button { Text = "Shot", Width = 44, Height = 30, Margin = new Padding(0, 0, 4, 0) };
            tt.SetToolTip(btnScreenshot, "Save Viewport Screenshot (PNG)");
            btnScreenshot.Click += (s, e) => SaveScreenshot();

            overlayToolbar.Controls.AddRange(new Control[] { btnResetRotation, btnRotate90, btnRotateAxis, btnToggleSpinShort, btnExplodeShort, btnToggleAxis, btnScreenshot });
            foreach (Control control in overlayToolbar.Controls)
                control.Font = QFG5Theme.CaptionFont;

            glContainer.Controls.Add(glControl);
            viewportEmptyState = new Panel {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(235, 22, 24, 21)
            };
            viewportEmptyState.Controls.Add(new Label {
                Text = "NO MODEL LOADED\r\nSelect a model file from the list to preview it.",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.BodyFont,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });
            glContainer.Controls.Add(viewportEmptyState);
            viewportEmptyState.BringToFront();

            TableLayoutPanel footer = new TableLayoutPanel {
                Dock = DockStyle.Bottom,
                Height = 100,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            Label lblHint = new Label {
                Text = "Drag to orbit · Wheel to zoom · Middle-drag to pan",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            TableLayoutPanel actionGrid = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            actionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            actionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            actionGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            actionGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            btnLog = new QFG5Button { Text = "Log", Height = 32, Dock = DockStyle.Fill, Margin = new Padding(4, 2, 2, 2) };
            btnLog.Click += (s, e) => Logger.OpenLogFile();
            btnExport = new QFG5Button { Text = "Export PNG", Height = 32, Dock = DockStyle.Fill, Margin = new Padding(2, 2, 2, 2) };
            btnExport.Click += BtnExport_Click;
            QFG5Button btnExportGlb = new QFG5Button { Text = "Export GLB", Height = 32, Dock = DockStyle.Fill, Margin = new Padding(4, 2, 2, 2) };
            btnExportGlb.Click += (s, e) => ExportToGlb();
            QFG5Button btnEditTexture = new QFG5Button { Text = "Edit Texture", Height = 32, Dock = DockStyle.Fill, Margin = new Padding(2, 2, 2, 2) };
            btnEditTexture.Click += BtnEditTexture_Click;

            actionGrid.Controls.Add(btnLog, 0, 0);
            actionGrid.Controls.Add(btnExport, 1, 0);
            actionGrid.Controls.Add(btnExportGlb, 0, 1);
            actionGrid.Controls.Add(btnEditTexture, 1, 1);
            footer.Controls.Add(lblHint, 0, 0);
            footer.Controls.Add(actionGrid, 0, 1);

            viewportLayout.Controls.Add(viewHeader, 0, 0);
            viewportLayout.Controls.Add(overlayToolbar, 0, 1);
            viewportLayout.Controls.Add(glContainer, 0, 2);
            viewportLayout.Controls.Add(footer, 0, 3);

            this.Controls.Add(mainLayout);
        }

        private TextBox CreateThemedTextBox() {
            return new TextBox { 
                Dock = DockStyle.Fill, 
                BorderStyle = BorderStyle.FixedSingle, 
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyFont,
                Margin = new Padding(0, 3, 5, 3),
                Height = 30
            };
        }

        private Panel CreateToolPanel(string title, int height) {
            Panel p = new Panel { 
                Dock = DockStyle.Top, 
                Height = height, 
                ForeColor = Color.Gold, 
                Padding = new Padding(5, 5, 5, 5),
                Margin = new Padding(0, 0, 0, 10),
                BackColor = Color.Transparent
            };
            Label lbl = new Label { 
                Text = title, 
                Dock = DockStyle.Top, 
                Height = 25, 
                ForeColor = Color.Gold, 
                Font = new Font("Arial", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            p.Controls.Add(lbl);
            return p;
        }

        private void BtnToggleMode_Click(object sender, EventArgs e)
        {
            if (currentMode == RenderMode.Textured3D) currentMode = RenderMode.Wireframe3D;
            else if (currentMode == RenderMode.Wireframe3D) currentMode = RenderMode.Texture2D;
            else currentMode = RenderMode.Textured3D;

            btnToggleMode.Text = currentMode.ToString();
            UpdateSpinTimer();
            if(glControl != null) glControl.Invalidate();
        }

        private void UpdateSpinTimer()
        {
            if (spinTimer == null) return;

            bool shouldRun = !IsDisposed && Visible && FindForm() != null &&
                (isAnimPlaying || (isSpinning && currentMode != RenderMode.Texture2D));
            if (shouldRun && !spinTimer.Enabled)
                spinTimer.Start();
            else if (!shouldRun && spinTimer.Enabled)
                spinTimer.Stop();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && spinTimer != null)
            {
                spinTimer.Stop();
                spinTimer.Dispose();
                spinTimer = null;
            }
            base.Dispose(disposing);
        }

        private void LoadFilesInFolder(string folderPath)
        {
            lstMDLFiles.Items.Clear();
            ResetForm();

            if (Directory.Exists(folderPath))
            {
                try
                {
                    string[] files = Directory.GetFiles(folderPath, "*.mdl");
                    if (files.Length == 0)
                    {
                        files = Directory.GetFiles(folderPath, "*.MDL");
                    }
                    
                    foreach (var f in files)
                    {
                        string fileName = Path.GetFileName(f);
                        string modelName = "<None>";
                        try {
                            modelName = viewerLogic.getName(f);
                        } catch { } // Ignore read errors, just fallback to None

                        lstMDLFiles.Items.Add(new MdlListItem { 
                            FileName = fileName, 
                            ModelName = modelName 
                        });
                    }

                    if (lstMDLFiles.Items.Count == 0)
                    {
                        MessageBox.Show("No MDL files found in " + folderPath, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error reading folder: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void TxtMDLFolder_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
            {
                if (Directory.Exists(txtMDLFolder.Text))
                {
                    LoadFilesInFolder(txtMDLFolder.Text);
                }
                else
                {
                    MessageBox.Show("Cannot find folder: " + txtMDLFolder.Text, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Logger.Log("MDLViewer: Folder not found: " + txtMDLFolder.Text);
                }
            }
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                if (Directory.Exists(txtMDLFolder.Text))
                {
                    fbd.SelectedPath = txtMDLFolder.Text;
                }

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtMDLFolder.Text = fbd.SelectedPath;
                    LoadFilesInFolder(fbd.SelectedPath);
                }
            }
        }

        private void BtnBrowseANM_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                if (Directory.Exists(txtANMFolder.Text))
                {
                    fbd.SelectedPath = txtANMFolder.Text;
                }

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtANMFolder.Text = fbd.SelectedPath;
                    // Auto-scan if a model is already selected
                    if (lstMDLFiles.SelectedItem is MdlListItem item)
                    {
                        ScanAnimationsForModel(item.FileName);
                    }
                }
            }
        }


        private void LstMDLFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstMDLFiles.SelectedItem is MdlListItem item)
            {
                currentMeshes = null;
                viewportEmptyState.Visible = true;
                StopAnimPlayback();
                ClearLoadedAnimation();
                cmbAnimations.ComboBox.Items.Clear();
                cmbAnimations.ComboBox.Items.Add("<No Animation>");
                cmbAnimations.ComboBox.SelectedIndex = 0;
                
                string fullPath = Path.Combine(txtMDLFolder.Text, item.FileName);
                if (File.Exists(fullPath))
                {
                    LoadMDL(fullPath);
                    ScanAnimationsForModel(item.FileName);
                    ScanTexturesForModel(item.FileName);
                }
                if (glControl != null) glControl.Invalidate();
            }
        }

        private void ScanTexturesForModel(string mdlFileName)
        {
            if (string.IsNullOrEmpty(mdlFileName)) return;

            string prefix = Path.GetFileNameWithoutExtension(mdlFileName);
            if (prefix.Length > 3) prefix = prefix.Substring(0, 3); // Characters usually start with 3 digits

            string[] foundBmp = Directory.GetFiles(txtMDLFolder.Text, prefix + "*.bmp");
            string[] foundPng = Directory.GetFiles(txtMDLFolder.Text, prefix + "*.png");
            
            availableTextures.Clear();
            cmbTextures.ComboBox.Items.Clear();
            cmbTextures.ComboBox.Items.Add("<Embedded>");

            foreach(var f in foundBmp) {
                availableTextures.Add(f);
                cmbTextures.ComboBox.Items.Add(Path.GetFileName(f));
            }
            foreach(var f in foundPng) {
                availableTextures.Add(f);
                cmbTextures.ComboBox.Items.Add(Path.GetFileName(f));
            }

            if (cmbTextures.ComboBox.Items.Count > 1) {
                // We default to embedded unless the user picks one, 
                // but we could auto-select the first one if preferred.
                // For now, keep it on embedded.
                cmbTextures.ComboBox.SelectedIndex = 0;
            } else {
                cmbTextures.ComboBox.SelectedIndex = 0;
            }
        }

        private void ScanAnimationsForModel(string mdlFileName)
        {
            if (!Directory.Exists(txtANMFolder.Text) || string.IsNullOrEmpty(mdlFileName)) return;

            string prefix = Path.GetFileNameWithoutExtension(mdlFileName);
            // Characters usually have 3-digit prefixes like 072
            // ANMs are like 07200.ANM, 07201.ANM
            int p;
            if (!int.TryParse(prefix, out p)) return; // If it's not a numbered model, skip

            string[] foundAnms = Directory.GetFiles(txtANMFolder.Text, prefix + "*.anm");
            if (foundAnms.Length == 0)
            {
                foundAnms = Directory.GetFiles(txtANMFolder.Text, prefix + "*.ANM");
            }

            availableAnimations.Clear();
            cmbAnimations.ComboBox.Items.Clear();
            cmbAnimations.ComboBox.Items.Add("<No Animation>");

            foreach(var anm in foundAnms)
            {
                // Optionally we could validate bone count here, but for speed we'll assume matching prefix is ok
                string name = Path.GetFileName(anm);
                availableAnimations.Add(anm);
                cmbAnimations.ComboBox.Items.Add(name);
            }

            if (cmbAnimations.ComboBox.Items.Count > 1)
            {
                // Auto-select the first animation
                cmbAnimations.ComboBox.SelectedIndex = 1;
            }
        }

        private void LoadMDL(string mdlFile)
        {
            try
            {
                ClearTextures(); // Clean up old textures
                Logger.Log("MDLViewer: Loading file: " + mdlFile);
                lblModelName.Text = viewerLogic.getName(mdlFile);
                
                // Update persistent database
                try {
                    currentMeshes = MeshLoader.LoadMesh(mdlFile); // Load mesh first to get submesh/vertex counts
                    int submeshCount = currentMeshes != null ? currentMeshes.Count : 0;
                    int totalVerts = 0;
                    if (currentMeshes != null) {
                        foreach(var m in currentMeshes) totalVerts += m.Vertices.Count;
                    }
                    
                    var entry = new ModelEntry {
                        Prefix = Path.GetFileNameWithoutExtension(mdlFile),
                        Name = viewerLogic.getName(mdlFile),
                        Submeshes = submeshCount,
                        Vertices = totalVerts
                    };
                    _db.AddOrUpdate(entry);
                } catch (Exception ex) {
                    Logger.LogError("MdlViewerTab", $"Error updating database for {mdlFile}: {ex.Message}");
                }

                if (cmbTextures.ComboBox.SelectedIndex > 0 && (cmbTextures.ComboBox.SelectedIndex - 1) < availableTextures.Count) {
                    string extTexPath = availableTextures[cmbTextures.ComboBox.SelectedIndex - 1];
                    try {
                        currentTextureIds.Clear();
                        if (chkAutoMultiTex.Checked) {
                            string baseDir = Path.GetDirectoryName(extTexPath);
                            string filename = Path.GetFileNameWithoutExtension(extTexPath);
                            string ext = Path.GetExtension(extTexPath);
                            
                            string prefix = filename;
                            if (prefix.Contains("-")) {
                                prefix = prefix.Substring(0, prefix.LastIndexOf('-'));
                            }
                            
                            int ti = 0;
                            while (true) {
                                string testPath = Path.Combine(baseDir, $"{prefix}-{ti}{ext}");
                                if (!File.Exists(testPath)) {
                                    if (ti == 0 && File.Exists(Path.Combine(baseDir, $"{prefix}{ext}"))) {
                                        testPath = Path.Combine(baseDir, $"{prefix}{ext}");
                                    } else {
                                        break;
                                    }
                                }
                                Bitmap bmpIn = new Bitmap(testPath);
                                currentBitmaps.Add(bmpIn);
                                currentTextureIds.Add(BindSingleTexture(bmpIn));
                                ti++;
                            }
                            fallbackTextureId = currentTextureIds.Count > 1 ? -1 : (currentTextureIds.Count > 0 ? currentTextureIds[0] : -1);
                        } else {
                            Bitmap bmpIn = new Bitmap(extTexPath);
                            currentBitmaps.Add(bmpIn);
                            int id = BindSingleTexture(bmpIn);
                            currentTextureIds.Add(id);
                            fallbackTextureId = id;
                        }
                    } catch (Exception ex) {
                        Logger.LogError("MdlViewerTab", $"Error loading external texture: {ex.Message}");
                    }
                } else {
                    var bmps = viewerLogic.getAllBMPs(mdlFile);
                    currentTextureIds.Clear();
                    foreach (var bmp in bmps) {
                        currentBitmaps.Add(bmp);
                        currentTextureIds.Add(BindSingleTexture(bmp));
                    }
                    fallbackTextureId = currentTextureIds.Count > 1 ? -1 : (currentTextureIds.Count > 0 ? currentTextureIds[0] : -1);
                }
                
                // The mesh loading logic was already here, ensure it's still present and correctly handles currentMeshes
                // If currentMeshes was already loaded in the database update block, this might be redundant or needs adjustment.
                // Assuming the instruction intended to keep the existing mesh loading logic for display purposes.
                if (currentMeshes != null && currentMeshes.Count > 0)
                {
                    float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
                    float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
                    bool hasVerts = false;
                    foreach (var m in currentMeshes) {
                        foreach (var v in m.Vertices) {
                            float vx = v.X, vy = v.Y, vz = v.Z;
                            if (vx < minX) minX = vx; if (vx > maxX) maxX = vx;
                            if (vy < minY) minY = vy; if (vy > maxY) maxY = vy;
                            if (vz < minZ) minZ = vz; if (vz > maxZ) maxZ = vz;
                            hasVerts = true;
                        }
                    }
                    if (hasVerts) {
                        meshCenter = new QFG5Extractor.Resources.Model.Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, (minZ + maxZ) / 2f);
                        float size = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
                        meshScale = (size > 0) ? 2.0f / size : 1.0f; // Scale to fit nicely in OpenGL coords (-1 to 1)
                        zoomLevel = 1.0f;
                    }
                } else {
                    currentMeshes = null;
                    StopAnimPlayback();
                    ClearLoadedAnimation();
                }

                viewportEmptyState.Visible = currentMeshes == null || currentMeshes.Count == 0;
                btnExport.Enabled = true;

                // Adjust the height of the control if the image is really tall
                this.AutoScrollMinSize = new Size(0, btnExport.Bottom + 20);
            }
            catch(Exception ex)
            {
                string errMsg = "Error loading image from MDL file: " + mdlFile + "\n" + "Error text:\n" + ex.Message;
                Logger.LogError("MDLViewer", errMsg);
                MessageBox.Show(errMsg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetForm();
            }
        }

        private void ResetForm()
        {
            lblModelName.Text = "<None>";
            currentMeshes = null;
            viewportEmptyState.Visible = true;
            StopAnimPlayback();
            ClearLoadedAnimation();
            
            foreach (var bmp in currentBitmaps) bmp.Dispose();
            currentBitmaps.Clear();
            
            foreach (int id in currentTextureIds) {
                if (id != 0) GL.DeleteTexture(id);
            }
            currentTextureIds.Clear();
            fallbackTextureId = -1;
            
            loadedAnmName = "<None>";
            cmbAnimations.ComboBox.Items.Clear();
            cmbAnimations.ComboBox.Items.Add("<No Animation>");
            cmbAnimations.ComboBox.SelectedIndex = 0;
            btnExport.Enabled = false;
            
            if (glControl != null) glControl.Invalidate();
        }

        private void GlControl_MouseDown(object sender, MouseEventArgs e)
        {
            lastMousePos = e.Location;
            if (currentMode != RenderMode.Texture2D) glControl.Focus();
        }

        private void GlControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && currentMode != RenderMode.Texture2D)
            {
                rotY += (e.X - lastMousePos.X) * 0.01f;
                rotX += (e.Y - lastMousePos.Y) * 0.01f;
                lastMousePos = e.Location;
                glControl.Invalidate();
            }
            else if (e.Button == MouseButtons.Right && currentMode != RenderMode.Texture2D)
            {
                panX += (e.X - lastMousePos.X) * 0.05f;
                panY -= (e.Y - lastMousePos.Y) * 0.05f;
                lastMousePos = e.Location;
                glControl.Invalidate();
            }
        }

        private void GlControl_MouseWheel(object sender, MouseEventArgs e)
        {
            if (currentMode != RenderMode.Texture2D)
            {
                zoomLevel += (e.Delta * 0.001f); // Replaces trkZoom logic
                zoomLevel = Math.Max(0.01f, Math.Min(10.0f, zoomLevel));
                glControl.Invalidate();
            }
        }

        private int BindSingleTexture(Bitmap bmp)
        {
            if (bmp == null) return 0;
            int id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, id);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, data.Width, data.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
            bmp.UnlockBits(data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            return id;
        }

        private void LoadTextureToOpenGL(Bitmap bmp)
        {
            currentTextureIds.Clear();
            currentTextureIds.Add(BindSingleTexture(bmp));
        }
    
        private void ClearTextures()
        {
            foreach(int id in currentTextureIds) {
                if (id != 0) GL.DeleteTexture(id);
            }
            currentTextureIds.Clear();
            // fallbackTextureId is always one of currentTextureIds when set — do not double-delete
            fallbackTextureId = -1;

            foreach (var bmp in currentBitmaps) {
                try { bmp.Dispose(); } catch { }
            }
            currentBitmaps.Clear();
        }

        private void GlControl_Load(object sender, EventArgs e)
        {
            // This method is now empty as its logic has been moved to the inline Load event handler
            // and SetupViewport method.
        }

        private void SetupViewport()
        {
            if (glControl == null || glControl.ClientSize.Height == 0 || glControl.ClientSize.Width == 0) return;
            if (glControl.Context != null) glControl.MakeCurrent();

            GL.Viewport(0, 0, glControl.ClientSize.Width, glControl.ClientSize.Height);
            
            float aspect_ratio = glControl.ClientSize.Width / (float)glControl.ClientSize.Height;
            Matrix4 perpective = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect_ratio, 0.1f, 200.0f);
            
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref perpective);
        }

        private void GlControl_Resize(object sender, EventArgs e)
        {
            SetupViewport();
        }

        private bool _firstGlPaintLogged = false;
        private void GlControl_Paint(object sender, PaintEventArgs e)
        {
            if (!_firstGlPaintLogged) {
                _firstGlPaintLogged = true;
                Logger.LogPerf($"First GL paint: {Logger.StartupWatch.ElapsedMilliseconds} ms from start");
            }
            if (glControl == null) return;
            if (!glControl.Context.IsCurrent) glControl.MakeCurrent();
            // Render!
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Ultimate protection against startup scramble
            GL.Viewport(0, 0, Math.Max(1, glControl.ClientSize.Width), Math.Max(1, glControl.ClientSize.Height));
            float aspect_ratio = Math.Max(1.0f, glControl.ClientSize.Width) / (float)Math.Max(1.0f, glControl.ClientSize.Height);
            Matrix4 perpective = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect_ratio, 0.1f, 200.0f);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref perpective);

            GL.Disable(EnableCap.CullFace); // Disabled for Phase 11: Fixes animation reverse-winding artifacts

            if (currentMode == RenderMode.Textured3D) GL.Enable(EnableCap.Texture2D);
            else GL.Disable(EnableCap.Texture2D);

            if (currentMeshes == null || currentMeshes.Count == 0)
            {
                glControl.SwapBuffers();
                return;
            }

            // --- Texture 2D flat view: draw texture as a screen-filling quad ---
            if (currentMode == RenderMode.Texture2D)
            {
                int texId = currentTextureIds.Count > 0 ? currentTextureIds[0]
                          : (fallbackTextureId != -1 ? fallbackTextureId : 0);
                if (texId != 0) {
                    // Switch to ortho so 1:1 texture spans [-1,1]
                    GL.MatrixMode(MatrixMode.Projection);
                    GL.PushMatrix();
                    GL.LoadIdentity();
                    GL.Ortho(-1, 1, -1, 1, -1, 1);
                    GL.MatrixMode(MatrixMode.Modelview);
                    GL.LoadIdentity();
                    
                    GL.Disable(EnableCap.DepthTest); // Prevent clipping in 2D mode
                    GL.Disable(EnableCap.Lighting);
                    GL.Enable(EnableCap.Texture2D);
                    GL.BindTexture(TextureTarget.Texture2D, texId);
                    GL.Color3(Color.White);
                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                    GL.Begin(PrimitiveType.Quads);
                    GL.TexCoord2(0f, 1f); GL.Vertex2(-1f, -1f);
                    GL.TexCoord2(1f, 1f); GL.Vertex2( 1f, -1f);
                    GL.TexCoord2(1f, 0f); GL.Vertex2( 1f,  1f);
                    GL.TexCoord2(0f, 0f); GL.Vertex2(-1f,  1f);
                    GL.End();
                    GL.Disable(EnableCap.Texture2D);
                    GL.Enable(EnableCap.DepthTest);
                    
                    // Reset projection
                    GL.MatrixMode(MatrixMode.Projection);
                    GL.PopMatrix();
                }
                glControl.SwapBuffers();
                return;
            }

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();

            float scale = zoomLevel;
            Matrix4 camera = Matrix4.LookAt(OpenTK.Vector3.Zero, -OpenTK.Vector3.UnitZ, OpenTK.Vector3.UnitY);
            GL.LoadMatrix(ref camera);

            // Move the object away from the camera a reasonable amount
            float cameraZ = -6.0f;
            if (isExploded && currentMeshes.Count > 1) {
                cameraZ -= (currentMeshes.Count * 0.4f); // Zoom out to fit exploded width
            }
            GL.Translate(panX, panY, cameraZ);
            GL.Scale(scale, scale, scale);

            // X rotation
            GL.Rotate(rotX * (180f/(float)Math.PI), 1.0f, 0.0f, 0.0f);
            
            // Y Rotation
            GL.Rotate(rotY * (180f/(float)Math.PI), 0.0f, 1.0f, 0.0f);
            
            // Fixed rotation offsets (90 deg steps)
            if (rotOffset90X != 0) GL.Rotate(rotOffset90X, 1.0f, 0.0f, 0.0f);
            if (rotOffset90Y != 0) GL.Rotate(rotOffset90Y, 0.0f, 1.0f, 0.0f);

            if (currentMode == RenderMode.Textured3D && (currentTextureIds.Count > 0 || fallbackTextureId != -1))
            {
                GL.Enable(EnableCap.Texture2D);
                GL.Enable(EnableCap.Lighting);
                GL.Color3(Color.White); // Fix brightness
                // Initial bind of the first texture if available
                int firstId = fallbackTextureId != -1 ? fallbackTextureId : (currentTextureIds.Count > 0 ? currentTextureIds[0] : 0);
                if (firstId != 0) GL.BindTexture(TextureTarget.Texture2D, firstId);
            }
            else
            {
                GL.Disable(EnableCap.Texture2D);
                GL.Disable(EnableCap.Lighting);
                // Wireframe coloring
                GL.Color3(Color.FromArgb(180, 180, 200));
            }

            if (currentMode == RenderMode.Wireframe3D)
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            else
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            // Draw Coordinate Axis at model origin
            if (showAxis)
            {
                GL.Disable(EnableCap.Texture2D);
                GL.Disable(EnableCap.Lighting);
                GL.LineWidth(2.0f);
                GL.Begin(PrimitiveType.Lines);
                // X - Red
                GL.Color3(Color.Red); GL.Vertex3(0, 0, 0); GL.Vertex3(2, 0, 0);
                // Y - Green
                GL.Color3(Color.Green); GL.Vertex3(0, 0, 0); GL.Vertex3(0, 2, 0);
                // Z - Blue
                GL.Color3(Color.Blue); GL.Vertex3(0, 0, 0); GL.Vertex3(0, 0, 2);
                GL.End();
                GL.LineWidth(1.0f);
            }

            // GL.Begin(PrimitiveType.Triangles); // Removed

            float explodeSpacing = 1.0f; // Scale is roughly 1.0 to fit inside viewing area, so spacing of 1.0 is side-by-side
            // float totalExplodeWidth = isExploded ? explodeSpacing * (currentMeshes.Count - 1) : 0; // Removed
            int meshIndex = 0;
            foreach (var mesh in currentMeshes)
            {
                float offsetX = 0f;
                bool applySkeleton = false;
                
                // Handle Exploded View OR Skeleton Assembly
                if (isExploded) {
                    offsetX = (float)meshIndex * 50f * meshScale;
                } else if (currentSkeleton != null && meshIndex < currentSkeleton.Length) {
                    applySkeleton = true;
                }

                if (applySkeleton)
                {
                    GL.PushMatrix();
                    // Apply global scaling/centering AFTER skeletal transform
                    GL.Scale(meshScale, meshScale, meshScale);
                    GL.Translate(-meshCenter.X, -meshCenter.Y, -meshCenter.Z);
                    
                    // Note: OpenTK matrices are row-major
                    Matrix4 skel = currentSkeleton[meshIndex];
                    GL.MultMatrix(ref skel);
                }

                GL.Begin(PrimitiveType.Triangles);
                
                    int activeTextureId = -1;
                    for (int fIdx = 0; fIdx < mesh.Faces.Count; fIdx++)
                    {
                        // Handle texture switching
                        if (currentMode == RenderMode.Textured3D)
                        {
                            int texIdx = (mesh.FaceTextures != null && fIdx < mesh.FaceTextures.Count) ? mesh.FaceTextures[fIdx] : 0;
                            int targetId = fallbackTextureId; // Default to override or first texture
                            
                            if (fallbackTextureId == -1 && texIdx >= 0 && texIdx < currentTextureIds.Count)
                            {
                                targetId = currentTextureIds[texIdx];
                            }
                            
                            if (targetId != activeTextureId && targetId != -1)
                            {
                                GL.End();
                                GL.BindTexture(TextureTarget.Texture2D, targetId);
                                GL.Begin(PrimitiveType.Triangles);
                                activeTextureId = targetId;
                            }
                        }

                        int[] face = mesh.Faces[fIdx];
                        int[] faceUV = (mesh.FaceUVs != null && mesh.FaceUVs.Count > fIdx) ? mesh.FaceUVs[fIdx] : null;

                        for (int i = 0; i < 3; i++)
                        {
                            int pIdx = face[i];
                            int uIdx = (faceUV != null && i < faceUV.Length) ? faceUV[i] : -1;

                            if (pIdx >= 0 && pIdx < mesh.Vertices.Count)
                            {
                                var vOrig = mesh.Vertices[pIdx];

                                if (uIdx >= 0 && uIdx < mesh.UVs.Count)
                                {
                                    var uv = mesh.UVs[uIdx];
                                    float u = uv.X;
                                    float v = 1.0f - uv.Y;

                                    if (uvRotation == 1) { // 90 deg
                                        float tmp = u;
                                        u = 1.0f - v; 
                                        v = tmp;
                                    } else if (uvRotation == 2) { // 180 deg
                                        u = 1.0f - u;
                                        v = 1.0f - v;
                                    } else if (uvRotation == 3) { // 270 deg
                                        float tmp = u;
                                        u = v;
                                        v = 1.0f - tmp;
                                    }

                                    GL.TexCoord2(u, v);
                                }

                                if (mesh.Normals != null && pIdx < mesh.Normals.Count)
                                {
                                    var n = mesh.Normals[pIdx];
                                    GL.Normal3(n.X, n.Y, n.Z);
                                }

                                float vx = vOrig.X;
                                float vy = vOrig.Y;
                                float vz = vOrig.Z;

                                if (!applySkeleton)
                                {
                                    vx = (vx - meshCenter.X) * meshScale + offsetX;
                                    vy = (vy - meshCenter.Y) * meshScale;
                                    vz = (vz - meshCenter.Z) * meshScale;
                                }

                                GL.Vertex3(vx, vy, vz);
                            }
                        }
                    }
                
                GL.End();
                
                if (applySkeleton)
                {
                    GL.PopMatrix();
                }
                
                meshIndex++;
            }

            // GL.End(); // Removed
            glControl.SwapBuffers();
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            try
            {
                if (lstMDLFiles.SelectedItem is MdlListItem selectedItem && !string.IsNullOrEmpty(txtMDLFolder.Text))
                {
                    using (SaveFileDialog sfd = new SaveFileDialog())
                    {
                        sfd.Filter = "PNG Files (*.png)|*.png"; // Changed to PNG
                        string selectedFile = selectedItem.FileName;
                        string suggestedName = selectedFile;
                        if (suggestedName.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase))
                        {
                            suggestedName = suggestedName.Substring(0, suggestedName.Length - 4) + ".png";
                        }
                        else
                        {
                            suggestedName = suggestedName + ".png";
                        }
                        
                        sfd.FileName = suggestedName;
                        if (!string.IsNullOrEmpty(txtMDLFolder.Text))
                        {
                            sfd.InitialDirectory = txtMDLFolder.Text;
                        }

                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            string fullPath = Path.Combine(txtMDLFolder.Text, selectedFile);
                            int exported = 0; // Added counter
                            if (File.Exists(fullPath))
                            {
                                Bitmap exportImage = null;
                                try {
                                    exportImage = viewerLogic.getBMP(fullPath);
                                } catch {}
                                
                                if (exportImage != null)
                                {
                                    string outName = Path.GetFileNameWithoutExtension(fullPath) + ".png";
                                    exportImage.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png); // Save to sfd.FileName
                                    exported++;
                                }
                            }
                            if (exported > 0) {
                                MessageBox.Show("Export Successful!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            } else {
                                MessageBox.Show("No image to export or export failed.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("MDLViewer: Error exporting image - " + ex.Message);
                MessageBox.Show("Error exporting file, see log for details", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRunAllLog_Click(object sender, EventArgs e)
        {
            if (lstMDLFiles.Items.Count == 0 || string.IsNullOrEmpty(txtMDLFolder.Text))
            {
                MessageBox.Show("No folder selected or no MDL files to process.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int errorCount = 0;
            Logger.Log("--- Starting Batch MDL Error Log ---");
            foreach (var item in lstMDLFiles.Items)
            {
                if (item is MdlListItem mdlItem)
                {
                    string fullPath = Path.Combine(txtMDLFolder.Text, mdlItem.FileName);
                    try
                    {
                        // This method internally logs the errors. We just want to suppress the message box
                        // for batch runs so we bypass LoadMDL and call viewerLogic directly.
                        viewerLogic.getName(fullPath);
                        viewerLogic.getBMP(fullPath);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Logger.LogError("MDLViewer (Batch)", "File: " + mdlItem.FileName + " | Error: " + ex.Message);
                    }
                }
            }
            Logger.Log("--- Finished Batch MDL Error Log ---");

            MessageBox.Show($"Batch log complete. Found {errorCount} errors out of {lstMDLFiles.Items.Count} files.\nOpen the log to view details.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CmbAnimations_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbAnimations.ComboBox.SelectedIndex <= 0)
            {
                StopAnimPlayback();
                ClearLoadedAnimation();
                RecalculateAssembledCenter();
                if (glControl != null) glControl.Invalidate();
                return;
            }
 
            string anmName = cmbAnimations.ComboBox.SelectedItem.ToString();
            string path = Path.Combine(txtANMFolder.Text, anmName);
            ApplyAnimation(path);
        }

        private void BtnPlayAnim_Click(object sender, EventArgs e)
        {
            if (loadedAnimationFrames == null || loadedAnimationFrames.Length == 0)
            {
                MessageBox.Show("Select an ANM first.", "Animation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            isAnimPlaying = !isAnimPlaying;
            if (btnPlayAnim != null)
                btnPlayAnim.Text = isAnimPlaying ? "Pause" : "Play";
            UpdateSpinTimer();
        }

        private void StopAnimPlayback()
        {
            isAnimPlaying = false;
            animTickAccumulator = 0;
            if (btnPlayAnim != null) btnPlayAnim.Text = "Play";
            UpdateSpinTimer();
        }

        private void ClearLoadedAnimation()
        {
            currentSkeleton = null;
            loadedAnimationFrames = null;
            currentAnimFrame = 0;
            loadedAnmName = "<None>";
            UpdateAnimFrameLabel();
        }

        private void UpdateAnimFrameLabel()
        {
            if (lblAnimFrame == null) return;
            if (loadedAnimationFrames == null || loadedAnimationFrames.Length == 0)
                lblAnimFrame.Text = "Frame: -";
            else
                lblAnimFrame.Text = string.Format("Frame: {0}/{1}", currentAnimFrame, loadedAnimationFrames.Length - 1);
        }

        private void ApplyCurrentAnimFrame(bool recenter)
        {
            if (loadedAnimationFrames == null || loadedAnimationFrames.Length == 0) return;
            if (currentAnimFrame < 0 || currentAnimFrame >= loadedAnimationFrames.Length) return;

            Matrix4[] frame = loadedAnimationFrames[currentAnimFrame];
            currentSkeleton = new Matrix4[frame.Length];
            Array.Copy(frame, currentSkeleton, frame.Length);

            if (recenter) RecalculateAssembledCenter();
            UpdateAnimFrameLabel();
        }

        private static Matrix4 ReadAnmBoneMatrix(BinaryReader br)
        {
            float tx = br.ReadSingle();
            float ty = br.ReadSingle();
            float tz = br.ReadSingle();

            float m11 = br.ReadSingle(); float m12 = br.ReadSingle(); float m13 = br.ReadSingle();
            float m21 = br.ReadSingle(); float m22 = br.ReadSingle(); float m23 = br.ReadSingle();
            float m31 = br.ReadSingle(); float m32 = br.ReadSingle(); float m33 = br.ReadSingle();

            Matrix4 mat = new Matrix4(
                m11, m21, m31, 0,
                m12, m22, m32, 0,
                m13, m23, m33, 0,
                tx,  ty,  tz,  1
            );
            br.BaseStream.Seek(8, SeekOrigin.Current); // Skip 2 ints
            return mat;
        }

        private void ApplyAnimation(string filePath)
        {
            if (currentMeshes == null || !File.Exists(filePath)) return;

            StopAnimPlayback();

            try
            {
                using (BinaryReader br = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read)))
                {
                    br.BaseStream.Seek(0x18, SeekOrigin.Begin);
                    int numBones = br.ReadInt32();
                    int numFrames = br.ReadInt32();

                    if (numBones != currentMeshes.Count)
                    {
                        Logger.Log($"Warning: Bone mismatch in {Path.GetFileName(filePath)}. ANM has {numBones}, MDL has {currentMeshes.Count}.");
                        ClearLoadedAnimation();
                        return;
                    }

                    if (numFrames <= 0)
                    {
                        Logger.Log($"Warning: ANM {Path.GetFileName(filePath)} has no frames.");
                        ClearLoadedAnimation();
                        return;
                    }

                    br.BaseStream.Seek(0x2C, SeekOrigin.Begin);

                    loadedAnimationFrames = new Matrix4[numFrames][];
                    for (int f = 0; f < numFrames; f++)
                    {
                        loadedAnimationFrames[f] = new Matrix4[numBones];
                        for (int b = 0; b < numBones; b++)
                            loadedAnimationFrames[f][b] = ReadAnmBoneMatrix(br);
                    }

                    currentAnimFrame = 0;
                    loadedAnmName = Path.GetFileName(filePath);
                    ApplyCurrentAnimFrame(recenter: true);
                    DumpSkeletonDiagnostics(loadedAnmName);
                    if (glControl != null) glControl.Invalidate();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("MdlViewerTab", "Failed to load ANM file: " + ex.Message);
                StopAnimPlayback();
                ClearLoadedAnimation();
            }
        }

        /// <summary>
        /// TEMPORARY diagnostic: dumps raw vs skeleton-transformed AABB and matrix scale proxies.
        /// Does not alter viewer/export behavior. Remove after transform audit.
        /// </summary>
        private void DumpSkeletonDiagnostics(string anmFileName)
        {
            if (currentMeshes == null || currentSkeleton == null) return;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("======== QFG5 SKELETON DIAGNOSTIC DUMP ========");
            sb.AppendLine("ANM: " + anmFileName);
            sb.AppendLine("MDL meshes: " + currentMeshes.Count + " | Skeleton bones: " + currentSkeleton.Length);
            sb.AppendLine("Matrix convention: OpenTK Vector4.Transform(v, M) = row-vector * M");
            sb.AppendLine("  (same as RecalculateAssembledCenter). Unit axes map to ROWS of 3x3:");
            sb.AppendLine("  X->(M11,M12,M13)  Y->(M21,M22,M23)  Z->(M31,M32,M33)");
            sb.AppendLine("Translation: (M41, M42, M43)");
            sb.AppendLine();

            int count = Math.Min(currentMeshes.Count, currentSkeleton.Length);
            for (int i = 0; i < count; i++)
            {
                MeshData mesh = currentMeshes[i];
                Matrix4 mat = currentSkeleton[i];

                float rawMinX = float.MaxValue, rawMinY = float.MaxValue, rawMinZ = float.MaxValue;
                float rawMaxX = float.MinValue, rawMaxY = float.MinValue, rawMaxZ = float.MinValue;
                float xfMinX = float.MaxValue, xfMinY = float.MaxValue, xfMinZ = float.MaxValue;
                float xfMaxX = float.MinValue, xfMaxY = float.MinValue, xfMaxZ = float.MinValue;
                bool hasVerts = false;

                foreach (var v in mesh.Vertices)
                {
                    hasVerts = true;
                    if (v.X < rawMinX) rawMinX = v.X; if (v.X > rawMaxX) rawMaxX = v.X;
                    if (v.Y < rawMinY) rawMinY = v.Y; if (v.Y > rawMaxY) rawMaxY = v.Y;
                    if (v.Z < rawMinZ) rawMinZ = v.Z; if (v.Z > rawMaxZ) rawMaxZ = v.Z;

                    Vector4 tp = Vector4.Transform(new Vector4(v.X, v.Y, v.Z, 1.0f), mat);
                    if (tp.X < xfMinX) xfMinX = tp.X; if (tp.X > xfMaxX) xfMaxX = tp.X;
                    if (tp.Y < xfMinY) xfMinY = tp.Y; if (tp.Y > xfMaxY) xfMaxY = tp.Y;
                    if (tp.Z < xfMinZ) xfMinZ = tp.Z; if (tp.Z > xfMaxZ) xfMaxZ = tp.Z;
                }

                // Basis lengths = length of images of unit axes under OpenTK Transform (ROWS of 3x3)
                float scaleX = (float)Math.Sqrt(mat.M11 * mat.M11 + mat.M12 * mat.M12 + mat.M13 * mat.M13);
                float scaleY = (float)Math.Sqrt(mat.M21 * mat.M21 + mat.M22 * mat.M22 + mat.M23 * mat.M23);
                float scaleZ = (float)Math.Sqrt(mat.M31 * mat.M31 + mat.M32 * mat.M32 + mat.M33 * mat.M33);

                float det =
                    mat.M11 * (mat.M22 * mat.M33 - mat.M23 * mat.M32)
                  - mat.M12 * (mat.M21 * mat.M33 - mat.M23 * mat.M31)
                  + mat.M13 * (mat.M21 * mat.M32 - mat.M22 * mat.M31);

                sb.AppendLine("----------------------------------------");
                sb.AppendLine("MESH " + i);
                sb.AppendLine("Name: " + (mesh.Name ?? "<null>"));
                sb.AppendLine("Vertices: " + mesh.Vertices.Count);
                sb.AppendLine("Faces: " + mesh.Faces.Count);
                sb.AppendLine();

                if (!hasVerts)
                {
                    sb.AppendLine("RAW AABB: (no vertices)");
                    sb.AppendLine("TRANSFORMED AABB: (no vertices)");
                }
                else
                {
                    sb.AppendLine("RAW AABB:");
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "min: ({0}, {1}, {2})", rawMinX, rawMinY, rawMinZ));
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "max: ({0}, {1}, {2})", rawMaxX, rawMaxY, rawMaxZ));
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "extent: ({0}, {1}, {2})", rawMaxX - rawMinX, rawMaxY - rawMinY, rawMaxZ - rawMinZ));
                    sb.AppendLine();

                    sb.AppendLine("ANM FRAME 0 MATRIX:");
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "translation (M41,M42,M43): ({0}, {1}, {2})", mat.M41, mat.M42, mat.M43));
                    sb.AppendLine("3x3:");
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "  {0}  {1}  {2}", mat.M11, mat.M12, mat.M13));
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "  {0}  {1}  {2}", mat.M21, mat.M22, mat.M23));
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "  {0}  {1}  {2}", mat.M31, mat.M32, mat.M33));
                    sb.AppendLine();
                    sb.AppendLine("BASIS LENGTHS (scale proxies):");
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "X: {0}", scaleX));
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "Y: {0}", scaleY));
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "Z: {0}", scaleZ));
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "det: {0}", det));
                    sb.AppendLine();

                    sb.AppendLine("TRANSFORMED AABB (Skeleton[i] * vertex):");
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "min: ({0}, {1}, {2})", xfMinX, xfMinY, xfMinZ));
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "max: ({0}, {1}, {2})", xfMaxX, xfMaxY, xfMaxZ));
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "extent: ({0}, {1}, {2})", xfMaxX - xfMinX, xfMaxY - xfMinY, xfMaxZ - xfMinZ));
                }
                sb.AppendLine("----------------------------------------");
                sb.AppendLine();
            }

            string text = sb.ToString();
            Logger.Log(text);

            try
            {
                string dumpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skeleton_diagnostic.txt");
                File.WriteAllText(dumpPath, text);
                Logger.Log("Skeleton diagnostic also written to: " + dumpPath);
            }
            catch (Exception ex)
            {
                Logger.LogError("MdlViewer", "Failed to write skeleton_diagnostic.txt: " + ex.Message);
            }
        }

        private void BtnBuildIndex_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtMDLFolder.Text) || string.IsNullOrEmpty(txtANMFolder.Text))
            {
                MessageBox.Show("Please select both an MDL folder and an ANM folder before syncing the database.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                string[] mdls = Directory.GetFiles(txtMDLFolder.Text, "*.mdl");
                if (mdls.Length == 0) mdls = Directory.GetFiles(txtMDLFolder.Text, "*.MDL");

                int processed = 0;
                foreach (string mdl in mdls)
                {
                    string prefix = Path.GetFileNameWithoutExtension(mdl);
                    int p;
                    if (int.TryParse(prefix, out p))
                    {
                        int submeshCount = 0;
                        int totalVerts = 0;
                        string intName = viewerLogic.getName(mdl);
                        
                        using (BinaryReader br = new BinaryReader(File.Open(mdl, FileMode.Open, FileAccess.Read)))
                        {
                            if (br.BaseStream.Length > 0x1C) {
                                br.BaseStream.Seek(0x1C, SeekOrigin.Begin);
                                submeshCount = br.ReadInt32();
                                
                                if (submeshCount > 0 && submeshCount < 100) 
                                {
                                    br.BaseStream.Seek(0x42c, SeekOrigin.Begin);
                                    int[] addrs = new int[submeshCount];
                                    for (int i=0; i<submeshCount; i++) addrs[i] = br.ReadInt32();
                                    
                                    for (int i=0; i<submeshCount; i++) {
                                        if (addrs[i] > 0 && addrs[i] + 0x64 < br.BaseStream.Length) {
                                            br.BaseStream.Seek(addrs[i] + 0x60, SeekOrigin.Begin);
                                            totalVerts += br.ReadInt32();
                                        }
                                    }
                                }
                            }
                        }

                        string[] anms = Directory.GetFiles(txtANMFolder.Text, prefix + "*.anm");
                        if (anms.Length == 0) anms = Directory.GetFiles(txtANMFolder.Text, prefix + "*.ANM");

                        List<string> validAnms = new List<string>();
                        foreach (var anm in anms)
                        {
                            using (BinaryReader br = new BinaryReader(File.Open(anm, FileMode.Open, FileAccess.Read)))
                            {
                                if (br.BaseStream.Length > 0x1C) {
                                    br.BaseStream.Seek(0x18, SeekOrigin.Begin);
                                    if (br.ReadInt32() == submeshCount) validAnms.Add(Path.GetFileName(anm));
                                }
                            }
                        }

                        var entry = new ModelEntry {
                            Prefix = prefix,
                            Name = intName,
                            Submeshes = submeshCount,
                            Vertices = totalVerts,
                            CompatibleAnms = validAnms
                        };
                        _db.AddOrUpdate(entry);
                        processed++;
                    }
                }

                _db.SyncToCsv(Path.ChangeExtension(_dbPath, ".csv")); // Bonus: maintain a CSV too
                MessageBox.Show($"Database synced successfully! {processed} models updated in global JSON storage.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error syncing database: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RecalculateAssembledCenter()
        {
            if (currentMeshes == null || currentSkeleton == null) return;
            
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            bool hasVerts = false;

            int meshIndex = 0;
            foreach (var mesh in currentMeshes)
            {
                if (meshIndex < currentSkeleton.Length)
                {
                    Matrix4 mat = currentSkeleton[meshIndex];
                    foreach (var v in mesh.Vertices)
                    {
                        // Transform the point
                        Vector4 p = new Vector4(v.X, v.Y, v.Z, 1.0f);
                        Vector4 tp = Vector4.Transform(p, mat);
                        
                        if (tp.X < minX) minX = tp.X; if (tp.X > maxX) maxX = tp.X;
                        if (tp.Y < minY) minY = tp.Y; if (tp.Y > maxY) maxY = tp.Y;
                        if (tp.Z < minZ) minZ = tp.Z; if (tp.Z > maxZ) maxZ = tp.Z;
                        hasVerts = true;
                    }
                }
                meshIndex++;
            }

            if (hasVerts)
            {
                meshCenter = new QFG5Extractor.Resources.Model.Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, (minZ + maxZ) / 2f);
                float size = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
                meshScale = (size > 0) ? 2.0f / size : 1.0f;
            }
        }

        private void BtnEditTexture_Click(object sender, EventArgs e)
        {
            if (currentBitmaps == null || currentBitmaps.Count == 0) return;
            
            Bitmap currentBmp = null;
            if (cmbTextures.ComboBox.SelectedIndex > 0 && (cmbTextures.ComboBox.SelectedIndex - 1) < currentBitmaps.Count) {
                 currentBmp = currentBitmaps[cmbTextures.ComboBox.SelectedIndex - 1];
            } else if (currentBitmaps.Count > 0) {
                 currentBmp = currentBitmaps[0];
            }

            if (lstMDLFiles.SelectedItem is MdlListItem item) {
                string fullPath = Path.Combine(txtMDLFolder.Text, item.FileName);
                if (File.Exists(fullPath)) {
                    SharedState.RequestEditModel(fullPath);
                    SharedState.RequestSwitchTab(6); // 6 is the index of TextureEditorTab
                    return;
                }
            }

            if (currentBmp != null) {
                SharedState.RequestEditTexture((Bitmap)currentBmp.Clone());
                SharedState.RequestSwitchTab(6); 
            }
        }

        private class MdlListItem
        {
            public string FileName { get; set; }
            public string ModelName { get; set; }

            public override string ToString()
            {
                return $"{FileName} - [{ModelName}]";
            }
        }
        private void SaveScreenshot()
        {
            if (glControl == null) return;
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "PNG Image (*.png)|*.png", Title = "Save Viewport Screenshot" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    int w = glControl.Width;
                    int h = glControl.Height;
                    Bitmap bmp = new Bitmap(w, h);
                    System.Drawing.Imaging.BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    GL.ReadPixels(0, 0, w, h, OpenTK.Graphics.OpenGL.PixelFormat.Bgr, PixelType.UnsignedByte, data.Scan0);
                    bmp.UnlockBits(data);
                    bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
                    bmp.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
                    MessageBox.Show("Screenshot saved to: " + sfd.FileName, "Success");
                }
            }
        }

        private void ExportToGlb()
        {
            if (currentMeshes == null || currentMeshes.Count == 0) {
                MessageBox.Show("No model loaded to export.");
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "GLB files (*.glb)|*.glb", Title = "Export Model as Static GLB" })
            {
                if (sfd.ShowDialog() != DialogResult.OK) return;

                try {
                    List<GlbMesh> glbMeshes = new List<GlbMesh>();
                    int meshIdx = 0;
                    foreach (var mesh in currentMeshes)
                    {
                        NormalGenerator.EnsureNormals(mesh);
                        var gMesh = new GlbMesh {
                            Name = mesh.Name ?? $"Mesh_{meshIdx}",
                            TextureIndex = 0 // set after export bitmaps are known
                        };
                        
                        for (int f = 0; f < mesh.Faces.Count; f++)
                        {
                            int[] face = mesh.Faces[f];
                            int[] faceUV = (mesh.FaceUVs != null && f < mesh.FaceUVs.Count) ? mesh.FaceUVs[f] : new int[] { 0, 0, 0 };

                            for (int i = 0; i < 3; i++)
                            {
                                int pIdx = face[i];
                                int uIdx = faceUV[i];

                                var v = mesh.Vertices[pIdx];
                                gMesh.Positions.Add(new OpenTK.Vector3(v.X, v.Y, v.Z));

                                if (mesh.Normals != null && pIdx < mesh.Normals.Count)
                                {
                                    var n = mesh.Normals[pIdx];
                                    gMesh.Normals.Add(new OpenTK.Vector3(n.X, n.Y, n.Z));
                                }
                                else
                                {
                                    gMesh.Normals.Add(new OpenTK.Vector3(0f, 1f, 0f));
                                }

                                var uv = (mesh.UVs != null && uIdx < mesh.UVs.Count) ? mesh.UVs[uIdx] : new PointF(0,0);
                                gMesh.UVs.Add(new OpenTK.Vector2(uv.X, 1.0f - uv.Y));
                                
                                gMesh.Indices.Add((uint)gMesh.Indices.Count);
                            }
                        }
                        glbMeshes.Add(gMesh);
                        meshIdx++;
                    }

                    // Static GLB: 0 animations. Assemble with current ANM frame-0 matrices if loaded.
                    // Fresh texture copies (32bpp) avoid stale/cross-model GDI+ bitmaps.
                    Matrix4[] assemblyPose = null;
                    if (currentSkeleton != null && currentSkeleton.Length == glbMeshes.Count)
                        assemblyPose = currentSkeleton;

                    List<Bitmap> textureCopies = new List<Bitmap>();
                    try {
                        textureCopies = BuildExportBitmaps();
                        for (int i = 0; i < glbMeshes.Count; i++)
                            glbMeshes[i].TextureIndex = MajorityFaceTexture(currentMeshes[i], textureCopies.Count);

                        GlbExporter.Export(sfd.FileName, glbMeshes, textureCopies, new List<GlbAnimation>(), assemblyPose);
                    } finally {
                        foreach (var bmp in textureCopies) {
                            try { bmp.Dispose(); } catch { }
                        }
                    }
                    string poseNote = assemblyPose != null
                        ? "assembled with ANM frame-0 matrices, 0 animations"
                        : "identity nodes (select an ANM first for assembled pose), 0 animations";
                    MessageBox.Show($"Exported {glbMeshes.Count} meshes ({poseNote}) to {Path.GetFileName(sfd.FileName)}", "Export Success");
                } catch (Exception ex) {
                    MessageBox.Show("Export failed: " + ex.Message);
                    Logger.LogError("MdlViewer", "GLB Export error: " + ex.Message + "\n" + ex.StackTrace);
                }
            }
        }

        private static int MajorityFaceTexture(MeshData mesh, int textureCount)
        {
            if (mesh.FaceTextures == null || mesh.FaceTextures.Count == 0 || textureCount <= 0)
                return 0;

            Dictionary<int, int> counts = new Dictionary<int, int>();
            foreach (int texIdx in mesh.FaceTextures)
            {
                int clamped = texIdx;
                if (clamped < 0) clamped = 0;
                if (clamped >= textureCount) clamped = textureCount - 1;
                if (!counts.ContainsKey(clamped)) counts[clamped] = 0;
                counts[clamped]++;
            }

            int best = 0, bestCount = -1;
            foreach (var kv in counts)
            {
                if (kv.Value > bestCount) {
                    best = kv.Key;
                    bestCount = kv.Value;
                }
            }
            return best;
        }

        /// <summary>
        /// Fresh bitmaps for GLB embed: re-read embedded MDL textures when possible,
        /// otherwise clone currentBitmaps. Always returns caller-owned 32bpp copies.
        /// </summary>
        private List<Bitmap> BuildExportBitmaps()
        {
            List<Bitmap> result = new List<Bitmap>();

            try
            {
                if (cmbTextures != null && cmbTextures.ComboBox.SelectedIndex <= 0
                    && lstMDLFiles.SelectedItem is MdlListItem item
                    && !string.IsNullOrEmpty(txtMDLFolder.Text))
                {
                    string fullPath = Path.Combine(txtMDLFolder.Text, item.FileName);
                    if (File.Exists(fullPath))
                    {
                        List<Bitmap> fresh = viewerLogic.getAllBMPs(fullPath);
                        foreach (var bmp in fresh)
                        {
                            result.Add(ToExportBitmap(bmp));
                            try { bmp.Dispose(); } catch { }
                        }
                        if (result.Count > 0) return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("BuildExportBitmaps: fresh MDL extract failed, falling back to currentBitmaps: " + ex.Message);
                foreach (var bmp in result) { try { bmp.Dispose(); } catch { } }
                result.Clear();
            }

            foreach (var bmp in currentBitmaps)
            {
                if (bmp != null) result.Add(ToExportBitmap(bmp));
            }
            return result;
        }

        private static Bitmap ToExportBitmap(Bitmap src)
        {
            Bitmap copy = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(copy))
            {
                g.DrawImage(src, 0, 0, src.Width, src.Height);
            }
            return copy;
        }
    }
}
