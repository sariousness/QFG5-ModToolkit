using QFG5Extractor.Core;
using System;
using System.IO;
using System.Collections.Generic;

namespace QFG5Extractor.Resources.Save
{
    public class SaveGameParser
    {
        private byte[] saveData;
        private string filePath;
        private int detectedOffset = -1;

        public const int INVENTORY_SLOT_SIZE = 16;
        public const int MAX_SLOTS = 250;

        public Dictionary<string, string> ObjectNames { get; private set; }

        public SaveGameParser()
        {
            ObjectNames = new Dictionary<string, string>();
        }

        public void LoadObjectNames(string objectsDatPath)
        {
            if (File.Exists(objectsDatPath))
            {
                var lines = File.ReadAllLines(objectsDatPath);
                for (int i = 0; i < lines.Length; i += 2)
                {
                    if (i + 1 < lines.Length)
                    {
                        string idHex = lines[i].Trim();
                        if (idHex.Length > 4) idHex = idHex.Substring(0, 4);
                        ObjectNames[idHex.ToUpper()] = lines[i + 1];
                    }
                }
            }
        }

        public void LoadSave(string path)
        {
            filePath = path;
            saveData = File.ReadAllBytes(path);
            detectedOffset = FindInventoryOffset();
        }

        private int FindInventoryOffset()
        {
            int bestStart = -1;
            int bestCount = 0;

            // Scan with all 16 possible alignments
            for (int align = 0; align < 16; align++)
            {
                int currentCluster = 0;
                int clusterStart = -1;

                for (int i = align; i < saveData.Length - 16; i += INVENTORY_SLOT_SIZE)
                {
                    string idHex = saveData[i].ToString("X2") + saveData[i + 1].ToString("X2");
                    int amount = saveData[i + 4] | (saveData[i + 5] << 8) | (saveData[i + 6] << 16) | (saveData[i + 7] << 24);

                    // Pattern: Valid ID or all zeros (empty slot), and usually [01 00] at +2/+3 if not empty
                    bool isValidId = idHex != "0000" && ItemDatabase.GetItems().Exists(it => it.ID == idHex);
                    bool isEmpty = (idHex == "0000" && amount == 0);
                    bool hasIndicator = (saveData[i + 2] == 0x01 && saveData[i + 3] == 0x00) || isEmpty;

                    if ((isValidId && hasIndicator) || (isEmpty && currentCluster > 0)) // Only count empty slots if we're already in a cluster
                    {
                        if (currentCluster == 0 && isValidId) { 
                            clusterStart = i;
                            currentCluster++;
                        } else if (currentCluster > 0) {
                            currentCluster++;
                        }
                    }
                    else
                    {
                        if (currentCluster > bestCount)
                        {
                            bestCount = currentCluster;
                            bestStart = clusterStart;
                        }
                        currentCluster = 0;
                    }
                }

                if (currentCluster > bestCount)
                {
                    bestCount = currentCluster;
                    bestStart = clusterStart;
                }
            }

            // Fallback to 0x0110 if nothing found, though scanning should work
            return bestCount > 5 ? bestStart : 0x0110;
        }

        public void SaveSave(string path = null)
        {
            if (path == null) path = filePath;
            File.WriteAllBytes(path, saveData);
        }

        public class InventoryItem
        {
            public int Offset { get; set; }
            public string IdHex { get; set; }
            public int Amount { get; set; }
            public string Name { get; set; }
            public bool IsHeader { get; set; }
            public string PlaceName { get; set; }
            public int ReservoirId { get; set; }
        }

