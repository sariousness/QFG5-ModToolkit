using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using QFG5Extractor.Core;

namespace QFG5Extractor.Resources.Gra
{
    /// <summary>
    /// Decodes QFG5 GRA animated sprite files.
    /// Format (per codecs.multimedia.cx/2023/11/qfg5-room-image-formats/):
    ///   0x00  u32   image coding mode (0-4)
    ///   0x04  u32   number of sprite collections
    ///   0x08  512b  palette (RGB555, 256 entries x 2 bytes each)
    ///   0x208 u32[] offsets to each sprite collection (one per collection)
    ///   Each sprite collection:
    ///     u32  x, y, width, height, num_sprites, delay, flags
    ///     u32[] frame offsets (from collection data start)
    ///     frame pixel data (mode-dependent)
    /// </summary>
    public class GraConverter
    {
        // Size of the fixed palette block
        const int PALETTE_OFFSET = 0x08;
        const int PALETTE_SIZE = 512; // 256 * 2 bytes (RGB555)
        const int COLLECTION_OFFSETS_START = 0x208; // 0x08 + 0x200

        // -------------------------------------------------------
        // Public API
        // -------------------------------------------------------

        /// <summary>Returns info from the GRA header for display in the grid.</summary>
        public bool TryReadHeader(string path, out int mode, out int numCollections, out string error)
        {
            mode = -1; numCollections = 0; error = null;
            try
            {
                byte[] buf = new byte[16];
                using (var fs = File.OpenRead(path))
                {
                    if (fs.Length < COLLECTION_OFFSETS_START + 4) { error = "File too short"; return false; }
                    fs.Read(buf, 0, 8);
                }
                mode = (int)BitConverter.ToUInt32(buf, 0);
                numCollections = (int)BitConverter.ToUInt32(buf, 4);
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public bool TryReadMetadata(string path, out int cX, out int cY, out int cW, out int cH, out int nSprites, out string error)
        {
            cX = cY = cW = cH = nSprites = 0;
            error = null;
            if (!File.Exists(path)) { error = "File not found"; return false; }
            try
            {
                byte[] buf = new byte[32];
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Length < COLLECTION_OFFSETS_START + 4) { error = "File too short"; return false; }
                    fs.Seek(COLLECTION_OFFSETS_START, SeekOrigin.Begin);
                    fs.Read(buf, 0, 4);
                    int col0Offset = (int)BitConverter.ToUInt32(buf, 0);
                    if (col0Offset <= 0 || col0Offset + 20 > fs.Length) { error = "Bad collection offset"; return false; }
                    
                    fs.Seek(col0Offset, SeekOrigin.Begin);
                    fs.Read(buf, 0, 20);
                }
                cX = (int)BitConverter.ToUInt32(buf, 0);
                cY = (int)BitConverter.ToUInt32(buf, 4);
                cW = (int)BitConverter.ToUInt32(buf, 8);
                cH = (int)BitConverter.ToUInt32(buf, 12);
                nSprites = (int)BitConverter.ToUInt32(buf, 16);
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        /// <summary>
        /// Reads the delay and flags fields from the first sprite collection header.
        /// flags bit 0 (value 1) = column-major (CM) ordering.
        /// </summary>
        public bool TryReadFlags(string path, out int delay, out int flags, out string error)
        {
            delay = 0; flags = 0; error = null;
            if (!File.Exists(path)) { error = "File not found"; return false; }
            try
            {
                byte[] buf = new byte[32];
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Length < COLLECTION_OFFSETS_START + 4) { error = "File too short"; return false; }
                    fs.Seek(COLLECTION_OFFSETS_START, SeekOrigin.Begin);
                    fs.Read(buf, 0, 4);
                    int col0Offset = (int)BitConverter.ToUInt32(buf, 0);
                    if (col0Offset <= 0 || col0Offset + 28 > fs.Length) { error = "Bad collection offset"; return false; }
                    fs.Seek(col0Offset + 20, SeekOrigin.Begin); // skip x,y,w,h,numSprites (5 u32s = 20 bytes)
                    fs.Read(buf, 0, 8); // read delay (u32) + flags (u32)
                }
                delay = (int)BitConverter.ToUInt32(buf, 0);
                flags = (int)BitConverter.ToUInt32(buf, 4);
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        /// <summary>Returns the first frame of the first sprite collection into a 32bpp ARGB Bitmap.</summary>
        public Bitmap Decode(string path, int modeOverride = -1, bool cmOverride = false)
        {
            try
            {
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < COLLECTION_OFFSETS_START + 4)
                {
                    Logger.Log($"GraConverter: {Path.GetFileName(path)} too short ({data.Length} bytes)");
                    return null;
                }

                int mode = modeOverride != -1 ? modeOverride : (int)BitConverter.ToUInt32(data, 0);
                int numCollections = (int)BitConverter.ToUInt32(data, 4);

                if (numCollections <= 0)
                {
                    Logger.Log($"GraConverter: {Path.GetFileName(path)} has 0 collections");
                    return null;
                }

                // Parse the 256-entry RGB555 palette
                Color[] palette = ParsePalette(data);

                // Read offset to first sprite collection
                int col0Offset = (int)BitConverter.ToUInt32(data, COLLECTION_OFFSETS_START);
                if (col0Offset <= 0 || col0Offset >= data.Length - 28)
                {
                    Logger.Log($"GraConverter: Bad collection 0 offset {col0Offset} in {Path.GetFileName(path)}");
                    return null;
                }

                // Parse sprite collection header (7 x u32)
                int cW       = (int)BitConverter.ToUInt32(data, col0Offset + 8);
                int cH       = (int)BitConverter.ToUInt32(data, col0Offset + 12);
                int numSprites = (int)BitConverter.ToUInt32(data, col0Offset + 16);
                // delay and flags at +20 and +24 ignored for preview

                if (cW <= 0 || cH <= 0 || cW > 4096 || cH > 4096 || numSprites <= 0)
                {
                    Logger.Log($"GraConverter: Invalid collection dims {cW}x{cH} sprites={numSprites}");
                    return null;
                }

                // User requirement: If multiple frames exist, the first one might be blank.
                // Always extract the LAST frame by default.
                int lastFrameIdx = numSprites - 1;
                int framePtr = col0Offset + 28 + (lastFrameIdx * 4);
                
                if (framePtr + 4 > data.Length)
                {
                    Logger.Log($"GraConverter: Final frame pointer out of range");
                    return null;
                }

                int frameRel = (int)BitConverter.ToUInt32(data, framePtr);
                int frameAbs = col0Offset + frameRel;

                if (frameAbs >= data.Length)
                {
                    Logger.Log($"GraConverter: Frame {lastFrameIdx} offset out of range ({frameAbs} >= {data.Length})");
                    return null;
                }

                return DecodeFrame(data, frameAbs, cW, cH, mode, palette, cmOverride);
            }
            catch (Exception ex)
            {
                Logger.LogError("GraConverter", $"Decode failed for {Path.GetFileName(path)}: {ex.Message}");
                return null;
            }
        }

        public bool AnalyzeHeuristics(string path, out bool suggestCm)
        {
            suggestCm = false;
            try {
                int cX, cY, cW, cH, nSprites;
                string err;
                if (!TryReadMetadata(path, out cX, out cY, out cW, out cH, out nSprites, out err)) return false;

                if (AnalyzeHeuristics(cW, cH, 0, true, false, out suggestCm))
                    return true;
                
                // Heuristic: Check flags for bit 0
                int delay, flags;
                bool flagsRead = TryReadFlags(path, out delay, out flags, out err);
                return AnalyzeHeuristics(cW, cH, flags, true, flagsRead, out suggestCm);
            } catch { }
            return false;
        }

        public bool AnalyzeHeuristics(int cW, int cH, int flags, bool metadataRead, bool flagsRead, out bool suggestCm)
        {
            suggestCm = false;
            if (!metadataRead) return false;

            // Heuristic: If height is much larger than width (e.g. 5x or more),
            // it's likely a scanline issue and needs CM (Correction Mode).
            if (cH > cW * 4 && cW > 0) {
                suggestCm = true;
                return true;
            }

            // Heuristic: Check flags for bit 0
            if (flagsRead && (flags & 1) != 0) {
                suggestCm = true;
                return true;
            }

            return false;
        }

        public bool TrySuggestColumnMajor(string path, int modeOverride, out bool suggestCm, out bool confident)
        {
            suggestCm = false;
            confident = false;
            int delay, flags;
            string error;
            if (TryReadFlags(path, out delay, out flags, out error) && (flags & 1) != 0)
            {
                suggestCm = true;
                confident = true;
                return true;
            }

            using (Bitmap rowMajor = Decode(path, modeOverride, false))
            using (Bitmap columnMajor = Decode(path, modeOverride, true))
            {
                if (rowMajor == null || columnMajor == null) return false;
                double rowScore = GetImageContinuityScore(rowMajor);
                double columnScore = GetImageContinuityScore(columnMajor);
                double difference = Math.Abs(rowScore - columnScore);
                if (difference < 0.10 || difference < Math.Min(rowScore, columnScore) * 0.10)
                    return true;

                suggestCm = columnScore < rowScore;
                confident = true;
                return true;
            }
        }

        private static double GetImageContinuityScore(Bitmap bitmap)
        {
            Rectangle bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] pixels = new int[bitmap.Width * bitmap.Height];
                for (int y = 0; y < bitmap.Height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * bitmap.Width, bitmap.Width);

                int stride = Math.Max(1, (Math.Max(bitmap.Width, bitmap.Height) + 127) / 128);
                long difference = 0;
                long samples = 0;
                for (int y = 0; y < bitmap.Height; y += stride)
                {
                    for (int x = 0; x < bitmap.Width; x += stride)
                    {
                        int pixel = pixels[y * bitmap.Width + x];
                        if (x + stride < bitmap.Width)
                        {
                            difference += GetColorDifference(pixel, pixels[y * bitmap.Width + x + stride]);
                            samples++;
                        }
                        if (y + stride < bitmap.Height)
                        {
                            difference += GetColorDifference(pixel, pixels[(y + stride) * bitmap.Width + x]);
                            samples++;
                        }
                    }
                }
                return samples == 0 ? 0 : (double)difference / (samples * 3);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static int GetColorDifference(int left, int right)
        {
            return Math.Abs(((left >> 16) & 0xFF) - ((right >> 16) & 0xFF)) +
                Math.Abs(((left >> 8) & 0xFF) - ((right >> 8) & 0xFF)) +
                Math.Abs((left & 0xFF) - (right & 0xFF));
        }

        public System.Collections.Generic.List<Bitmap> DecodeAllFrames(string path, int modeOverride = -1, bool cmOverride = false)
        {
            return DecodeCollectionFrames(path, 0, modeOverride, cmOverride);
        }

        public List<Bitmap> DecodeCollectionFrames(string path, int collectionIndex,
            int modeOverride = -1, bool cmOverride = false)
        {
            List<Bitmap> frames = new List<Bitmap>();
            try
            {
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < COLLECTION_OFFSETS_START + 4)
                    throw new InvalidDataException("GRA file is too small to contain a collection.");

                int mode = modeOverride != -1 ? modeOverride : (int)BitConverter.ToUInt32(data, 0);
                int numCollections = (int)BitConverter.ToUInt32(data, 4);
                if (mode < 0 || mode > 4 || numCollections <= 0 ||
                    numCollections > (data.Length - COLLECTION_OFFSETS_START) / 4 ||
                    collectionIndex < 0 || collectionIndex >= numCollections)
                    throw new InvalidDataException("GRA file contains an invalid mode or collection index.");

                List<GraCollection> collections = ReadCollections(data, numCollections);
                GraCollection collection = collections.Find(item => item.Index == collectionIndex);
                if (collection == null)
                    throw new InvalidDataException("The requested GRA collection could not be read.");

                int width = ReadInt32(data, collection.Offset + 8);
                int height = ReadInt32(data, collection.Offset + 12);
                if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
                    throw new InvalidDataException("GRA collection contains invalid dimensions.");

                Color[] palette = ParsePalette(data);
                foreach (GraFrame frame in collection.Frames)
                {
                    Bitmap bmp = DecodeFrame(data, frame.Offset, width, height, mode, palette, cmOverride);
                    if (bmp == null)
                        throw new InvalidDataException("A GRA animation frame could not be decoded.");
                    frames.Add(bmp);
                }
            }
            catch (Exception ex)
            {
                foreach (Bitmap frame in frames) frame.Dispose();
                frames.Clear();
                Logger.LogError("GraConverter", "DecodeCollectionFrames failed: " + ex.Message);
            }
            return frames;
        }

