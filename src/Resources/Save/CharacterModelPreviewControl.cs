using QFG5Extractor.Core;
using QFG5Extractor.Core.UI;
using QFG5Extractor.Resources.Model;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using OpenTKVector3 = OpenTK.Vector3;

namespace QFG5Extractor.Resources.Save
{
    public sealed class CharacterModelPreviewControl : UserControl
    {
        private readonly GLControl glControl;
        private readonly Label statusLabel;
        private readonly Timer spinTimer;
        private readonly Label titleLabel;
        private readonly QFG5Button playButton;
        private readonly QFG5Button spinButton;
        private readonly QFG5Button zoomOutButton;
        private readonly QFG5Button zoomInButton;
        private readonly ToolTip previewToolTip;
        private readonly string previewTitle;
        private readonly OldMdlViewerLogic viewerLogic = new OldMdlViewerLogic();
        private List<MeshData> meshes = new List<MeshData>();
        private Matrix4[][] animationFrames = new Matrix4[0][];
        private readonly List<Bitmap> bitmaps = new List<Bitmap>();
        private readonly List<int> textureIds = new List<int>();
        private QFG5Extractor.Resources.Model.Vector3 meshCenter;
        private float meshScale = 1.0f;
        private float rotation;
        private float dragRotationX;
        private float zoom = 1.0f;
        private Point lastDragPosition;
        private int animationFrame;
        private int animationTickAccumulator;
        private int highlightedTrackIndex = -1;
        private bool animationPlaying;
        private bool spinning;
        private bool dragging;
        private bool glInitialized;
        private bool hasModel;
        private bool disposed;
        private int modelId = -1;
        private string statusMessage = "Choose a character model to preview.";

        public CharacterModelPreviewControl()
            : this("MODEL PREVIEW", false)
        {
        }

        public CharacterModelPreviewControl(string title, bool showPlaybackButton)
        {
            BackColor = QFG5Theme.ColorSurface;
            previewTitle = title;
            spinning = !showPlaybackButton;
            previewToolTip = new ToolTip();

            TableLayoutPanel layout = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = showPlaybackButton ? 3 : 2,
                BackColor = QFG5Theme.ColorSurface,
                Padding = new Padding(6),
                Margin = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            if (showPlaybackButton)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            titleLabel = new Label {
                Text = title,
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.SectionHeaderFont,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = QFG5Theme.ColorSurface,
                Margin = new Padding(0)
            };
            layout.Controls.Add(titleLabel, 0, 0);

            if (showPlaybackButton)
            {
                FlowLayoutPanel controls = new FlowLayoutPanel {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    BackColor = QFG5Theme.ColorSurface,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                zoomInButton = CreatePreviewButton("+", 22);
                zoomOutButton = CreatePreviewButton("-", 22);
                playButton = CreatePreviewButton("Play", 44);
                spinButton = CreatePreviewButton("Spin Off", 56);
                spinButton.Click += (s, e) => SetSpinning(!spinning);
                playButton.Enabled = false;
                playButton.Click += (s, e) => SetAnimationPlayback(!animationPlaying);
                zoomOutButton.Click += (s, e) => SetZoom(zoom - 0.1f);
                zoomInButton.Click += (s, e) => SetZoom(zoom + 0.1f);

                controls.Controls.Add(zoomInButton);
                controls.Controls.Add(zoomOutButton);
                controls.Controls.Add(playButton);
                controls.Controls.Add(spinButton);
                previewToolTip.SetToolTip(spinButton, "Toggle model rotation");
                previewToolTip.SetToolTip(zoomOutButton, "Zoom out");
                previewToolTip.SetToolTip(zoomInButton, "Zoom in");
                layout.Controls.Add(controls, 0, 1);
            }

            Panel frame = new Panel {
                Dock = DockStyle.Fill,
                Padding = new Padding(4),
                BackColor = QFG5Theme.ColorBorderOuter
            };
            Panel viewport = new Panel {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Padding = new Padding(1)
            };
            glControl = new GLControl(new GraphicsMode(32, 24, 0, 8)) {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            glControl.Load += GlControl_Load;
            glControl.Paint += GlControl_Paint;
            glControl.Resize += (s, e) => InvalidatePreview();
            if (showPlaybackButton)
            {
                glControl.MouseWheel += GlControl_MouseWheel;
                glControl.MouseEnter += (s, e) => glControl.Focus();
                glControl.MouseDown += GlControl_MouseDown;
                glControl.MouseMove += GlControl_MouseMove;
                glControl.MouseUp += GlControl_MouseUp;
                previewToolTip.SetToolTip(glControl, "Click and drag to rotate; use the mouse wheel to zoom");
            }

            statusLabel = new Label {
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextMuted,
                Font = QFG5Theme.CaptionFont,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Black,
                Padding = new Padding(6),
                AutoEllipsis = true
            };

            viewport.Controls.Add(glControl);
            viewport.Controls.Add(statusLabel);
            frame.Controls.Add(viewport);
            layout.Controls.Add(frame, 0, showPlaybackButton ? 2 : 1);
            Controls.Add(layout);

            spinTimer = new Timer { Interval = 33 };
            spinTimer.Tick += (s, e) => {
                if (spinning)
                    rotation = (rotation + 1.2f) % 360.0f;
                if (animationPlaying && animationFrames.Length > 1)
                {
                    animationTickAccumulator++;
                    if (animationTickAccumulator >= 3)
                    {
                        animationTickAccumulator = 0;
                        animationFrame = (animationFrame + 1) % animationFrames.Length;
                    }
                }
                InvalidatePreview();
            };
            VisibleChanged += (s, e) => {
                if (Visible && modelId >= 0 && !hasModel)
                    LoadSelectedModel();
                UpdateSpinTimer();
            };
            ParentChanged += (s, e) => UpdateSpinTimer();
            Disposed += (s, e) => DisposePreviewResources();
            UpdateStatusLabel();
        }

        public void SetModelId(int selectedModelId)
        {
            if (modelId == selectedModelId && hasModel)
                return;

            modelId = selectedModelId;
            LoadSelectedModel();
        }

        public void SetPreviewFiles(string modelPath, string animationPath)
        {
            modelId = -1;
            LoadModelFiles(modelPath, animationPath);
        }

        public void SetAnimationFrames(Matrix4[][] frames)
        {
            if (!hasModel)
                throw new InvalidOperationException("Load a model before updating its preview animation.");
            if (frames == null || frames.Length == 0)
                throw new ArgumentException("At least one animation frame is required.", "frames");
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null || frames[i].Length != meshes.Count)
                    throw new ArgumentException("Preview frame track counts must match the loaded model.", "frames");
            }

            animationFrames = frames;
            animationFrame = 0;
            animationTickAccumulator = 0;
            CalculateAnimatedModelBounds();
            UpdatePlaybackButton();
            UpdateStatusLabel();
            UpdateSpinTimer();
            InvalidatePreview();
        }

