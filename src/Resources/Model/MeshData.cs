using QFG5Extractor.Core;
using QFG5Extractor.Resources.Model;
using System.Collections.Generic;
using System.Drawing;

namespace QFG5Extractor.Resources.Model
{
    public class MeshData
    {
        public List<Vector3> Vertices { get; set; } = new List<Vector3>();
        public List<Vector3> Normals { get; set; } = new List<Vector3>();
        public List<PointF> UVs { get; set; } = new List<PointF>();
        public List<int[]> Faces { get; set; } = new List<int[]>(); // Each int[] is 3 vertex indices
        public List<int[]> FaceUVs { get; set; } = new List<int[]>(); // Each int[] is 3 UV indices
        public List<int> FaceTextures { get; set; } = new List<int>(); // Texture ID for each face
        public string Name { get; set; }
    }

    public struct Vector3
    {
        public float X, Y, Z;
        public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }
}
