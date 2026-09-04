using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using OpenTK;
using Re4QuadExtremeEditor.src.Class.Enums;
using Re4QuadExtremeEditor.src.Class.Interfaces;
using Re4QuadExtremeEditor.src.Class.TreeNodeObj;

namespace Re4QuadExtremeEditor.src.Class.ElementLibrary
{
    /// <summary>
    /// Bridge between a selected TreeView element and a persisted ElementLibraryEntry.
    /// Capture() reads the element's raw/typed data; Insert() rebuilds a fresh element
    /// of the same type inside the currently-open file. Reuses the exact same cloning
    /// primitives that Ctrl+D (DuplicateSelection) already uses, so stored elements
    /// behave exactly like a duplicated object.
    /// </summary>
    public static class ElementLibraryService
    {
        /// <summary>True if the GroupType can be stored in the library (ESL + CAM_ZONE excluded).</summary>
        public static bool IsStorable(GroupType g)
        {
            if (g == GroupType.NULL) return false;
            if (g == GroupType.ESL) return false;        // has its own template system
            if (g == GroupType.CAM_ZONE) return false;   // part of its camera
            return true;
        }

        public static string GroupLabel(GroupType g)
        {
            switch (g)
            {
                case GroupType.AEV: return "AEV (Event)";
                case GroupType.ITA: return "ITA (Item)";
                case GroupType.ETS: return "ETS (EtcModel)";
                case GroupType.DSE: return "DSE (Door Sound)";
                case GroupType.SMX: return "SMX (Shadow Material)";
                case GroupType.AVL: return "AVL (Aev Lock)";
                case GroupType.FSE: return "FSE (Floor Sound)";
                case GroupType.SAR: return "SAR (Ctrl Light Group)";
                case GroupType.EAR: return "EAR (Ctrl Effect Group)";
                case GroupType.EMI: return "EMI (Interaction Point)";
                case GroupType.ESE: return "ESE (Env Sound)";
                case GroupType.LIT_GROUPS: return "LIT (Light Group)";
                case GroupType.LIT_ENTRYS: return "LIT (Light Entry)";
                case GroupType.CAM: return "CAM (Camera Keyframe)";
                case GroupType.RTP: return "RTP (Route Node)";
                case GroupType.EFF_Table0: return "EFF T0";
                case GroupType.EFF_Table1: return "EFF T1";
                case GroupType.EFF_Table2: return "EFF T2";
                case GroupType.EFF_Table3: return "EFF T3";
                case GroupType.EFF_Table4: return "EFF T4";
                case GroupType.EFF_Table6: return "EFF T6";
                case GroupType.EFF_Table7_Effect_0: return "EFF T7";
                case GroupType.EFF_Table8_Effect_1: return "EFF T8";
                case GroupType.EFF_EffectEntry: return "EFF Entry";
                case GroupType.EFF_Table9: return "EFF T9";
                default: return g.ToString();
            }
        }

        // ---------------------------------------------------------------
        // capture : selected element -> entry.Data
        // ---------------------------------------------------------------

        /// <summary>Fills entry metadata + Data from the selected element. Returns false if unsupported/empty.</summary>
        public static bool Capture(ElementLibraryEntry entry, Object3D node)
        {
            if (entry == null || node == null) return false;
            GroupType g = node.Group;

            if (!IsStorable(g)) return false;

            entry.SetGroupType(g);
            entry.Re4Version = DetectVersion(node);

            if (g == GroupType.CAM)
                return CaptureCam(entry, node);
            if (g == GroupType.RTP)
                return CaptureRtp(entry, node);

            // raw-line based types (single byte[] line via PropertyMethods.ReturnLine)
            byte[] line = ReadLine(node.Parent, node.ObjLineRef);
            if (line == null || line.Length == 0) return false;

            var data = new JObject
            {
                ["line"] = Convert.ToBase64String(line)
            };

            // store position so it can be re-applied / edited
            Vector3 pos = SafeGetPosition(node);
            data["posX"] = pos.X;
            data["posY"] = pos.Y;
            data["posZ"] = pos.Z;

            entry.Data = data;
            return true;
        }

