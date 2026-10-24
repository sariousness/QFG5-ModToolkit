using OpenTK;
using System;
using System.IO;

namespace QFG5Extractor.Resources.Model
{
    public static class AnmAnimationLoader
    {
        private const int HeaderOffset = 0x18;
        private const int FrameDataOffset = 0x2C;
        private const int BoneMatrixDataLength = 48;
        private const int BoneMatrixLength = 56;

        public static Matrix4[][] LoadFrames(string filePath, int expectedBoneCount)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("An animation file path is required.", "filePath");
            if (expectedBoneCount <= 0)
                throw new ArgumentOutOfRangeException("expectedBoneCount");

            using (BinaryReader reader = new BinaryReader(
                File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                if (reader.BaseStream.Length < FrameDataOffset)
                    throw new InvalidDataException("The ANM file is too short to contain its animation header.");

                reader.BaseStream.Seek(HeaderOffset, SeekOrigin.Begin);
                int boneCount = reader.ReadInt32();
                int frameCount = reader.ReadInt32();

                if (boneCount != expectedBoneCount)
                    throw new InvalidDataException(
                        "The ANM contains " + boneCount + " bones; the MDL contains " +
                        expectedBoneCount + " submeshes.");
                if (frameCount <= 0)
                    throw new InvalidDataException("The ANM contains no animation frames.");

                long boneRecordCount = (long)frameCount * boneCount;
                long requiredLength = FrameDataOffset +
                    (boneRecordCount - 1) * BoneMatrixLength + BoneMatrixDataLength;
                if (requiredLength > reader.BaseStream.Length)
                    throw new InvalidDataException("The ANM frame data is incomplete.");

                reader.BaseStream.Seek(FrameDataOffset, SeekOrigin.Begin);
                Matrix4[][] frames = new Matrix4[frameCount][];
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    frames[frameIndex] = new Matrix4[boneCount];
                    for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
                        frames[frameIndex][boneIndex] = ReadBoneMatrix(reader);
                }

                return frames;
            }
        }

        private static Matrix4 ReadBoneMatrix(BinaryReader reader)
        {
            float tx = reader.ReadSingle();
            float ty = reader.ReadSingle();
            float tz = reader.ReadSingle();

            float m11 = reader.ReadSingle(); float m12 = reader.ReadSingle(); float m13 = reader.ReadSingle();
            float m21 = reader.ReadSingle(); float m22 = reader.ReadSingle(); float m23 = reader.ReadSingle();
            float m31 = reader.ReadSingle(); float m32 = reader.ReadSingle(); float m33 = reader.ReadSingle();

            Matrix4 matrix = new Matrix4(
                m11, m21, m31, 0,
                m12, m22, m32, 0,
                m13, m23, m33, 0,
                tx, ty, tz, 1
            );
            if (reader.BaseStream.Length - reader.BaseStream.Position >= 8)
                reader.BaseStream.Seek(8, SeekOrigin.Current);
            return matrix;
        }
    }
}
