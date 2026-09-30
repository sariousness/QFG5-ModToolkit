using QFG5Extractor.Core;
using QFG5Extractor.Resources.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;

namespace QFG5Extractor.Resources.Model
{
    public class MeshLoader
    {
        public static List<MeshData> LoadMesh(string mdlFile)
        {
            List<MeshData> submeshes = new List<MeshData>();

            using (BinaryReader bReader = new BinaryReader(File.Open(mdlFile, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                long fileSize = bReader.BaseStream.Length;
                if (fileSize < Constants.MDL_HEADER_3DDATA_ADDRESS + 4) return submeshes;

                bReader.BaseStream.Seek(Constants.MDL_HEADER_NUMBER_OF_SUBMESHES_ADDRESS, SeekOrigin.Begin);
                int numberOfSubmeshes = bReader.ReadInt32();

                bReader.BaseStream.Seek(Constants.MDL_HEADER_3DDATA_ADDRESS, SeekOrigin.Begin);
                int meshDataAddress = (int)bReader.BaseStream.Position;

                int[] submeshRelativeAddresses = new int[numberOfSubmeshes];
                for (int i = 0; i < numberOfSubmeshes; i++)
                {
                    submeshRelativeAddresses[i] = bReader.ReadInt32();
                }

                // Mesh data in the file is absolute addresses, but the references are to the start of mesh data block?
                // Actually qfg5modelMesh.cs says: submeshAddressRelative = submeshAddressArray[submeshIndex] - mdlMeshDataAddress;
                // Where mdlMeshDataAddress is Constants.MDL_HEADER_3DDATA_ADDRESS (0x42c)

                foreach (int absAddr in submeshRelativeAddresses)
                {
                    if (absAddr < 0 || absAddr >= fileSize) continue;
                    bReader.BaseStream.Seek(absAddr, SeekOrigin.Begin);

                    MeshData mesh = new MeshData();
                    
                    // Read Name (10 bytes)
                    byte[] nameBytes = bReader.ReadBytes(10);
                    mesh.Name = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');

                    // Debug dump first 96 bytes as floats
                    bReader.BaseStream.Seek(absAddr, SeekOrigin.Begin);
                    for(int d=0; d<24; d++)
                    {
                        float val = bReader.ReadSingle();
                        System.Diagnostics.Debug.WriteLine($"Submesh {mesh.Name} offset {d*4:X2}: {val}");
                        Logger.Log($"DEBUG: Submesh {mesh.Name} offset {d*4:X2}: {val}");
                    }

                    // Header fields
                    bReader.BaseStream.Seek(absAddr + Constants.SUBMESH_HEADER_NUMBER_OF_VERTICES_ADDRESS, SeekOrigin.Begin);
                    int vertexCount = bReader.ReadInt32();
                    int uvCount = bReader.ReadInt32();
                    int faceCount = bReader.ReadInt32();

                    // Vertices
                    bReader.BaseStream.Seek(absAddr + Constants.SUBMESH_HEADER_VERTICES_LIST_ADDRESS, SeekOrigin.Begin);
                    for (int i = 0; i < vertexCount; i++)
                    {
                        float x = bReader.ReadSingle();
                        float y = bReader.ReadSingle();
                        float z = bReader.ReadSingle();
                        mesh.Vertices.Add(new Vector3(x, y, z));
                    }

                    // UVs
                    bReader.BaseStream.Seek(absAddr + Constants.SUBMESH_HEADER_VERTICES_LIST_ADDRESS + (vertexCount * 3 * 4), SeekOrigin.Begin);
                    for (int i = 0; i < uvCount; i++)
                    {
                        float u = bReader.ReadSingle();
                        float v = bReader.ReadSingle();
                        mesh.UVs.Add(new PointF(u, v));
                    }

                    // Faces
                    for (int i = 0; i < faceCount; i++)
                    {
                        int v1 = bReader.ReadInt32();
                        int v2 = bReader.ReadInt32();
                        int v3 = bReader.ReadInt32();
                        int uv1 = bReader.ReadInt32();
                        int uv2 = bReader.ReadInt32();
                        int uv3 = bReader.ReadInt32();
                        int texIdx = bReader.ReadInt32(); // 7th int is the subbitmap index
                        
                        // Each face record is 10 ints (40 bytes total)
                        // Read: v1, v2, v3, uv1, uv2, uv3, texIdx. Skip remaining 3 ints.
                        bReader.BaseStream.Seek(3 * 4, SeekOrigin.Current);
                        
                        mesh.Faces.Add(new int[] { v1, v2, v3 });
                        mesh.FaceUVs.Add(new int[] { uv1, uv2, uv3 });
                        mesh.FaceTextures.Add(texIdx);
                    }

                    NormalGenerator.Generate(mesh);
                    submeshes.Add(mesh);
                }
            }

            return submeshes;
        }
    }
}
