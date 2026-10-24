using QFG5Extractor.Core;
using QFG5Extractor.Resources.Model;
using System;
using System.IO;

namespace QFG5Extractor.Resources.Model
{
    public class qfg5modelBitmap
    {
        public static void importBMPfile(string inputFileNameBase, byte[] mdlTextureData, ref byte[] bitmapPalette, out byte[] newmdlTextureData)
        {
            if (mdlTextureData == null || mdlTextureData.Length < Constants.BYTES_PER_INT)
                throw new InvalidDataException("The MDL texture section is missing or incomplete.");
            byte[] mdlBitmapHeaderType = new byte[] { mdlTextureData[0], mdlTextureData[1], mdlTextureData[2], mdlTextureData[3] };
            int mdlBitmapType = (int)((BitConverter.ToUInt32(mdlBitmapHeaderType, 0)) / Constants.MDL_BITMAP_HEADER_TYPE_MULTIPLIER) - 1;
            if (mdlBitmapType < 0 || mdlBitmapType > 31)
                throw new InvalidDataException("The MDL contains an unsupported bitmap level count.");

            int numberOfBytesOfMultiBitmapHeaderAdditionSpace = Constants.BYTES_PER_INT * mdlBitmapType;
            byte[] mdlBitmapHeader = new byte[Constants.BYTES_PER_INT + numberOfBytesOfMultiBitmapHeaderAdditionSpace];
            if (mdlBitmapHeader.Length > mdlTextureData.Length)
                throw new InvalidDataException("The MDL bitmap header is truncated.");
            for (int i = 0; i < Constants.BYTES_PER_INT; i++)
            {
                mdlBitmapHeader[i] = mdlTextureData[i];
            }
            for (int i = 0; i < numberOfBytesOfMultiBitmapHeaderAdditionSpace; i++)
            {
                mdlBitmapHeader[Constants.BYTES_PER_INT + i] = mdlTextureData[Constants.BYTES_PER_INT + i];
            }

            byte[][] newSubBitmapHeaderArray = new byte[mdlBitmapType + 1][];
            byte[][] newSubBitmapPixelDataArray = new byte[mdlBitmapType + 1][];
            int newSubBitmapPixelDataArrayTotalContentsSize = 0;
            int newSubBitmapHeaderArrayTotalContentsSize = 0;

            for (int subBitmap = 0; subBitmap < mdlBitmapType + 1; subBitmap++)
            {
                string inputFileNameSubBitmap = inputFileNameBase;
                if (mdlBitmapType > 0)
                {
                    string mdlBitmapTypeString = qfg5modelShared.convertIntToManagedString(subBitmap);
                    inputFileNameSubBitmap = inputFileNameBase + "-" + mdlBitmapTypeString;
                }
                inputFileNameSubBitmap = inputFileNameSubBitmap + Constants.BMP_EXTENSION;

                if (!File.Exists(inputFileNameSubBitmap))
                {
                    throw new FileNotFoundException("A BMP is required for every MDL texture level.", inputFileNameSubBitmap);
                }

                using (BinaryReader bReader = new BinaryReader(File.Open(inputFileNameSubBitmap, FileMode.Open)))
                {
                    if (bReader.BaseStream.Length < 54 + bitmapPalette.Length)
                        throw new InvalidDataException("The replacement BMP is incomplete: " + inputFileNameSubBitmap);
                    bReader.BaseStream.Seek(10, SeekOrigin.Begin);
                    int pixelDataOffset = bReader.ReadInt32();
                    bReader.BaseStream.Seek(18, SeekOrigin.Begin);
                    int newSubBitmapwidth = bReader.ReadInt32();
                    int newSubBitmapheight = bReader.ReadInt32();
                    bReader.BaseStream.Seek(28, SeekOrigin.Begin);
                    int bitsPerPixel = bReader.ReadUInt16();
                    int compression = bReader.ReadInt32();
                    if (newSubBitmapwidth <= 0 || newSubBitmapheight <= 0 ||
                        bitsPerPixel != 8 || compression != 0 ||
                        pixelDataOffset < 54 + bitmapPalette.Length ||
                        pixelDataOffset > bReader.BaseStream.Length)
                        throw new InvalidDataException("The replacement BMP pixel data is invalid: " + inputFileNameSubBitmap);

                    byte[] newSubBitmapHeader = new byte[Constants.SUBBITMAP_HEADER_LENGTH];
                    byte[] mdlBitmapHeader32bitWidthValueToInsert;
                    byte[] mdlBitmapHeader32bitHeightValueToInsert;

                    mdlBitmapHeader32bitWidthValueToInsert = BitConverter.GetBytes((float)newSubBitmapwidth);
                    mdlBitmapHeader32bitHeightValueToInsert = BitConverter.GetBytes((float)newSubBitmapheight);
                    insertByteArray(newSubBitmapHeader, mdlBitmapHeader32bitWidthValueToInsert, Constants.SUBBITMAP_HEADER_WIDTH_FLOAT_ADDRESS);
                    insertByteArray(newSubBitmapHeader, mdlBitmapHeader32bitHeightValueToInsert, Constants.SUBBITMAP_HEADER_HEIGHT_FLOAT_ADDRESS);

                    mdlBitmapHeader32bitWidthValueToInsert = BitConverter.GetBytes((uint)calculatePowerOf2(newSubBitmapwidth));
                    mdlBitmapHeader32bitHeightValueToInsert = BitConverter.GetBytes((uint)calculatePowerOf2(newSubBitmapheight));
                    insertByteArray(newSubBitmapHeader, mdlBitmapHeader32bitWidthValueToInsert, Constants.SUBBITMAP_HEADER_WIDTH_POWER_OF_TWO_ADDRESS);
                    insertByteArray(newSubBitmapHeader, mdlBitmapHeader32bitHeightValueToInsert, Constants.SUBBITMAP_HEADER_HEIGHT_POWER_OF_TWO_ADDRESS);

                    mdlBitmapHeader32bitWidthValueToInsert = BitConverter.GetBytes((uint)(newSubBitmapwidth - 1));
                    mdlBitmapHeader32bitHeightValueToInsert = BitConverter.GetBytes((uint)(newSubBitmapheight - 1));
                    insertByteArray(newSubBitmapHeader, mdlBitmapHeader32bitWidthValueToInsert, Constants.SUBBITMAP_HEADER_WIDTH_MINUS_ONE_ADDRESS);
                    insertByteArray(newSubBitmapHeader, mdlBitmapHeader32bitHeightValueToInsert, Constants.SUBBITMAP_HEADER_HEIGHT_MINUS_ONE_ADDRESS);

                    newSubBitmapHeaderArray[subBitmap] = newSubBitmapHeader;
                    newSubBitmapHeaderArrayTotalContentsSize += newSubBitmapHeader.Length;

                    bReader.BaseStream.Seek(54, SeekOrigin.Begin);
                    byte[] newSubBitmapPalette = bReader.ReadBytes(bitmapPalette.Length);
                    int bmpRowStride = checked(((newSubBitmapwidth + 3) / 4) * 4);
                    long bmpPixelDataLength = checked((long)bmpRowStride * newSubBitmapheight);
                    if (newSubBitmapPalette.Length != bitmapPalette.Length ||
                        pixelDataOffset + bmpPixelDataLength > bReader.BaseStream.Length)
                        throw new InvalidDataException("The replacement BMP data is truncated: " + inputFileNameSubBitmap);

                    bReader.BaseStream.Seek(pixelDataOffset, SeekOrigin.Begin);
                    byte[] bmpPixelRows = bReader.ReadBytes((int)bmpPixelDataLength);
                    int pixelDataSize = checked(newSubBitmapwidth * newSubBitmapheight);
                    byte[] newSubBitmapPixelData = new byte[pixelDataSize];
                    for (int row = 0; row < newSubBitmapheight; row++)
                        Array.Copy(bmpPixelRows, row * bmpRowStride, newSubBitmapPixelData, row * newSubBitmapwidth, newSubBitmapwidth);
                    if (newSubBitmapPalette.Length != bitmapPalette.Length ||
                        newSubBitmapPixelData.Length != pixelDataSize)
                        throw new InvalidDataException("The replacement BMP data is truncated: " + inputFileNameSubBitmap);
                    newSubBitmapPixelDataArray[subBitmap] = newSubBitmapPixelData;
                    newSubBitmapPixelDataArrayTotalContentsSize += newSubBitmapPixelData.Length;

                    swapPalette(newSubBitmapPalette);
                    bitmapPalette = newSubBitmapPalette;
                }
            }

            newmdlTextureData = new byte[mdlBitmapHeader.Length + newSubBitmapHeaderArrayTotalContentsSize + newSubBitmapPixelDataArrayTotalContentsSize];
            int positionInNewMdlTextureData = 0;
            for (int i = 0; i < mdlBitmapHeader.Length; i++)
            {
                newmdlTextureData[positionInNewMdlTextureData] = mdlBitmapHeader[i];
                positionInNewMdlTextureData++;
            }
            for (int subBitmap = 0; subBitmap < mdlBitmapType + 1; subBitmap++)
            {
                byte[] newSubBitmapHeader = newSubBitmapHeaderArray[subBitmap];
                byte[] newSubBitmapPixelData = newSubBitmapPixelDataArray[subBitmap];
                
                if (newSubBitmapHeader == null || newSubBitmapPixelData == null) continue;

                for (int i = 0; i < newSubBitmapHeader.Length; i++)
                {
                    newmdlTextureData[positionInNewMdlTextureData] = newSubBitmapHeader[i];
                    positionInNewMdlTextureData++;
                }
                for (int i = 0; i < newSubBitmapPixelData.Length; i++)
                {
                    newmdlTextureData[positionInNewMdlTextureData] = newSubBitmapPixelData[i];
                    positionInNewMdlTextureData++;
                }
            }
        }