        public void SetHighlightedTrack(int trackIndex)
        {
            if (trackIndex < -1)
                throw new ArgumentOutOfRangeException("trackIndex");
            if (hasModel && trackIndex >= meshes.Count)
                throw new ArgumentOutOfRangeException("trackIndex");

            highlightedTrackIndex = trackIndex;
            InvalidatePreview();
        }

        public void ClearModel(string message)
        {
            modelId = -1;
            ClearModelResources();
            statusMessage = string.IsNullOrWhiteSpace(message)
                ? "No character model is selected."
                : message;
            UpdateStatusLabel();
            InvalidatePreview();
        }

        private void LoadSelectedModel()
        {
            string modelPath = FindModelPath(SharedState.GetGameDataPath("hdnw", "mdl"), modelId);
            if (modelPath == null)
                modelPath = FindModelPath(SharedState.GetModsPath("hdnw", "mdl"), modelId);
            if (modelPath == null)
            {
                ClearModelResources();
                statusMessage = "Model " + modelId.ToString("D3") +
                    ".MDL was not found in the configured hdnw/mdl folder.";
                UpdateStatusLabel();
                InvalidatePreview();
                return;
            }

            string animationPath;
            try
            {
                List<MeshData> modelParts = MeshLoader.LoadMesh(modelPath);
                if (modelParts == null || modelParts.Count == 0)
                    throw new InvalidDataException("The model file contains no readable mesh data.");
                Matrix4[][] frames = LoadCharacterAnimation(modelId, modelParts.Count, out animationPath);
                LoadModelFiles(modelPath, animationPath, modelParts, frames);
            }
            catch (Exception ex)
            {
                ClearModelResources();
                statusMessage = "Could not preview model " + modelId + ": " + ex.Message;
                Logger.LogError("CharacterModelPreview", statusMessage);
                UpdateStatusLabel();
                InvalidatePreview();
            }
        }

