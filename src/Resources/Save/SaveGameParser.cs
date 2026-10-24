using QFG5Extractor.Core;
using System;
using System.IO;
using System.Collections.Generic;

namespace QFG5Extractor.Resources.Save
{
    public class SaveGameParser
    {
        private readonly SaveSession session;

        public const int INVENTORY_SLOT_SIZE = 16;
        public const int INVENTORY_POOL_SIZE = 120;
        public const int MAX_SLOTS = 250;

        public Dictionary<string, string> ObjectNames { get; private set; }

        public SaveGameParser(SaveSession session)
        {
            if (session == null) throw new ArgumentNullException("session");
            this.session = session;
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
            if (!session.IsLoaded) return items;
            int detectedOffset = session.InventoryOffset;
            if (!session.HasInventoryRange(detectedOffset, INVENTORY_POOL_SIZE))
                throw new InvalidDataException("The active save does not contain a complete inventory slot range.");

            string[] placeNames = new string[] {
                "Inventory", "Inn Room Chest", "Hydra Chest", "Tinos Chest", 
                "Parros Chest", "Naxos Chest", "Keros Chest", "Ios Chest",
                "Front-Left Minos Alcove", "Mid-Left Minos Alcove", "Back-Left Minos Alcove", "Blackbird Alcove",
                "Mid-Right Minos Alcove", "Front-Right Minos Alcove", "Back Minos Chest",
                "Front Minos Chest"
            };

            // The main pool seems to be 120 slots
            for (int i = 0; i < INVENTORY_POOL_SIZE; i++)
            {
                int offset = detectedOffset + (i * INVENTORY_SLOT_SIZE);
                if (offset + 16 > session.Length) break;

                string idHex = session.GetByte(offset).ToString("X2") + session.GetByte(offset + 1).ToString("X2");
                int amount = session.GetByte(offset + 4) | (session.GetByte(offset + 5) << 8) | (session.GetByte(offset + 6) << 16) | (session.GetByte(offset + 7) << 24);
                int resId = session.GetByte(offset + 8);
                
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
                else if (idHex == "2A00" && session.GetByte(offset + 8) == 0x04 && session.GetByte(offset + 9) == 0x01)
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
            if (!session.HasInventoryRange(session.InventoryOffset, INVENTORY_POOL_SIZE) ||
                offset < session.InventoryOffset ||
                offset - session.InventoryOffset > INVENTORY_POOL_SIZE * INVENTORY_SLOT_SIZE - 8)
                return;

            if (newIdHex.Length == 4)
            {
                session.SetByte(offset, Convert.ToByte(newIdHex.Substring(0, 2), 16));
                session.SetByte(offset + 1, Convert.ToByte(newIdHex.Substring(2, 2), 16));
                
                // Set the indicator [01 00] if we are setting an item and it's not a header
                // We'll assume the user doesn't manually edit headers into items or vice versa easily
                if (newIdHex != "0000" && newIdHex != "2A00")
                {
                    session.SetByte(offset + 2, 0x01);
                    session.SetByte(offset + 3, 0x00);
                }
            }

            session.SetByte(offset + 4, (byte)(newAmount & 0xFF));
            session.SetByte(offset + 5, (byte)((newAmount >> 8) & 0xFF));
            session.SetByte(offset + 6, (byte)((newAmount >> 16) & 0xFF));
            session.SetByte(offset + 7, (byte)((newAmount >> 24) & 0xFF));
        }

        public void InsertInventoryItem(int index)
        {
            int detectedOffset = MakeRoomForInventoryItem(index);
            int currentResId = session.GetByte(detectedOffset + index * INVENTORY_SLOT_SIZE + 8);
            int targetOffset = detectedOffset + (index + 1) * INVENTORY_SLOT_SIZE;
            session.CopyWithin(detectedOffset + index * INVENTORY_SLOT_SIZE, targetOffset, INVENTORY_SLOT_SIZE);
            session.SetByte(targetOffset + 8, (byte)currentResId);
            session.AdjustInventoryCount(1);
        }

        public void InsertEmptyInventoryItem(int index)
        {
            int detectedOffset = MakeRoomForInventoryItem(index);
            int reservoirId = session.GetByte(detectedOffset + index * INVENTORY_SLOT_SIZE + 8);
            int targetOffset = detectedOffset + (index + 1) * INVENTORY_SLOT_SIZE;
            session.Clear(targetOffset, INVENTORY_SLOT_SIZE);
            session.SetByte(targetOffset + 8, (byte)reservoirId);
            session.AdjustInventoryCount(1);
        }

        private int MakeRoomForInventoryItem(int index)
        {
            int detectedOffset = session.InventoryOffset;
            if (index < 0 || index >= session.InventoryCount ||
                session.InventoryCount >= INVENTORY_POOL_SIZE ||
                !session.HasInventoryRange(detectedOffset, INVENTORY_POOL_SIZE))
                throw new InvalidDataException("The selected inventory range cannot safely accommodate an inserted slot.");

            for (int i = INVENTORY_POOL_SIZE - 1; i > index + 1; i--)
            {
                int sourceOffset = detectedOffset + (i - 1) * INVENTORY_SLOT_SIZE;
                int targetOffset = detectedOffset + i * INVENTORY_SLOT_SIZE;
                session.CopyWithin(sourceOffset, targetOffset, INVENTORY_SLOT_SIZE);
            }
            return detectedOffset;
        }

        public void DeleteInventoryItem(int index)
        {
            int POOL_SIZE = INVENTORY_POOL_SIZE;
            int detectedOffset = session.InventoryOffset;
            if (index < 0 || index >= session.InventoryCount ||
                !session.HasInventoryRange(detectedOffset, POOL_SIZE))
                throw new InvalidDataException("The selected inventory range cannot safely accommodate a deleted slot.");

            // Shift items UP from index+1
            for (int i = index; i < POOL_SIZE - 1; i++)
            {
                int src = detectedOffset + (i + 1) * INVENTORY_SLOT_SIZE;
                int dst = detectedOffset + i * INVENTORY_SLOT_SIZE;
                session.CopyWithin(src, dst, INVENTORY_SLOT_SIZE);
            }

            // Zero out the last slot
            int lastOffset = detectedOffset + (POOL_SIZE - 1) * INVENTORY_SLOT_SIZE;
            session.Clear(lastOffset, INVENTORY_SLOT_SIZE);
            session.AdjustInventoryCount(-1);
        }
    }
}
