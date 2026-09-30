using QFG5Extractor.Core;
using QFG5Extractor.Resources.Model;
using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Collections.Generic;

namespace QFG5Extractor.Resources.Model
{
    public class OldMdlViewerLogic
    {
        private static byte[] swapPalette(ref byte[] palette)
        {
            //swap RGB to BGR
            byte[] tmpPalette = new byte[1023];

            for (int i = 0; i < 1023; i += 4)
            {
                tmpPalette[i] = palette[(i + 2)];
                tmpPalette[(i + 1)] = palette[(i + 1)];
                tmpPalette[(i + 2)] = palette[i];
                if ((i + 4) < 1023)
                {
                    tmpPalette[(i + 3)] = (byte)0;
                }
            }
            return tmpPalette;
        }

        public string getName(string file)
        {
            // Hardcoded name string location, max length is 10 bytes
            int mdlNameAddress = 0x0C;
            int mdlNameMaxLength = 10;
            byte[] mdlName = new byte[mdlNameMaxLength];
            string mdlNameString;

            using (BinaryReader bReader = new BinaryReader(File.Open(file, FileMode.Open)))
            {
                if (bReader.BaseStream.Length < mdlNameAddress + mdlNameMaxLength) return "";
                bReader.BaseStream.Seek(mdlNameAddress, SeekOrigin.Begin);
                mdlName = bReader.ReadBytes(mdlNameMaxLength);
                mdlNameString = Encoding.Default.GetString(mdlName);
                int nullIdx = mdlNameString.IndexOf('\0');
                if (nullIdx >= 0) mdlNameString = mdlNameString.Substring(0, nullIdx);
                return mdlNameString;
            }
        }

        public List<Bitmap> getAllBMPs(string file)
        {
            List<Bitmap> bitmaps = new List<Bitmap>();
            try
            {
                int mdlPaletteAddress = 0x29;
                byte[] mdlPalette = new byte[1023];

                using (BinaryReader bReader = new BinaryReader(File.Open(file, FileMode.Open, FileAccess.Read)))
                {
                    if (bReader.BaseStream.Length <= mdlPaletteAddress + 1023)
                        return bitmaps;

                    bReader.BaseStream.Seek(mdlPaletteAddress, SeekOrigin.Begin);
                    mdlPalette = bReader.ReadBytes(1023);
                    mdlPalette = swapPalette(ref mdlPalette);

                    bReader.BaseStream.Seek(0x428, SeekOrigin.Begin);
                    int bitmapSectionAddress = (int)bReader.ReadUInt32();
                    if (bitmapSectionAddress <= 0 || bitmapSectionAddress >= bReader.BaseStream.Length)
                        return bitmaps;

                    bReader.BaseStream.Seek(bitmapSectionAddress, SeekOrigin.Begin);
                    int headerSize = bReader.ReadInt32();
                    int numSubBitmaps = headerSize / 4;

                    // Skip the rest of the header if numSubBitmaps > 1
                    if (numSubBitmaps > 1) {
                        bReader.BaseStream.Seek(bitmapSectionAddress + headerSize, SeekOrigin.Begin);
                    }

                    for (int s = 0; s < numSubBitmaps; s++)
                    {
                        // Each sub-bitmap starts with a 24-byte header (6 floats/ints)
                        bReader.BaseStream.Seek(16, SeekOrigin.Current); // Skip floats and pow2
                        int widthMinus1 = bReader.ReadInt32();
                        int heightMinus1 = bReader.ReadInt32();

                        int width = widthMinus1 + 1;
                        int height = heightMinus1 + 1;
                        int dataSize = width * height;

                        byte[] pixelData = bReader.ReadBytes(dataSize);

                        using (MemoryStream ms = new MemoryStream())
                        {
                            BinaryWriter bw = new BinaryWriter(ms);
                            bw.Write(new byte[] { 0x42, 0x4d }); // BM
                            bw.Write((uint)(54 + 1024 + pixelData.Length));
                            bw.Write((ushort)0); bw.Write((ushort)0);
                            bw.Write((uint)(54 + 1024));
                            bw.Write((uint)40);
                            bw.Write(width);
                            bw.Write(height);
                            bw.Write((ushort)1);
                            bw.Write((ushort)8);
                            bw.Write((uint)0);
                            bw.Write((uint)pixelData.Length);
                            bw.Write(0); bw.Write(0);
                            bw.Write((uint)256);
                            bw.Write((uint)0);
                            bw.Write(mdlPalette);
                            bw.Write((byte)0);
                            bw.Write(pixelData);
                            bw.Flush();

                            ms.Position = 0;
                            using (Bitmap temp = new Bitmap(ms))
                            {
                                bitmaps.Add(new Bitmap(temp));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[MDL ERROR] {Path.GetFileName(file)}: Exception during multi-bitmap load: {ex.Message}");
            }
            return bitmaps;
        }

        public Bitmap getBMP(string file)
        {
            var list = getAllBMPs(file);
            return list.Count > 0 ? list[0] : null;
        }
    }
}
