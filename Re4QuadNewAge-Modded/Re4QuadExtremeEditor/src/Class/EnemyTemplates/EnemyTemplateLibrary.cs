using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Re4QuadExtremeEditor.src.Class.EnemyTemplates
{
    public static class EnemyTemplateLibrary
    {
        private static string _dirOverride;

        private static string Dir
        {
            get
            {
                if (!string.IsNullOrEmpty(_dirOverride)) return _dirOverride;
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    if (!string.IsNullOrEmpty(baseDir))
                        return Path.Combine(baseDir, "data", "EnemyTemplates");
                }
                catch { }
                return Path.Combine("data", "EnemyTemplates");
            }
        }

        private static List<EnemyTemplate> _list;
        private static readonly object _lock = new object();

        public static event Action LibraryChanged;

        public static List<EnemyTemplate> Templates
        {
            get { lock (_lock) { if (_list == null) LoadInternal(); return _list; } }
        }

        public static IReadOnlyList<string> BuiltInCategories
        {
            get { return new[] { "Village", "Castle", "Island", "Boss", "Custom" }; }
        }

        public static List<string> Categories
        {
            get
            {
                var cats = Templates.Select(t => t.GetCategorySafe())
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                // ensure built-ins appear even when empty library
                foreach (string b in BuiltInCategories)
                    if (!cats.Any(c => c.Equals(b, StringComparison.OrdinalIgnoreCase)))
                        cats.Add(b);
                return cats;
            }
        }

        public static void SetDirectoryForTests(string dir) { _dirOverride = dir; }

        private static void LoadInternal()
        {
            _list = new List<EnemyTemplate>();
            try
            {
                if (!Directory.Exists(Dir))
                    Directory.CreateDirectory(Dir);

                foreach (string f in Directory.GetFiles(Dir, "*.json"))
                {
                    try
                    {
                        string txt = File.ReadAllText(f);
                        if (string.IsNullOrWhiteSpace(txt)) continue;
                        var t = EnemyTemplate.FromJson(JObject.Parse(txt));
                        if (t != null && !string.IsNullOrWhiteSpace(t.Name) && !_list.Any(x => x.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase)))
                            _list.Add(t);
                    }
                    catch { }
                }
                _list = _list.OrderBy(t => t.Category).ThenBy(t => t.Name).ToList();
            }
            catch { }
        }

        public static void Reload()
        {
            lock (_lock) { _list = null; LoadInternal(); }
            try { LibraryChanged?.Invoke(); } catch { }
        }

        public static void Save(EnemyTemplate t)
        {
            if (t == null) return;
            string err;
            if (!t.IsValid(out err))
                throw new ArgumentException(err);
            lock (_lock)
            {
                if (_list == null) LoadInternal();
                if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
                t.UpdatedAt = DateTime.Now;
                string safe = SafeName(t.Name);
                string path = Path.Combine(Dir, safe + ".json");
                // handle rename: remove old file if name changed and old file exists
                int idx = _list.FindIndex(x => x.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    string oldSafe = SafeName(_list[idx].Name);
                    string oldPath = Path.Combine(Dir, oldSafe + ".json");
                    if (!oldPath.Equals(path, StringComparison.OrdinalIgnoreCase) && File.Exists(oldPath))
                        try { File.Delete(oldPath); } catch { }
                    _list[idx] = t.Clone();
                }
                else
                {
                    _list.Add(t.Clone());
                }
                _list = _list.OrderBy(x => x.Category).ThenBy(x => x.Name).ToList();
                try
                {
                    string json = t.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);
                    string tmp = path + ".tmp";
                    File.WriteAllText(tmp, json);
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(tmp, path);
                }
                catch { }
            }
            try { LibraryChanged?.Invoke(); } catch { }
        }

        public static bool Delete(EnemyTemplate t)
        {
            if (t == null) return false;
            bool removed = false;
            lock (_lock)
            {
                if (_list == null) LoadInternal();
                string safe = SafeName(t.Name);
                string path = Path.Combine(Dir, safe + ".json");
                try { if (File.Exists(path)) File.Delete(path); } catch { }
                int c0 = _list.Count;
                _list.RemoveAll(x => x.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase));
                removed = _list.Count != c0;
            }
            if (removed) try { LibraryChanged?.Invoke(); } catch { }
            return removed;
        }

        public static EnemyTemplate Duplicate(EnemyTemplate src)
        {
            if (src == null) return null;
            string newName = NextCloneName(src.Name);
            var clone = src.Clone();
            clone.Name = newName;
            clone.CreatedAt = DateTime.Now;
            clone.UpdatedAt = DateTime.Now;
            Save(clone);
            return clone;
        }

        public static bool Rename(EnemyTemplate t, string newName)
        {
            if (t == null || string.IsNullOrWhiteSpace(newName)) return false;
            newName = newName.Trim();
            if (newName.Length > 64) return false;
            lock (_lock)
            {
                if (_list == null) LoadInternal();
                if (_list.Any(x => !ReferenceEquals(x, t) && x.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                    return false;
                string oldSafe = SafeName(t.Name);
                string oldPath = Path.Combine(Dir, oldSafe + ".json");
                t.Name = newName;
                t.UpdatedAt = DateTime.Now;
                string newSafe = SafeName(newName);
                string newPath = Path.Combine(Dir, newSafe + ".json");
                try
                {
                    string json = t.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);
                    string tmp = newPath + ".tmp";
                    File.WriteAllText(tmp, json);
                    if (File.Exists(newPath)) File.Delete(newPath);
                    File.Move(tmp, newPath);
                    if (!oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase) && File.Exists(oldPath))
                        try { File.Delete(oldPath); } catch { }
                }
                catch { return false; }
                _list = _list.OrderBy(x => x.Category).ThenBy(x => x.Name).ToList();
            }
            try { LibraryChanged?.Invoke(); } catch { }
            return true;
        }

        public static List<EnemyTemplate> Search(string category, string search, IEnumerable<string> tags = null)
        {
            IEnumerable<EnemyTemplate> q = Templates;
            if (!string.IsNullOrEmpty(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
                q = q.Where(t => t.GetCategorySafe().Equals(category, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(search))
            {
                string needle = search.Trim().ToLowerInvariant();
                if (needle.Length > 0)
                {
                    q = q.Where(t =>
                        (t.Name ?? "").ToLowerInvariant().Contains(needle) ||
                        (t.EnemyName ?? "").ToLowerInvariant().Contains(needle) ||
                        t.EnemyId.ToString("X4").ToLowerInvariant().Contains(needle) ||
                        (t.Description ?? "").ToLowerInvariant().Contains(needle) ||
                        (t.Category ?? "").ToLowerInvariant().Contains(needle) ||
                        t.Tags.Any(tag => (tag ?? "").ToLowerInvariant().Contains(needle)));
                }
            }
            if (tags != null && tags.Any())
            {
                var lower = tags.Where(s => !string.IsNullOrWhiteSpace(s)).Select(x => x.ToLowerInvariant()).ToList();
                if (lower.Count > 0)
                    q = q.Where(t => lower.All(l => t.Tags.Any(tag => (tag ?? "").ToLowerInvariant() == l)));
            }
            return q.OrderBy(t => t.Category).ThenBy(t => t.Name).ToList();
        }

        public static string SafeName(string name)
        {
            string safe = string.Join("_", (name ?? "").Split(Path.GetInvalidFileNameChars()));
            safe = safe.Trim();
            if (string.IsNullOrWhiteSpace(safe)) safe = "Template_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            if (safe.Length > 80) safe = safe.Substring(0, 80);
            return safe;
        }

        public static string NextCloneName(string baseName)
        {
            string baseN = string.IsNullOrWhiteSpace(baseName) ? "Template" : baseName.Trim();
            // strip existing " (n)"
            string bare = System.Text.RegularExpressions.Regex.Replace(baseN, @"\s*\(\d+\)\s*$", "");
            int i = 2;
            string candidate = bare + " (" + i + ")";
            var names = new HashSet<string>(Templates.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);
            while (names.Contains(candidate))
            {
                i++;
                candidate = bare + " (" + i + ")";
            }
            return candidate;
        }

        public static bool Export(EnemyTemplate t, string filePath)
        {
            if (t == null || string.IsNullOrWhiteSpace(filePath)) return false;
            try
            {
                string json = t.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch { return false; }
        }

        public static EnemyTemplate Import(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;
            try
            {
                var jo = JObject.Parse(File.ReadAllText(filePath));
                var t = EnemyTemplate.FromJson(jo);
                if (t == null || string.IsNullOrWhiteSpace(t.Name)) return null;
                // avoid collision
                if (Templates.Any(x => x.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase)))
                    t.Name = NextCloneName(t.Name);
                Save(t);
                return t;
            }
            catch { return null; }
        }

        public static int BulkImport(string folderOrFile)
        {
            int n = 0;
            try
            {
                if (File.Exists(folderOrFile))
                {
                    if (Import(folderOrFile) != null) n = 1;
                }
                else if (Directory.Exists(folderOrFile))
                {
                    foreach (string f in Directory.GetFiles(folderOrFile, "*.json"))
                        if (Import(f) != null) n++;
                }
            }
            catch { }
            return n;
        }
    }
}
