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
        private readonly OldMdlViewerLogic viewerLogic = new OldMdlViewerLogic();
        private List<MeshData> meshes = new List<MeshData>();
        private Matrix4[][] animationFrames = new Matrix4[0][];
        private readonly List<Bitmap> bitmaps = new List<Bitmap>();
        private readonly List<int> textureIds = new List<int>();
        private QFG5Extractor.Resources.Model.Vector3 meshCenter;
        private float meshScale = 1.0f;
        private float rotation;
        private int animationFrame;
        private bool glInitialized;
        private bool hasModel;
        private bool disposed;
        private int modelId = -1;
        private string statusMessage = "Choose a character model to preview.";

        public CharacterModelPreviewControl()
        {
            BackColor = QFG5Theme.ColorSurface;

            TableLayoutPanel layout = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = QFG5Theme.ColorSurface,
                Padding = new Padding(6),
                Margin = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Label titleLabel = new Label {
                Text = "MODEL PREVIEW",
                Dock = DockStyle.Fill,
                ForeColor = QFG5Theme.ColorTextGold,
                Font = QFG5Theme.SectionHeaderFont,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = QFG5Theme.ColorSurface
            };

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
            layout.Controls.Add(titleLabel, 0, 0);
            layout.Controls.Add(frame, 0, 1);
            Controls.Add(layout);

            spinTimer = new Timer { Interval = 33 };
            spinTimer.Tick += (s, e) => {
                rotation = (rotation + 1.2f) % 360.0f;
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
            ClearModelResources();
            statusMessage = "Loading model " + modelId + "...";
            UpdateStatusLabel();

            try
            {
                string modelPath = FindModelPath(SharedState.GetGameDataPath("hdnw", "mdl"), modelId);
                if (modelPath == null)
                    modelPath = FindModelPath(SharedState.GetModsPath("hdnw", "mdl"), modelId);
                if (modelPath == null)
                {
                    statusMessage = "Model " + modelId.ToString("D3") +
                        ".MDL was not found in the configured hdnw/mdl folder.";
                    UpdateStatusLabel();
                    InvalidatePreview();
                    return;
                }

                List<MeshData> loadedMeshes = MeshLoader.LoadMesh(modelPath);
                if (loadedMeshes == null || loadedMeshes.Count == 0)
                    throw new InvalidDataException("The model file contains no readable mesh data.");

                List<Bitmap> loadedBitmaps = viewerLogic.getAllBMPs(modelPath);
                meshes = loadedMeshes;
                bitmaps.AddRange(loadedBitmaps);
                string animationPath;
                animationFrames = LoadCharacterAnimation(modelId, meshes.Count, out animationPath);
                animationFrame = 0;
                CalculateAnimatedModelBounds();

                if (glInitialized)
                    UploadTextures();

                hasModel = true;
                statusMessage = Path.GetFileName(modelPath) + " / " + Path.GetFileName(animationPath);
                UpdateStatusLabel();
                UpdateSpinTimer();
                InvalidatePreview();
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
            meshScale = size > 0.0001f ? 2.0f / size : 1.0f;
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
                    GL.Translate(0.0f, 0.0f, -5.5f);
                    GL.Rotate(rotation, 0.0f, 1.0f, 0.0f);
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
                GL.PopMatrix();
            }
        }

        private void ClearModelResources()
        {
            hasModel = false;
            spinTimer.Enabled = false;
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
                glControl != null && glControl.IsHandleCreated;
        }

        private void InvalidatePreview()
        {
            if (glControl != null && glControl.IsHandleCreated && !glControl.IsDisposed)
                glControl.Invalidate();
        }
    }
}
