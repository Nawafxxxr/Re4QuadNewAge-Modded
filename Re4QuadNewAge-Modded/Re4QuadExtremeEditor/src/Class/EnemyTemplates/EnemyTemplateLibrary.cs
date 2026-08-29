using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Re4QuadExtremeEditor.src.Class.EnemyTemplates
{
    public static class EnemyTemplateLibrary
    {
        private static readonly string Dir = Path.Combine("data", "EnemyTemplates");
        private static List<EnemyTemplate> _list = null;

        public static List<EnemyTemplate> Templates { get { if (_list == null) Load(); return _list; } }

        /// <summary>
        /// Lista todas as categorias usadas pelos templates (em ordem de
        /// aparição), para que o filtro seja dinâmico em vez de fixo.
        /// </summary>
        public static List<string> Categories
        {
            get { return Templates.Select(t => t.Category).Where(c => !string.IsNullOrEmpty(c)).Distinct().OrderBy(c => c).ToList(); }
        }

        public static void Load()
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
                        var t = EnemyTemplate.FromJson(JObject.Parse(File.ReadAllText(f)));
                        if (t != null && !string.IsNullOrEmpty(t.Name) && !_list.Any(x => x.Name == t.Name))
                            _list.Add(t);
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static void Save(EnemyTemplate t)
        {
            if (_list == null) Load();
            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
            string safe = SafeName(t.Name);
            try { File.WriteAllText(Path.Combine(Dir, safe + ".json"), t.ToJson().ToString()); }
            catch { }
            int idx = _list.FindIndex(x => x.Name == t.Name);
            if (idx >= 0) _list[idx] = t; else _list.Add(t);
        }

        public static void Delete(EnemyTemplate t)
        {
            string safe = SafeName(t.Name);
            try { File.Delete(Path.Combine(Dir, safe + ".json")); } catch { }
            _list.Remove(t);
        }

        public static List<EnemyTemplate> Search(string category, string search, IEnumerable<string> tags = null)
        {
            var list = Templates;
            if (!string.IsNullOrEmpty(category) && category != "All")
                list = list.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrEmpty(search))
            {
                string q = search.ToLowerInvariant();
                list = list.Where(t =>
                    t.Name.ToLowerInvariant().Contains(q) ||
                    (t.EnemyName ?? "").ToLowerInvariant().Contains(q) ||
                    t.EnemyId.ToString("X4").ToLowerInvariant().Contains(q) ||
                    (t.Description ?? "").ToLowerInvariant().Contains(q) ||
                    t.Tags.Any(tag => tag.ToLowerInvariant().Contains(q))).ToList();
            }
            if (tags != null && tags.Any())
            {
                var lower = tags.Select(x => x.ToLowerInvariant()).ToList();
                list = list.Where(t => lower.All(l => t.Tags.Any(tag => tag.ToLowerInvariant() == l))).ToList();
            }
            return list;
        }

        /// <summary>Normaliza um texto para virar um nome de arquivo seguro.</summary>
        public static string SafeName(string name)
        {
            string safe = string.Join("_", (name ?? "").Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safe)) safe = "Template_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            return safe;
        }

        /// <summary>Retorna um novo nome não repetido para clones.</summary>
        public static string NextCloneName(string baseName)
        {
            string baseN = baseName ?? "Template";
            int i = 2;
            string candidate = baseN + " (2)";
            while (Templates.Any(t => t.Name == candidate))
            {
                i++;
                candidate = baseN + " (" + i + ")";
            }
            return candidate;
        }
    }
}
