using QFG5Extractor.Core;
using System;
using System.IO;
using System.Text;
using QFG5Extractor.Resources.Panorama;

namespace QFG5Extractor.Resources.Save
{
    public struct CharacterPlacementInfo
    {
        public float X { get; private set; }
        public uint XRaw { get; private set; }
        public float Y { get; private set; }
        public uint YRaw { get; private set; }
        public float Z { get; private set; }
        public uint ZRaw { get; private set; }
        public float Rotation { get; private set; }
        public uint RotationRaw { get; private set; }
        public float Size { get; private set; }
        public uint SizeRaw { get; private set; }

        internal CharacterPlacementInfo(float x, uint xRaw, float y, uint yRaw,
            float z, uint zRaw, float rotation, uint rotationRaw, float size, uint sizeRaw)
        {
            X = x;
            XRaw = xRaw;
            Y = y;
            YRaw = yRaw;
            Z = z;
            ZRaw = zRaw;
            Rotation = rotation;
            RotationRaw = rotationRaw;
            Size = size;
            SizeRaw = sizeRaw;
        }
    }

    public sealed class SaveSession
    {
        private const int MinimumSaveLength = 0x125C;
        private const int InventorySlotSize = SaveGameParser.INVENTORY_SLOT_SIZE;
        private const int InventorySlotCount = SaveGameParser.INVENTORY_POOL_SIZE;
        private const int SaveHeaderLength = 18;
        private const int PlayerTypeLength = 4;
        private const int PlayerStateLength = 1096;
        private const int EngineStateLength = 1536;
        private const int ObjectStateRoomCount = 133;
        private const int MaximumFlagCount = 730;
        private const int MinimumNoiseLength = 50;
        private const int MaximumNoiseLength = 150;
        private const int CharacterPlacementRecordLength = 32;
        private const int CharacterXOffset = 4;
        private const int CharacterYOffset = 8;
        private const int CharacterZOffset = 12;
        private const int CharacterRotationOffset = 16;
        private const int CharacterSizeOffset = 28;
        private const int CharacterModelOffsetBeforeRoomLabel = 1966;
        private static readonly byte[] CharacterModelMarker = { 0x81, 0x01, 0x00, 0x00 };
        private static readonly byte[] RoomLabelMarker = Encoding.Unicode.GetBytes("Quest ");
        private byte[] saveData;
        private byte[] loadedFileSnapshot;
        private int characterModelOffset = -1;
        private int characterPlacementOffset = -1;
        private int roomIdOffset = -1;
        private int flagArrayOffset = -1;
        private bool hasIncompleteCharacterRecord;

