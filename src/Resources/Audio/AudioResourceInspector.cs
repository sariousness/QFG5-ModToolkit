using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QFG5Extractor.Resources.Audio
{
    public sealed class AudioResourceInfo
    {
        public string FullPath { get; private set; }
        public string FileName { get; private set; }
        public string ResourceIds { get; private set; }
        public string HeaderName { get; private set; }
        public string DurationSummary { get; private set; }
        public string HeaderSummary { get; private set; }
        public string FormatSummary { get; private set; }
        public bool IsPlayableWave { get; private set; }

        internal AudioResourceInfo(string fullPath, string resourceIds)
        {
            FullPath = fullPath;
            FileName = Path.GetFileName(fullPath);
            ResourceIds = resourceIds;
            HeaderName = string.Empty;
            DurationSummary = "--";
            HeaderSummary = "Header: not inspected";
            FormatSummary = "Unknown format";
        }

        internal void SetHeader(string headerName, string durationSummary, string headerSummary, string formatSummary, bool playable)
        {
            HeaderName = headerName;
            DurationSummary = durationSummary;
            HeaderSummary = headerSummary;
            FormatSummary = formatSummary;
            IsPlayableWave = playable;
        }
    }

    public static class AudioResourceInspector
    {
        private const int MaxId3ChunkSize = 1024 * 1024;
        private static readonly string[] InfoTagsToDisplay = {
            "INAM", "IART", "IPRD", "ISBJ", "ICMT", "ICOP", "ICRD", "IGNR",
            "IKEY", "ISFT", "ISRC", "ITCH", "IENG", "IMED", "ISRF", "ICMS"
        };

        private sealed class WaveFormat
        {
            public ushort FormatTag;
            public ushort Channels;
            public uint SampleRate;
            public uint ByteRate;
            public ushort BlockAlign;
            public ushort BitsPerSample;
        }

        public static bool IsAudioCandidate(string path)
        {
            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".aud", StringComparison.OrdinalIgnoreCase))
                return true;

            string ignored;
            return TryParseResourceIds(Path.GetFileName(path), out ignored);
        }

        public static AudioResourceInfo Inspect(string path)
        {
            string resourceIds;
            TryParseResourceIds(Path.GetFileName(path), out resourceIds);
            AudioResourceInfo info = new AudioResourceInfo(path, resourceIds);

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII))
            {
                if (stream.Length < 12)
                {
                    info.SetHeader(string.Empty, "--", "Header: file is shorter than a RIFF/WAVE header.", "Unknown format", false);
                    return info;
                }

                string riffId = ReadFourCc(reader);
                uint riffSize = reader.ReadUInt32();
                string waveId = ReadFourCc(reader);
                if (riffId != "RIFF" || waveId != "WAVE")
                {
                    info.SetHeader(string.Empty,
                        "--",
                        "Header: " + riffId + " / " + waveId + " (not a RIFF/WAVE file).",
                        "Unknown format", false);
                    return info;
                }

                long declaredEnd = riffSize + 8L;
                bool malformed = declaredEnd < 12 || declaredEnd > stream.Length;
                long riffEnd = Math.Min(stream.Length, Math.Max(12, declaredEnd));
                WaveFormat waveFormat = null;
                long dataSize = -1;
                List<string> chunkIds = new List<string>();
                Dictionary<string, string> infoTags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string broadcastDescription = string.Empty;
                string cartTitle = string.Empty;
                string id3Title = string.Empty;

                while (stream.Position + 8 <= riffEnd)
                {
                    string chunkId = ReadFourCc(reader);
                    uint chunkSize = reader.ReadUInt32();
                    long chunkStart = stream.Position;
                    long chunkEnd = chunkStart + chunkSize;
                    if (chunkEnd < chunkStart || chunkEnd > riffEnd)
                    {
                        malformed = true;
                        break;
                    }

                    chunkIds.Add(chunkId);
                    if (chunkId == "fmt " && chunkSize >= 16)
                    {
                        waveFormat = new WaveFormat
                        {
                            FormatTag = reader.ReadUInt16(),
                            Channels = reader.ReadUInt16(),
                            SampleRate = reader.ReadUInt32(),
                            ByteRate = reader.ReadUInt32(),
                            BlockAlign = reader.ReadUInt16(),
                            BitsPerSample = reader.ReadUInt16()
                        };
                    }
                    else if (chunkId == "data")
                    {
                        dataSize = chunkSize;
                    }
                    else if (chunkId == "LIST" && chunkSize >= 4)
                    {
                        ReadInfoTags(reader, chunkEnd, infoTags);
                    }
                    else if (chunkId == "bext" && chunkSize >= 256)
                    {
                        broadcastDescription = ReadFixedText(reader, 256);
                    }
                    else if (chunkId == "cart" && chunkSize >= 64)
                    {
                        cartTitle = ReadFixedText(reader, 64);
                    }
                    else if ((chunkId == "ID3 " || chunkId == "id3 ")
                        && chunkSize <= MaxId3ChunkSize)
                    {
                        id3Title = ParseId3Title(reader.ReadBytes((int)chunkSize));
                    }

                    long nextChunk = chunkEnd + (chunkSize & 1);
                    if (nextChunk > riffEnd)
                    {
                        if (chunkEnd != riffEnd)
                            malformed = true;
                        break;
                    }
                    stream.Position = nextChunk;
                }

                string headerName = FirstNonEmpty(
                    GetInfoTag(infoTags, "INAM"),
                    broadcastDescription,
                    id3Title,
                    cartTitle);
                StringBuilder header = new StringBuilder();
                header.Append("Header: RIFF/WAVE; RIFF size ").Append(riffSize).Append(" bytes.");
                header.AppendLine();
                header.Append("Chunks: ").Append(chunkIds.Count == 0 ? "(none)" : string.Join(", ", chunkIds.ToArray())).Append('.');
                AppendInfoMetadata(header, infoTags);
                AppendMetadata(header, "BWF description", broadcastDescription);
                AppendMetadata(header, "ID3 title", id3Title);
                AppendMetadata(header, "CART title", cartTitle);
                if (malformed)
                    header.AppendLine().Append("Warning: declared RIFF/chunk size is truncated or inconsistent.");

                if (waveFormat == null)
                {
                    info.SetHeader(headerName, "--", header.ToString(), "WAVE format chunk missing", false);
                    return info;
                }

                string formatName = GetFormatName(waveFormat.FormatTag);
                StringBuilder format = new StringBuilder();
                string durationSummary = "--";
                format.Append(formatName)
                    .Append(" (0x").Append(waveFormat.FormatTag.ToString("X4")).Append("), ")
                    .Append(waveFormat.Channels).Append(" channel(s), ")
                    .Append(waveFormat.SampleRate).Append(" Hz, ")
                    .Append(waveFormat.BitsPerSample).Append(" bits/sample, ")
                    .Append(waveFormat.ByteRate).Append(" bytes/sec, block align ")
                    .Append(waveFormat.BlockAlign);
                if (dataSize >= 0)
                {
                    format.Append(", ").Append(dataSize).Append(" data bytes");
                    if (waveFormat.ByteRate > 0)
                    {
                        TimeSpan duration = TimeSpan.FromSeconds((double)dataSize / waveFormat.ByteRate);
                        durationSummary = FormatDuration(duration);
                        format.Append(", about ").Append(durationSummary);
                    }
                }

                AppendInfoTag(format, infoTags, "IART", "Artist");
                AppendInfoTag(format, infoTags, "ICMT", "Comment");
                AppendInfoTag(format, infoTags, "IPRD", "Product");

                bool playable = !malformed && dataSize >= 0 && waveFormat.Channels > 0
                    && waveFormat.SampleRate > 0 && waveFormat.BlockAlign > 0;
                info.SetHeader(headerName, durationSummary, header.ToString(), format.ToString(), playable);
                return info;
            }
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
                return duration.ToString(@"d\.hh\:mm\:ss");
            if (duration.TotalHours >= 1)
                return duration.ToString(@"h\:mm\:ss");
            return duration.ToString(@"m\:ss");
        }

        private static bool TryParseResourceIds(string fileName, out string resourceIds)
        {
            resourceIds = string.Empty;
            string name = fileName;
            if (name.EndsWith(".WAV", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".AUD", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);

            if (name.Length != 12 || (name[0] != 'A' && name[0] != 'a') || name[8] != '.')
                return false;

            string qgmId = name.Substring(1, 3);
            string[] ids = {
                name.Substring(4, 2),
                name.Substring(6, 2),
                name.Substring(9, 2),
                name.Substring(11, 1)
            };
            if (!IsBase36(qgmId))
                return false;
            foreach (string id in ids)
            {
                if (!IsBase36(id))
                    return false;
            }

            long qgmValue = ParseBase36(qgmId);
            long[] idValues = new long[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                idValues[i] = ParseBase36(ids[i]);

            resourceIds = string.Format(
                "QGM {0} ({1}); IDs {2}/{3}/{4}/{5} ({6}/{7}/{8}/{9})",
                qgmId, qgmValue, ids[0], ids[1], ids[2], ids[3],
                idValues[0], idValues[1], idValues[2], idValues[3]);
            return true;
        }

        private static bool IsBase36(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char character = char.ToUpperInvariant(value[i]);
                if (!((character >= '0' && character <= '9') || (character >= 'A' && character <= 'Z')))
                    return false;
            }
            return true;
        }

        private static long ParseBase36(string value)
        {
            long result = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char character = char.ToUpperInvariant(value[i]);
                int digit = character >= '0' && character <= '9'
                    ? character - '0'
                    : character - 'A' + 10;
                result = result * 36 + digit;
            }
            return result;
        }

        private static string ReadFourCc(BinaryReader reader)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(4));
        }

        private static void ReadInfoTags(BinaryReader reader, long chunkEnd, Dictionary<string, string> infoTags)
        {
            long listEnd = chunkEnd;
            if (reader.BaseStream.Position + 4 > listEnd)
                return;
            string listType = ReadFourCc(reader);
            if (listType != "INFO")
                return;

            while (reader.BaseStream.Position + 8 <= listEnd)
            {
                string tagId = ReadFourCc(reader);
                uint tagSize = reader.ReadUInt32();
                long valueStart = reader.BaseStream.Position;
                long valueEnd = valueStart + tagSize;
                if (valueEnd < valueStart || valueEnd > listEnd)
                    return;

                if (!infoTags.ContainsKey(tagId) && tagSize > 0 && tagSize <= 4096)
                {
                    byte[] value = reader.ReadBytes((int)tagSize);
                    string text = DecodeText(value);
                    if (!string.IsNullOrWhiteSpace(text))
                        infoTags.Add(tagId, text);
                }
                reader.BaseStream.Position = valueEnd + (tagSize & 1);
            }
        }

        private static string ReadFixedText(BinaryReader reader, int byteCount)
        {
            return DecodeText(reader.ReadBytes(byteCount));
        }

        private static string DecodeText(byte[] bytes)
        {
            int length = bytes.Length;
            if (length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                length = TrimNullCodeUnits(bytes, 2, length, 2);
                return Encoding.Unicode.GetString(bytes, 2, length - 2).Trim();
            }
            if (length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                length = TrimNullCodeUnits(bytes, 2, length, 2);
                return Encoding.BigEndianUnicode.GetString(bytes, 2, length - 2).Trim();
            }
            if (length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                length = TrimTrailingNulls(bytes, 3, length);
                return Encoding.UTF8.GetString(bytes, 3, length - 3).Trim();
            }

            int oddZeroCount = 0;
            int evenZeroCount = 0;
            for (int i = 0; i < length; i++)
            {
                if (bytes[i] != 0)
                    continue;
                if ((i & 1) == 0)
                    evenZeroCount++;
                else
                    oddZeroCount++;
            }
            if (oddZeroCount > length / 4 && oddZeroCount > evenZeroCount * 2)
            {
                length = TrimNullCodeUnits(bytes, 0, bytes.Length, 2);
                return Encoding.Unicode.GetString(bytes, 0, length).Trim();
            }
            if (evenZeroCount > length / 4 && evenZeroCount > oddZeroCount * 2)
            {
                length = TrimNullCodeUnits(bytes, 0, bytes.Length, 2);
                return Encoding.BigEndianUnicode.GetString(bytes, 0, length).Trim();
            }
            length = TrimTrailingNulls(bytes, 0, length);
            if (length == 0)
                return string.Empty;
            return DecodeUtf8OrLatin1(bytes, 0, length);
        }

        private static int TrimNullCodeUnits(byte[] bytes, int offset, int length, int unitSize)
        {
            while (length - offset >= unitSize)
            {
                bool isNullUnit = true;
                for (int i = 0; i < unitSize; i++)
                    isNullUnit &= bytes[length - unitSize + i] == 0;
                if (!isNullUnit)
                    break;
                length -= unitSize;
            }
            return length;
        }

        private static int TrimTrailingNulls(byte[] bytes, int offset, int length)
        {
            while (length > offset && (bytes[length - 1] == 0 || bytes[length - 1] == (byte)' '))
                length--;
            return length;
        }

        private static string ParseId3Title(byte[] bytes)
        {
            if (bytes.Length < 10 || bytes[0] != (byte)'I'
                || bytes[1] != (byte)'D' || bytes[2] != (byte)'3')
                return string.Empty;

            int version = bytes[3];
            if (version < 2 || version > 4)
                return string.Empty;

            int tagSize = ReadSynchsafeInt(bytes, 6);
            int end = Math.Min(bytes.Length, 10 + tagSize);
            int frameHeaderSize = version == 2 ? 6 : 10;
            int position = 10;
            if ((bytes[5] & 0x40) != 0 && version >= 3)
            {
                if (position + 4 > end)
                    return string.Empty;
                int extendedHeaderSize = version == 4
                    ? ReadSynchsafeInt(bytes, position)
                    : ReadBigEndianInt(bytes, position);
                if (extendedHeaderSize < 0 || extendedHeaderSize > end - position - (version == 3 ? 4 : 0))
                    return string.Empty;
                position += extendedHeaderSize + (version == 3 ? 4 : 0);
            }

            while (position + frameHeaderSize <= end)
            {
                int idLength = version == 2 ? 3 : 4;
                if (bytes[position] == 0)
                    break;
                string frameId = Encoding.ASCII.GetString(bytes, position, idLength);
                int frameSize = version == 2
                    ? (bytes[position + 3] << 16) | (bytes[position + 4] << 8) | bytes[position + 5]
                    : version == 4
                        ? ReadSynchsafeInt(bytes, position + 4)
                        : ReadBigEndianInt(bytes, position + 4);
                int frameData = position + frameHeaderSize;
                if (frameSize < 0 || frameSize > end - frameData)
                    break;

                if (frameId == "TIT2" || frameId == "TT2")
                {
                    if (frameSize > 1)
                        return DecodeId3Text(bytes, frameData, frameSize);
                    return string.Empty;
                }
                position = frameData + frameSize;
            }
            return string.Empty;
        }

        private static string DecodeId3Text(byte[] bytes, int offset, int length)
        {
            byte encoding = bytes[offset];
            int textOffset = offset + 1;
            int textLength = length - 1;
            if (encoding == 1)
            {
                if (textLength >= 2 && bytes[textOffset] == 0xFE && bytes[textOffset + 1] == 0xFF)
                    return Encoding.BigEndianUnicode.GetString(bytes, textOffset + 2, textLength - 2).TrimEnd('\0', ' ');
                if (textLength >= 2 && bytes[textOffset] == 0xFF && bytes[textOffset + 1] == 0xFE)
                {
                    textOffset += 2;
                    textLength -= 2;
                }
                return Encoding.Unicode.GetString(bytes, textOffset, textLength).TrimEnd('\0', ' ');
            }
            if (encoding == 2)
                return Encoding.BigEndianUnicode.GetString(bytes, textOffset, textLength).TrimEnd('\0', ' ');
            if (encoding == 3)
                return Encoding.UTF8.GetString(bytes, textOffset, textLength).TrimEnd('\0', ' ');
            return DecodeUtf8OrLatin1(bytes, textOffset, textLength).TrimEnd('\0', ' ');
        }

        private static string DecodeUtf8OrLatin1(byte[] bytes, int offset, int length)
        {
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes, offset, length).Trim();
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(28591).GetString(bytes, offset, length).Trim();
            }
        }

        private static int ReadSynchsafeInt(byte[] bytes, int offset)
        {
            if (offset < 0 || offset + 4 > bytes.Length
                || (bytes[offset] & 0x80) != 0
                || (bytes[offset + 1] & 0x80) != 0
                || (bytes[offset + 2] & 0x80) != 0
                || (bytes[offset + 3] & 0x80) != 0)
                return -1;
            return (bytes[offset] << 21) | (bytes[offset + 1] << 14)
                | (bytes[offset + 2] << 7) | bytes[offset + 3];
        }

        private static int ReadBigEndianInt(byte[] bytes, int offset)
        {
            if (offset < 0 || offset + 4 > bytes.Length)
                return -1;
            uint value = ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16)
                | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
            return value > int.MaxValue ? -1 : (int)value;
        }

        private static void AppendInfoMetadata(StringBuilder output, Dictionary<string, string> infoTags)
        {
            foreach (string tag in InfoTagsToDisplay)
                AppendMetadata(output, "INFO/" + tag, GetInfoTag(infoTags, tag));
        }

        private static void AppendMetadata(StringBuilder output, string label, string value)
        {
            if (!string.IsNullOrEmpty(value))
                output.AppendLine().Append(label).Append(": ").Append(value);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            return string.Empty;
        }

        private static string GetInfoTag(Dictionary<string, string> infoTags, string tag)
        {
            string value;
            return infoTags.TryGetValue(tag, out value) ? value : string.Empty;
        }

        private static void AppendInfoTag(StringBuilder output, Dictionary<string, string> infoTags, string tag, string label)
        {
            string value = GetInfoTag(infoTags, tag);
            if (!string.IsNullOrEmpty(value))
                output.Append(", ").Append(label).Append(": ").Append(value);
        }

        private static string GetFormatName(ushort formatTag)
        {
            switch (formatTag)
            {
                case 0x0001: return "PCM";
                case 0x0002: return "Microsoft ADPCM";
                case 0x0003: return "IEEE float";
                case 0x0011: return "IMA ADPCM";
                default: return "WAVE";
            }
        }
    }
}