        public List<InventoryItem> GetInventory()
        {
            var items = new List<InventoryItem>();
            if (detectedOffset == -1) detectedOffset = FindInventoryOffset();

            string[] placeNames = new string[] {
                "Inventory", "Inn Room Chest", "Hydra Chest", "Tinos Chest", 
                "Parros Chest", "Naxos Chest", "Keros Chest", "Ios Chest",
                "Front-Left Minos Alcove", "Mid-Left Minos Alcove", "Back-Left Minos Alcove", "Blackbird Alcove",
                "Mid-Right Minos Alcove", "Front-Right Minos Alcove", "Back Minos Chest",
                "Front Minos Chest"
            };

            // The main pool seems to be 120 slots
            int POOL_SIZE = 120;
            for (int i = 0; i < POOL_SIZE; i++)
            {
                int offset = detectedOffset + (i * INVENTORY_SLOT_SIZE);
                if (offset + 16 > saveData.Length) break;

                string idHex = saveData[offset].ToString("X2") + saveData[offset + 1].ToString("X2");
                int amount = saveData[offset + 4] | (saveData[offset + 5] << 8) | (saveData[offset + 6] << 16) | (saveData[offset + 7] << 24);
                int resId = saveData[offset + 8];
                
                string currentPlace = resId < placeNames.Length ? placeNames[resId] : $"Place {resId}";

                if (idHex == "0000" && amount == 0)
                {
                    items.Add(new InventoryItem
                    {
                        Offset = offset,
                        IdHex = "0000",
                        Amount = 0,
                        Name = "Empty Slot",
                        IsHeader = false,
                        PlaceName = currentPlace,
                        ReservoirId = resId
                    });
                }
                else if (idHex == "2A00" && saveData[offset + 8] == 0x04 && saveData[offset + 9] == 0x01)
                {
                    items.Add(new InventoryItem
                    {
                        Offset = offset,
                        IdHex = idHex,
                        Amount = amount,
                        Name = "[Pool Divider]",
                        IsHeader = true,
                        PlaceName = "System",
                        ReservoirId = resId
                    });
                }
                else
                {
                    string name = ItemDatabase.GetName(idHex);
                    items.Add(new InventoryItem
                    {
                        Offset = offset,
                        IdHex = idHex,
                        Amount = amount,
                        Name = name,
                        IsHeader = false,
                        PlaceName = currentPlace,
                        ReservoirId = resId
                    });
                }
            }
            return items;
        }

        public void UpdateInventoryItem(int offset, string newIdHex, int newAmount)
        {
            if (offset < 0 || offset + 8 > saveData.Length) return;

            if (newIdHex.Length == 4)
            {
                saveData[offset] = Convert.ToByte(newIdHex.Substring(0, 2), 16);
                saveData[offset + 1] = Convert.ToByte(newIdHex.Substring(2, 2), 16);
                
                // Set the indicator [01 00] if we are setting an item and it's not a header
                // We'll assume the user doesn't manually edit headers into items or vice versa easily
                if (newIdHex != "0000" && newIdHex != "2A00")
                {
                    saveData[offset + 2] = 0x01;
                    saveData[offset + 3] = 0x00;
                }
            }

            saveData[offset + 4] = (byte)(newAmount & 0xFF);
            saveData[offset + 5] = (byte)((newAmount >> 8) & 0xFF);
            saveData[offset + 6] = (byte)((newAmount >> 16) & 0xFF);
            saveData[offset + 7] = (byte)((newAmount >> 24) & 0xFF);
        }

        public void InsertInventoryItem(int index)
        {
            if (detectedOffset == -1) detectedOffset = FindInventoryOffset();
            
            // Limit shifting to the 120-slot pool
            int POOL_SIZE = 120;
            if (index < 0 || index >= POOL_SIZE - 1) return;

            int currentResId = saveData[detectedOffset + index * INVENTORY_SLOT_SIZE + 8];

            // Shift all items from index+1 to POOL_SIZE-1 down
            for (int i = POOL_SIZE - 1; i > index + 1; i--)
            {
                int src = detectedOffset + (i - 1) * INVENTORY_SLOT_SIZE;
                int dst = detectedOffset + i * INVENTORY_SLOT_SIZE;
                Array.Copy(saveData, src, saveData, dst, INVENTORY_SLOT_SIZE);
            }

            // Set the new slot (index + 1) to a duplicate of the current item
            int targetOffset = detectedOffset + (index + 1) * INVENTORY_SLOT_SIZE;
            Array.Copy(saveData, detectedOffset + index * INVENTORY_SLOT_SIZE, saveData, targetOffset, INVENTORY_SLOT_SIZE);
            
            // Ensure the Reservoir ID is preserved
            saveData[targetOffset + 8] = (byte)currentResId;
        }

        public void DeleteInventoryItem(int index)
        {
            if (detectedOffset == -1) detectedOffset = FindInventoryOffset();
            
            int POOL_SIZE = 120;
            if (index < 0 || index >= POOL_SIZE) return;

            // Shift items UP from index+1
            for (int i = index; i < POOL_SIZE - 1; i++)
            {
                int src = detectedOffset + (i + 1) * INVENTORY_SLOT_SIZE;
                int dst = detectedOffset + i * INVENTORY_SLOT_SIZE;
                Array.Copy(saveData, src, saveData, dst, INVENTORY_SLOT_SIZE);
            }

            // Zero out the last slot
            int lastOffset = detectedOffset + (POOL_SIZE - 1) * INVENTORY_SLOT_SIZE;
            Array.Clear(saveData, lastOffset, INVENTORY_SLOT_SIZE);
        }
    }
}
