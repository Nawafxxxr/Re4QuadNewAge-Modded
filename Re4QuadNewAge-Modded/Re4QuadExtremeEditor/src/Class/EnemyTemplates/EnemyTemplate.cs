using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Re4QuadExtremeEditor.src.Class.EnemyTemplates
{
    /// <summary>
    /// Uma linha .ESL tem 32 bytes. Este template guarda cada campo de forma
    /// separada e legível (em vez de apenas um hex cru) para permitir edição
    /// fina e aplicação seletiva dos campos desejados.
    /// </summary>
    public class EnemyTemplate
    {
        // --- Metadados ---
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public List<string> Tags { get; set; }

        // --- Identificação do inimigo ---
        public ushort EnemyId { get; set; }
        public string EnemyName { get; set; }

        // --- Campos da linha (offsets) ---
        public byte Enable { get; set; }          // 0x00
        public byte Unknown03 { get; set; }        // 0x03
        public byte Unknown04 { get; set; }        // 0x04
        public byte Unknown05 { get; set; }        // 0x05
        public byte Unknown06 { get; set; }        // 0x06
        public byte Unknown07 { get; set; }        // 0x07
        public short Life { get; set; }            // 0x08-0x09
        public byte Unknown0A { get; set; }        // 0x0A
        public byte Unknown0B { get; set; }        // 0x0B

        public short PositionX { get; set; }       // 0x0C-0x0D
        public short PositionY { get; set; }       // 0x0E-0x0F
        public short PositionZ { get; set; }       // 0x10-0x11

        public short RotationX { get; set; }       // 0x12-0x13
        public short RotationY { get; set; }       // 0x14-0x15
        public short RotationZ { get; set; }       // 0x16-0x17

        public ushort RoomId { get; set; }         // 0x18-0x19

        public byte Unknown1A { get; set; }        // 0x1A
        public byte Unknown1B { get; set; }        // 0x1B
        public byte Unknown1C { get; set; }        // 0x1C
        public byte Unknown1D { get; set; }        // 0x1D
        public byte Unknown1E { get; set; }        // 0x1E
        public byte Unknown1F { get; set; }        // 0x1F

        /// <summary>Quais campos devem ser aplicados ao destino.</summary>
        public ApplyOptions Apply { get; set; }

        public EnemyTemplate()
        {
            Name = "";
            Description = "";
            Category = "Village";
            Tags = new List<string>();
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

        /// <summary>
        /// Converte os campos separados de volta em uma linha completa de 32 bytes.
        /// Usa valores 0 para qualquer byte inexistente, permitindo gerar uma
        /// linha base mesmo quando o template foi criado manualmente.
        /// </summary>
        public byte[] ToLineBytes()
        {
            byte[] r = new byte[32];
            r[0x00] = Enable;
            r[0x01] = BitConverter.GetBytes(EnemyId)[1];
            r[0x02] = BitConverter.GetBytes(EnemyId)[0];
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

        /// <summary>Preenche todos os campos a partir de uma linha de 32 bytes.</summary>
        public void FromLine(byte[] line)
        {
            if (line == null || line.Length < 32) return;
            Enable = line[0x00];
            EnemyId = BitConverter.ToUInt16(line, 0x01);
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
            t.EnemyName = string.IsNullOrEmpty(enemyName) ? "Unknown" : enemyName;
            return t;
        }

        /// <summary>
        /// Aplica somente os campos marcados no <see cref="Apply"/> ao destino.
        /// Posição/Rotação/Room nunca são sobrescritas por padrão
        /// (Aplicar<= false), mas podem ser ligadas pelo usuário.
        /// </summary>
        public void ApplyToTarget(ushort targetIndex)
        {
            if (DataBase.FileESL == null || !DataBase.FileESL.Lines.ContainsKey(targetIndex))
                return;
            byte[] dst = DataBase.FileESL.Lines[targetIndex];

            if (Apply.Enable)      dst[0x00] = Enable;
            if (Apply.EnemyId)     { BitConverter.GetBytes(EnemyId).CopyTo(dst, 0x01); }
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
            if (Apply.Life)        { BitConverter.GetBytes(Life).CopyTo(dst, 0x08); }
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
            if (Apply.RoomId)      { BitConverter.GetBytes(RoomId).CopyTo(dst, 0x18); }
            if (Apply.UnknownTail)
            {
                dst[0x1A] = Unknown1A;
                dst[0x1B] = Unknown1B;
                dst[0x1C] = Unknown1C;
                dst[0x1D] = Unknown1D;
                dst[0x1E] = Unknown1E;
                dst[0x1F] = Unknown1F;
            }
        }

        public EnemyTemplate Clone()
        {
            var n = new EnemyTemplate();
            n.Name = Name;
            n.Description = Description;
            n.Category = Category;
            n.Tags = new List<string>(Tags);
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

        public JObject ToJson()
        {
            var jo = new JObject
            {
                ["Name"] = Name,
                ["Description"] = Description,
                ["Category"] = Category,
                ["Tags"] = new JArray(Tags),
                ["EnemyId"] = EnemyId.ToString("X4"),
                ["EnemyName"] = EnemyName,
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
            t.Name = o["Name"]?.ToString() ?? "";
            t.Description = o["Description"]?.ToString() ?? "";
            t.Category = o["Category"]?.ToString() ?? "Village";
            if (o["Tags"] is JArray arr)
                t.Tags = arr.Select(x => x.ToString()).ToList();
            try { t.EnemyId = ushort.Parse(o["EnemyId"]?.ToString() ?? "0", System.Globalization.NumberStyles.HexNumber); } catch { }
            t.EnemyName = o["EnemyName"]?.ToString() ?? "Unknown";

            // Novo formato (campos separados)
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
            // Formato antigo: apenas Life + LineHex cru
            else
            {
                try { t.Life = (short)int.Parse(o["Life"]?.ToString() ?? "0"); } catch { }
                string lineHex = o["LineHex"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(lineHex) && lineHex.Length >= 64)
                {
                    byte[] line = new byte[32];
                    try
                    {
                        for (int i = 0; i < 32; i++)
                            line[i] = Convert.ToByte(lineHex.Substring(i * 2, 2), 16);
                        t.FromLine(line);
                    }
                    catch { }
                }
            }

            var a = o["Apply"] as JObject;
            if (a != null) t.Apply.ApplyFromJson(a);
            else t.Apply = ApplyOptions.CreateDefault();

            return t;
        }

        // --- Helpers de leitura de campos ---
        private static byte ReadByte(JObject f, string key, byte def)
        {
            var tok = f[key];
            if (tok == null) return def;
            byte v;
            if (byte.TryParse(tok.ToString(), out v)) return v;
            int iv;
            if (int.TryParse(tok.ToString(), out iv)) return (byte)iv;
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
    /// Determina quais grupos de campos são copiados quando o template é
    /// aplicado a um inimigo de destino.
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

        /// <summary>
        /// Por padrão, aplicamos apenas o "corpo" do inimigo (Enable, ID,
        /// Life e bytes desconhecidos) — nunca mexemos na posição/rotação/sala
        /// do inimigo colocado no mapa.
        /// </summary>
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