        public string FilePath { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsLoaded { get { return saveData != null; } }
        public int Length { get { return saveData == null ? 0 : saveData.Length; } }
        public int OriginalLength { get; private set; }
        public bool IsLengthPreserved
        {
            get
            {
                return saveData != null && loadedFileSnapshot != null &&
                    saveData.Length == OriginalLength &&
                    loadedFileSnapshot.Length == OriginalLength;
            }
        }
        public int InventoryOffset { get; private set; }
        public int InventoryCount { get; private set; }
        public bool CanEditFlags
        {
            get { return IsLoaded && flagArrayOffset >= 0 && !hasIncompleteCharacterRecord; }
        }
        public string FlagsUnavailableReason { get; private set; }
        public bool HasIncompleteCharacterRecord { get { return hasIncompleteCharacterRecord; } }
        public bool CanEditCharacterData { get { return IsLoaded && !hasIncompleteCharacterRecord; } }
        public string CharacterDataUnavailableReason { get; private set; }
        public string CharacterModelUnavailableReason { get; private set; }
        public string CharacterPlacementUnavailableReason { get; private set; }
        public string RoomUnavailableReason { get; private set; }

        public byte GetByte(int index)
        {
            EnsureLoaded();
            if (!HasRange(index, 1)) throw new ArgumentOutOfRangeException("index");
            return saveData[index];
        }

        public void SetByte(int index, byte value)
        {
            EnsureLoaded();
            if (!HasRange(index, 1)) throw new ArgumentOutOfRangeException("index");
            if (saveData[index] != value)
            {
                saveData[index] = value;
                IsDirty = true;
            }
        }

        public void CopyWithin(int sourceIndex, int destinationIndex, int length)
        {
            EnsureLoaded();
            if (!HasRange(sourceIndex, length)) throw new ArgumentOutOfRangeException("sourceIndex");
            if (!HasRange(destinationIndex, length)) throw new ArgumentOutOfRangeException("destinationIndex");

            bool changesData = false;
            for (int i = 0; i < length; i++)
            {
                if (saveData[sourceIndex + i] != saveData[destinationIndex + i])
                {
                    changesData = true;
                    break;
                }
            }

            Array.Copy(saveData, sourceIndex, saveData, destinationIndex, length);
            if (changesData) IsDirty = true;
        }

        public void Clear(int index, int length)
        {
            EnsureLoaded();
            if (!HasRange(index, length)) throw new ArgumentOutOfRangeException("index");
            for (int i = index; i < index + length; i++)
            {
                if (saveData[i] != 0)
                {
                    Array.Clear(saveData, index, length);
                    IsDirty = true;
                    return;
                }
            }
        }

        public bool TryLoad(string path, Func<bool> approveReplacingDirtyDocument)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("A save-file path is required.", "path");

            string fullPath = Path.GetFullPath(path);
            byte[] candidate = ReadCandidate(fullPath);
            int candidateInventoryOffset = ValidateInventory(candidate);
            int candidateFlagArrayOffset = FindFlagArrayOffset(candidate, out string flagArrayError);
            int candidateCharacterModelOffset = FindCharacterModelOffset(candidate, out string characterModelError);
            int candidateCharacterPlacementOffset = FindCharacterPlacementOffset(candidate,
                candidateCharacterModelOffset, out string characterPlacementError);
            int candidateRoomIdOffset = FindRoomIdOffset(candidate, out string roomError);
            bool candidateHasIncompleteCharacterRecord =
                FindIncompleteRoomLabelOffset(candidate) >= 0;

            if (IsDirty)
            {
                if (approveReplacingDirtyDocument == null || !approveReplacingDirtyDocument())
                    return false;

                candidate = ReadCandidate(fullPath);
                candidateInventoryOffset = ValidateInventory(candidate);
                candidateFlagArrayOffset = FindFlagArrayOffset(candidate, out flagArrayError);
                candidateCharacterModelOffset = FindCharacterModelOffset(candidate, out characterModelError);
                candidateCharacterPlacementOffset = FindCharacterPlacementOffset(candidate,
                    candidateCharacterModelOffset, out characterPlacementError);
                candidateRoomIdOffset = FindRoomIdOffset(candidate, out roomError);
                candidateHasIncompleteCharacterRecord =
                    FindIncompleteRoomLabelOffset(candidate) >= 0;
            }

            saveData = candidate;
            loadedFileSnapshot = (byte[])candidate.Clone();
            FilePath = fullPath;
            OriginalLength = candidate.Length;
            InventoryOffset = candidateInventoryOffset;
            InventoryCount = ReadInventoryCount(candidate);
            flagArrayOffset = candidateFlagArrayOffset;
            FlagsUnavailableReason = flagArrayError;
            characterModelOffset = candidateCharacterModelOffset;
            CharacterModelUnavailableReason = characterModelError;
            characterPlacementOffset = candidateCharacterPlacementOffset;
            CharacterPlacementUnavailableReason = characterPlacementError;
            roomIdOffset = candidateRoomIdOffset;
            RoomUnavailableReason = roomError;
            hasIncompleteCharacterRecord = candidateHasIncompleteCharacterRecord;
            CharacterDataUnavailableReason = hasIncompleteCharacterRecord
                ? "The save appears truncated: its character room label stops at 'Qu'. Character fields are read-only; restore the backup or re-save in game."
                : null;
            IsDirty = false;
            return true;
        }