        private static bool CaptureCam(ElementLibraryEntry entry, Object3D node)
        {
            try
            {
                var f = Re4QuadExtremeEditor.src.DataBase.FileCAM;
                if (f == null) return false;
                Re4QuadExtremeEditor.src.Class.Files.CamZoneRecord z;
                Re4QuadExtremeEditor.src.Class.Files.CamCameraRecord c;
                if (!f.TryGetEntry(node.ObjLineRef, out z, out c)) return false;

                var data = new JObject();

                var cam = new JObject
                {
                    ["Unk021"] = c.Unk021,
                    ["CamId"] = c.CamId,
                    ["CamType"] = c.CamType,
                    ["Flags"] = c.Flags,
                    ["Unk025"] = (long)c.Unk025,
                    ["Distance"] = c.Distance,
                    ["Unk027"] = c.Unk027,
                    ["Raw12"] = Convert.ToBase64String(c.Raw12 ?? new byte[12])
                };
                var posArr = new JArray();
                if (c.Positions != null) foreach (var p in c.Positions) posArr.Add(new JArray(p.X, p.Y, p.Z));
                cam["Positions"] = posArr;
                var tgtArr = new JArray();
                if (c.Targets != null) foreach (var p in c.Targets) tgtArr.Add(new JArray(p.X, p.Y, p.Z));
                cam["Targets"] = tgtArr;
                cam["Zoom"] = c.Zoom != null ? new JArray(c.Zoom) : new JArray();
                cam["Fov"] = c.Fov != null ? new JArray(c.Fov) : new JArray();
                cam["TimeFrames"] = c.TimeFrames != null ? new JArray(c.TimeFrames) : new JArray();
                data["cam"] = cam;

                var zone = new JObject
                {
                    ["TriggerType"] = z.TriggerType,
                    ["LinkUnk012"] = z.LinkUnk012,
                    ["Unk015"] = z.Unk015,
                    ["Unk016"] = z.Unk016,
                    ["Unk017"] = z.Unk017,
                    ["Unk051"] = z.Unk051,
                    ["CamTypeTz"] = z.CamTypeTz,
                    ["Subtype"] = z.Subtype,
                    ["Height"] = z.Height,
                    ["Bottom"] = z.Bottom
                };
                if (z.Unk055 != null) zone["Unk055"] = new JArray(z.Unk055);
                var pts = new JArray();
                if (z.Points != null) foreach (var p in z.Points) pts.Add(new JArray(p.X, p.Y, p.Z));
                zone["Points"] = pts;
                data["zone"] = zone;

                entry.Data = data;
                return true;
            }
            catch { return false; }
        }

        private static bool CaptureRtp(ElementLibraryEntry entry, Object3D node)
        {
            try
            {
                var f = Re4QuadExtremeEditor.src.DataBase.FileRTP;
                if (f == null) return false;
                if (node.ObjLineRef >= f.Nodes.Count) return false;
                var nd = f.Nodes[node.ObjLineRef];
                var raw = (byte[])nd.Raw.Clone();
                entry.Data = new JObject
                {
                    ["raw16"] = Convert.ToBase64String(raw),
                    ["pos"] = new JObject { ["x"] = nd.FileX, ["y"] = nd.FileY, ["z"] = nd.FileZ }
                };
                return true;
            }
            catch { return false; }
        }

        // ---------------------------------------------------------------
        // insert : entry.Data -> new element in current file
        // ---------------------------------------------------------------

        /// <summary>
        /// Rebuilds a fresh element of the entry's type into the currently-open file,
        /// returning the created Object3D node(s). Position will be overridden with
        /// posOverride when supplied, else the stored position is kept.
        /// </summary>
        public static List<Object3D> Insert(ElementLibraryEntry entry, Vector3? posOverride)
        {
            var created = new List<Object3D>();
            if (entry == null || entry.Data == null) return created;
            GroupType g = entry.GetGroupType();
            if (!IsStorable(g)) return created;

            if (g == GroupType.CAM)
                return InsertCam(entry, posOverride);
            if (g == GroupType.RTP)
                return InsertRtp(entry, posOverride);

            // raw-line types
            string b64 = entry.Data.Value<string>("line");
            byte[] line = string.IsNullOrEmpty(b64) ? null : Convert.FromBase64String(b64);
            if (line == null || line.Length == 0) return created;

            TreeNode parent = FindParentFor(g);
            if (parent == null) return created;
            var change = parent as INodeChangeAmount;
            if (change == null || change.ChangeAmountMethods == null || change.ChangeAmountMethods.AddNewLineID == null) return created;

            try
            {
                ushort newId = change.ChangeAmountMethods.AddNewLineID(0);
                Object3D clone = Object3D.CreateNewInstance(g, newId);
                parent.Nodes.Add(clone);

                if (WriteLine(parent, newId, line))
                {
                    created.Add(clone);
                }

                // position
                Vector3 pos;
                if (posOverride.HasValue)
                {
                    pos = posOverride.Value;
                }
                else
                {
                    pos = new Vector3(
                        entry.Data.Value<float?>("posX") ?? 0f,
                        entry.Data.Value<float?>("posY") ?? 0f,
                        entry.Data.Value<float?>("posZ") ?? 0f);
                }
                TrySetPosition(clone, pos);
            }
            catch { return created; }
            return created;
        }