        public bool ExportToMP4(string srcPath, string destMp4, int modeOverride = -1, bool cmOverride = false)
        {
            var frames = DecodeAllFrames(srcPath, modeOverride, cmOverride);
            if (frames == null || frames.Count == 0) return false;

            string tmpDir = Path.Combine(Path.GetTempPath(), "QFG5_GRA_Export_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);
            try
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    string fPath = Path.Combine(tmpDir, $"frame_{i:D4}.png");
                    frames[i].Save(fPath, ImageFormat.Png);
                }

                System.Diagnostics.Process p = new System.Diagnostics.Process();
                p.StartInfo.FileName = "ffmpeg";
                p.StartInfo.Arguments = $"-y -framerate 10 -i \"{tmpDir}/frame_%04d.png\" -c:v libx264 -pix_fmt yuv420p \"{destMp4}\"";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Logger.LogError("GraConverter", "MP4 Export failed: " + ex.Message);
                return false;
            }
            finally
            {
                foreach (var f in frames) f.Dispose();
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
            }
        }

        public bool Extract(string srcPath, string destPath, bool usePng, int modeOverride = -1, bool cmOverride = false)
        {
            using (Bitmap bmp = Decode(srcPath, modeOverride, cmOverride))
            {
                if (bmp == null) return false;
                var fmt = usePng ? ImageFormat.Png : ImageFormat.Bmp;
                bmp.Save(destPath, fmt);
                return true;
            }
        }

