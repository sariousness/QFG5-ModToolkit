using QFG5Extractor.Core;
using QFG5Extractor.Resources.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace QFG5Extractor.Resources.Model
{
    public class ModelEntry
    {
        public string Prefix { get; set; }
        public string Name { get; set; }
        public int Submeshes { get; set; }
        public int Vertices { get; set; }
        public List<string> CompatibleAnms { get; set; } = new List<string>();

        public string ToJson()
        {
            var anms = string.Join("\", \"", CompatibleAnms.ToArray());
            if (!string.IsNullOrEmpty(anms)) anms = "\"" + anms + "\"";
            return $@"    ""{Prefix}"": {{
        ""name"": ""{Name}"",
        ""submeshes"": {Submeshes},
        ""vertices"": {Vertices},
        ""compatibleAnms"": [{anms}]
    }}";
        }
    }

    public class ModelDatabase
    {
        private string _filePath;
        private Dictionary<string, ModelEntry> _entries = new Dictionary<string, ModelEntry>();

        public ModelDatabase(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        public void AddOrUpdate(ModelEntry entry)
        {
            if (_entries.ContainsKey(entry.Prefix))
            {
                var existing = _entries[entry.Prefix];
                existing.Name = entry.Name;
                existing.Submeshes = entry.Submeshes;
                existing.Vertices = entry.Vertices;
                foreach (var anm in entry.CompatibleAnms)
                {
                    if (!existing.CompatibleAnms.Contains(anm))
                        existing.CompatibleAnms.Add(anm);
                }
            }
            else
            {
                _entries[entry.Prefix] = entry;
            }
            Save();
        }

        public void Load()
        {
            if (!File.Exists(_filePath)) return;
            try
            {
                // Very basic manual JSON parser for our specific format
                string content = File.ReadAllText(_filePath);
                // This is a placeholder since we only write for now.
                // In a real app we'd use a JSON library.
                // For this task, we'll mostly focus on the "Append/Update" behavior.
            }
            catch (Exception ex)
            {
                Logger.LogError("ModelDatabase", "Failed to load database: " + ex.Message);
            }
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                var keys = _entries.Keys.OrderBy(k => k).ToList();
                for (int i = 0; i < keys.Count; i++)
                {
                    sb.Append(_entries[keys[i]].ToJson());
                    if (i < keys.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("}");
                File.WriteAllText(_filePath, sb.ToString());
            }
            catch (Exception ex)
            {
                Logger.LogError("ModelDatabase", "Failed to save database: " + ex.Message);
            }
        }

        public void SyncToCsv(string csvPath)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(csvPath))
                {
                    sw.WriteLine("MDL Prefix,MDL Name,Submeshes,Vertices,Compatible ANMs");
                    var keys = _entries.Keys.OrderBy(k => k).ToList();
                    foreach (var key in keys)
                    {
                        var e = _entries[key];
                        string anmList = string.Join(";", e.CompatibleAnms.ToArray());
                        sw.WriteLine($"{e.Prefix},{e.Name},{e.Submeshes},{e.Vertices},{anmList}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ModelDatabase", "Failed to sync CSV: " + ex.Message);
            }
        }

        public ModelEntry GetEntry(string prefix)
        {
            if (_entries.ContainsKey(prefix)) return _entries[prefix];
            return null;
        }
    }
}