        public bool TryGetFlag(int index, int flagCount, out bool value)
        {
            value = false;
            if (!IsLoaded || flagArrayOffset < 0 ||
                flagCount <= 0 || flagCount > MaximumFlagCount ||
                index < 0 || index >= flagCount)
                return false;

            int byteOffset = flagArrayOffset + (index / 8);
            if (byteOffset >= flagArrayOffset + EngineStateLength || !HasRange(byteOffset, 1))
                return false;

            value = (saveData[byteOffset] & (1 << (index % 8))) != 0;
            return true;
        }

        public void SetFlag(int index, int flagCount, bool value)
        {
            EnsureLoaded();
            if (hasIncompleteCharacterRecord)
                throw new InvalidOperationException(CharacterDataUnavailableReason);
            if (flagArrayOffset < 0)
                throw new InvalidOperationException(FlagsUnavailableReason ??
                    "The flag array could not be located safely in this save.");
            if (flagCount <= 0 || flagCount > MaximumFlagCount)
                throw new ArgumentOutOfRangeException("flagCount", "The flag count exceeds the supported QFG5 flag array.");
            if (index < 0 || index >= flagCount)
                throw new ArgumentOutOfRangeException("index", "The flag index is outside the loaded flag-name list.");

            int byteOffset = flagArrayOffset + (index / 8);
            int bitMask = 1 << (index % 8);
            byte currentValue = saveData[byteOffset];
            byte updatedValue = value
                ? (byte)(currentValue | bitMask)
                : (byte)(currentValue & ~bitMask);
            SetByte(byteOffset, updatedValue);
        }

        public bool TryGetCharacterModelId(out int modelId)
        {
            EnsureLoaded();
            modelId = 0;
            if (characterModelOffset < 0 || !HasRange(characterModelOffset, 4))
                return false;

            int value = ReadInt32(saveData, characterModelOffset);
            if (value < 0 || value > byte.MaxValue)
                return false;

            modelId = value;
            return true;
        }

        public void SetCharacterModelId(int modelId)
        {
            EnsureLoaded();
            if (modelId < 0 || modelId > byte.MaxValue)
                throw new ArgumentOutOfRangeException("modelId", "Character model IDs must be between 0 and 255.");
            if (hasIncompleteCharacterRecord)
                throw new InvalidOperationException(CharacterDataUnavailableReason);
            if (characterModelOffset < 0 || !HasRange(characterModelOffset, 4))
                throw new InvalidOperationException(CharacterModelUnavailableReason ??
                    "The character model field could not be located safely in this save.");
            if (!TryGetCharacterModelId(out int currentModelId))
                throw new InvalidOperationException("The character model field contains an unsupported value.");
            if (currentModelId == modelId) return;

            SetByte(characterModelOffset, (byte)modelId);
            SetByte(characterModelOffset + 1, 0);
            SetByte(characterModelOffset + 2, 0);
            SetByte(characterModelOffset + 3, 0);
        }

        public bool TryGetCharacterPlacement(out CharacterPlacementInfo placement)
        {
            EnsureLoaded();
            placement = default(CharacterPlacementInfo);
            if (characterPlacementOffset < 0 ||
                !HasRange(characterPlacementOffset, CharacterPlacementRecordLength))
                return false;

            int offset = characterPlacementOffset;
            uint xRaw = unchecked((uint)ReadInt32(saveData, offset + CharacterXOffset));
            uint yRaw = unchecked((uint)ReadInt32(saveData, offset + CharacterYOffset));
            uint zRaw = unchecked((uint)ReadInt32(saveData, offset + CharacterZOffset));
            uint rotationRaw = unchecked((uint)ReadInt32(saveData, offset + CharacterRotationOffset));
            uint sizeRaw = unchecked((uint)ReadInt32(saveData, offset + CharacterSizeOffset));
            float x = ReadSingleLittleEndian(saveData, offset + CharacterXOffset);
            float y = ReadSingleLittleEndian(saveData, offset + CharacterYOffset);
            float z = ReadSingleLittleEndian(saveData, offset + CharacterZOffset);
            float rotation = ReadSingleLittleEndian(saveData, offset + CharacterRotationOffset);
            float size = ReadSingleLittleEndian(saveData, offset + CharacterSizeOffset);
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z) ||
                !IsFinite(rotation) || !IsFinite(size))
                return false;

