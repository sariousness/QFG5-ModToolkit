using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenTK;
using System.Drawing;
using System.Drawing.Imaging;

namespace QFG5Extractor.Viewers.Model
{
    public class GlbMesh
    {
        public List<OpenTK.Vector3> Positions { get; set; } = new List<OpenTK.Vector3>();
        public List<OpenTK.Vector3> Normals { get; set; } = new List<OpenTK.Vector3>();
        public List<OpenTK.Vector2> UVs { get; set; } = new List<OpenTK.Vector2>();
        public List<uint> Indices { get; set; } = new List<uint>();
        public int TextureIndex { get; set; } = 0;
        public string Name { get; set; }
    }

    public class GlbAnimation
    {
        public string Name { get; set; }
        public float[] Timestamps { get; set; }
        public OpenTK.Matrix4[][] Keyframes { get; set; } // Frame Index -> Bone Index -> Matrix
    }

    public class GlbExporter
    {
        public static void Export(string path, List<GlbMesh> meshes, List<Bitmap> textures, List<GlbAnimation> animations)
        {
            Export(path, meshes, textures, animations, null);
        }

        public static void Export(string path, List<GlbMesh> meshes, List<Bitmap> textures, List<GlbAnimation> animations, Matrix4[] bindPose)
        {
            if (textures == null) textures = new List<Bitmap>();
            if (animations == null) animations = new List<GlbAnimation>();

            using (FileStream fs = new FileStream(path, FileMode.Create))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                MemoryStream msBuffer = new MemoryStream();
                BinaryWriter bwBuffer = new BinaryWriter(msBuffer);

                List<int> meshByteOffsets = new List<int>();
                List<int> meshIndexCounts = new List<int>();
                List<int> meshVertexCounts = new List<int>();

                // 1. Write Geometry Data
                foreach (var mesh in meshes)
                {
                    // Indices
                    int idxStart = (int)msBuffer.Position;
                    foreach (var idx in mesh.Indices) bwBuffer.Write(idx);
                    while (msBuffer.Position % 4 != 0) bwBuffer.Write((byte)0);

                    // Positions
                    int posStart = (int)msBuffer.Position;
                    foreach (var v in mesh.Positions) { bwBuffer.Write(v.X); bwBuffer.Write(v.Y); bwBuffer.Write(v.Z); }
                    while (msBuffer.Position % 4 != 0) bwBuffer.Write((byte)0);

                    // Normals
                    int normStart = (int)msBuffer.Position;
                    foreach (var v in mesh.Normals) { bwBuffer.Write(v.X); bwBuffer.Write(v.Y); bwBuffer.Write(v.Z); }
                    while (msBuffer.Position % 4 != 0) bwBuffer.Write((byte)0);

                    // UVs
                    int uvStart = (int)msBuffer.Position;
                    foreach (var v in mesh.UVs) { bwBuffer.Write(v.X); bwBuffer.Write(v.Y); }
                    while (msBuffer.Position % 4 != 0) bwBuffer.Write((byte)0);

                    meshByteOffsets.Add(idxStart);
                    meshByteOffsets.Add(posStart);
                    meshByteOffsets.Add(normStart);
                    meshByteOffsets.Add(uvStart);
                    meshIndexCounts.Add(mesh.Indices.Count);
                    meshVertexCounts.Add(mesh.Positions.Count);
                }

                // 2. Write Animation Data
                List<int> animTimeOffsets = new List<int>();
                List<int> animDataOffsets = new List<int>();
                foreach (var anim in animations)
                {
                    // TimeTrack
                    int timeStart = (int)msBuffer.Position;
                    foreach (var t in anim.Timestamps) bwBuffer.Write(t);
                    animTimeOffsets.Add(timeStart);
                    while (msBuffer.Position % 4 != 0) bwBuffer.Write((byte)0);

                    // Bone Tracks (Translation & Rotation)
                    for (int b = 0; b < meshes.Count; b++)
                    {
                        // Translation
                        int transStart = (int)msBuffer.Position;
                        for (int f = 0; f < anim.Timestamps.Length; f++)
                        {
                            var mat = anim.Keyframes[f][b];
                            bwBuffer.Write(mat.M41); bwBuffer.Write(mat.M42); bwBuffer.Write(mat.M43);
                        }
                        animDataOffsets.Add(transStart);
                        while (msBuffer.Position % 4 != 0) bwBuffer.Write((byte)0);

                        // Rotation (Quaternion)
                        int rotStart = (int)msBuffer.Position;
                        for (int f = 0; f < anim.Timestamps.Length; f++)
                        {
                            var mat = anim.Keyframes[f][b];
                            OpenTK.Quaternion q = mat.ExtractRotation();
                            bwBuffer.Write(q.X); bwBuffer.Write(q.Y); bwBuffer.Write(q.Z); bwBuffer.Write(q.W);
                        }
                        animDataOffsets.Add(rotStart);
                        while (msBuffer.Position % 4 != 0) bwBuffer.Write((byte)0);
                    }
                }

                // 3. Textures — encode as 32bpp PNG so palette/format quirks cannot mix images
                List<int> textureOffsets = new List<int>();
                List<int> textureLengths = new List<int>();
                foreach (var bmp in textures)
                {
                    int start = (int)msBuffer.Position;
                    using (Bitmap rgba = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb))
                    {
                        using (Graphics g = Graphics.FromImage(rgba))
                        {
                            g.Clear(Color.Transparent);
                            g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
                        }
                        rgba.Save(msBuffer, ImageFormat.Png);
                    }
                    textureOffsets.Add(start);
                    textureLengths.Add((int)msBuffer.Position - start);
                    while (msBuffer.Position % 4 != 0) bwBuffer.Write((byte)0);
                }

