using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Re4QuadExtremeEditor.src.Class.Enums;

namespace Re4QuadExtremeEditor.src.Class.ElementLibrary
{
    /// <summary>
    /// On-disk container for reusable elements shared across all rooms/files.
    /// Every element is persisted as its own JSON file under data\ElementLibrary.
    /// Mirrors the proven EnemyTemplateLibrary persistence pattern.
    /// </summary>
    public static class ElementLibrary
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
                        return Path.Combine(baseDir, "data", "ElementLibrary");
                }
                catch { }
                return Path.Combine("data", "ElementLibrary");
            }
        }

        private static List<ElementLibraryEntry> _list;
        private static readonly object _lock = new object();

        public static event Action LibraryChanged;

        public static List<ElementLibraryEntry> Entries
        {
            get { lock (_lock) { if (_list == null) LoadInternal(); return _list; } }
        }

        public static string[] DefaultCategories
        {
            get { return new[] { "Custom" }; }
        }

        public static List<string> Categories
        {
            get
            {
                var cats = Entries.Select(t => t.GetCategorySafe())
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (!cats.Any(c => c.Equals("Custom", StringComparison.OrdinalIgnoreCase)))
                    cats.Insert(0, "Custom");
                return cats;
            }
        }

        public static void SetDirectoryForTests(string dir) { _dirOverride = dir; }

        private static void LoadInternal()
        {
            _list = new List<ElementLibraryEntry>();
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
                        var e = ElementLibraryEntry.FromJson(JObject.Parse(txt));
                        if (e != null && !string.IsNullOrWhiteSpace(e.Name) && !_list.Any(x => x.Id.Equals(e.Id, StringComparison.OrdinalIgnoreCase)))
                            _list.Add(e);
                    }
                    catch { }
                }
                _list = _list.OrderBy(t => t.GetCategorySafe()).ThenBy(t => t.Name).ToList();
            }
            catch { }
        }

        public static void Reload()
        {
            lock (_lock) { _list = null; LoadInternal(); }
            try { LibraryChanged?.Invoke(); } catch { }
        }

        public static bool ContainsName(string name)
        {
            return Entries.Any(x => x.Name.Equals((name ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static void Save(ElementLibraryEntry e)
        {
            if (e == null) return;
            string err;
            if (!e.IsValid(out err))
                throw new ArgumentException(err);
            lock (_lock)
            {
                if (_list == null) LoadInternal();
                if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
                e.UpdatedAt = DateTime.Now;
                string safe = SafeName(e.Name);
                string path = Path.Combine(Dir, safe + ".json");
                int idx = _list.FindIndex(x => x.Id.Equals(e.Id, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                    _list[idx] = e.Clone();
                else
                    _list.Add(e.Clone());
                _list = _list.OrderBy(x => x.GetCategorySafe()).ThenBy(x => x.Name).ToList();
                try
                {
                    string json = e.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);
                    string tmp = path + ".tmp";
                    File.WriteAllText(tmp, json);
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(tmp, path);
                }
                catch { }
            }
            try { LibraryChanged?.Invoke(); } catch { }
        }

        public static bool Delete(ElementLibraryEntry e)
        {
            if (e == null) return false;
            bool removed = false;
            lock (_lock)
            {
                if (_list == null) LoadInternal();
                string safe = SafeName(e.Name);
                string path = Path.Combine(Dir, safe + ".json");
                try { if (File.Exists(path)) File.Delete(path); } catch { }
                int c0 = _list.Count;
                _list.RemoveAll(x => x.Id.Equals(e.Id, StringComparison.OrdinalIgnoreCase));
                removed = _list.Count != c0;
            }
            if (removed) try { LibraryChanged?.Invoke(); } catch { }
            return removed;
        }

        public static ElementLibraryEntry Duplicate(ElementLibraryEntry src)
        {
            if (src == null) return null;
            string newName = NextCloneName(src.Name);
            var clone = src.Clone();
            clone.Id = Guid.NewGuid().ToString("N");
            clone.Name = newName;
            clone.CreatedAt = DateTime.Now;
            clone.UpdatedAt = DateTime.Now;
            Save(clone);
            return clone;
        }

        public static bool Rename(ElementLibraryEntry e, string newName)
        {
            if (e == null || string.IsNullOrWhiteSpace(newName)) return false;
            newName = newName.Trim();
            if (newName.Length > 64) return false;
            lock (_lock)
            {
                if (_list == null) LoadInternal();
                if (_list.Any(x => !ReferenceEquals(x, e) && x.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                    return false;
                string oldSafe = SafeName(e.Name);
                string oldPath = Path.Combine(Dir, oldSafe + ".json");
                e.Name = newName;
                e.UpdatedAt = DateTime.Now;
                string json = e.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);
                string tmp = Path.Combine(Dir, SafeName(newName) + ".json") + ".tmp";
                try
                {
                    File.WriteAllText(tmp, json);
                    string newPath = Path.Combine(Dir, SafeName(newName) + ".json");
                    if (File.Exists(newPath) && !newPath.Equals(oldPath, StringComparison.OrdinalIgnoreCase)) File.Delete(newPath);
                    if (!newPath.Equals(oldPath, StringComparison.OrdinalIgnoreCase) && File.Exists(oldPath)) File.Delete(oldPath);
                    File.Move(tmp, newPath);
                }
                catch { return false; }
                _list = _list.OrderBy(x => x.GetCategorySafe()).ThenBy(x => x.Name).ToList();
            }
            try { LibraryChanged?.Invoke(); } catch { }
            return true;
        }

        public static List<ElementLibraryEntry> Search(string category, string search, GroupType? typeFilter = null)
        {
            IEnumerable<ElementLibraryEntry> q = Entries;
            if (!string.IsNullOrEmpty(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
                q = q.Where(t => t.GetCategorySafe().Equals(category, StringComparison.OrdinalIgnoreCase));
            if (typeFilter.HasValue && typeFilter.Value != GroupType.NULL)
            {
                string tn = typeFilter.Value.ToString();
                q = q.Where(t => (t.GroupType ?? "").Equals(tn, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrEmpty(search))
            {
                string needle = search.Trim().ToLowerInvariant();
                if (needle.Length > 0)
                {
                    q = q.Where(t =>
                        (t.Name ?? "").ToLowerInvariant().Contains(needle) ||
                        (t.Description ?? "").ToLowerInvariant().Contains(needle) ||
                        (t.Category ?? "").ToLowerInvariant().Contains(needle) ||
                        (t.GroupType ?? "").ToLowerInvariant().Contains(needle) ||
                        (t.Tags != null && t.Tags.Any(tag => (tag ?? "").ToLowerInvariant().Contains(needle))));
                }
            }
            return q.OrderBy(t => t.GetCategorySafe()).ThenBy(t => t.Name).ToList();
        }

        public static string SafeName(string name)
        {
            string safe = string.Join("_", (name ?? "").Split(Path.GetInvalidFileNameChars()));
            safe = safe.Trim();
            if (string.IsNullOrWhiteSpace(safe)) safe = "Element_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            if (safe.Length > 80) safe = safe.Substring(0, 80);
            return safe;
        }

        public static string NextCloneName(string baseName)
        {
            string baseN = string.IsNullOrWhiteSpace(baseName) ? "Element" : baseName.Trim();
            string bare = System.Text.RegularExpressions.Regex.Replace(baseN, @"\s*\(\d+\)\s*$", "");
            int i = 2;
            string candidate = bare + " (" + i + ")";
            var names = new HashSet<string>(Entries.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);
            while (names.Contains(candidate)) { i++; candidate = bare + " (" + i + ")"; }
            return candidate;
        }

        public static bool Export(ElementLibraryEntry e, string filePath)
        {
            if (e == null || string.IsNullOrWhiteSpace(filePath)) return false;
            try { File.WriteAllText(filePath, e.ToJson().ToString(Newtonsoft.Json.Formatting.Indented)); return true; }
            catch { return false; }
        }

        public static ElementLibraryEntry Import(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;
            try
            {
                var jo = JObject.Parse(File.ReadAllText(filePath));
                var e = ElementLibraryEntry.FromJson(jo);
                if (e == null || string.IsNullOrWhiteSpace(e.Name)) return null;
                if (Entries.Any(x => x.Name.Equals(e.Name, StringComparison.OrdinalIgnoreCase)))
                    e.Name = NextCloneName(e.Name);
                e.Id = Guid.NewGuid().ToString("N");
                Save(e);
                return e;
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
