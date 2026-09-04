using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Re4QuadExtremeEditor.src.Class.Enums;

namespace Re4QuadExtremeEditor.src.Class.ElementLibrary
{
    /// <summary>
    /// A single reusable element captured from any generic file type
    /// (AEV, ITA, ETS, DSE, SMX, AVL, FSE, SAR, EAR, ESE, EMI, LIT,
    /// EFF, CAM, RTP ... — ESL is intentionally excluded because it already
    /// has its own template system).
    ///
    /// The actual element payload is stored as an opaque JSON "Data" node.
    /// For the raw-line based types this is a base64 blob of the fixed-size
    /// byte[] line (plus extra companion records where needed). CAM and RTP
    /// store their typed records. Inserting decodes this back into a fresh
    /// element of the same GroupType.
    /// </summary>
    public class ElementLibraryEntry
    {
        public const int CurrentVersion = 1;

        // --- metadata ---
        public int Version { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public List<string> Tags { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // --- element identity ---
        // string form of GroupType so JSON is stable across enum renames
        public string GroupType { get; set; }
        public string Re4Version { get; set; }

        // --- payload : per type, serialized inside JObject ---
        // raw-line types: { "line": "...", "extraLines": { "k": "..." }, "etsId": 123 }
        // CAM           : { "camera": {...}, "keyframe": n }
        // RTP           : { "raw16": "...", "links": [{target,distance},...] }
        public JObject Data { get; set; }

        public ElementLibraryEntry()
        {
            Version = CurrentVersion;
            Id = Guid.NewGuid().ToString("N");
            Name = "";
            Description = "";
            Category = "Custom";
            Tags = new List<string>();
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
            GroupType = GroupTypeEnumName(Enums.GroupType.NULL);
            Re4Version = "";
            Data = null;
        }

        public Enums.GroupType GetGroupType()
        {
            Enums.GroupType g;
            if (Enum.TryParse(GroupType, out g)) return g;
            return Enums.GroupType.NULL;
        }

        public void SetGroupType(Enums.GroupType g) { GroupType = g.ToString(); }

        public string GetCategorySafe()
        {
            return string.IsNullOrWhiteSpace(Category) ? "Custom" : Category.Trim();
        }

        public bool IsValid(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(Name)) { error = "Name is required."; return false; }
            if (GetGroupType() == Enums.GroupType.NULL) { error = "Invalid element type."; return false; }
            if (Data == null) { error = "Element has no data."; return false; }
            return true;
        }

        public ElementLibraryEntry Clone()
        {
            var c = new ElementLibraryEntry();
            c.Version = Version;
            c.Id = Id;
            c.Name = Name;
            c.Description = Description;
            c.Category = Category;
            c.Tags = Tags == null ? new List<string>() : new List<string>(Tags);
            c.CreatedAt = CreatedAt;
            c.UpdatedAt = UpdatedAt;
            c.GroupType = GroupType;
            c.Re4Version = Re4Version;
            c.Data = Data == null ? null : (JObject)Data.DeepClone();
            return c;
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["version"] = Version,
                ["id"] = Id,
                ["name"] = Name,
                ["description"] = Description,
                ["category"] = GetCategorySafe(),
                ["tags"] = new JArray(Tags ?? new List<string>()),
                ["createdAt"] = CreatedAt.ToString("o"),
                ["updatedAt"] = UpdatedAt.ToString("o"),
                ["groupType"] = GroupType,
                ["re4Version"] = Re4Version,
                ["data"] = Data == null ? null : Data.DeepClone()
            };
        }

        public static ElementLibraryEntry FromJson(JObject jo)
        {
            try
            {
                var e = new ElementLibraryEntry();
                e.Version = jo.Value<int?>("version") ?? CurrentVersion;
                e.Id = jo.Value<string>("id");
                if (string.IsNullOrWhiteSpace(e.Id)) e.Id = Guid.NewGuid().ToString("N");
                e.Name = jo.Value<string>("name");
                e.Description = jo.Value<string>("description");
                e.Category = jo.Value<string>("category");
                e.Re4Version = jo.Value<string>("re4Version") ?? "";
                var ta = jo["tags"] as JArray;
                e.Tags = ta == null ? new List<string>() : ta.Values<string>().ToList();
                DateTime dt;
                e.CreatedAt = DateTime.TryParse(jo.Value<string>("createdAt"), out dt) ? dt : DateTime.Now;
                e.UpdatedAt = DateTime.TryParse(jo.Value<string>("updatedAt"), out dt) ? dt : DateTime.Now;
                e.GroupType = jo.Value<string>("groupType");
                e.Data = jo["data"] as JObject;
                return e;
            }
            catch
            {
                return null;
            }
        }

        private static string GroupTypeEnumName(Enums.GroupType g) { return g.ToString(); }
    }
}