                byte[] binData = msBuffer.ToArray();

                // 4. Construct JSON
                StringBuilder json = new StringBuilder();
                json.Append("{");
                json.Append("\"asset\": {\"version\": \"2.0\"},");
                
                // Nodes — optional assembly matrices (OpenTK row-vector M → glTF column-vector Transpose(M))
                json.Append("\"nodes\": [");
                for (int i = 0; i < meshes.Count; i++)
                {
                    string safeName = (meshes[i].Name ?? $"Mesh_{i}").Replace("\\", "\\\\").Replace("\"", "\\\"");
                    json.Append($"{{\"name\": \"{safeName}\", \"mesh\": {i}");
                    if (bindPose != null && i < bindPose.Length)
                    {
                        // Viewer: v' = Vector4.Transform(v, M)  (row-vector * M)
                        // glTF:   v' = G * v                  (column-vector)
                        // Equivalent: G = Transpose(M). Store G as glTF column-major float[16].
                        Matrix4 g = Matrix4.Transpose(bindPose[i]);
                        json.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            ", \"matrix\": [{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, {15}]",
                            g.M11, g.M21, g.M31, g.M41,
                            g.M12, g.M22, g.M32, g.M42,
                            g.M13, g.M23, g.M33, g.M43,
                            g.M14, g.M24, g.M34, g.M44));
                    }
                    json.Append("}");
                    if (i < meshes.Count - 1) json.Append(",");
                }
                json.Append("],");

                // Scenes
                json.Append("\"scenes\": [{\"nodes\": [");
                for (int i = 0; i < meshes.Count; i++) { json.Append(i); if (i < meshes.Count - 1) json.Append(","); }
                json.Append("]}], \"scene\": 0,");

                // Meshes — each submesh uses its TextureIndex material
                json.Append("\"meshes\": [");
                for (int i = 0; i < meshes.Count; i++)
                {
                    int accIdx = i * 4;
                    int matIdx = 0;
                    if (textures.Count > 0)
                    {
                        matIdx = meshes[i].TextureIndex;
                        if (matIdx < 0) matIdx = 0;
                        if (matIdx >= textures.Count) matIdx = textures.Count - 1;
                    }
                    json.Append($"{{\"primitives\": [{{ \"attributes\": {{ \"POSITION\": {accIdx + 1}, \"NORMAL\": {accIdx + 2}, \"TEXCOORD_0\": {accIdx + 3} }}, \"indices\": {accIdx}, \"material\": {matIdx} }}]}}");
                    if (i < meshes.Count - 1) json.Append(",");
                }
                json.Append("],");

                // BufferViews
                json.Append("\"bufferViews\": [");
                // Mesh Views
                for (int i = 0; i < meshes.Count; i++)
                {
                    int o = i * 4;
                    json.Append($"{{\"buffer\": 0, \"byteOffset\": {meshByteOffsets[o]}, \"byteLength\": {meshIndexCounts[i] * 4}, \"target\": 34963}},");
                    json.Append($"{{\"buffer\": 0, \"byteOffset\": {meshByteOffsets[o+1]}, \"byteLength\": {meshVertexCounts[i] * 12}, \"target\": 34962}},");
                    json.Append($"{{\"buffer\": 0, \"byteOffset\": {meshByteOffsets[o+2]}, \"byteLength\": {meshVertexCounts[i] * 12}, \"target\": 34962}},");
                    json.Append($"{{\"buffer\": 0, \"byteOffset\": {meshByteOffsets[o+3]}, \"byteLength\": {meshVertexCounts[i] * 8}, \"target\": 34962}}");
                    if (i < meshes.Count - 1 || animations.Count > 0 || textures.Count > 0) json.Append(",");
                }

                // Animation Views
                int currentDataOffsetIdx = 0;
                for (int a = 0; a < animations.Count; a++)
                {
                    int frames = animations[a].Timestamps.Length;
                    json.Append($"{{\"buffer\": 0, \"byteOffset\": {animTimeOffsets[a]}, \"byteLength\": {frames * 4}}}");
                    if (a < animations.Count - 1 || meshes.Count > 0 || textures.Count > 0) json.Append(",");
                    
                    for (int b = 0; b < meshes.Count; b++)
                    {
                        json.Append($"{{\"buffer\": 0, \"byteOffset\": {animDataOffsets[currentDataOffsetIdx++]}, \"byteLength\": {frames * 12}}},");
                        json.Append($"{{\"buffer\": 0, \"byteOffset\": {animDataOffsets[currentDataOffsetIdx++]}, \"byteLength\": {frames * 16}}}");
                        if (currentDataOffsetIdx < animDataOffsets.Count || textures.Count > 0) json.Append(",");
                    }
                }

                // Texture Views
                for (int i = 0; i < textures.Count; i++)
                {
                    json.Append($"{{\"buffer\": 0, \"byteOffset\": {textureOffsets[i]}, \"byteLength\": {textureLengths[i]}}}");
                    if (i < textures.Count - 1) json.Append(",");
                }
                json.Append("],");

                // Accessors — record animation accessor indices as they are created
                json.Append("\"accessors\": [");
                int nextAccessorIndex = 0;
                List<int> animTimeAccessorIndices = new List<int>();
                List<int[]> animOutputAccessorIndices = new List<int[]>(); // per anim: [trans0, rot0, trans1, rot1, ...]

                // Mesh Accessors
                for (int i = 0; i < meshes.Count; i++)
                {
                    int vIdx = i * 4;
                    json.Append($"{{\"bufferView\": {vIdx}, \"componentType\": 5125, \"count\": {meshIndexCounts[i]}, \"type\": \"SCALAR\"}},");
                    json.Append($"{{\"bufferView\": {vIdx+1}, \"componentType\": 5126, \"count\": {meshVertexCounts[i]}, \"type\": \"VEC3\"}},");
                    json.Append($"{{\"bufferView\": {vIdx+2}, \"componentType\": 5126, \"count\": {meshVertexCounts[i]}, \"type\": \"VEC3\"}},");
                    json.Append($"{{\"bufferView\": {vIdx+3}, \"componentType\": 5126, \"count\": {meshVertexCounts[i]}, \"type\": \"VEC2\"}}");
                    nextAccessorIndex += 4;
                    if (i < meshes.Count - 1 || animations.Count > 0) json.Append(",");
                }

                // Animation Accessors (interleaved: time, then per-bone translation+rotation)
                int currentViewIdx = meshes.Count * 4;
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                for (int a = 0; a < animations.Count; a++)
                {
                    int frames = animations[a].Timestamps.Length;
                    float tMin = 0f, tMax = 0f;
                    if (frames > 0)
                    {
                        tMin = animations[a].Timestamps[0];
                        tMax = animations[a].Timestamps[0];
                        for (int ti = 1; ti < frames; ti++)
                        {
                            float t = animations[a].Timestamps[ti];
                            if (t < tMin) tMin = t;
                            if (t > tMax) tMax = t;
                        }
                    }

                    animTimeAccessorIndices.Add(nextAccessorIndex);
                    json.Append(string.Format(inv,
                        "{{\"bufferView\": {0}, \"componentType\": 5126, \"count\": {1}, \"type\": \"SCALAR\", \"min\": [{2}], \"max\": [{3}]}}",
                        currentViewIdx++, frames, tMin, tMax));
                    nextAccessorIndex++;
                    if (currentViewIdx < (meshes.Count * 4 + animations.Count + animDataOffsets.Count)) json.Append(",");

                    int[] outputs = new int[meshes.Count * 2];
                    for (int b = 0; b < meshes.Count; b++)
                    {
                        outputs[b * 2] = nextAccessorIndex;
                        json.Append($"{{\"bufferView\": {currentViewIdx++}, \"componentType\": 5126, \"count\": {frames}, \"type\": \"VEC3\"}},");
                        nextAccessorIndex++;

                        outputs[b * 2 + 1] = nextAccessorIndex;
                        json.Append($"{{\"bufferView\": {currentViewIdx++}, \"componentType\": 5126, \"count\": {frames}, \"type\": \"VEC4\"}}");
                        nextAccessorIndex++;
                        if (currentViewIdx < (meshes.Count * 4 + animations.Count + animDataOffsets.Count)) json.Append(",");
                    }
                    animOutputAccessorIndices.Add(outputs);
                }
                json.Append("],");

                // Animations — samplers use recorded accessor indices; channel.sampler is local to this animation
                if (animations.Count > 0)
                {
                    json.Append("\"animations\": [");
                    for (int a = 0; a < animations.Count; a++)
                    {
                        var anim = animations[a];
                        string animName = (anim.Name ?? $"Anim_{a}").Replace("\\", "\\\\").Replace("\"", "\\\"");
                        int timeAcc = animTimeAccessorIndices[a];
                        int[] outputs = animOutputAccessorIndices[a];

                        json.Append($"{{\"name\": \"{animName}\", \"samplers\": [");
                        for (int b = 0; b < meshes.Count; b++)
                        {
                            json.Append($"{{\"input\": {timeAcc}, \"interpolation\": \"LINEAR\", \"output\": {outputs[b * 2]}}}");
                            json.Append($",{{\"input\": {timeAcc}, \"interpolation\": \"LINEAR\", \"output\": {outputs[b * 2 + 1]}}}");
                            if (b < meshes.Count - 1) json.Append(",");
                        }
                        json.Append("], \"channels\": [");
                        int localSamplerIdx = 0;
                        for (int b = 0; b < meshes.Count; b++)
                        {
                            json.Append($"{{\"sampler\": {localSamplerIdx}, \"target\": {{\"node\": {b}, \"path\": \"translation\"}}}}");
                            json.Append($",{{\"sampler\": {localSamplerIdx + 1}, \"target\": {{\"node\": {b}, \"path\": \"rotation\"}}}}");
                            if (b < meshes.Count - 1) json.Append(",");
                            localSamplerIdx += 2;
                        }
                        json.Append("]}");
                        if (a < animations.Count - 1) json.Append(",");
                    }
                    json.Append("],");
                }

                // Materials & Images — one entry per embedded texture
                int firstTexView = meshes.Count * 4 + animations.Count + animDataOffsets.Count;
                if (textures.Count > 0)
                {
                    json.Append("\"images\": [");
                    for (int i = 0; i < textures.Count; i++)
                    {
                        json.Append($"{{\"bufferView\": {firstTexView + i}, \"mimeType\": \"image/png\"}}");
                        if (i < textures.Count - 1) json.Append(",");
                    }
                    json.Append("],");

                    json.Append("\"textures\": [");
                    for (int i = 0; i < textures.Count; i++)
                    {
                        json.Append($"{{\"source\": {i}}}");
                        if (i < textures.Count - 1) json.Append(",");
                    }
                    json.Append("],");

                    json.Append("\"materials\": [");
                    for (int i = 0; i < textures.Count; i++)
                    {
                        json.Append($"{{\"name\": \"Material_{i}\", \"pbrMetallicRoughness\": {{\"baseColorTexture\": {{\"index\": {i}}}, \"metallicFactor\": 0.1, \"roughnessFactor\": 0.8}}}}");
                        if (i < textures.Count - 1) json.Append(",");
                    }
                    json.Append("]");
                }
                else
                {
                    json.Append("\"materials\": [{\"pbrMetallicRoughness\": {\"baseColorFactor\": [0.8, 0.8, 0.8, 1.0], \"metallicFactor\": 0.1, \"roughnessFactor\": 0.8}}]");
                }

                json.Append($", \"buffers\": [{{ \"byteLength\": {binData.Length} }}]");
                json.Append("}");

                byte[] jsonData = Encoding.UTF8.GetBytes(json.ToString());
                int jsonPaddedLength = (jsonData.Length + 3) & ~3;

                bw.Write(0x46546C67); // "glTF"
                bw.Write(2);
                bw.Write(12 + 8 + jsonPaddedLength + 8 + binData.Length);

                bw.Write(jsonPaddedLength);
                bw.Write(0x4E4F534A); // "JSON"
                bw.Write(jsonData);
                for (int i = jsonData.Length; i < jsonPaddedLength; i++) bw.Write((byte)0x20);

                bw.Write(binData.Length);
                bw.Write(0x004E4942); // "BIN"
                bw.Write(binData);
            }
        }
    }
}