        private static List<Object3D> InsertCam(ElementLibraryEntry entry, Vector3? posOverride)
        {
            var created = new List<Object3D>();
            try
            {
                var f = Re4QuadExtremeEditor.src.DataBase.FileCAM;
                var nodeGroup = Re4QuadExtremeEditor.src.DataBase.NodeCAM;
                if (f == null || nodeGroup == null) return created;

                var cam = entry.Data["cam"] as JObject;
                var zone = entry.Data["zone"] as JObject;
                if (cam == null || zone == null) return created;

                // --- rebuild camera record (full track, all keyframes) ---
                var c = new Re4QuadExtremeEditor.src.Class.Files.CamCameraRecord();
                c.Unk021 = (byte)(cam.Value<int?>("Unk021") ?? 1);
                c.CamId = (byte)(cam.Value<int?>("CamId") ?? 1);
                c.CamType = (byte)(cam.Value<int?>("CamType") ?? 0);
                c.Flags = (byte)(cam.Value<int?>("Flags") ?? 0);
                c.Unk025 = (uint)(cam.Value<long?>("Unk025") ?? 0);
                c.Distance = cam.Value<float?>("Distance") ?? 1000f;
                c.Unk027 = cam.Value<float?>("Unk027") ?? 0f;
                string raw12 = cam.Value<string>("Raw12");
                if (!string.IsNullOrEmpty(raw12))
                {
                    try { c.Raw12 = Convert.FromBase64String(raw12); } catch { }
                }
                JArray positions = cam["Positions"] as JArray;
                JArray targets = cam["Targets"] as JArray;
                if (positions != null)
                {
                    foreach (var arr in positions)
                    {
                        if (arr is JArray a && a.Count >= 3)
                            c.Positions.Add(new Re4QuadExtremeEditor.src.Class.Files.CamVector(a[0].Value<float>(), a[1].Value<float>(), a[2].Value<float>()));
                    }
                }
                if (targets != null)
                {
                    foreach (var arr in targets)
                    {
                        if (arr is JArray a && a.Count >= 3)
                            c.Targets.Add(new Re4QuadExtremeEditor.src.Class.Files.CamVector(a[0].Value<float>(), a[1].Value<float>(), a[2].Value<float>()));
                    }
                }
                JArray zoomA = cam["Zoom"] as JArray;
                if (zoomA != null) foreach (var v in zoomA) c.Zoom.Add(v.Value<float>());
                JArray fovA = cam["Fov"] as JArray;
                if (fovA != null) foreach (var v in fovA) c.Fov.Add(v.Value<float>());
                JArray timeA = cam["TimeFrames"] as JArray;
                if (timeA != null) foreach (var v in timeA) c.TimeFrames.Add((ushort)v.Value<int>());

                // --- rebuild zone record ---
                var z = new Re4QuadExtremeEditor.src.Class.Files.CamZoneRecord();
                z.TriggerType = (byte)(zone.Value<int?>("TriggerType") ?? 0x03);
                z.LinkUnk012 = (byte)(zone.Value<int?>("LinkUnk012") ?? 0);
                z.Unk015 = (ushort)(zone.Value<int?>("Unk015") ?? 0);
                z.Unk016 = (ushort)(zone.Value<int?>("Unk016") ?? 0);
                z.Unk017 = (ushort)(zone.Value<int?>("Unk017") ?? 0);
                z.Unk051 = (byte)(zone.Value<int?>("Unk051") ?? 1);
                z.CamTypeTz = (byte)(zone.Value<int?>("CamTypeTz") ?? 0);
                z.Subtype = (byte)(zone.Value<int?>("Subtype") ?? 0x03);
                z.Height = zone.Value<float?>("Height") ?? 1000f;
                z.Bottom = zone.Value<float?>("Bottom") ?? 0f;
                JArray unk055 = zone["Unk055"] as JArray;
                if (unk055 != null)
                {
                    z.Unk055 = new ushort[14];
                    for (int i = 0; i < unk055.Count && i < 14; i++) z.Unk055[i] = (ushort)unk055[i].Value<int>();
                }
                JArray pts = zone["Points"] as JArray;
                if (pts != null)
                {
                    foreach (var arr in pts)
                    {
                        if (arr is JArray a && a.Count >= 3)
                            z.Points.Add(new Re4QuadExtremeEditor.src.Class.Files.CamVector(a[0].Value<float>(), a[1].Value<float>(), a[2].Value<float>()));
                    }
                }

                // optional override: translate the whole camera so key0 lands at the requested position
                OpenTK.Vector3? translate = null;
                if (posOverride.HasValue && c.Positions.Count > 0)
                {
                    var k0 = c.Positions[0];
                    translate = new OpenTK.Vector3(posOverride.Value.X - k0.X, posOverride.Value.Y - k0.Y, posOverride.Value.Z - k0.Z);
                }

                ushort first = f.AddEntryCopy(z, c, translate);
                int total = f.CamNodeList.Count;
                for (int i = first; i < total; i++)
                {
                    if (i >= nodeGroup.Nodes.Count) break;
                    var o = nodeGroup.Nodes[i] as Object3D;
                    if (o != null) created.Add(o);
                }
            }
            catch { }
            return created;
        }

