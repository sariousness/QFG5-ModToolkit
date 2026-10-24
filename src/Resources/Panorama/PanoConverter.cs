using QFG5Extractor.Core;
using System;
using System.Collections.Generic;
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
        public static bool TryReadProjectionAngles(string imgFile, out double startAngle, out double endAngle)
        {
            startAngle = 0;
            endAngle = 0;
            if (!File.Exists(imgFile)) return false;

            byte[] header = new byte[64];
            using (FileStream stream = new FileStream(imgFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length < header.Length) return false;
                int bytesRead = 0;
                while (bytesRead < header.Length)
                {
                    int count = stream.Read(header, bytesRead, header.Length - bytesRead);
                    if (count == 0) return false;
                    bytesRead += count;
                }
            }

            double bigEndianStart = ReadDouble(header, 8, true);
            double bigEndianEnd = ReadDouble(header, 16, true);
            double littleEndianStart = ReadDouble(header, 40, false);
            double littleEndianEnd = ReadDouble(header, 48, false);
            if (!IsFinite(bigEndianStart) || !IsFinite(bigEndianEnd) ||
                !IsFinite(littleEndianStart) || !IsFinite(littleEndianEnd) ||
                !NearlyEqual(bigEndianStart, littleEndianStart) ||
                !NearlyEqual(bigEndianEnd, littleEndianEnd))
                return false;

            double span = bigEndianEnd - bigEndianStart;
            if (span <= 0)
                span += 2.0 * Math.PI;
            if (span <= 0 || span > 2.0 * Math.PI + 0.001)
                return false;

            startAngle = bigEndianStart;
            endAngle = bigEndianStart + span;
            return true;
        }

        private static double ReadDouble(byte[] bytes, int offset, bool bigEndian)
        {
            byte[] value = new byte[8];
            for (int i = 0; i < value.Length; i++)
                value[i] = bytes[offset + (bigEndian ? value.Length - 1 - i : i)];
            return BitConverter.ToDouble(value, 0);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool NearlyEqual(double left, double right)
        {
            return Math.Abs(left - right) <= Math.Max(0.00000001,
                Math.Max(Math.Abs(left), Math.Abs(right)) * 0.00000001);
        }

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

            if (pixelBytes.Length < 64)
                throw new InvalidDataException("IMG file is too small to contain its complete header.");

            int bitmapWidth = BitConverter.ToInt32(pixelBytes, 32);
            int bitmapHeight = BitConverter.ToInt32(pixelBytes, 36);
            if (bitmapWidth <= 0 || bitmapHeight <= 0 ||
                bitmapWidth > short.MaxValue || bitmapHeight > short.MaxValue)
                throw new InvalidDataException("IMG file contains invalid panorama dimensions.");

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
            using (Bitmap bmp = new Bitmap(bmpFile))
                importToFiles(bmp, originalNod, originalImg, outNod, outImg);
        }

        public static void importToFiles(Bitmap sourceBitmap, string originalNod, string originalImg,
            string outNod, string outImg)
        {
            if (sourceBitmap == null) throw new ArgumentNullException("sourceBitmap");
            if (!File.Exists(originalNod)) throw new FileNotFoundException("Original NOD file not found.", originalNod);
            if (!File.Exists(originalImg)) throw new FileNotFoundException("Original IMG file not found.", originalImg);

            using (Bitmap bmp = new Bitmap(sourceBitmap))
            {
                // Rotate back
                bmp.RotateFlip(RotateFlipType.Rotate270FlipNone);

                int width = bmp.Width;
                int height = bmp.Height;
                if (width > short.MaxValue || height > short.MaxValue)
                    throw new InvalidDataException("BMP dimensions exceed the maximum supported panorama size.");

                byte[] nodBytes = File.ReadAllBytes(originalNod);
                Color[] originalPalette = ReadNodPalette(nodBytes);
                Color[] outputPalette;
                byte[] pixelData = BuildPixelData(bmp, originalPalette, out outputPalette);

                byte[] imgBytes = File.ReadAllBytes(originalImg);
                if (imgBytes.Length < 64)
                    throw new InvalidDataException("IMG file is too small to contain its header.");

                int fileVersion = nodBytes[6];

                int pIdx = Config.nodPaletteStartIndex;
                for (int i = 0; i < outputPalette.Length; i++)
                {
                    nodBytes[pIdx] = outputPalette[i].R;
                    nodBytes[pIdx + 1] = outputPalette[i].G;
                    nodBytes[pIdx + 2] = outputPalette[i].B;
                    pIdx += 4;
                }

                // Pack bits for IMG
                byte[] packedData = packBits(pixelData, width, height, fileVersion);

                // Create new IMG bytes
                // The IMG file seems to have a 64-byte header (32 * 2)
                byte[] newImg = new byte[64 + packedData.Length];
                Array.Copy(imgBytes, 0, newImg, 0, 64);

                WriteImageDimensions(newImg, width, height);

                Array.Copy(packedData, 0, newImg, 64, packedData.Length);

                File.WriteAllBytes(outNod, nodBytes);
                File.WriteAllBytes(outImg, newImg);
            }
        }

        private static Color[] ReadNodPalette(byte[] nodBytes)
        {
            const int paletteEntryCount = 256;
            if (nodBytes.Length < Config.nodPaletteStartIndex + paletteEntryCount * 4)
                throw new InvalidDataException("NOD file does not contain a complete 256-color palette.");

            Color[] palette = new Color[paletteEntryCount];
            int offset = Config.nodPaletteStartIndex;
            for (int i = 0; i < palette.Length; i++, offset += 4)
                palette[i] = Color.FromArgb(nodBytes[offset], nodBytes[offset + 1], nodBytes[offset + 2]);
            return palette;
        }

        private static byte[] BuildPixelData(Bitmap bmp, Color[] originalPalette, out Color[] outputPalette)
        {
            int pixelCount = checked(bmp.Width * bmp.Height);
            byte[] pixelData = new byte[pixelCount];
            outputPalette = (Color[])originalPalette.Clone();

            if (bmp.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
                try
                {
                    for (int y = 0; y < bmp.Height; y++)
                        System.Runtime.InteropServices.Marshal.Copy(
                            IntPtr.Add(data.Scan0, y * data.Stride), pixelData, y * bmp.Width, bmp.Width);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }

                Color[] bitmapPalette = bmp.Palette.Entries;
                for (int i = 0; i < outputPalette.Length && i < bitmapPalette.Length; i++)
                    outputPalette[i] = Color.FromArgb(bitmapPalette[i].R, bitmapPalette[i].G, bitmapPalette[i].B);
                return pixelData;
            }

            const int bucketCount = 32 * 32 * 32;
            int[] bucketCounts = new int[bucketCount];
            long[] redTotals = new long[bucketCount];
            long[] greenTotals = new long[bucketCount];
            long[] blueTotals = new long[bucketCount];
            List<int> populatedBuckets = new List<int>();

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color color = bmp.GetPixel(x, y);
                    int bucket = ((color.R >> 3) << 10) | ((color.G >> 3) << 5) | (color.B >> 3);
                    if (bucketCounts[bucket] == 0)
                        populatedBuckets.Add(bucket);
                    bucketCounts[bucket]++;
                    redTotals[bucket] += color.R;
                    greenTotals[bucket] += color.G;
                    blueTotals[bucket] += color.B;
                }
            }

            List<PaletteBox> boxes = new List<PaletteBox> {
                new PaletteBox(populatedBuckets, bucketCounts, redTotals, greenTotals, blueTotals)
            };
            while (boxes.Count < 256)
            {
                int boxToSplit = -1;
                int largestRange = 0;
                for (int i = 0; i < boxes.Count; i++)
                {
                    int range = boxes[i].LargestChannelRange;
                    if (range > largestRange && boxes[i].BucketIndexes.Count > 1)
                    {
                        largestRange = range;
                        boxToSplit = i;
                    }
                }
                if (boxToSplit < 0) break;

                PaletteBox[] split = boxes[boxToSplit].Split(bucketCounts, redTotals, greenTotals, blueTotals);
                boxes[boxToSplit] = split[0];
                boxes.Add(split[1]);
            }

            Color[] palette = new Color[boxes.Count];
            for (int i = 0; i < boxes.Count; i++)
            {
                palette[i] = boxes[i].AverageColor;
                outputPalette[i] = palette[i];
            }

            byte[] bucketToPalette = new byte[bucketCount];
            for (int bucket = 0; bucket < bucketCount; bucket++)
                bucketToPalette[bucket] = FindNearestPaletteIndex(bucket, palette);

            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color color = bmp.GetPixel(x, y);
                    int bucket = ((color.R >> 3) << 10) | ((color.G >> 3) << 5) | (color.B >> 3);
                    pixelData[y * bmp.Width + x] = bucketToPalette[bucket];
                }
            return pixelData;
        }

        private static byte FindNearestPaletteIndex(int bucket, Color[] palette)
        {
            int red = ((bucket >> 10) & 31) * 255 / 31;
            int green = ((bucket >> 5) & 31) * 255 / 31;
            int blue = (bucket & 31) * 255 / 31;
            int bestDifference = int.MaxValue;
            byte bestIndex = 0;
            for (int i = 0; i < palette.Length; i++)
            {
                int redDifference = red - palette[i].R;
                int greenDifference = green - palette[i].G;
                int blueDifference = blue - palette[i].B;
                int difference = redDifference * redDifference +
                    greenDifference * greenDifference + blueDifference * blueDifference;
                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    bestIndex = (byte)i;
                }
            }
            return bestIndex;
        }

        private static void WriteImageDimensions(byte[] image, int width, int height)
        {
            WriteBigEndianInt32(image, 0, width);
            WriteBigEndianInt32(image, 4, height);
            Buffer.BlockCopy(BitConverter.GetBytes(width), 0, image, 32, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(height), 0, image, 36, 4);
        }

        private static void WriteBigEndianInt32(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }

        private sealed class PaletteBox
        {
            public readonly List<int> BucketIndexes;
            public readonly int LargestChannelRange;
            public readonly Color AverageColor;

            public PaletteBox(List<int> bucketIndexes, int[] counts,
                long[] redTotals, long[] greenTotals, long[] blueTotals)
            {
                BucketIndexes = bucketIndexes;
                int minRed = 31, maxRed = 0;
                int minGreen = 31, maxGreen = 0;
                int minBlue = 31, maxBlue = 0;
                long totalCount = 0, red = 0, green = 0, blue = 0;

                foreach (int bucket in bucketIndexes)
                {
                    int bucketRed = (bucket >> 10) & 31;
                    int bucketGreen = (bucket >> 5) & 31;
                    int bucketBlue = bucket & 31;
                    minRed = Math.Min(minRed, bucketRed);
                    maxRed = Math.Max(maxRed, bucketRed);
                    minGreen = Math.Min(minGreen, bucketGreen);
                    maxGreen = Math.Max(maxGreen, bucketGreen);
                    minBlue = Math.Min(minBlue, bucketBlue);
                    maxBlue = Math.Max(maxBlue, bucketBlue);
                    totalCount += counts[bucket];
                    red += redTotals[bucket];
                    green += greenTotals[bucket];
                    blue += blueTotals[bucket];
                }

                LargestChannelRange = Math.Max(maxRed - minRed,
                    Math.Max(maxGreen - minGreen, maxBlue - minBlue));
                AverageColor = Color.FromArgb(
                    (int)((red + totalCount / 2) / totalCount),
                    (int)((green + totalCount / 2) / totalCount),
                    (int)((blue + totalCount / 2) / totalCount));
            }

            public PaletteBox[] Split(int[] counts, long[] redTotals, long[] greenTotals, long[] blueTotals)
            {
                int redRange = GetRange(BucketIndexes, 10);
                int greenRange = GetRange(BucketIndexes, 5);
                int blueRange = GetRange(BucketIndexes, 0);
                int shift = redRange >= greenRange && redRange >= blueRange ? 10
                    : greenRange >= blueRange ? 5 : 0;

                BucketIndexes.Sort((left, right) =>
                    ((left >> shift) & 31).CompareTo((right >> shift) & 31));

                long total = 0;
                foreach (int bucket in BucketIndexes) total += counts[bucket];
                long halfway = (total + 1) / 2;
                long accumulated = 0;
                int splitAt = 1;
                for (int i = 0; i < BucketIndexes.Count - 1; i++)
                {
                    accumulated += counts[BucketIndexes[i]];
                    if (accumulated >= halfway)
                    {
                        splitAt = i + 1;
                        break;
                    }
                }

                return new[] {
                    new PaletteBox(BucketIndexes.GetRange(0, splitAt), counts, redTotals, greenTotals, blueTotals),
                    new PaletteBox(BucketIndexes.GetRange(splitAt, BucketIndexes.Count - splitAt),
                        counts, redTotals, greenTotals, blueTotals)
                };
            }

            private static int GetRange(List<int> buckets, int shift)
            {
                int min = 31, max = 0;
                foreach (int bucket in buckets)
                {
                    int component = (bucket >> shift) & 31;
                    min = Math.Min(min, component);
                    max = Math.Max(max, component);
                }
                return max - min;
            }
        }

        static byte[] packBits(byte[] pixelData, int width, int height, int fileVersion)
        {
            if (width <= 0 || height <= 0 || checked(width * height) != pixelData.Length)
                throw new ArgumentException("Pixel data does not match the panorama dimensions.", "pixelData");

            MemoryStream ms = new MemoryStream();
            for (int row = 0; row < height; row++)
            {
                int rowStart = row * width;
                int rowEnd = rowStart + width;
                int i = rowStart;
                while (i < rowEnd)
                {
                    int runLen = 1;
                    while (i + runLen < rowEnd && pixelData[i + runLen] == pixelData[i] && runLen < 127)
                        runLen++;

                    if (runLen >= 2)
                    {
                        ms.WriteByte((byte)runLen);
                        ms.WriteByte((fileVersion > 0) ? pixelData[i] : (byte)(pixelData[i] + 85));
                        i += runLen;
                    }
                    else
                    {
                        int literalLen = 0;
                        while (i + literalLen < rowEnd && literalLen < 128)
                        {
                            if (i + literalLen + 1 < rowEnd &&
                                pixelData[i + literalLen] == pixelData[i + literalLen + 1])
                                break;
                            literalLen++;
                        }

                        ms.WriteByte((byte)(256 - literalLen));
                        for (int k = 0; k < literalLen; k++)
                            ms.WriteByte((fileVersion > 0)
                                ? pixelData[i + k]
                                : (byte)(pixelData[i + k] + 85));
                        i += literalLen;
                    }
                }

                ms.WriteByte(0);
            }
            return ms.ToArray();
        }

        static byte[] unpackBits(ref int bitmapWidth, ref int bitmapHeight, ref byte[] pixelBytes, ref int fileVersion)
        {
            int i = Config.imgPixelDataStartIndex;
            byte[] pixelData = new byte[bitmapWidth * bitmapHeight];
            int row = 0;
            int column = 0;

            while (row < bitmapHeight)
            {
                if (column == bitmapWidth)
                {
                    if (i < pixelBytes.Length && pixelBytes[i] == 0)
                        i++;
                    row++;
                    column = 0;
                    continue;
                }

                if (i >= pixelBytes.Length)
                    throw new InvalidDataException("IMG pixel data ended before all panorama columns were decoded.");

                sbyte count = unchecked((sbyte)pixelBytes[i++]);
                if (count == 0)
                    throw new InvalidDataException("IMG contains a column terminator before the column is complete.");

                int runLength = Math.Abs((int)count);
                if (column + runLength > bitmapWidth)
                    throw new InvalidDataException("IMG RLE packet exceeds the panorama column width.");

                if (count > 0)
                {
                    if (i >= pixelBytes.Length)
                        throw new InvalidDataException("IMG ends inside a repeated-value RLE packet.");
                    byte value = DecodePaletteIndex(pixelBytes[i++], fileVersion);
                    for (int j = 0; j < runLength; j++)
                        pixelData[row * bitmapWidth + column++] = value;
                }
                else
                {
                    if (i + runLength > pixelBytes.Length)
                        throw new InvalidDataException("IMG ends inside a literal RLE packet.");
                    for (int j = 0; j < runLength; j++)
                        pixelData[row * bitmapWidth + column++] = DecodePaletteIndex(pixelBytes[i++], fileVersion);
                }
            }
            return pixelData;
        }

        private static byte DecodePaletteIndex(byte value, int fileVersion)
        {
            return fileVersion > 0 ? value : (byte)(value - 85);
        }
    }
}
