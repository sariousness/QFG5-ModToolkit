using QFG5Extractor.Core;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace QFG5Extractor.Resources.Panorama
{
    class Config
    {
        //bmp
        public const int bmpPixelDataStartIndex = 1078;

        //nod
        public const int nodPaletteStartIndex = 168;

        //img
        public const int imgBitmapWidthOffset = 0x0;
        public const int imgBitmapHeightOffset = 0x4;
        public const int imgHeaderSize = 32;
        public const int imgPixelDataStartIndex = imgHeaderSize * 2;
    }

    public class PanoConverter
    {
        public static void exportFromFiles(string nodFile, string imgFile, string destination)
        {
            if (!File.Exists(nodFile))
            {
                throw new FileNotFoundException("NOD file not found.", nodFile);
            }

            if (!File.Exists(imgFile))
            {
                throw new FileNotFoundException("IMG file not found.", imgFile);
            }

            //get palette data from nod
            byte[] paletteBytes = File.ReadAllBytes(nodFile);
            byte[] pixelBytes = File.ReadAllBytes(imgFile);

            if (paletteBytes.Length < 8)
            {
                throw new InvalidDataException("NOD file is too small: " + nodFile);
            }

            if (pixelBytes.Length < 38)
            {
                throw new InvalidDataException("IMG file is too small: " + imgFile);
            }

            //palette version: 0 = demo, 4 = retail
            int paletteVersion = paletteBytes[6];

            int bitmapWidth = BitConverter.ToInt16(new byte[2] { pixelBytes[32], pixelBytes[33] }, 0);
            int bitmapHeight = BitConverter.ToInt16(new byte[2] { pixelBytes[36], pixelBytes[37] }, 0);

            //unpack bits
            byte[] pixelData = unpackBits(ref bitmapWidth, ref bitmapHeight, ref pixelBytes, ref paletteVersion);

            Bitmap newBitmap;

            unsafe
            {
                fixed (byte* pixelDataPointer = pixelData)
                {
                    IntPtr pointer = (IntPtr)pixelDataPointer;

                    //create new bitmap object
                    newBitmap = new Bitmap(
                        bitmapWidth,
                        bitmapHeight,
                        bitmapWidth,
                        PixelFormat.Format8bppIndexed,
                        pointer
                    );
                }
            }

            ColorPalette tmpPalette = newBitmap.Palette;

            int i = Config.nodPaletteStartIndex;
            int j = 0;

            for (; i + 2 < paletteBytes.Length && j < tmpPalette.Entries.Length; i += 4)
            {
                tmpPalette.Entries[j] = Color.FromArgb(paletteBytes[i], paletteBytes[i + 1], paletteBytes[i + 2]);
                j++;
            }

            newBitmap.Palette = tmpPalette;
            newBitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
            newBitmap.Save(destination, ImageFormat.Bmp);
        }

        public static void importToFiles(string bmpFile, string originalNod, string originalImg, string outNod, string outImg)
        {
            if (!File.Exists(bmpFile)) throw new FileNotFoundException("BMP file not found.", bmpFile);
            if (!File.Exists(originalNod)) throw new FileNotFoundException("Original NOD file not found.", originalNod);
            if (!File.Exists(originalImg)) throw new FileNotFoundException("Original IMG file not found.", originalImg);

            using (Bitmap bmp = new Bitmap(bmpFile))
            {
                // Rotate back
                bmp.RotateFlip(RotateFlipType.Rotate270FlipNone);

                int width = bmp.Width;
                int height = bmp.Height;
                byte[] pixelData = new byte[width * height];

                byte[] nodBytes = File.ReadAllBytes(originalNod);
                
                if (bmp.PixelFormat == PixelFormat.Format8bppIndexed)
                {
                    BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixelData, 0, pixelData.Length);
                    bmp.UnlockBits(data);
                }
                else
                {
                    // Quantize 24-bit to original palette
                    Color[] originalPalette = new Color[256];
                    int pStart = Config.nodPaletteStartIndex;
                    for (int i = 0; i < 256 && pStart + 2 < nodBytes.Length; i++)
                    {
                        originalPalette[i] = Color.FromArgb(nodBytes[pStart], nodBytes[pStart + 1], nodBytes[pStart + 2]);
                        pStart += 4;
                    }

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            pixelData[y * width + x] = GetNearestColorIndex(bmp.GetPixel(x, y), originalPalette);
                        }
                    }
                }

                byte[] imgBytes = File.ReadAllBytes(originalImg);

                int fileVersion = nodBytes[6];

                // Update palette in NOD
                ColorPalette palette = bmp.Palette;
                int pIdx = Config.nodPaletteStartIndex;
                for (int i = 0; i < 256 && pIdx + 2 < nodBytes.Length; i++)
                {
                    nodBytes[pIdx] = palette.Entries[i].R;
                    nodBytes[pIdx + 1] = palette.Entries[i].G;
                    nodBytes[pIdx + 2] = palette.Entries[i].B;
                    pIdx += 4;
                }

                // Pack bits for IMG
                byte[] packedData = packBits(pixelData, fileVersion);

                // Create new IMG bytes
                // The IMG file seems to have a 64-byte header (32 * 2)
                byte[] newImg = new byte[64 + packedData.Length];
                Array.Copy(imgBytes, 0, newImg, 0, 64);

                // Update width/height in header (at offset 32 and 36)
                byte[] wBytes = BitConverter.GetBytes((short)width);
                byte[] hBytes = BitConverter.GetBytes((short)height);
                newImg[32] = wBytes[0];
                newImg[33] = wBytes[1];
                newImg[36] = hBytes[0];
                newImg[37] = hBytes[1];

                Array.Copy(packedData, 0, newImg, 64, packedData.Length);

                File.WriteAllBytes(outNod, nodBytes);
                File.WriteAllBytes(outImg, newImg);
            }
        }

        static byte[] packBits(byte[] pixelData, int fileVersion)
        {
            MemoryStream ms = new MemoryStream();
            int i = 0;
            while (i < pixelData.Length)
            {
                // Look for a run of identical bytes
                int runLen = 1;
                while (i + runLen < pixelData.Length && pixelData[i + runLen] == pixelData[i] && runLen < 127)
                {
                    runLen++;
                }

                if (runLen >= 2)
                {
                    // Repeat command (1-127)
                    ms.WriteByte((byte)runLen);
                    byte b = (fileVersion > 0) ? pixelData[i] : (byte)(pixelData[i] + 85);
                    ms.WriteByte(b);
                    i += runLen;
                }
                else
                {
                    // Literal command (128-255 -> 256-count pixels)
                    // Scan lookahead for the next run
                    int literalLen = 0;
                    while (i + literalLen < pixelData.Length && literalLen < 128)
                    {
                        // Stop if we find a run of 2 or more
                        if (i + literalLen + 1 < pixelData.Length && pixelData[i + literalLen] == pixelData[i + literalLen + 1])
                            break;
                        literalLen++;
                    }

                    if (literalLen > 0)
                    {
                        ms.WriteByte((byte)(256 - literalLen));
                        for (int k = 0; k < literalLen; k++)
                        {
                            byte b = (fileVersion > 0) ? pixelData[i + k] : (byte)(pixelData[i + k] + 85);
                            ms.WriteByte(b);
                        }
                        i += literalLen;
                    }
                }
            }
            return ms.ToArray();
        }

        static byte[] unpackBits(ref int bitmapWidth, ref int bitmapHeight, ref byte[] pixelBytes, ref int fileVersion)
        {
            int i = Config.imgPixelDataStartIndex;
            int j = 0;
            
            int p = 0; //pixel
            
            int count;

            byte[] pixelData = new byte[bitmapWidth * bitmapHeight];

            //unpack bits
            while (i < pixelBytes.Length)
            {
                count = pixelBytes[i];

                if (count == 0)
                {
                    i++;
                }
                else if (count < 128)
                {
                    for (j = 0; j < count; j++)
                    {
                        pixelData[p] = (fileVersion > 0)? pixelBytes[i + 1] : (byte)(pixelBytes[i + 1] - 85);
                        p++;

                        if (p == pixelData.Length)
                        {
                            break;
                        }
                    }
                    i += 2;
                }
                else
                {
                    count = 256 - count;

                    for (j = 0; j < count; j++)
                    {
                        pixelData[p] = (fileVersion > 0)? pixelBytes[i + j + 1] : (byte)(pixelBytes[i + j + 1] - 85);
                        p++;

                        if (p == pixelData.Length)
                        {
                            break;
                        }
                    }
                    i = i + j + 1;
                }

                if (p == pixelData.Length)
                {
                    break;
                }
            }

            return pixelData;
        }
        private static byte GetNearestColorIndex(Color target, Color[] palette)
        {
            int minDiff = int.MaxValue;
            byte bestIndex = 0;
            for (int i = 0; i < palette.Length; i++)
            {
                int rd = target.R - palette[i].R;
                int gd = target.G - palette[i].G;
                int bd = target.B - palette[i].B;
                int diff = rd * rd + gd * gd + bd * bd;
                if (diff < minDiff)
                {
                    minDiff = diff;
                    bestIndex = (byte)i;
                }
            }
            return bestIndex;
        }
    }
}