        private static List<Object3D> InsertRtp(ElementLibraryEntry entry, Vector3? posOverride)
        {
            var created = new List<Object3D>();
            try
            {
                var f = Re4QuadExtremeEditor.src.DataBase.FileRTP;
                var nodeGroup = Re4QuadExtremeEditor.src.DataBase.NodeRTP;
                if (f == null || nodeGroup == null) return created;
                if (f.Nodes.Count == 0) return created;

                ushort srcId = f.DuplicateNode(0);
                if (srcId == ushort.MaxValue) return created;
                Object3D clone = Object3D.CreateNewInstance(GroupType.RTP, srcId);
                nodeGroup.Nodes.Add(clone);

                // apply stored raw node data
                string b64 = entry.Data.Value<string>("raw16");
                if (!string.IsNullOrEmpty(b64))
                {
                    try
                    {
                        byte[] raw = Convert.FromBase64String(b64);
                        float x = BitConverter.ToSingle(raw, 0);
                        float y = BitConverter.ToSingle(raw, 4);
                        float z = BitConverter.ToSingle(raw, 8);
                        ushort dt = BitConverter.ToUInt16(raw, 12);
                        ushort cc = BitConverter.ToUInt16(raw, 14);
                        if (posOverride.HasValue) { x = posOverride.Value.X; y = posOverride.Value.Y; z = posOverride.Value.Z; }
                        f.ApplyStoredNode(srcId, x, y, z, dt, cc);
                    }
                    catch { }
                }
                created.Add(clone);
            }
            catch { }
            return created;
        }

        // ---------------------------------------------------------------
        // helpers
        // ---------------------------------------------------------------

        private static string DetectVersion(Object3D node)
        {
            try
            {
                object file = null;
                switch (node.Group)
                {
                    case GroupType.ETS: file = Re4QuadExtremeEditor.src.DataBase.FileETS; break;
                    case GroupType.ITA: file = Re4QuadExtremeEditor.src.DataBase.FileITA; break;
                    case GroupType.AEV: file = Re4QuadExtremeEditor.src.DataBase.FileAEV; break;
                    case GroupType.EMI: file = Re4QuadExtremeEditor.src.DataBase.FileEMI; break;
                    case GroupType.ESE: file = Re4QuadExtremeEditor.src.DataBase.FileESE; break;
                    case GroupType.LIT_GROUPS:
                    case GroupType.LIT_ENTRYS: file = Re4QuadExtremeEditor.src.DataBase.FileLIT; break;
                }
                if (file == null) return "";
                var pi = file.GetType().GetProperty("GetRe4Version");
                if (pi == null) return "";
                return pi.GetValue(file, null)?.ToString() ?? "";
            }
            catch { return ""; }
        }

        public static Vector3 SafeGetPosition(Object3D node)
        {
            try { return node.GetObjPosition_ToCamera(); }
            catch { return Vector3.Zero; }
        }

        public static void TrySetPosition(Object3D node, Vector3 pos)
        {
            try { node.SetObjPosition_ToCamera(pos); }
            catch { }
        }

