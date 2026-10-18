using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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

        public System.Collections.Generic.List<Bitmap> DecodeAllFrames(string path, int modeOverride = -1, bool cmOverride = false)
        {
            var frames = new System.Collections.Generic.List<Bitmap>();
            try
            {
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < COLLECTION_OFFSETS_START + 4) return frames;

                int mode = modeOverride != -1 ? modeOverride : (int)BitConverter.ToUInt32(data, 0);
                int numCollections = (int)BitConverter.ToUInt32(data, 4);
                if (numCollections <= 0) return frames;

                Color[] palette = ParsePalette(data);

                int col0Offset = (int)BitConverter.ToUInt32(data, COLLECTION_OFFSETS_START);
                if (col0Offset <= 0 || col0Offset >= data.Length - 28) return frames;

                int cW = (int)BitConverter.ToUInt32(data, col0Offset + 8);
                int cH = (int)BitConverter.ToUInt32(data, col0Offset + 12);
                int numSprites = (int)BitConverter.ToUInt32(data, col0Offset + 16);

                if (cW <= 0 || cH <= 0 || cW > 4096 || cH > 4096 || numSprites <= 0) return frames;

                for (int i = 0; i < numSprites; i++)
                {
                    int ptr = col0Offset + 28 + (i * 4);
                    if (ptr + 4 > data.Length) break;
                    int frameRel = (int)BitConverter.ToUInt32(data, ptr);
                    int frameAbs = col0Offset + frameRel;
                    if (frameAbs < data.Length)
                    {
                        Bitmap bmp = DecodeFrame(data, frameAbs, cW, cH, mode, palette, cmOverride);
                        if (bmp != null) frames.Add(bmp);
                    }
                }
            }
            catch (Exception ex) { Logger.LogError("GraConverter", "DecodeAllFrames failed: " + ex.Message); }
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