        public static void exportBMPfile(string inputFileName, string outputFileNameBase)
        {
            int mdlPaletteAddress = Constants.MDL_DISPLAY_PALETTE_ADDRESS;
            int mdlBitmapHeaderAddress;

            byte[] mdlPalette = new byte[Constants.BITMAP_DISPLAY_PALETTE_LENGTH];

            using (BinaryReader bReader = new BinaryReader(File.Open(inputFileName, FileMode.Open)))
            {
                bReader.BaseStream.Seek(mdlPaletteAddress, SeekOrigin.Begin);
                mdlPalette = bReader.ReadBytes(mdlPalette.Length);
                swapPalette(mdlPalette);

                mdlBitmapHeaderAddress = (int)bReader.ReadUInt32();

                if (mdlBitmapHeaderAddress < 0 || mdlBitmapHeaderAddress + 4 > bReader.BaseStream.Length)
                {
                    throw new Exception($"Invalid bitmap header address in MDL file. (Address read: {mdlBitmapHeaderAddress}, File Length: {bReader.BaseStream.Length})");
                }

                bReader.BaseStream.Seek(mdlBitmapHeaderAddress, SeekOrigin.Begin);
                byte[] mdlBitmapHeaderType = bReader.ReadBytes(4);
                if (mdlBitmapHeaderType.Length < 4)
                {
                    throw new Exception("Could not read enough bytes for bitmap header type.");
                }

                int mdlBitmapType = (int)((BitConverter.ToUInt32(mdlBitmapHeaderType, 0)) / Constants.MDL_BITMAP_HEADER_TYPE_MULTIPLIER) - 1;

                int numberOfBytesOfMultiBitmapHeaderAdditionSpace = Constants.BYTES_PER_INT * mdlBitmapType;
                bReader.BaseStream.Seek(numberOfBytesOfMultiBitmapHeaderAdditionSpace, SeekOrigin.Current);

                for (int subBitmap = 0; subBitmap < mdlBitmapType + 1; subBitmap++)
                {
                    if (Constants.SUBBITMAP_HEADER_LENGTH > bReader.BaseStream.Length - bReader.BaseStream.Position) break;
                    byte[] SubBitmapHeader = bReader.ReadBytes(Constants.SUBBITMAP_HEADER_LENGTH);
                    byte[] temp1 = new byte[] { SubBitmapHeader[Constants.SUBBITMAP_HEADER_WIDTH_MINUS_ONE_ADDRESS + 0], SubBitmapHeader[Constants.SUBBITMAP_HEADER_WIDTH_MINUS_ONE_ADDRESS + 1], SubBitmapHeader[Constants.SUBBITMAP_HEADER_WIDTH_MINUS_ONE_ADDRESS + 2], SubBitmapHeader[Constants.SUBBITMAP_HEADER_WIDTH_MINUS_ONE_ADDRESS + 3] };
                    byte[] temp2 = new byte[] { SubBitmapHeader[Constants.SUBBITMAP_HEADER_HEIGHT_MINUS_ONE_ADDRESS + 0], SubBitmapHeader[Constants.SUBBITMAP_HEADER_HEIGHT_MINUS_ONE_ADDRESS + 1], SubBitmapHeader[Constants.SUBBITMAP_HEADER_HEIGHT_MINUS_ONE_ADDRESS + 2], SubBitmapHeader[Constants.SUBBITMAP_HEADER_HEIGHT_MINUS_ONE_ADDRESS + 3] };
                    int SubBitmapwidth = (int)BitConverter.ToUInt32(temp1, 0) + 1;
                    int SubBitmapheight = (int)BitConverter.ToUInt32(temp2, 0) + 1;

                    long pixelDataSize = (long)SubBitmapwidth * SubBitmapheight;
                    if (pixelDataSize > bReader.BaseStream.Length - bReader.BaseStream.Position) break;
                    byte[] SubBitmapPixelData = bReader.ReadBytes((int)pixelDataSize);

                    string outputFileNameSubBitmap = outputFileNameBase;
                    if (mdlBitmapType > 0)
                    {
                        string mdlBitmapTypeString = qfg5modelShared.convertIntToManagedString(subBitmap);
                        outputFileNameSubBitmap = outputFileNameBase + "-" + mdlBitmapTypeString;
                    }
                    outputFileNameSubBitmap = outputFileNameSubBitmap + Constants.BMP_EXTENSION;

                    writeBMP(outputFileNameSubBitmap, mdlPalette, SubBitmapPixelData, SubBitmapwidth, SubBitmapheight);
                }
            }
        }

