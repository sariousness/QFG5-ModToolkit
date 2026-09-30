using QFG5Extractor.Core;
using QFG5Extractor.Resources.Model;
using System;
using System.IO;

namespace QFG5Extractor.Resources.Model
{
    public class ModelConverter
    {
        public static void importOrExportBitmapOrMesh(string inputFileName, string outputFileName, bool exportBitmap, bool importBitmap, bool exportMesh, bool importMesh)
        {
            string inputFileNameBase = inputFileName.Substring(0, inputFileName.Length - 4);
            string outputFileNameBase = outputFileName.Substring(0, outputFileName.Length - 4);

            if (exportBitmap)
            {
                qfg5modelBitmap.exportBMPfile(inputFileName, outputFileNameBase);
            }
            else
            {
                int mdlNumberOfSubmeshesAddress = Constants.MDL_HEADER_NUMBER_OF_SUBMESHES_ADDRESS;
                int mdlPaletteAddress = Constants.MDL_HEADER_BITMAPPALETTE_ADDRESS;
                int mdlMeshDataAddress = Constants.MDL_HEADER_3DDATA_ADDRESS;

                byte[] mdlHeader = new byte[mdlPaletteAddress];
                int numberOfSubmeshes = 0;
                byte[] bitmapPalette = new byte[Constants.BITMAP_PALETTE_LENGTH];
                int mdlBitmapHeaderAddress;

                byte[] mdlMeshData;
                byte[] mdlTextureData;

                string mdlFileName = null;
                if (importBitmap || importMesh)
                {
                    mdlFileName = outputFileName;
                }
                else if (exportMesh)
                {
                    mdlFileName = inputFileName;
                }

                int sizeOfMDLfile;
                using (BinaryReader bReader = new BinaryReader(File.Open(mdlFileName, FileMode.Open)))
                {
                    sizeOfMDLfile = (int)bReader.BaseStream.Length;

                    mdlHeader = bReader.ReadBytes(mdlHeader.Length);
                    bitmapPalette = bReader.ReadBytes(bitmapPalette.Length);
                    mdlBitmapHeaderAddress = (int)(bReader.ReadUInt32());

                    if (mdlBitmapHeaderAddress < 0 || mdlBitmapHeaderAddress > sizeOfMDLfile)
                    {
                        throw new Exception($"Invalid bitmap header address ({mdlBitmapHeaderAddress}). This does not appear to be a standard textured MDL format.");
                    }

                    int sizeForMdlMeshData = mdlBitmapHeaderAddress - Constants.BYTES_PER_INT - bitmapPalette.Length - mdlHeader.Length;
                    if (sizeForMdlMeshData < 0 || sizeForMdlMeshData > sizeOfMDLfile)
                    {
                        throw new Exception($"Invalid mesh data bounds ({sizeForMdlMeshData}). This does not appear to be a standard textured MDL format.");
                    }

                    mdlMeshData = new byte[sizeForMdlMeshData];
                    mdlMeshData = bReader.ReadBytes(mdlMeshData.Length);

                    mdlTextureData = new byte[sizeOfMDLfile - mdlBitmapHeaderAddress];
                    mdlTextureData = bReader.ReadBytes(mdlTextureData.Length);

                    byte[] mdlHeadernumberOfSubmeshes = new byte[] { mdlHeader[mdlNumberOfSubmeshesAddress], mdlHeader[mdlNumberOfSubmeshesAddress + 1], mdlHeader[mdlNumberOfSubmeshesAddress + 2], mdlHeader[mdlNumberOfSubmeshesAddress + 3] };
                    numberOfSubmeshes = (int)BitConverter.ToUInt32(mdlHeadernumberOfSubmeshes, 0);
                }

                if (importBitmap)
                {
                    byte[] mdlTextureDataNew;
                    qfg5modelBitmap.importBMPfile(inputFileNameBase, mdlTextureData, ref bitmapPalette, out mdlTextureDataNew);
                    mdlTextureData = mdlTextureDataNew;
                }

                if (importMesh || exportMesh)
                {
                    byte[] mdlMeshDataNew;

                    qfg5modelMesh.importOrExportMeshFile(ref mdlMeshData, out mdlMeshDataNew, numberOfSubmeshes, mdlMeshDataAddress, inputFileNameBase, outputFileNameBase, importMesh);

                    if (importMesh)
                    {
                        for (int i = 0; i < mdlMeshData.Length; i++)
                        {
                            if (mdlMeshData[i] != mdlMeshDataNew[i])
                            {
                                // Console.WriteLine("mdlMeshData[i] != mdlMeshDataNew[i] @ {0}", i);
                            }
                        }

                        mdlBitmapHeaderAddress = mdlBitmapHeaderAddress - mdlMeshData.Length + mdlMeshDataNew.Length;
                        mdlMeshData = mdlMeshDataNew;
                    }
                }

                if (importBitmap || importMesh)
                {
                    using (BinaryWriter bWriter = new BinaryWriter(File.Open(outputFileName, FileMode.Create)))
                    {
                        bWriter.Write(mdlHeader);
                        bWriter.Write(bitmapPalette);
                        bWriter.Write((uint)mdlBitmapHeaderAddress);
                        bWriter.Write(mdlMeshData);
                        bWriter.Write(mdlTextureData);
                    }
                }
            }
        }
    }
}
