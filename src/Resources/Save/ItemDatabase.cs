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

            string objectsDatPath = FindObjectsDatPath();
            if (objectsDatPath != null)
            {
                var lines = File.ReadAllLines(objectsDatPath);
                List<ItemEntry> loadedItems = new List<ItemEntry>();
                for (int i = 0; i + 1 < lines.Length; i += 2)
                {
                    string id = lines[i].Trim();
                    string name = lines[i + 1].Trim();
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

                items.AddRange(loadedItems);
                loaded = true;
                Logger.LogInfo("ItemDatabase: Loaded " + items.Count + " items from " + objectsDatPath);
                return;
            }

            loaded = true;
            Logger.LogWarning("ItemDatabase: objects.dat was not found beside the application or in a parent Resources/QG5Edit folder; inventory item names will be unavailable.");
        }

        private static string FindObjectsDatPath()
        {
            string[] startingDirectories = {
                AppDomain.CurrentDomain.BaseDirectory,
                Environment.CurrentDirectory
            };

            foreach (string startingDirectory in startingDirectories)
            {
                DirectoryInfo directory = new DirectoryInfo(Path.GetFullPath(startingDirectory));
                while (directory != null)
                {
                    string besideApplication = Path.Combine(directory.FullName, "objects.dat");
                    if (File.Exists(besideApplication))
                        return besideApplication;

                    string projectResource = Path.Combine(directory.FullName,
                        Path.Combine("Resources", Path.Combine("QG5Edit", "objects.dat")));
                    if (File.Exists(projectResource))
                        return projectResource;

                    directory = directory.Parent;
                }
            }

            return null;
        }
    }
}
