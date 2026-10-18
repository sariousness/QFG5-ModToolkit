using QFG5Extractor.Core;
using System;
using System.IO;

namespace QFG5Extractor.Resources.Save
{
    public sealed class SaveSession
    {
        private const int MinimumSaveLength = 0x125C;
        private const int InventorySlotSize = SaveGameParser.INVENTORY_SLOT_SIZE;
        private const int InventorySlotCount = SaveGameParser.INVENTORY_POOL_SIZE;
        private byte[] saveData;

        public string FilePath { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsLoaded { get { return saveData != null; } }
        public int Length { get { return saveData == null ? 0 : saveData.Length; } }
        public int InventoryOffset { get; private set; }
        public int InventoryCount { get; private set; }

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

            if (IsDirty)
            {
                if (approveReplacingDirtyDocument == null || !approveReplacingDirtyDocument())
                    return false;

                candidate = ReadCandidate(fullPath);
                candidateInventoryOffset = ValidateInventory(candidate);
            }

            saveData = candidate;
            FilePath = fullPath;
            InventoryOffset = candidateInventoryOffset;
            InventoryCount = ReadInventoryCount(candidate);
            IsDirty = false;
            return true;
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
            int inventoryOffset = SaveGameParser.FindInventoryOffset(candidate.Length,
                index => candidate[index]);
            int inventoryLength = InventorySlotSize * InventorySlotCount;
            if (inventoryOffset < 0 || inventoryOffset > candidate.Length - inventoryLength)
                throw new InvalidDataException("The selected save does not contain a complete inventory slot range.");
            return inventoryOffset;
        }

        private static int ReadInventoryCount(byte[] candidate)
        {
            int encodedCount = candidate[12] | (candidate[13] << 8);
            return encodedCount ^ 0xBEAD;
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

        private bool HasRange(int index, int count)
        {
            return saveData != null && index >= 0 && count >= 0 &&
                count <= saveData.Length && index <= saveData.Length - count;
        }

        public void Save()
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(FilePath))
                throw new InvalidOperationException("No save file is active.");

            string directory = Path.GetDirectoryName(FilePath);
            string temporaryPath = Path.Combine(directory,
                "." + Path.GetFileName(FilePath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(saveData, 0, saveData.Length);
                    stream.Flush(true);
                }

                if (File.Exists(FilePath))
                    File.Replace(temporaryPath, FilePath, null);
                else
                    File.Move(temporaryPath, FilePath);

                IsDirty = false;
            }
            finally
            {
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