        private static void writeBMP(string outputFileName, byte[] mdlPalette, byte[] pixelData, int bitmapwidth, int bitmapheight)
        {
            int rowStride = ((bitmapwidth + 3) / 4) * 4;
            int imageSize = rowStride * bitmapheight;
            using (BinaryWriter bWriter = new BinaryWriter(File.Open(outputFileName, FileMode.Create)))
            {
                byte[] temp3 = new byte[] { 0x42, 0x4d };
                bWriter.Write(temp3);
                bWriter.Write(54 + 1024 + imageSize);
                bWriter.Write((ushort)0);
                bWriter.Write((ushort)0);
                bWriter.Write(54 + 1024);
                bWriter.Write(40);
                bWriter.Write(bitmapwidth);
                bWriter.Write(bitmapheight);
                bWriter.Write((ushort)1);
                bWriter.Write((ushort)8);
                bWriter.Write(0);
                bWriter.Write(imageSize);
                bWriter.Write(2834);
                bWriter.Write(2834);
                bWriter.Write(256);
                bWriter.Write(0);
                bWriter.Write(mdlPalette);
                for (int i = 0; i < 1024 - mdlPalette.Length; i++)
                {
                    bWriter.Write((byte)0);
                }
                for (int row = 0; row < bitmapheight; row++)
                {
                    bWriter.Write(pixelData, row * bitmapwidth, bitmapwidth);
                    for (int padding = bitmapwidth; padding < rowStride; padding++)
                        bWriter.Write((byte)0);
                }
            }
        }

        private static void insertByteArray(byte[] byteArrayToUpdate, byte[] byteArrayToInsert, int index)
        {
            for (int i = 0; i < byteArrayToInsert.Length; i++)
            {
                byteArrayToUpdate[i + index] = byteArrayToInsert[i];
            }
        }

        private static int calculatePowerOf2(int value)
        {
            int powerOf2 = 0;
            while (((value % 2) == 0) && (value > 1))
            {
                value = value / 2;
                powerOf2++;
            }
            return powerOf2;
        }

        private static void swapPalette(byte[] palette)
        {
            byte[] tmpPalette = new byte[Constants.BITMAP_PALETTE_LENGTH];

            for (int i = 0; i < Constants.BITMAP_PALETTE_LENGTH; i += 4)
            {
                tmpPalette[i] = palette[i + 2];
                tmpPalette[i + 1] = palette[i + 1];
                tmpPalette[i + 2] = palette[i];
                if ((i + 4) < Constants.BITMAP_PALETTE_LENGTH)
                {
                    tmpPalette[i + 3] = 0;
                }
            }

            for (int i = 0; i < Constants.BITMAP_PALETTE_LENGTH; i++)
            {
                palette[i] = tmpPalette[i];
            }
        }
    }
}