        private static byte[] ReadLine(TreeNode parent, ushort id)
        {
            try
            {
                PropertyInfo pi = parent != null ? parent.GetType().GetProperty("PropertyMethods") : null;
                if (pi == null) return null;
                object methods = pi.GetValue(parent, null);
                if (methods == null) return null;
                FieldInfo retField = methods.GetType().GetField("ReturnLine");
                if (retField == null) return null;
                Delegate retDel = retField.GetValue(methods) as Delegate;
                if (retDel == null) return null;
                return retDel.DynamicInvoke(id) as byte[];
            }
            catch { return null; }
        }

        private static bool WriteLine(TreeNode parent, ushort id, byte[] line)
        {
            try
            {
                PropertyInfo pi = parent != null ? parent.GetType().GetProperty("PropertyMethods") : null;
                if (pi == null) return false;
                object methods = pi.GetValue(parent, null);
                if (methods == null) return false;
                FieldInfo setField = methods.GetType().GetField("SetLine");
                if (setField == null) return false;
                Delegate setDel = setField.GetValue(methods) as Delegate;
                if (setDel == null) return false;
                setDel.DynamicInvoke(id, line);
                return true;
            }
            catch { return false; }
        }

        private static TreeNode FindParentFor(GroupType g)
        {
            switch (g)
            {
                case GroupType.ETS: return Re4QuadExtremeEditor.src.DataBase.NodeETS;
                case GroupType.ITA: return Re4QuadExtremeEditor.src.DataBase.NodeITA;
                case GroupType.AEV: return Re4QuadExtremeEditor.src.DataBase.NodeAEV;
                case GroupType.DSE: return Re4QuadExtremeEditor.src.DataBase.NodeDSE;
                case GroupType.SMX: return Re4QuadExtremeEditor.src.DataBase.NodeSMX;
                case GroupType.AVL: return Re4QuadExtremeEditor.src.DataBase.NodeAVL;
                case GroupType.FSE: return Re4QuadExtremeEditor.src.DataBase.NodeFSE;
                case GroupType.SAR: return Re4QuadExtremeEditor.src.DataBase.NodeSAR;
                case GroupType.EAR: return Re4QuadExtremeEditor.src.DataBase.NodeEAR;
                case GroupType.EMI: return Re4QuadExtremeEditor.src.DataBase.NodeEMI;
                case GroupType.ESE: return Re4QuadExtremeEditor.src.DataBase.NodeESE;
                case GroupType.LIT_GROUPS: return Re4QuadExtremeEditor.src.DataBase.NodeLIT_Groups;
                case GroupType.LIT_ENTRYS: return Re4QuadExtremeEditor.src.DataBase.NodeLIT_Entrys;
                case GroupType.EFF_Table0: return Re4QuadExtremeEditor.src.DataBase.NodeEFF_Table0;
                case GroupType.EFF_Table1: return Re4QuadExtremeEditor.src.DataBase.NodeEFF_Table1;
                case GroupType.EFF_Table2: return Re4QuadExtremeEditor.src.DataBase.NodeEFF_Table2;
                case GroupType.EFF_Table3: return Re4QuadExtremeEditor.src.DataBase.NodeEFF_Table3;
                case GroupType.EFF_Table4: return Re4QuadExtremeEditor.src.DataBase.NodeEFF_Table4;
                case GroupType.EFF_Table6: return Re4QuadExtremeEditor.src.DataBase.NodeEFF_Table6;
                case GroupType.EFF_Table7_Effect_0: return Re4QuadExtremeEditor.src.DataBase.NodeEFF_Table7_Effect_0;
                case GroupType.EFF_Table8_Effect_1: return Re4QuadExtremeEditor.src.DataBase.NodeEFF_Table8_Effect_1;
                case GroupType.EFF_EffectEntry: return Re4QuadExtremeEditor.src.DataBase.NodeEFF_EffectEntry;
                case GroupType.EFF_Table9: return Re4QuadExtremeEditor.src.DataBase.NodeEFF_Table9;
                default: return null;
            }
        }

        private static float? NestedFloat(JObject data, string obj, string key)
        {
            try
            {
                var o = data[obj] as JObject;
                if (o == null) return null;
                return o.Value<float?>(key);
            }
            catch { return null; }
        }

        private static Vector3 StoredPos(ElementLibraryEntry entry, Vector3 def)
        {
            try
            {
                Vector3 pos = new Vector3(
                    NestedFloat(entry.Data, "pos", "x") ?? def.X,
                    NestedFloat(entry.Data, "pos", "y") ?? def.Y,
                    NestedFloat(entry.Data, "pos", "z") ?? def.Z);
                return pos;
            }
            catch { return def; }
        }
    }
}
