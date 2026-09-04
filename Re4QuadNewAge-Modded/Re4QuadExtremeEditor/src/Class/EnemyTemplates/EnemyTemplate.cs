using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Re4QuadExtremeEditor.src.Class.EnemyTemplates
{
    /// <summary>
    /// Represents a single 32-byte .ESL line as a reusable template.
    /// Storage note — EnemyId is big-endian on disk (0x01 = high, 0x02 = low);
    /// every other multi-byte field is little-endian. Helpers here hide that.
    /// </summary>
    public class EnemyTemplate
    {
        public const int LineLength = 32;
        public const int CurrentVersion = 2;

        // --- metadata ---
        public int Version { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public List<string> Tags { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // --- enemy identity ---
        public ushort EnemyId { get; set; }
        public string EnemyName { get; set; }

        // --- raw line fields ---
        public byte Enable { get; set; }          // 0x00
        public byte Unknown03 { get; set; }        // 0x03
        public byte Unknown04 { get; set; }        // 0x04
        public byte Unknown05 { get; set; }        // 0x05
        public byte Unknown06 { get; set; }        // 0x06
        public byte Unknown07 { get; set; }        // 0x07
        public short Life { get; set; }            // 0x08-0x09 LE
        public byte Unknown0A { get; set; }        // 0x0A
        public byte Unknown0B { get; set; }        // 0x0B
        public short PositionX { get; set; }       // 0x0C-0x0D LE
        public short PositionY { get; set; }       // 0x0E-0x0F LE
        public short PositionZ { get; set; }       // 0x10-0x11 LE
        public short RotationX { get; set; }       // 0x12-0x13 LE
        public short RotationY { get; set; }       // 0x14-0x15 LE
        public short RotationZ { get; set; }       // 0x16-0x17 LE
        public ushort RoomId { get; set; }         // 0x18-0x19 LE
        public byte Unknown1A { get; set; }        // 0x1A
        public byte Unknown1B { get; set; }        // 0x1B
        public byte Unknown1C { get; set; }        // 0x1C
        public byte Unknown1D { get; set; }        // 0x1D
        public byte Unknown1E { get; set; }        // 0x1E
        public byte Unknown1F { get; set; }        // 0x1F

        public ApplyOptions Apply { get; set; }

        public EnemyTemplate()
        {
            Version = CurrentVersion;
            Name = "";
            Description = "";
            Category = "Village";
            Tags = new List<string>();
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
            EnemyId = 0;
            EnemyName = "Unknown";
            Enable = 1;
            Unknown03 = Unknown04 = Unknown05 = Unknown06 = Unknown07 = 0;
            Life = 0;
            Unknown0A = Unknown0B = 0;
            PositionX = PositionY = PositionZ = 0;
            RotationX = RotationY = RotationZ = 0;
            RoomId = 0;
            Unknown1A = Unknown1B = Unknown1C = Unknown1D = Unknown1E = Unknown1F = 0;
            Apply = ApplyOptions.CreateDefault();
        }

        // ------------------------------------------------------------
        // ESL helpers — centralize endian knowledge
        // ------------------------------------------------------------

        private static ushort ReadEnemyIdBE(byte[] line, int offset)
        {
            // BE: high at 0x01, low at 0x02
            byte[] tmp = new byte[2];
            tmp[1] = line[offset];
            tmp[0] = line[offset + 1];
            return BitConverter.ToUInt16(tmp, 0);
        }

        private static void WriteEnemyIdBE(byte[] dst, int offset, ushort value)
        {
            byte[] b = BitConverter.GetBytes(value); // LE
            dst[offset] = b[1];
            dst[offset + 1] = b[0];
        }

        public byte[] ToLineBytes()
        {
            byte[] r = new byte[LineLength];
            r[0x00] = Enable;
            WriteEnemyIdBE(r, 0x01, EnemyId);
            r[0x03] = Unknown03;
            r[0x04] = Unknown04;
            r[0x05] = Unknown05;
            r[0x06] = Unknown06;
            r[0x07] = Unknown07;
            BitConverter.GetBytes(Life).CopyTo(r, 0x08);
            r[0x0A] = Unknown0A;
            r[0x0B] = Unknown0B;
            BitConverter.GetBytes(PositionX).CopyTo(r, 0x0C);
            BitConverter.GetBytes(PositionY).CopyTo(r, 0x0E);
            BitConverter.GetBytes(PositionZ).CopyTo(r, 0x10);
            BitConverter.GetBytes(RotationX).CopyTo(r, 0x12);
            BitConverter.GetBytes(RotationY).CopyTo(r, 0x14);
            BitConverter.GetBytes(RotationZ).CopyTo(r, 0x16);
            BitConverter.GetBytes(RoomId).CopyTo(r, 0x18);
            r[0x1A] = Unknown1A;
            r[0x1B] = Unknown1B;
            r[0x1C] = Unknown1C;
            r[0x1D] = Unknown1D;
            r[0x1E] = Unknown1E;
            r[0x1F] = Unknown1F;
            return r;
        }

        public void FromLine(byte[] line)
        {
            if (line == null || line.Length < LineLength) return;
            Enable = line[0x00];
            EnemyId = ReadEnemyIdBE(line, 0x01);
            Unknown03 = line[0x03];
            Unknown04 = line[0x04];
            Unknown05 = line[0x05];
            Unknown06 = line[0x06];
            Unknown07 = line[0x07];
            Life = BitConverter.ToInt16(line, 0x08);
            Unknown0A = line[0x0A];
            Unknown0B = line[0x0B];
            PositionX = BitConverter.ToInt16(line, 0x0C);
            PositionY = BitConverter.ToInt16(line, 0x0E);
            PositionZ = BitConverter.ToInt16(line, 0x10);
            RotationX = BitConverter.ToInt16(line, 0x12);
            RotationY = BitConverter.ToInt16(line, 0x14);
            RotationZ = BitConverter.ToInt16(line, 0x16);
            RoomId = BitConverter.ToUInt16(line, 0x18);
            Unknown1A = line[0x1A];
            Unknown1B = line[0x1B];
            Unknown1C = line[0x1C];
            Unknown1D = line[0x1D];
            Unknown1E = line[0x1E];
            Unknown1F = line[0x1F];
        }

        public static EnemyTemplate FromEnemy(ushort enemyId, string enemyName, byte[] line)
        {
            var t = new EnemyTemplate();
            t.FromLine(line);
            // Ensure the map's truth wins for identity even if line had stray bytes
            // (the line already contains it, but be explicit)
            t.EnemyId = ReadEnemyIdBE(line, 0x01);
            t.EnemyName = string.IsNullOrEmpty(enemyName) ? "Unknown" : enemyName;
            t.Name = t.EnemyName + " 0x" + t.EnemyId.ToString("X4");
            t.UpdatedAt = t.CreatedAt = DateTime.Now;
            return t;
        }

        // ------------------------------------------------------------
        // Apply
        // ------------------------------------------------------------

        public bool ApplyToTarget(ushort targetIndex)
        {
            if (DataBase.FileESL == null || !DataBase.FileESL.Lines.ContainsKey(targetIndex))
                return false;
            byte[] dst = DataBase.FileESL.Lines[targetIndex];
            if (Apply.Enable) dst[0x00] = Enable;
            if (Apply.EnemyId) WriteEnemyIdBE(dst, 0x01, EnemyId);
            if (Apply.UnknownBody)
            {
                dst[0x03] = Unknown03;
                dst[0x04] = Unknown04;
                dst[0x05] = Unknown05;
                dst[0x06] = Unknown06;
                dst[0x07] = Unknown07;
                dst[0x0A] = Unknown0A;
                dst[0x0B] = Unknown0B;
            }
            if (Apply.Life) BitConverter.GetBytes(Life).CopyTo(dst, 0x08);
            if (Apply.Position)
            {
                BitConverter.GetBytes(PositionX).CopyTo(dst, 0x0C);
                BitConverter.GetBytes(PositionY).CopyTo(dst, 0x0E);
                BitConverter.GetBytes(PositionZ).CopyTo(dst, 0x10);
            }
            if (Apply.Rotation)
            {
                BitConverter.GetBytes(RotationX).CopyTo(dst, 0x12);
                BitConverter.GetBytes(RotationY).CopyTo(dst, 0x14);
                BitConverter.GetBytes(RotationZ).CopyTo(dst, 0x16);
            }
            if (Apply.RoomId) BitConverter.GetBytes(RoomId).CopyTo(dst, 0x18);
            if (Apply.UnknownTail)
            {
                dst[0x1A] = Unknown1A;
                dst[0x1B] = Unknown1B;
                dst[0x1C] = Unknown1C;
                dst[0x1D] = Unknown1D;
                dst[0x1E] = Unknown1E;
                dst[0x1F] = Unknown1F;
            }
            return true;
        }

        public int ApplyToTargets(IEnumerable<ushort> indices)
        {
            if (indices == null) return 0;
            int n = 0;
            foreach (ushort id in indices)
                if (ApplyToTarget(id)) n++;
            return n;
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------

        public string GetCategorySafe()
        {
            if (string.IsNullOrWhiteSpace(Category)) return "Village";
            return Category.Trim();
        }

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(Name)) { error = "Name is required."; return false; }
            if (Name.Length > 64) { error = "Name too long (max 64)."; return false; }
            if (string.IsNullOrWhiteSpace(Category)) { error = "Category is required."; return false; }
            error = null; return true;
        }

        public EnemyTemplate Clone()
        {
            var n = new EnemyTemplate();
            n.Version = Version;
            n.Name = Name;
            n.Description = Description;
            n.Category = Category;
            n.Tags = new List<string>(Tags);
            n.CreatedAt = CreatedAt;
            n.UpdatedAt = DateTime.Now;
            n.EnemyId = EnemyId;
            n.EnemyName = EnemyName;
            n.Enable = Enable;
            n.Unknown03 = Unknown03; n.Unknown04 = Unknown04; n.Unknown05 = Unknown05;
            n.Unknown06 = Unknown06; n.Unknown07 = Unknown07;
            n.Life = Life;
            n.Unknown0A = Unknown0A; n.Unknown0B = Unknown0B;
            n.PositionX = PositionX; n.PositionY = PositionY; n.PositionZ = PositionZ;
            n.RotationX = RotationX; n.RotationY = RotationY; n.RotationZ = RotationZ;
            n.RoomId = RoomId;
            n.Unknown1A = Unknown1A; n.Unknown1B = Unknown1B; n.Unknown1C = Unknown1C;
            n.Unknown1D = Unknown1D; n.Unknown1E = Unknown1E; n.Unknown1F = Unknown1F;
            n.Apply = Apply.Clone();
            return n;
        }

        public string SummaryLine()
        {
            return string.Format("0x{0:X4} {1}  HP:{2}  R:{3:X3}  [{4}]", EnemyId, EnemyName, Life, RoomId, GetCategorySafe());
        }

        // ------------------------------------------------------------
        // JSON
        // ------------------------------------------------------------

        public JObject ToJson()
        {
            var jo = new JObject
            {
                ["Version"] = CurrentVersion,
                ["Name"] = Name ?? "",
                ["Description"] = Description ?? "",
                ["Category"] = GetCategorySafe(),
                ["Tags"] = new JArray(Tags ?? new List<string>()),
                ["EnemyId"] = EnemyId.ToString("X4"),
                ["EnemyName"] = EnemyName ?? "Unknown",
                ["CreatedAt"] = CreatedAt.ToString("o"),
                ["UpdatedAt"] = DateTime.Now.ToString("o"),
                ["Fields"] = new JObject
                {
                    ["Enable"] = Enable,
                    ["Unknown03"] = Unknown03,
                    ["Unknown04"] = Unknown04,
                    ["Unknown05"] = Unknown05,
                    ["Unknown06"] = Unknown06,
                    ["Unknown07"] = Unknown07,
                    ["Life"] = Life,
                    ["Unknown0A"] = Unknown0A,
                    ["Unknown0B"] = Unknown0B,
                    ["PositionX"] = PositionX,
                    ["PositionY"] = PositionY,
                    ["PositionZ"] = PositionZ,
                    ["RotationX"] = RotationX,
                    ["RotationY"] = RotationY,
                    ["RotationZ"] = RotationZ,
                    ["RoomId"] = RoomId,
                    ["Unknown1A"] = Unknown1A,
                    ["Unknown1B"] = Unknown1B,
                    ["Unknown1C"] = Unknown1C,
                    ["Unknown1D"] = Unknown1D,
                    ["Unknown1E"] = Unknown1E,
                    ["Unknown1F"] = Unknown1F,
                },
                ["Apply"] = Apply.ToJson(),
            };
            return jo;
        }

        public static EnemyTemplate FromJson(JObject o)
        {
            if (o == null) return null;
            var t = new EnemyTemplate();
            try { t.Version = o["Version"] != null ? (int)o["Version"] : 1; } catch { t.Version = 1; }
            t.Name = o["Name"]?.ToString() ?? "";
            t.Description = o["Description"]?.ToString() ?? "";
            t.Category = o["Category"]?.ToString() ?? "Village";
            if (o["Tags"] is JArray arr)
                t.Tags = arr.Select(x => x.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            try { t.EnemyId = ushort.Parse(o["EnemyId"]?.ToString() ?? "0", System.Globalization.NumberStyles.HexNumber); } catch { }
            t.EnemyName = o["EnemyName"]?.ToString() ?? "Unknown";
            DateTime ca, ua;
            if (DateTime.TryParse(o["CreatedAt"]?.ToString(), out ca)) t.CreatedAt = ca;
            if (DateTime.TryParse(o["UpdatedAt"]?.ToString(), out ua)) t.UpdatedAt = ua;

            var f = o["Fields"] as JObject;
            if (f != null)
            {
                t.Enable = ReadByte(f, "Enable", 1);
                t.Unknown03 = ReadByte(f, "Unknown03", 0);
                t.Unknown04 = ReadByte(f, "Unknown04", 0);
                t.Unknown05 = ReadByte(f, "Unknown05", 0);
                t.Unknown06 = ReadByte(f, "Unknown06", 0);
                t.Unknown07 = ReadByte(f, "Unknown07", 0);
                t.Life = ReadShort(f, "Life", 0);
                t.Unknown0A = ReadByte(f, "Unknown0A", 0);
                t.Unknown0B = ReadByte(f, "Unknown0B", 0);
                t.PositionX = ReadShort(f, "PositionX", 0);
                t.PositionY = ReadShort(f, "PositionY", 0);
                t.PositionZ = ReadShort(f, "PositionZ", 0);
                t.RotationX = ReadShort(f, "RotationX", 0);
                t.RotationY = ReadShort(f, "RotationY", 0);
                t.RotationZ = ReadShort(f, "RotationZ", 0);
                t.RoomId = ReadUShort(f, "RoomId", 0);
                t.Unknown1A = ReadByte(f, "Unknown1A", 0);
                t.Unknown1B = ReadByte(f, "Unknown1B", 0);
                t.Unknown1C = ReadByte(f, "Unknown1C", 0);
                t.Unknown1D = ReadByte(f, "Unknown1D", 0);
                t.Unknown1E = ReadByte(f, "Unknown1E", 0);
                t.Unknown1F = ReadByte(f, "Unknown1F", 0);
            }
            else
            {
                // Legacy: LineHex + Life
                try { t.Life = (short)int.Parse(o["Life"]?.ToString() ?? "0"); } catch { }
                string lineHex = o["LineHex"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(lineHex) && lineHex.Length >= 64)
                {
                    byte[] line = new byte[32];
                    try
                    {
                        for (int i = 0; i < 32; i++)
                            line[i] = Convert.ToByte(lineHex.Substring(i * 2, 2), 16);
                        // Old loader swapped EnemyId; normalize via BE read
                        t.FromLine(line);
                        // Life in legacy was separate; keep file value if present
                    }
                    catch { }
                }
            }

            var a = o["Apply"] as JObject;
            if (a != null) t.Apply.ApplyFromJson(a);
            else t.Apply = ApplyOptions.CreateDefault();

            // sanitize
            if (string.IsNullOrWhiteSpace(t.Name)) t.Name = "Template 0x" + t.EnemyId.ToString("X4");
            if (string.IsNullOrWhiteSpace(t.Category)) t.Category = "Village";
            t.Version = CurrentVersion;
            return t;
        }

        private static byte ReadByte(JObject f, string key, byte def)
        {
            var tok = f[key];
            if (tok == null) return def;
            byte v;
            if (byte.TryParse(tok.ToString(), out v)) return v;
            int iv;
            if (int.TryParse(tok.ToString(), out iv)) return (byte)(iv & 0xFF);
            return def;
        }
        private static short ReadShort(JObject f, string key, short def)
        {
            var tok = f[key];
            if (tok == null) return def;
            short v;
            if (short.TryParse(tok.ToString(), out v)) return v;
            int iv;
            if (int.TryParse(tok.ToString(), out iv)) return (short)iv;
            return def;
        }
        private static ushort ReadUShort(JObject f, string key, ushort def)
        {
            var tok = f[key];
            if (tok == null) return def;
            ushort v;
            if (ushort.TryParse(tok.ToString(), out v)) return v;
            int iv;
            if (int.TryParse(tok.ToString(), out iv)) return (ushort)iv;
            return def;
        }
    }

    /// <summary>
    /// Which field groups are copied when a template is applied.
    /// </summary>
    public class ApplyOptions
    {
        public bool Enable { get; set; }
        public bool EnemyId { get; set; }
        public bool Life { get; set; }
        public bool UnknownBody { get; set; }
        public bool Position { get; set; }
        public bool Rotation { get; set; }
        public bool RoomId { get; set; }
        public bool UnknownTail { get; set; }

        public static ApplyOptions CreateDefault()
        {
            return new ApplyOptions
            {
                Enable = true,
                EnemyId = true,
                Life = true,
                UnknownBody = true,
                Position = false,
                Rotation = false,
                RoomId = false,
                UnknownTail = true,
            };
        }

        public ApplyOptions Clone()
        {
            return new ApplyOptions
            {
                Enable = Enable,
                EnemyId = EnemyId,
                Life = Life,
                UnknownBody = UnknownBody,
                Position = Position,
                Rotation = Rotation,
                RoomId = RoomId,
                UnknownTail = UnknownTail,
            };
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["Enable"] = Enable,
                ["EnemyId"] = EnemyId,
                ["Life"] = Life,
                ["UnknownBody"] = UnknownBody,
                ["Position"] = Position,
                ["Rotation"] = Rotation,
                ["RoomId"] = RoomId,
                ["UnknownTail"] = UnknownTail,
            };
        }

        public void ApplyFromJson(JObject a)
        {
            Enable = BoolOf(a, "Enable", Enable);
            EnemyId = BoolOf(a, "EnemyId", EnemyId);
            Life = BoolOf(a, "Life", Life);
            UnknownBody = BoolOf(a, "UnknownBody", UnknownBody);
            Position = BoolOf(a, "Position", Position);
            Rotation = BoolOf(a, "Rotation", Rotation);
            RoomId = BoolOf(a, "RoomId", RoomId);
            UnknownTail = BoolOf(a, "UnknownTail", UnknownTail);
        }

        public IEnumerable<string> EnabledGroups()
        {
            if (Enable) yield return "Enable";
            if (EnemyId) yield return "EnemyId";
            if (Life) yield return "Life";
            if (UnknownBody) yield return "Body(03-0B)";
            if (Position) yield return "Position";
            if (Rotation) yield return "Rotation";
            if (RoomId) yield return "Room";
            if (UnknownTail) yield return "Tail(1A-1F)";
        }

        private static bool BoolOf(JObject a, string key, bool def)
        {
            var tok = a[key];
            if (tok == null) return def;
            bool b;
            if (bool.TryParse(tok.ToString(), out b)) return b;
            return def;
        }
    }
}
