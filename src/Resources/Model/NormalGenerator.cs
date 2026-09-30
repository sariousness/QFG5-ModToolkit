using System;
using System.Collections.Generic;

namespace QFG5Extractor.Resources.Model
{
    /// <summary>
    /// Computes smooth per-vertex normals from mesh faces (area-weighted average of face normals).
    /// MDL files do not store normals; these are derived for shading and modern format export.
    /// </summary>
    public static class NormalGenerator
    {
        private const float Epsilon = 1e-8f;

        public static void Generate(MeshData mesh)
        {
            if (mesh == null || mesh.Vertices == null || mesh.Vertices.Count == 0)
                return;

            int vertexCount = mesh.Vertices.Count;
            float[] accumX = new float[vertexCount];
            float[] accumY = new float[vertexCount];
            float[] accumZ = new float[vertexCount];

            if (mesh.Faces != null)
            {
                foreach (int[] face in mesh.Faces)
                {
                    if (face == null || face.Length < 3)
                        continue;

                    int i0 = face[0], i1 = face[1], i2 = face[2];
                    if (i0 < 0 || i0 >= vertexCount ||
                        i1 < 0 || i1 >= vertexCount ||
                        i2 < 0 || i2 >= vertexCount)
                        continue;

                    Vector3 a = mesh.Vertices[i0];
                    Vector3 b = mesh.Vertices[i1];
                    Vector3 c = mesh.Vertices[i2];

                    // Unnormalized cross product (b-a) x (c-a) — area-weighted face normal
                    float e1x = b.X - a.X, e1y = b.Y - a.Y, e1z = b.Z - a.Z;
                    float e2x = c.X - a.X, e2y = c.Y - a.Y, e2z = c.Z - a.Z;

                    float nx = e1y * e2z - e1z * e2y;
                    float ny = e1z * e2x - e1x * e2z;
                    float nz = e1x * e2y - e1y * e2x;

                    accumX[i0] += nx; accumY[i0] += ny; accumZ[i0] += nz;
                    accumX[i1] += nx; accumY[i1] += ny; accumZ[i1] += nz;
                    accumX[i2] += nx; accumY[i2] += ny; accumZ[i2] += nz;
                }
            }

            mesh.Normals = new List<Vector3>(vertexCount);
            for (int i = 0; i < vertexCount; i++)
            {
                float nx = accumX[i], ny = accumY[i], nz = accumZ[i];
                float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len > Epsilon)
                {
                    float inv = 1.0f / len;
                    mesh.Normals.Add(new Vector3(nx * inv, ny * inv, nz * inv));
                }
                else
                {
                    mesh.Normals.Add(new Vector3(0f, 1f, 0f));
                }
            }
        }

        public static void GenerateAll(IList<MeshData> meshes)
        {
            if (meshes == null) return;
            foreach (MeshData mesh in meshes)
                Generate(mesh);
        }

        /// <summary>
        /// Ensures Normals is populated and matches Vertices.Count.
        /// </summary>
        public static void EnsureNormals(MeshData mesh)
        {
            if (mesh == null) return;
            if (mesh.Normals == null || mesh.Normals.Count != mesh.Vertices.Count)
                Generate(mesh);
        }
    }
}