        private void LoadModelFiles(string modelPath, string animationPath)
        {
            ClearModelResources();
            statusMessage = "Loading model preview...";
            UpdateStatusLabel();

            try
            {
                if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
                    throw new FileNotFoundException("Select an existing model file.", modelPath);
                List<MeshData> loadedMeshes = MeshLoader.LoadMesh(modelPath);
                if (loadedMeshes == null || loadedMeshes.Count == 0)
                    throw new InvalidDataException("The model file contains no readable mesh data.");
                Matrix4[][] loadedFrames = string.IsNullOrWhiteSpace(animationPath)
                    ? CreateIdentityFrame(loadedMeshes.Count)
                    : AnmAnimationLoader.LoadFrames(animationPath, loadedMeshes.Count);
                LoadModelFiles(modelPath, animationPath, loadedMeshes, loadedFrames);
            }
            catch (Exception ex)
            {
                ClearModelResources();
                statusMessage = "Could not preview " + Path.GetFileName(modelPath) + ": " + ex.Message;
                Logger.LogError("CharacterModelPreview", statusMessage);
                UpdateStatusLabel();
                InvalidatePreview();
            }
        }

        private void LoadModelFiles(string modelPath, string animationPath, List<MeshData> loadedMeshes, Matrix4[][] loadedFrames)
        {
            ClearModelResources();
            try
            {
                meshes = loadedMeshes;
                bitmaps.AddRange(viewerLogic.getAllBMPs(modelPath));
                animationFrames = loadedFrames;
                animationFrame = 0;
                CalculateAnimatedModelBounds();

                if (glInitialized)
                    UploadTextures();

                hasModel = true;
                statusMessage = Path.GetFileName(modelPath) +
                    (string.IsNullOrWhiteSpace(animationPath) ? " / identity pose" : " / " + Path.GetFileName(animationPath));
                UpdatePlaybackButton();
                UpdateStatusLabel();
                UpdateSpinTimer();
                InvalidatePreview();
            }
            catch (Exception ex)
            {
                ClearModelResources();
                statusMessage = "Could not preview " + Path.GetFileName(modelPath) + ": " + ex.Message;
                Logger.LogError("CharacterModelPreview", statusMessage);
                UpdateStatusLabel();
                InvalidatePreview();
            }
        }

        private static Matrix4[][] CreateIdentityFrame(int trackCount)
        {
            Matrix4[] frame = new Matrix4[trackCount];
            for (int i = 0; i < frame.Length; i++)
                frame[i] = Matrix4.Identity;
            return new[] { frame };
        }

        private void SetAnimationPlayback(bool enabled)
        {
            if (animationFrames.Length <= 1)
                enabled = false;
            animationPlaying = enabled;
            animationTickAccumulator = 0;
            UpdatePlaybackButton();
            UpdateSpinTimer();
        }

        private void UpdatePlaybackButton()
        {
            if (playButton == null || playButton.IsDisposed) return;
            playButton.Enabled = hasModel && animationFrames.Length > 1;
            playButton.Text = animationPlaying ? "Pause" : "Play";
            if (!playButton.Enabled)
                animationPlaying = false;
        }

        private static string FindModelPath(string directory, int selectedModelId)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return null;

            string expectedName = selectedModelId.ToString("D3") + ".MDL";
            string directPath = Path.Combine(directory, expectedName);
            if (File.Exists(directPath))
                return directPath;

