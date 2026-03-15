using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace GearSetsMod.Core
{
    public static class SetManager
    {
        public static string ConfigPath = "GearSets";
        public static IJsonWrapper JsonWrapper { get; set; } = new StandardJsonWrapper();

        public static void Save(GearSet set)
        {
            if (JsonWrapper == null) throw new Exception("JsonWrapper not initialized");
            if (string.IsNullOrEmpty(set?.Name)) return;

            var safeName = SanitizeFileName(set.Name);
            if (!Directory.Exists(ConfigPath)) Directory.CreateDirectory(ConfigPath);

            string json = JsonWrapper.ToJson(set);
            File.WriteAllText(Path.Combine(ConfigPath, safeName + ".json"), json);
        }

        public static GearSet Load(string name)
        {
            if (JsonWrapper == null) throw new Exception("JsonWrapper not initialized");

            var safeName = SanitizeFileName(name);
            string path = Path.Combine(ConfigPath, safeName + ".json");
            if (!File.Exists(path)) return null;

            string json = File.ReadAllText(path);
            return JsonWrapper.FromJson<GearSet>(json);
        }

        public static List<GearSet> GetAllSets()
        {
            var sets = new List<GearSet>();
            if (!Directory.Exists(ConfigPath)) return sets;

            foreach (var file in Directory.GetFiles(ConfigPath, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var set = JsonWrapper.FromJson<GearSet>(json);
                    if (set != null) sets.Add(set);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GearSetsMod] Failed to deserialize {file}: {ex.Message}");
                }
            }

            return sets.OrderByDescending(s => s.CreatedAt).ToList();
        }

        public static void Delete(string name)
        {
            var safeName = SanitizeFileName(name);
            var path = Path.Combine(ConfigPath, safeName + ".json");
            if (File.Exists(path)) File.Delete(path);
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }
    }
}