            placement = new CharacterPlacementInfo(x, xRaw, y, yRaw, z, zRaw,
                rotation, rotationRaw, size, sizeRaw);
            return true;
        }

        public bool TryGetRoomId(out int roomId)
        {
            EnsureLoaded();
            roomId = 0;
            if (roomIdOffset < 0 || !HasRange(roomIdOffset, 4))
                return false;

            int value = ReadInt32(saveData, roomIdOffset);
            RoomLocationInfo location;
            if (value < 0 || value > ushort.MaxValue ||
                !RoomLocationCatalog.TryGet(value, out location))
                return false;

            roomId = value;
            return true;
        }

        public void SetRoomId(int roomId)
        {
            EnsureLoaded();
            if (roomId < 0 || roomId > ushort.MaxValue)
                throw new ArgumentOutOfRangeException("roomId", "Room IDs must be between 0 and 65535.");
            if (hasIncompleteCharacterRecord)
                throw new InvalidOperationException(CharacterDataUnavailableReason);
            RoomLocationInfo location;
            if (!RoomLocationCatalog.TryGet(roomId, out location))
                throw new ArgumentOutOfRangeException("roomId", "Choose a room ID from the supported room catalog.");
            if (roomIdOffset < 0 || !HasRange(roomIdOffset, 4))
                throw new InvalidOperationException(RoomUnavailableReason ??
                    "The room field could not be located safely in this save.");
            if (!TryGetRoomId(out int currentRoomId))
                throw new InvalidOperationException("The room field contains an unsupported value.");
            if (currentRoomId == roomId) return;

            SetByte(roomIdOffset, (byte)(roomId & 0xFF));
            SetByte(roomIdOffset + 1, (byte)((roomId >> 8) & 0xFF));
            SetByte(roomIdOffset + 2, 0);
            SetByte(roomIdOffset + 3, 0);
        }

        private static int FindCharacterModelOffset(byte[] candidate, out string error)
        {
            int markerOffset = -1;
            for (int i = 0; i <= candidate.Length - CharacterModelMarker.Length; i++)
            {
                if (!HasBytes(candidate, i, CharacterModelMarker))
                    continue;
                if (markerOffset >= 0)
                {
                    markerOffset = -2;
                    break;
                }
                markerOffset = i;
            }

            int markedModelOffset = markerOffset >= 0
                ? markerOffset + CharacterModelMarker.Length
                : -1;
            bool markerRecordValid = markedModelOffset >= 0 &&
                IsSupportedCharacterModelRecord(candidate, markedModelOffset);

            int roomLabelOffset = FindUniqueRoomLabelOffset(candidate, out string roomLabelError);
            if (roomLabelOffset < 0 &&
                roomLabelError == "A supported room record was not found in this save.")
            {
                int incompleteLabelOffset = FindIncompleteRoomLabelOffset(candidate);
                if (incompleteLabelOffset >= 0)
                {
                    error = null;
                    return incompleteLabelOffset - CharacterModelOffsetBeforeRoomLabel;
                }
            }

            if (roomLabelOffset >= 0)
            {
                int roomRelativeModelOffset = roomLabelOffset - CharacterModelOffsetBeforeRoomLabel;
                bool roomRelativeRecordValid =
                    IsSupportedCharacterModelRecord(candidate, roomRelativeModelOffset);

                if (markerRecordValid)
                {
                    if (markedModelOffset != roomRelativeModelOffset && roomRelativeRecordValid)
                    {
                        error = "The character model marker and room-relative record point to different fields. Editing is disabled.";
                        return -1;
                    }

                    if (markedModelOffset == roomRelativeModelOffset || !roomRelativeRecordValid)
                    {
                        error = null;
                        return markedModelOffset;
                    }
                }

                if (roomRelativeRecordValid)
                {
                    error = null;
                    return roomRelativeModelOffset;
                }

                error = "The character model field near the room record could not be validated safely.";
                return -1;
            }

            if (markerRecordValid)
            {
                error = null;
                return markedModelOffset;
            }

            if (markerOffset == -2)
            {
                error = "Multiple character model markers were found and no unique room-relative character record could be validated.";
                return -1;
            }

            error = markerOffset < 0
                ? "A supported character model record was not found. " + roomLabelError
                : "The character model marker contains an unsupported value, and no valid room-relative character record was found.";
            return -1;
        }

        private static bool IsSupportedCharacterModelRecord(byte[] candidate, int modelOffset)
        {
            if (!HasRange(candidate, modelOffset, CharacterPlacementRecordLength))
                return false;

            int modelId = ReadInt32(candidate, modelOffset);
            if (modelId < 0 || modelId > byte.MaxValue)
                return false;

            float x = ReadSingleLittleEndian(candidate, modelOffset + CharacterXOffset);
            float y = ReadSingleLittleEndian(candidate, modelOffset + CharacterYOffset);
            float z = ReadSingleLittleEndian(candidate, modelOffset + CharacterZOffset);
            float rotation = ReadSingleLittleEndian(candidate, modelOffset + CharacterRotationOffset);
            float size = ReadSingleLittleEndian(candidate, modelOffset + CharacterSizeOffset);
            return IsFinite(x) && IsFinite(y) && IsFinite(z) &&
                IsFinite(rotation) && IsFinite(size);
        }

        private static int FindCharacterPlacementOffset(byte[] candidate, int modelOffset, out string error)
        {
            if (modelOffset < 0 || !HasRange(candidate, modelOffset, CharacterPlacementRecordLength))
            {
                error = "The character placement fields are incomplete in this save.";
                return -1;
            }

            float x = ReadSingleLittleEndian(candidate, modelOffset + CharacterXOffset);
            float y = ReadSingleLittleEndian(candidate, modelOffset + CharacterYOffset);
            float z = ReadSingleLittleEndian(candidate, modelOffset + CharacterZOffset);
            float rotation = ReadSingleLittleEndian(candidate, modelOffset + CharacterRotationOffset);
            float size = ReadSingleLittleEndian(candidate, modelOffset + CharacterSizeOffset);
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z) ||
                !IsFinite(rotation) || !IsFinite(size))
            {
                error = "The character placement fields contain unsupported values.";
                return -1;
            }

            error = null;
            return modelOffset;
        }

        private static int FindRoomIdOffset(byte[] candidate, out string error)
        {
            int labelOffset = FindUniqueRoomLabelOffset(candidate, out error);
            if (labelOffset < 0 && error == "A supported room record was not found in this save.")
                labelOffset = FindIncompleteRoomLabelOffset(candidate);
            if (labelOffset < 8)
            {
                if (labelOffset >= 0)
                    error = "The character room label is incomplete in this save.";
                return -1;
            }

            int fieldOffset = labelOffset - 8;
            if (!HasRange(candidate, fieldOffset, 8) ||
                candidate[fieldOffset + 4] != 0 ||
                candidate[fieldOffset + 5] != 0 ||
                candidate[fieldOffset + 6] != 0 ||
                candidate[fieldOffset + 7] != 0)
            {
                error = "The room record did not match the supported save layout.";
                return -1;
            }

            int roomId = ReadInt32(candidate, fieldOffset);
            RoomLocationInfo location;
            if (roomId < 0 || roomId > ushort.MaxValue ||
                !RoomLocationCatalog.TryGet(roomId, out location))
            {
                error = "The room ID is not in the supported room catalog. Editing is disabled.";
                return -1;
            }

            error = null;
            return fieldOffset;
        }

        private static int FindIncompleteRoomLabelOffset(byte[] candidate)
        {
            byte[] partialLabel = { (byte)'Q', 0, (byte)'u', 0 };
            int labelOffset = candidate.Length - partialLabel.Length;
            if (!HasBytes(candidate, labelOffset, partialLabel))
                return -1;

            int roomFieldOffset = labelOffset - 8;
            if (!HasRange(candidate, roomFieldOffset, 8) ||
                candidate[roomFieldOffset + 4] != 0 ||
                candidate[roomFieldOffset + 5] != 0 ||
                candidate[roomFieldOffset + 6] != 0 ||
                candidate[roomFieldOffset + 7] != 0)
                return -1;

            int roomId = ReadInt32(candidate, roomFieldOffset);
            RoomLocationInfo location;
            if (!RoomLocationCatalog.TryGet(roomId, out location))
                return -1;

            int modelOffset = labelOffset - CharacterModelOffsetBeforeRoomLabel;
            return IsSupportedCharacterModelRecord(candidate, modelOffset)
                ? labelOffset
                : -1;
        }

        private static int FindUniqueRoomLabelOffset(byte[] candidate, out string error)
        {
            int labelOffset = -1;
            for (int i = 0; i <= candidate.Length - RoomLabelMarker.Length; i++)
            {
                if (!HasBytes(candidate, i, RoomLabelMarker))
                    continue;
                if (labelOffset >= 0)
                {
                    error = "Multiple room records were found. Editing is disabled to avoid changing the wrong one.";
                    return -1;
                }
                labelOffset = i;
            }

            if (labelOffset < 0)
            {
                error = "A supported room record was not found in this save.";
                return -1;
            }

            error = null;
            return labelOffset;
        }

        private static byte[] ReadCandidate(string path)
        {
            byte[] candidate = File.ReadAllBytes(path);
            if (candidate.Length < MinimumSaveLength)
                throw new InvalidDataException("The selected file is too short to contain the supported QFG5 save data.");
            return candidate;
        }

        private static int ValidateInventory(byte[] candidate)
        {
            byte[] signature = Encoding.ASCII.GetBytes("QG5P1.000 ");
            if (candidate.Length < SaveHeaderLength ||
                !HasBytes(candidate, 0, signature) ||
                ReadUInt16(candidate, 10) != 0xDE89 ||
                ReadUInt16(candidate, 16) != 0xA8A5)
                throw new InvalidDataException("The selected file does not have a supported QFG5 save header.");

            int noiseLength = ReadUInt16(candidate, 14) ^ 0xFACE;
            if (noiseLength < MinimumNoiseLength || noiseLength > MaximumNoiseLength)
                throw new InvalidDataException("The selected save contains an invalid header noise length.");

            int inventoryCount = ReadInventoryCount(candidate);
            if (inventoryCount > InventorySlotCount)
                throw new InvalidDataException("The save declares more inventory items than the editor can safely handle.");

            int inventoryOffset = SaveHeaderLength + PlayerTypeLength + noiseLength + PlayerStateLength;
            int inventoryLength = InventorySlotSize * InventorySlotCount;
            if (inventoryOffset < 0 || inventoryOffset > candidate.Length - inventoryLength)
                throw new InvalidDataException("The selected save does not contain a complete inventory slot range at the documented location.");
            return inventoryOffset;
        }

        private static int FindFlagArrayOffset(byte[] candidate, out string error)
        {
            int inventoryCount = ReadInventoryCount(candidate);
            int noiseLength = ReadUInt16(candidate, 14) ^ 0xFACE;
            long inventoryStart = (long)SaveHeaderLength + PlayerTypeLength +
                noiseLength + PlayerStateLength;
            long inventoryEnd = inventoryStart + (long)inventoryCount * InventorySlotSize;
            if (inventoryStart < 0 || inventoryEnd > candidate.Length)
            {
                error = "The inventory section is incomplete, so the flag array cannot be located safely.";
                return -1;
            }

            int offset = (int)inventoryEnd;
            if (!HasRange(candidate, offset, 4))
            {
                error = "The dialogue section is missing, so the flag array cannot be located safely.";
                return -1;
            }

            int dialogueRoomCount = ReadInt32(candidate, offset);
            offset += 4;
            if (dialogueRoomCount < 0 || dialogueRoomCount > ObjectStateRoomCount)
            {
                error = "The save contains an unsupported dialogue-room count.";
                return -1;
            }

            for (int room = 0; room < dialogueRoomCount; room++)
            {
                if (!HasRange(candidate, offset, 8))
                {
                    error = "The dialogue section is truncated, so the flag array cannot be located safely.";
                    return -1;
                }

                int messageCount = ReadInt32(candidate, offset + 4);
                offset += 8;
                if (messageCount < 0 || messageCount > (candidate.Length - offset) / 8)
                {
                    error = "The dialogue section contains an invalid message-state count.";
                    return -1;
                }
                offset += messageCount * 8;
            }

            if (!HasRange(candidate, offset, 4))
            {
                error = "The object-state section is missing, so the flag array cannot be located safely.";
                return -1;
            }

            int declaredObjectCount = ReadInt32(candidate, offset);
            offset += 4;
            if (declaredObjectCount < 0 || declaredObjectCount > (candidate.Length - offset) / 4)
            {
                error = "The save contains an invalid object-state count.";
                return -1;
            }

            for (int room = 0; room < ObjectStateRoomCount; room++)
            {
                if (!HasRange(candidate, offset, 4))
                {
                    error = "The object-state section is truncated, so the flag array cannot be located safely.";
                    return -1;
                }

                int roomObjectCount = ReadInt32(candidate, offset);
                offset += 4;
                if (roomObjectCount < 0 || roomObjectCount > (candidate.Length - offset) / 4)
                {
                    error = "The object-state section contains an invalid room object count.";
                    return -1;
                }

                for (int item = 0; item < roomObjectCount; item++)
                {
                    if (!HasRange(candidate, offset, 4))
                    {
                        error = "The object-state section is truncated, so the flag array cannot be located safely.";
                        return -1;
                    }

                    int dataSize = ReadInt32(candidate, offset);
                    if (dataSize < 0)
                    {
                        error = "The object-state section contains an invalid object data size.";
                        return -1;
                    }

                    // A zero-sized object stores only its size field; nonzero records also contain type and ID.
                    int recordLength = dataSize == 0 ? 4 : 12;
                    if (dataSize > candidate.Length - offset - recordLength)
                    {
                        error = "The object-state section contains an incomplete object record.";
                        return -1;
                    }
                    offset += recordLength + dataSize;
                }
            }

            if (!HasRange(candidate, offset, EngineStateLength))
            {
                error = "The engine-state block is incomplete, so the flag array cannot be located safely.";
                return -1;
            }

            error = null;
            return offset;
        }

        private static int ReadInventoryCount(byte[] candidate)
        {
            return ReadUInt16(candidate, 12) ^ 0xBEAD;
        }

        private static int ReadUInt16(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8);
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24);
        }

        private static float ReadSingleLittleEndian(byte[] data, int offset)
        {
            byte[] value = { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] };
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(value);
            return BitConverter.ToSingle(value, 0);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool HasBytes(byte[] data, int offset, byte[] expected)
        {
            if (offset < 0 || data.Length - offset < expected.Length) return false;
            for (int i = 0; i < expected.Length; i++)
            {
                if (data[offset + i] != expected[i]) return false;
            }
            return true;
        }

        private static bool HasRange(byte[] data, int index, int count)
        {
            return data != null && index >= 0 && count >= 0 &&
                count <= data.Length && index <= data.Length - count;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i]) return false;
            }
            return true;
        }

        public bool IsActiveInventorySlot(int offset)
        {
            EnsureLoaded();
            int relativeOffset = offset - InventoryOffset;
            if (relativeOffset < 0 || relativeOffset % InventorySlotSize != 0)
                return false;

            int slotIndex = relativeOffset / InventorySlotSize;
            return InventoryCount <= InventorySlotCount &&
                slotIndex < InventoryCount &&
                HasRange(offset, InventorySlotSize);
        }

        public bool HasInventoryRange(int offset, int slotCount)
        {
            EnsureLoaded();
            if (slotCount < 0 || slotCount > InventorySlotCount) return false;
            int rangeLength = slotCount * InventorySlotSize;
            return offset == InventoryOffset && HasRange(offset, rangeLength);
        }

        public void AdjustInventoryCount(int change)
        {
            EnsureLoaded();
            int updatedCount = InventoryCount + change;
            if ((change != -1 && change != 1) ||
                updatedCount < 0 || updatedCount > InventorySlotCount)
                throw new InvalidDataException("The inventory count cannot be changed safely.");

            int encodedCount = updatedCount ^ 0xBEAD;
            SetByte(12, (byte)(encodedCount & 0xFF));
            SetByte(13, (byte)((encodedCount >> 8) & 0xFF));
            InventoryCount = updatedCount;
        }

        private bool HasRange(int index, int count)
        {
            return saveData != null && index >= 0 && count >= 0 &&
                count <= saveData.Length && index <= saveData.Length - count;
        }

        public bool Save()
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(FilePath))
                throw new InvalidOperationException("No save file is active.");
            if (!IsDirty) return false;
            if (hasIncompleteCharacterRecord)
                throw new InvalidDataException(CharacterDataUnavailableReason +
                    " Saving is disabled to avoid overwriting an incomplete save.");
            if (!IsLengthPreserved)
                throw new InvalidDataException("The edited save length differs from the loaded file. It was not saved.");
            if (!File.Exists(FilePath))
                throw new IOException("The active save file no longer exists. It was not recreated.");

            byte[] currentFile = File.ReadAllBytes(FilePath);
            if (!BytesEqual(currentFile, loadedFileSnapshot))
                throw new IOException("The save file changed on disk after it was loaded. Reload it before saving to avoid overwriting newer changes.");

            string directory = Path.GetDirectoryName(FilePath);
            string backupPath = FilePath + ".bak";
            string temporaryPath = Path.Combine(directory,
                "." + Path.GetFileName(FilePath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            bool createdBackup = false;
            bool backupWriteComplete = false;

            try
            {
                if (!File.Exists(backupPath))
                {
                    using (FileStream backup = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        createdBackup = true;
                        backup.Write(loadedFileSnapshot, 0, loadedFileSnapshot.Length);
                        backup.Flush(true);
                    }
                    backupWriteComplete = true;
                }

                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(saveData, 0, saveData.Length);
                    stream.Flush(true);
                }

                if (new FileInfo(temporaryPath).Length != OriginalLength)
                    throw new IOException("The prepared save file length differs from the original. The existing file was left untouched.");

                if (!File.Exists(FilePath) || !BytesEqual(File.ReadAllBytes(FilePath), loadedFileSnapshot))
                    throw new IOException("The save file changed while the new version was being prepared. The existing file was left untouched.");

                if (File.Exists(FilePath))
                    File.Replace(temporaryPath, FilePath, null);
                else
                    throw new IOException("The active save file disappeared before it could be replaced.");

                if (new FileInfo(FilePath).Length != OriginalLength)
                    throw new IOException("The saved file length could not be verified as unchanged.");

                loadedFileSnapshot = (byte[])saveData.Clone();
                IsDirty = false;
                return createdBackup;
            }
            finally
            {
                if (createdBackup && !backupWriteComplete && File.Exists(backupPath))
                {
                    try
                    {
                        File.Delete(backupPath);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("SaveSession: Could not remove incomplete backup " + backupPath + ": " + ex.Message);
                    }
                }
                if (File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("SaveSession: Could not remove temporary save file " + temporaryPath + ": " + ex.Message);
                    }
                }
            }
        }

        private void EnsureLoaded()
        {
            if (saveData == null)
                throw new InvalidOperationException("No save file is loaded.");
        }
    }
}