            return Directory.GetFiles(directory)
                .FirstOrDefault(path => string.Equals(Path.GetFileName(path), expectedName,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static Matrix4[][] LoadCharacterAnimation(int selectedModelId, int expectedBoneCount,
            out string animationPath)
        {
            string prefix = selectedModelId.ToString("D3");
            string[] directories = {
                SharedState.GetGameDataPath("hdnw", "anm"),
                SharedState.GetModsPath("hdnw", "anm")
            };
            string lastRejectedAnimation = null;

            foreach (string directory in directories)
            {
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                    continue;

                string[] candidates = Directory.GetFiles(directory)
                    .Where(path => Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(Path.GetExtension(path), ".anm", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                foreach (string candidate in candidates)
                {
                    try
                    {
                        Matrix4[][] frames = AnmAnimationLoader.LoadFrames(candidate, expectedBoneCount);
                        animationPath = candidate;
                        return frames;
                    }
                    catch (InvalidDataException ex)
                    {
                        lastRejectedAnimation = Path.GetFileName(candidate) + ": " + ex.Message;
                    }
                }
            }

            animationPath = null;
            string detail = lastRejectedAnimation == null
                ? "No ANM files were found for model " + prefix + "."
                : "No ANM file matched the model's " + expectedBoneCount +
                    " submeshes. Last candidate: " + lastRejectedAnimation;
            throw new InvalidDataException(detail);
        }

        private void CalculateAnimatedModelBounds()
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float minZ = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float maxZ = float.MinValue;
            bool hasVertices = false;

            Matrix4[] frame = animationFrames[animationFrame];
            for (int meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
            {
                MeshData mesh = meshes[meshIndex];
                Matrix4 bone = frame[meshIndex];
                foreach (QFG5Extractor.Resources.Model.Vector3 vertex in mesh.Vertices)
                {
                    Vector4 transformed = Vector4.Transform(
                        new Vector4(vertex.X, vertex.Y, vertex.Z, 1.0f), bone);
                    minX = Math.Min(minX, transformed.X);
                    minY = Math.Min(minY, transformed.Y);
                    minZ = Math.Min(minZ, transformed.Z);
                    maxX = Math.Max(maxX, transformed.X);
                    maxY = Math.Max(maxY, transformed.Y);
                    maxZ = Math.Max(maxZ, transformed.Z);
                    hasVertices = true;
                }
            }

            if (!hasVertices)
                throw new InvalidDataException("The model has no vertices to preview.");

            meshCenter = new QFG5Extractor.Resources.Model.Vector3(
                (minX + maxX) / 2.0f,
                (minY + maxY) / 2.0f,
                (minZ + maxZ) / 2.0f);
            float size = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
            meshScale = size > 0.0001f ? 2.4f / size : 1.0f;
        }

        private void GlControl_Load(object sender, EventArgs e)
        {
            try
            {
                glControl.MakeCurrent();
                GL.ClearColor(Color.Black);
                GL.Enable(EnableCap.DepthTest);
                GL.Enable(EnableCap.Texture2D);
                GL.Enable(EnableCap.Lighting);
                GL.Enable(EnableCap.Light0);
                GL.Enable(EnableCap.ColorMaterial);
                GL.ColorMaterial(MaterialFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);
                GL.Light(LightName.Light0, LightParameter.Position, new float[] { 0.4f, 1.0f, 0.8f, 0.0f });
                GL.Light(LightName.Light0, LightParameter.Diffuse, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                GL.Light(LightName.Light0, LightParameter.Ambient, new float[] { 0.35f, 0.35f, 0.35f, 1.0f });
                GL.ShadeModel(ShadingModel.Smooth);
                glInitialized = true;
                if (hasModel)
                    UploadTextures();
                UpdateSpinTimer();
                InvalidatePreview();
            }
            catch (Exception ex)
            {
                glInitialized = false;
                spinTimer.Stop();
                statusMessage = "3D preview could not initialize: " + ex.Message;
                Logger.LogError("CharacterModelPreview", statusMessage);
                UpdateStatusLabel();
                statusLabel.Visible = true;
            }
        }

        private void UploadTextures()
        {
            if (!glInitialized || !glControl.Context.IsCurrent)
                glControl.MakeCurrent();

            DeleteTextures();
            foreach (Bitmap source in bitmaps)
            {
                using (Bitmap bitmap = new Bitmap(source.Width, source.Height, DrawingPixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.DrawImage(source, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    BitmapData data = bitmap.LockBits(
                        new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                        ImageLockMode.ReadOnly,
                        DrawingPixelFormat.Format32bppArgb);
                    try
                    {
                        int textureId = GL.GenTexture();
                        GL.BindTexture(TextureTarget.Texture2D, textureId);
                        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                            data.Width, data.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                            PixelType.UnsignedByte, data.Scan0);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                            (int)TextureMinFilter.Linear);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                            (int)TextureMagFilter.Linear);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                            (int)TextureWrapMode.ClampToEdge);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                            (int)TextureWrapMode.ClampToEdge);
                        textureIds.Add(textureId);
                    }
                    finally
                    {
                        bitmap.UnlockBits(data);
                    }
                }
            }
        }

        private void GlControl_Paint(object sender, PaintEventArgs e)
        {
            if (!glInitialized || disposed)
                return;

            try
            {
                if (!glControl.Context.IsCurrent)
                    glControl.MakeCurrent();

                int width = Math.Max(1, glControl.ClientSize.Width);
                int height = Math.Max(1, glControl.ClientSize.Height);
                GL.Viewport(0, 0, width, height);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                if (hasModel)
                {
                    Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(
                        MathHelper.PiOver4, width / (float)height, 0.1f, 100.0f);
                    GL.MatrixMode(MatrixMode.Projection);
                    GL.LoadMatrix(ref projection);
                    GL.MatrixMode(MatrixMode.Modelview);
                    GL.LoadIdentity();
                    Matrix4 camera = Matrix4.LookAt(OpenTKVector3.Zero, -OpenTKVector3.UnitZ, OpenTKVector3.UnitY);
                    GL.LoadMatrix(ref camera);
                    GL.Translate(0.0f, 0.0f, -5.5f / zoom);
                    GL.Rotate(rotation, 0.0f, 1.0f, 0.0f);
                    GL.Rotate(dragRotationX, 1.0f, 0.0f, 0.0f);
                    GL.Rotate(270.0f, 1.0f, 0.0f, 0.0f);
                    DrawMeshes();
                }
                glControl.SwapBuffers();
                if (hasModel)
                    statusLabel.Visible = false;
            }
            catch (Exception ex)
            {
                spinTimer.Stop();
                statusMessage = "3D preview rendering failed: " + ex.Message;
                Logger.LogError("CharacterModelPreview", statusMessage);
                UpdateStatusLabel();
                statusLabel.Visible = true;
            }
        }

        private void DrawMeshes()
        {
            Matrix4[] frame = animationFrames[animationFrame];
            for (int meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
            {
                MeshData mesh = meshes[meshIndex];
                GL.PushMatrix();
                GL.Scale(meshScale, meshScale, meshScale);
                GL.Translate(-meshCenter.X, -meshCenter.Y, -meshCenter.Z);
                Matrix4 bone = frame[meshIndex];
                GL.MultMatrix(ref bone);

                for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
                {
                    int textureIndex = faceIndex < mesh.FaceTextures.Count
                        ? mesh.FaceTextures[faceIndex]
                        : 0;
                    if (textureIndex >= 0 && textureIndex < textureIds.Count)
                    {
                        GL.Enable(EnableCap.Texture2D);
                        GL.BindTexture(TextureTarget.Texture2D, textureIds[textureIndex]);
                        GL.Color3(Color.White);
                    }
                    else
                    {
                        GL.Disable(EnableCap.Texture2D);
                        GL.Color3(Color.FromArgb(190, 180, 150));
                    }

                    int[] face = mesh.Faces[faceIndex];
                    int[] faceUvs = faceIndex < mesh.FaceUVs.Count ? mesh.FaceUVs[faceIndex] : null;
                    GL.Begin(PrimitiveType.Triangles);
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int vertexIndex = face[corner];
                        if (vertexIndex < 0 || vertexIndex >= mesh.Vertices.Count)
                            continue;

                        if (vertexIndex < mesh.Normals.Count)
                        {
                            QFG5Extractor.Resources.Model.Vector3 normal = mesh.Normals[vertexIndex];
                            GL.Normal3(normal.X, normal.Y, normal.Z);
                        }

                        if (faceUvs != null && corner < faceUvs.Length &&
                            faceUvs[corner] >= 0 && faceUvs[corner] < mesh.UVs.Count)
                        {
                            PointF uv = mesh.UVs[faceUvs[corner]];
                            GL.TexCoord2(uv.X, 1.0f - uv.Y);
                        }

                        QFG5Extractor.Resources.Model.Vector3 vertex = mesh.Vertices[vertexIndex];
                        GL.Vertex3(vertex.X, vertex.Y, vertex.Z);
                    }
                    GL.End();
                }

                if (meshIndex == highlightedTrackIndex)
                    DrawMeshHighlight(mesh);
                GL.PopMatrix();
            }
        }

        private static void DrawMeshHighlight(MeshData mesh)
        {
            GL.Disable(EnableCap.Texture2D);
            GL.Disable(EnableCap.Lighting);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.LineWidth(2.0f);
            GL.Color3(Color.FromArgb(255, 255, 70, 30));
            GL.Begin(PrimitiveType.Lines);
            foreach (int[] face in mesh.Faces)
            {
                if (face == null || face.Length < 3)
                    continue;

                for (int edge = 0; edge < 3; edge++)
                {
                    int firstIndex = face[edge];
                    int secondIndex = face[(edge + 1) % 3];
                    if (firstIndex < 0 || firstIndex >= mesh.Vertices.Count ||
                        secondIndex < 0 || secondIndex >= mesh.Vertices.Count)
                        continue;

                    QFG5Extractor.Resources.Model.Vector3 first = mesh.Vertices[firstIndex];
                    QFG5Extractor.Resources.Model.Vector3 second = mesh.Vertices[secondIndex];
                    GL.Vertex3(first.X, first.Y, first.Z);
                    GL.Vertex3(second.X, second.Y, second.Z);
                }
            }
            GL.End();
            GL.LineWidth(1.0f);
            GL.DepthFunc(DepthFunction.Less);
            GL.Enable(EnableCap.Lighting);
        }

        private void ClearModelResources()
        {
            hasModel = false;
            highlightedTrackIndex = -1;
            animationPlaying = false;
            animationTickAccumulator = 0;
            spinTimer.Enabled = false;
            UpdatePlaybackButton();
            if (glInitialized && !glControl.IsDisposed && glControl.IsHandleCreated)
            {
                try
                {
                    if (!glControl.Context.IsCurrent)
                        glControl.MakeCurrent();
                    DeleteTextures();
                }
                catch (Exception ex)
                {
                    Logger.LogError("CharacterModelPreview", "Could not release previous model textures: " + ex.Message);
                    textureIds.Clear();
                }
            }
            else
            {
                textureIds.Clear();
            }

            foreach (Bitmap bitmap in bitmaps)
                bitmap.Dispose();
            bitmaps.Clear();
            meshes.Clear();
            animationFrames = new Matrix4[0][];
            animationFrame = 0;
        }

        private void DeleteTextures()
        {
            foreach (int textureId in textureIds)
                GL.DeleteTexture(textureId);
            textureIds.Clear();
        }

        private void DisposePreviewResources()
        {
            if (disposed) return;
            disposed = true;
            spinTimer.Stop();
            ClearModelResources();
            spinTimer.Dispose();
            previewToolTip.Dispose();
        }

        private void UpdateStatusLabel()
        {
            if (statusLabel == null) return;
            statusLabel.Text = statusMessage;
            statusLabel.Visible = !hasModel;
        }

        private void UpdateSpinTimer()
        {
            spinTimer.Enabled = !disposed && hasModel && glInitialized && Visible &&
                (spinning || animationPlaying) &&
                glControl != null && glControl.IsHandleCreated;
        }

        private static QFG5Button CreatePreviewButton(string text, int width)
        {
            return new QFG5Button {
                Text = text,
                Width = width,
                Height = 26,
                Margin = new Padding(1)
            };
        }

        private void SetSpinning(bool enabled)
        {
            spinning = enabled;
            if (spinButton != null)
                spinButton.Text = spinning ? "Spin On" : "Spin Off";
            UpdateSpinTimer();
            InvalidatePreview();
        }

        private void SetZoom(float value)
        {
            zoom = Math.Max(0.5f, Math.Min(3.0f, value));
            titleLabel.Text = previewTitle + "  " + Math.Round(zoom * 100.0f).ToString("0") + "%";
            InvalidatePreview();
        }

        private void GlControl_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta != 0)
                SetZoom(zoom + Math.Sign(e.Delta) * 0.1f);
        }

        private void GlControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            dragging = true;
            lastDragPosition = e.Location;
            glControl.Cursor = Cursors.SizeAll;
            glControl.Capture = true;
        }

        private void GlControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging)
                return;

            int deltaX = e.X - lastDragPosition.X;
            int deltaY = e.Y - lastDragPosition.Y;
            lastDragPosition = e.Location;
            rotation = (rotation + deltaX * 0.7f) % 360.0f;
            dragRotationX = Math.Max(-85.0f, Math.Min(85.0f, dragRotationX + deltaY * 0.7f));
            InvalidatePreview();
        }

        private void GlControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !dragging)
                return;

            dragging = false;
            glControl.Capture = false;
            glControl.Cursor = Cursors.Default;
        }

        private void InvalidatePreview()
        {
            if (glControl != null && glControl.IsHandleCreated && !glControl.IsDisposed)
                glControl.Invalidate();
        }
    }
}
