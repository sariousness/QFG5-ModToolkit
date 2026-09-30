using QFG5Extractor.Core;
using System;
using System.Collections.Generic;
using System.IO;

namespace QFG5Extractor.Resources.Save
{
    public class ItemDatabase
    {
        public class ItemEntry
        {
            public string ID { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
            public int CategoryIndex { get; set; }
            public override string ToString() => Name;
        }

        private static List<ItemEntry> items = new List<ItemEntry>();
        private static bool loaded = false;

        public static List<ItemEntry> GetItems()
        {
            if (!loaded) Load();
            return items;
        }

        public static string GetName(string id)
        {
            if (!loaded) Load();
            var item = items.Find(i => i.ID.Equals(id, StringComparison.OrdinalIgnoreCase));
            return item != null ? item.Name : "Unknown (" + id + ")";
        }

        public static string GetID(string name)
        {
            if (!loaded) Load();
            var item = items.Find(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return item != null ? item.ID : "0000";
        }

        private static void Load()
        {
            items.Clear();
            // Add a default "Empty Slot" entry
            items.Add(new ItemEntry { ID = "0000", Name = "Empty Slot" });

            string[] searchPaths = { 
                "objects.dat",
                "../objects.dat",
                "../../objects.dat",
                "../../../../Resources/QG5Edit/objects.dat",
                "../../../Resources/QG5Edit/objects.dat",
                "../../Resources/QG5Edit/objects.dat",
                "../QG5Edit/objects.dat",
                "../../QG5Edit/objects.dat",
                "../../../QG5Edit/objects.dat",
                "/home/vandali/Desktop/QFG5-Tool/Resources/QG5Edit/objects.dat",
                "/home/vandali/.wine32/drive_c/Program Files/QFG5-RE/QG5Edit/objects.dat"
            };

            foreach (string p in searchPaths)
            {
                if (File.Exists(p))
                {
                    var lines = File.ReadAllLines(p);
                    // Sorting by ID alphabetically within categories is likely how the icons are mapped
                    List<ItemEntry> loadedItems = new List<ItemEntry>();
                    for (int i = 0; i + 1 < lines.Length; i += 2)
                    {
                        string id = lines[i].Trim();
                        string name = lines[i+1].Trim();
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                        {
                            string cat = "Other";
                            string suffix = id.EndsWith("8A") ? "8A" : (id.EndsWith("8B") ? "8B" : (id.EndsWith("8C") ? "8C" : ""));
                            if (suffix == "8A") cat = "Equipment";
                            else if (suffix == "8B") cat = "Items";
                            else if (suffix == "8C") cat = "Spells";

                            loadedItems.Add(new ItemEntry { ID = id, Name = name, Category = cat });
                        }
                    }

                    // Sorting removed to maintain original file order for icon mapping
                    // loadedItems.Sort((a, b) => string.Compare(a.ID, b.ID, StringComparison.OrdinalIgnoreCase));

                    int eqIdx = 0, itIdx = 0, spIdx = 0;
                    foreach (var it in loadedItems)
                    {
                        if (it.Category == "Equipment") it.CategoryIndex = eqIdx++;
                        else if (it.Category == "Items") it.CategoryIndex = itIdx++;
                        else if (it.Category == "Spells") it.CategoryIndex = spIdx++;
                        else it.CategoryIndex = -1;
                        
                        items.Add(it);
                    }
                    loaded = true;
                    Logger.LogInfo($"ItemDatabase: Loaded {items.Count} items from {p}");
                    break;
                }
            }
        }
    }
}