        public void ReplaceFrame(string srcPath, string destPath, int collectionIndex, int frameIndex, Bitmap editedFrame)
        {
            if (editedFrame == null) throw new ArgumentNullException("editedFrame");
            byte[] source = File.ReadAllBytes(srcPath);
            if (source.Length < COLLECTION_OFFSETS_START)
                throw new InvalidDataException("GRA file is too small to contain its header.");

            int mode = ReadInt32(source, 0);
            int collectionCount = ReadInt32(source, 4);
            if (mode < 0 || mode > 4)
                throw new InvalidDataException("The selected GRA uses an unsupported image mode.");
            if (collectionCount <= 0 || collectionCount > (source.Length - COLLECTION_OFFSETS_START) / 4)
                throw new InvalidDataException("GRA file contains an invalid collection count.");
            if (collectionIndex < 0 || collectionIndex >= collectionCount)
                throw new ArgumentOutOfRangeException("collectionIndex");

            List<GraCollection> collections = ReadCollections(source, collectionCount);
            GraCollection selectedCollection = collections.Find(c => c.Index == collectionIndex);
            if (selectedCollection == null || frameIndex < 0 || frameIndex >= selectedCollection.Frames.Count)
                throw new ArgumentOutOfRangeException("frameIndex");

            int width = ReadInt32(source, selectedCollection.Offset + 8);
            int height = ReadInt32(source, selectedCollection.Offset + 12);
            if (editedFrame.Width != width || editedFrame.Height != height)
                throw new InvalidDataException(string.Format(
                    "Edited image size {0}x{1} does not match the GRA frame size {2}x{3}.",
                    editedFrame.Width, editedFrame.Height, width, height));

            bool columnMajor = (ReadInt32(source, selectedCollection.Offset + 24) & 1) != 0;
            Color[] palette = ParsePalette(source);
            GraFrame selectedFrame = selectedCollection.Frames[frameIndex];
            byte[] replacement = EncodeFrame(editedFrame, palette, mode, columnMajor,
                selectedFrame.Data, width, height);
            if (replacement.Length > selectedFrame.Data.Length)
                throw new InvalidDataException(string.Format(
                    "The edited frame needs {0} bytes, but its original GRA frame slot has {1}. " +
                    "The file was not changed to avoid relocating GRA data.",
                    replacement.Length, selectedFrame.Data.Length));

            byte[] rewritten = (byte[])source.Clone();
            Buffer.BlockCopy(replacement, 0, rewritten, selectedFrame.Offset, replacement.Length);
            using (FileStream output = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                output.Write(rewritten, 0, rewritten.Length);
        }

        public void ReplaceFrames(string srcPath, string destPath, int collectionIndex,
            IList<Bitmap> editedFrames, IList<bool> framesToReplace)
        {
            if (editedFrames == null) throw new ArgumentNullException("editedFrames");
            if (framesToReplace == null) throw new ArgumentNullException("framesToReplace");

            byte[] source = File.ReadAllBytes(srcPath);
            if (source.Length < COLLECTION_OFFSETS_START)
                throw new InvalidDataException("GRA file is too small to contain its header.");

            int mode = ReadInt32(source, 0);
            int collectionCount = ReadInt32(source, 4);
            if (mode < 0 || mode > 4 || collectionCount <= 0 ||
                collectionCount > (source.Length - COLLECTION_OFFSETS_START) / 4)
                throw new InvalidDataException("GRA file contains an invalid mode or collection count.");
            if (collectionIndex < 0 || collectionIndex >= collectionCount)
                throw new ArgumentOutOfRangeException("collectionIndex");

            List<GraCollection> collections = ReadCollections(source, collectionCount);
            GraCollection collection = collections.Find(item => item.Index == collectionIndex);
            if (collection == null || editedFrames.Count != collection.Frames.Count ||
                framesToReplace.Count != collection.Frames.Count)
                throw new InvalidDataException("Edited frame data does not match the selected GRA collection.");

            int width = ReadInt32(source, collection.Offset + 8);
            int height = ReadInt32(source, collection.Offset + 12);
            bool columnMajor = (ReadInt32(source, collection.Offset + 24) & 1) != 0;
            Color[] palette = ParsePalette(source);
            Dictionary<int, byte[]> replacements = new Dictionary<int, byte[]>();

            for (int frameIndex = 0; frameIndex < collection.Frames.Count; frameIndex++)
            {
                if (!framesToReplace[frameIndex]) continue;
                Bitmap editedFrame = editedFrames[frameIndex];
                if (editedFrame == null)
                    throw new InvalidDataException("An edited GRA frame is missing.");
                if (editedFrame.Width != width || editedFrame.Height != height)
                    throw new InvalidDataException(string.Format(
                        "Edited image size {0}x{1} does not match the GRA frame size {2}x{3}.",
                        editedFrame.Width, editedFrame.Height, width, height));

                GraFrame frame = collection.Frames[frameIndex];
                byte[] replacement = EncodeFrame(editedFrame, palette, mode, columnMajor,
                    frame.Data, width, height);
                if (replacement.Length > frame.Data.Length)
                    throw new InvalidDataException(string.Format(
                        "Frame {0} needs {1} bytes, but its original GRA frame slot has {2}. " +
                        "The file was not changed to avoid relocating GRA data.",
                        frameIndex + 1, replacement.Length, frame.Data.Length));
                replacements.Add(frameIndex, replacement);
            }

            if (replacements.Count == 0)
                throw new InvalidOperationException("There are no edited GRA frames to inject.");

            byte[] rewritten = (byte[])source.Clone();
            foreach (KeyValuePair<int, byte[]> replacement in replacements)
                Buffer.BlockCopy(replacement.Value, 0, rewritten,
                    collection.Frames[replacement.Key].Offset, replacement.Value.Length);
            using (FileStream output = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                output.Write(rewritten, 0, rewritten.Length);
        }

        private static List<GraCollection> ReadCollections(byte[] data, int count)
        {
            List<GraCollection> collections = new List<GraCollection>(count);
            List<int> offsets = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                int offset = ReadInt32(data, COLLECTION_OFFSETS_START + i * 4);
                if (offset < COLLECTION_OFFSETS_START + count * 4 || offset > data.Length - 28)
                    throw new InvalidDataException("GRA contains an invalid sprite collection offset.");
                offsets.Add(offset);
                collections.Add(new GraCollection { Index = i, Offset = offset });
            }

            List<int> sortedOffsets = new List<int>(offsets);
            sortedOffsets.Sort();
            for (int i = 0; i < collections.Count; i++)
            {
                GraCollection collection = collections[i];
                int nextCollection = sortedOffsets.Find(offset => offset > collection.Offset);
                collection.End = nextCollection == 0 ? data.Length : nextCollection;
                int frameCount = ReadInt32(data, collection.Offset + 16);
                if (frameCount <= 0 || frameCount > (collection.End - collection.Offset - 28) / 4)
                    throw new InvalidDataException("GRA collection contains an invalid frame count.");

                int frameTableEnd = collection.Offset + 28 + frameCount * 4;
                List<GraFrame> frames = new List<GraFrame>(frameCount);
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    int relativeOffset = ReadInt32(data, collection.Offset + 28 + frameIndex * 4);
                    long absoluteOffset = (long)collection.Offset + relativeOffset;
                    if (absoluteOffset < frameTableEnd || absoluteOffset >= collection.End)
                        throw new InvalidDataException("GRA collection contains an invalid frame offset.");
                    frames.Add(new GraFrame { Index = frameIndex, Offset = (int)absoluteOffset });
                }

                List<int> frameOffsets = new List<int>();
                foreach (GraFrame frame in frames) frameOffsets.Add(frame.Offset);
                frameOffsets.Sort();
                for (int frameIndex = 1; frameIndex < frameOffsets.Count; frameIndex++)
                    if (frameOffsets[frameIndex] == frameOffsets[frameIndex - 1])
                        throw new InvalidDataException("GRA frames share a data offset; safe frame replacement is not supported.");

                for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
                {
                    GraFrame frame = frames[frameIndex];
                    int frameEnd = collection.End;
                    foreach (int nextFrame in frameOffsets)
                        if (nextFrame > frame.Offset) { frameEnd = nextFrame; break; }
                    frame.Data = new byte[frameEnd - frame.Offset];
                    Buffer.BlockCopy(data, frame.Offset, frame.Data, 0, frame.Data.Length);
                }
                collection.Frames = frames;
            }
            return collections;
        }

