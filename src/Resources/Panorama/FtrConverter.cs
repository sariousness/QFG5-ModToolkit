using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace QFG5Extractor.Resources.Panorama
{
    public sealed class FtrRegion
    {
        public int Index { get; private set; }
        public short Depth { get; private set; }
        public short Unknown { get; private set; }
        public short PortalFlag { get; private set; }
        public IList<Point> Points { get; private set; }

        internal FtrRegion(int index, short depth, short unknown, short portalFlag,
            IList<Point> points)
        {
            Index = index;
            Depth = depth;
            Unknown = unknown;
            PortalFlag = portalFlag;
            Points = points;
        }
    }

    public static class FtrConverter
    {
        public static IList<FtrRegion> Read(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("An FTR file path is required.", "path");
            return Parse(File.ReadAllBytes(path));
        }

        public static IList<FtrRegion> Parse(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");
            if (data.Length < 2)
                throw new InvalidDataException("The FTR file is too short to contain a region count.");

            int regionCount = ReadInt16(data, 0);
            if (regionCount < 0)
                throw new InvalidDataException("The FTR region count is negative.");

            List<FtrRegion> regions = new List<FtrRegion>(regionCount);
            int offset = 2;
            for (int i = 0; i < regionCount; i++)
            {
                EnsureRange(data, offset, 8, "region header");
                short depth = ReadInt16(data, offset);
                short unknown = ReadInt16(data, offset + 2);
                short portalFlag = ReadInt16(data, offset + 4);
                short pointCount = ReadInt16(data, offset + 6);
                offset += 8;

                if (pointCount < 0)
                    throw new InvalidDataException("FTR region " + i + " has a negative point count.");

                int pointBytes = checked(pointCount * 4);
                EnsureRange(data, offset, pointBytes, "region points");
                List<Point> points = new List<Point>(pointCount);
                for (int point = 0; point < pointCount; point++)
                {
                    points.Add(new Point(
                        ReadInt16(data, offset + point * 4),
                        ReadInt16(data, offset + point * 4 + 2)));
                }

                offset += pointBytes;
                regions.Add(new FtrRegion(i, depth, unknown, portalFlag, points));
            }

            if (offset != data.Length)
                throw new InvalidDataException("The FTR file contains unexpected trailing data.");

            return regions.AsReadOnly();
        }

        private static short ReadInt16(byte[] data, int offset)
        {
            return unchecked((short)(data[offset] | (data[offset + 1] << 8)));
        }

        private static void EnsureRange(byte[] data, int offset, int length, string section)
        {
            if (offset < 0 || length < 0 || offset > data.Length - length)
                throw new InvalidDataException("The FTR file ends inside its " + section + ".");
        }
    }
}