        private static byte[] EncodeFrame(Bitmap bitmap, Color[] palette, int mode,
            bool columnMajor, byte[] originalFrame, int width, int height)
        {
            byte[] indexes = new byte[checked(width * height)];
            bool preserveTransparency = mode == 3 || mode == 4;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Color color = bitmap.GetPixel(x, y);
                    int pixel = y * width + x;
                    indexes[pixel] = preserveTransparency && color.A < 128
                        ? (byte)0xFF
                        : FindNearestPaletteIndex(color, palette, preserveTransparency ? 0xFF : -1);
                }

            if (mode == 0)
                return WriteRawIndexes(indexes, width, height, columnMajor);
            if (mode == 1)
                return WriteIndexesWithDepth(indexes, originalFrame, width, height, columnMajor);
            return WriteRleIndexes(indexes, width, height, columnMajor);
        }

        private static byte[] WriteRawIndexes(byte[] indexes, int width, int height, bool columnMajor)
        {
            byte[] encoded = new byte[indexes.Length];
            int offset = 0;
            for (int outer = 0; outer < (columnMajor ? width : height); outer++)
                for (int inner = 0; inner < (columnMajor ? height : width); inner++)
                {
                    int pixel = columnMajor ? inner * width + outer : outer * width + inner;
                    encoded[offset++] = indexes[pixel];
                }
            return encoded;
        }

        private static byte[] WriteIndexesWithDepth(byte[] indexes, byte[] originalFrame,
            int width, int height, bool columnMajor)
        {
            int pixelCount = checked(width * height);
            if (originalFrame.Length < pixelCount * 2)
                throw new InvalidDataException("Mode 1 GRA frame does not contain all pixel/depth pairs.");
            byte[] encoded = (byte[])originalFrame.Clone();
            int offset = 0;
            for (int outer = 0; outer < (columnMajor ? width : height); outer++)
                for (int inner = 0; inner < (columnMajor ? height : width); inner++)
                {
                    int pixel = columnMajor ? inner * width + outer : outer * width + inner;
                    encoded[offset] = indexes[pixel];
                    offset += 2;
                }
            return encoded;
        }

        private static byte[] WriteRleIndexes(byte[] indexes, int width, int height, bool columnMajor)
        {
            using (MemoryStream output = new MemoryStream())
            {
                int outerLimit = columnMajor ? width : height;
                int innerLimit = columnMajor ? height : width;
                for (int outer = 0; outer < outerLimit; outer++)
                {
                    int inner = 0;
                    while (inner < innerLimit)
                    {
                        byte value = GetOrderedPixel(indexes, width, columnMajor, outer, inner);
                        int runLength = 1;
                        while (inner + runLength < innerLimit && runLength < sbyte.MaxValue &&
                            GetOrderedPixel(indexes, width, columnMajor, outer, inner + runLength) == value)
                            runLength++;

                        if (runLength >= 2)
                        {
                            output.WriteByte((byte)runLength);
                            output.WriteByte(value);
                            inner += runLength;
                        }
                        else
                        {
                            int literalLength = 1;
                            while (inner + literalLength < innerLimit && literalLength < 128)
                            {
                                byte next = GetOrderedPixel(indexes, width, columnMajor, outer, inner + literalLength);
                                byte after = inner + literalLength + 1 < innerLimit
                                    ? GetOrderedPixel(indexes, width, columnMajor, outer, inner + literalLength + 1)
                                    : (byte)0;
                                if (inner + literalLength + 1 < innerLimit && next == after)
                                    break;
                                literalLength++;
                            }
                            output.WriteByte(unchecked((byte)(sbyte)-literalLength));
                            for (int i = 0; i < literalLength; i++)
                                output.WriteByte(GetOrderedPixel(indexes, width, columnMajor, outer, inner + i));
                            inner += literalLength;
                        }
                    }
                    output.WriteByte(0);
                }
                return output.ToArray();
            }
        }

        private static byte GetOrderedPixel(byte[] indexes, int width, bool columnMajor, int outer, int inner)
        {
            return indexes[columnMajor ? inner * width + outer : outer * width + inner];
        }

        private static byte FindNearestPaletteIndex(Color target, Color[] palette, int reservedIndex)
        {
            int bestDifference = int.MaxValue;
            byte bestIndex = 0;
            for (int i = 0; i < palette.Length; i++)
            {
                if (i == reservedIndex) continue;
                int red = target.R - palette[i].R;
                int green = target.G - palette[i].G;
                int blue = target.B - palette[i].B;
                int difference = red * red + green * green + blue * blue;
                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    bestIndex = (byte)i;
                }
            }
            return bestIndex;
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            if (offset < 0 || offset > data.Length - 4)
                throw new InvalidDataException("GRA data is truncated.");
            return BitConverter.ToInt32(data, offset);
        }

        private sealed class GraCollection
        {
            public int Index;
            public int Offset;
            public int End;
            public List<GraFrame> Frames;
        }

        private sealed class GraFrame
        {
            public int Index;
            public int Offset;
            public byte[] Data;
        }

        // -------------------------------------------------------
        // Palette helpers
        // -------------------------------------------------------

        private static Color[] ParsePalette(byte[] data)
        {
            Color[] pal = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                int off = PALETTE_OFFSET + i * 2;
                if (off + 1 >= data.Length) { pal[i] = Color.Black; continue; }
                ushort rgb555 = BitConverter.ToUInt16(data, off);
                int r = ((rgb555 >> 10) & 0x1F) * 255 / 31;
                int g = ((rgb555 >>  5) & 0x1F) * 255 / 31;
                int b = ((rgb555      ) & 0x1F) * 255 / 31;
                pal[i] = Color.FromArgb(255, r, g, b);
            }
            return pal;
        }

        // -------------------------------------------------------
        // Frame decoders
        // -------------------------------------------------------

        private static Bitmap DecodeFrame(byte[] data, int frameStart, int w, int h, int mode, Color[] pal, bool cmOverride)
        {
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            int[] pixels = new int[w * h];

            switch (mode)
            {
                case 0: DecodeMode0(data, frameStart, w, h, pal, pixels, cmOverride); break;
                case 1: DecodeMode1(data, frameStart, w, h, pal, pixels, cmOverride); break;
                case 2: DecodeMode2(data, frameStart, w, h, pal, pixels, false, cmOverride); break;
                case 3: DecodeMode2(data, frameStart, w, h, pal, pixels, true, cmOverride);  break;
                case 4: DecodeMode2(data, frameStart, w, h, pal, pixels, true, cmOverride);  break;
                default:
                    Logger.Log($"GraConverter: Unknown mode {mode}");
                    break;
            }

            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bd.Scan0, pixels.Length);
            bmp.UnlockBits(bd);
            return bmp;
        }

        /// <summary>Mode 0: raw uncompressed 8bpp indexed data.</summary>
        private static void DecodeMode0(byte[] data, int start, int w, int h, Color[] pal, int[] pixels, bool cm)
        {
            int src = start;
            if (cm) {
                for (int col = 0; col < w; col++) {
                    for (int row = 0; row < h && src < data.Length; row++, src++) {
                        Color c = pal[data[src]];
                        pixels[row * w + col] = (255 << 24) | (c.R << 16) | (c.G << 8) | c.B;
                    }
                }
            } else {
                for (int row = 0; row < h; row++) {
                    for (int col = 0; col < w && src < data.Length; col++, src++) {
                        Color c = pal[data[src]];
                        pixels[row * w + col] = (255 << 24) | (c.R << 16) | (c.G << 8) | c.B;
                    }
                }
            }
        }

        /// <summary>Mode 1: raw data interleaved with depth (palette, depth, palette, depth...).</summary>
        private static void DecodeMode1(byte[] data, int start, int w, int h, Color[] pal, int[] pixels, bool cm)
        {
            int src = start;
            if (cm) {
                for (int col = 0; col < w; col++) {
                    for (int row = 0; row < h && src + 1 < data.Length; row++, src+=2) {
                        Color c = pal[data[src]];
                        pixels[row * w + col] = (255 << 24) | (c.R << 16) | (c.G << 8) | c.B;
                    }
                }
            } else {
                for (int row = 0; row < h; row++) {
                    for (int col = 0; col < w && src + 1 < data.Length; col++, src+=2) {
                        Color c = pal[data[src]];
                        pixels[row * w + col] = (255 << 24) | (c.R << 16) | (c.G << 8) | c.B;
                    }
                }
            }
        }

        /// <summary>
        /// Modes 2/3/4: column-first RLE compressed data.
        /// cm=true means columns outer loop. cm=false means rows outer loop.
        /// </summary>
        private static void DecodeMode2(byte[] data, int start, int w, int h, Color[] pal, int[] pixels, bool transparentFF, bool cm)
        {
            int src = start;
            int outerLimit = cm ? w : h;
            int innerLimit = cm ? h : w;

            for (int outer = 0; outer < outerLimit && src < data.Length; outer++)
            {
                int inner = 0;
                while (src < data.Length)
                {
                    sbyte ctr = (sbyte)data[src++];
                    if (ctr == 0) break; // end of line

                    if (ctr < 0)
                    {
                        // Literal run
                        int count = (-ctr); 
                        for (int k = 0; k < count && src < data.Length; k++, inner++)
                        {
                            byte idx = data[src++];
                            if (transparentFF && idx == 0xFF) continue;
                            if (inner >= innerLimit) continue; // clamp
                            Color c = pal[idx];
                            int pidx = cm ? (inner * w + outer) : (outer * w + inner);
                            pixels[pidx] = (255 << 24) | (c.R << 16) | (c.G << 8) | c.B;
                        }
                    }
                    else
                    {
                        // RLE run
                        int count = ctr; // If ctr=32, repeat 32 times
                        if (src >= data.Length) break;
                        byte idx = data[src++];
                        bool transparent = transparentFF && idx == 0xFF;
                        for (int k = 0; k < count; k++, inner++)
                        {
                            if (transparent) continue;
                            if (inner >= innerLimit) continue; // clamp rendering but still consume count
                            Color c = pal[idx];
                            int pidx = cm ? (inner * w + outer) : (outer * w + inner);
                            pixels[pidx] = (255 << 24) | (c.R << 16) | (c.G << 8) | c.B;
                        }
                    }
                }
            }
        }
    }
}
