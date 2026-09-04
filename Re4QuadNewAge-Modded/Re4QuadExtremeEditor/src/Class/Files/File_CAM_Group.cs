using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Re4QuadExtremeEditor.src.Class.CustomDelegates;
using Re4QuadExtremeEditor.src.Class.Enums;
using Re4QuadExtremeEditor.src.Class.ObjMethods;
using SimpleEndianBinaryIO;
using OpenTK;

namespace Re4QuadExtremeEditor.src.Class.Files
{
    public class CamVector
    {
        public float X;
        public float Y;
        public float Z;

        public CamVector() { }
        public CamVector(float x, float y, float z) { X = x; Y = y; Z = z; }

        public CamVector Clone()
        {
            return new CamVector(X, Y, Z);
        }

        public override string ToString()
        {
            return "(" + X.ToString("0.###") + ", " + Y.ToString("0.###") + ", " + Z.ToString("0.###") + ")";
        }
    }

    /// <summary>
    /// Table3 record: camera setup + keyframe buffers
    /// </summary>
    public class CamCameraRecord
    {
        public byte Unk021 = 1;
        public byte CamId = 1;
        public byte CamType = 0;
        public byte Flags = 0;
        public uint Unk025 = 0;
        public float Distance = 1000f;
        public float Unk027 = 0f;
        public byte[] Raw12 = new byte[12];
        public uint RawBuf0Addr = 0;
        public uint RawBuf1Addr = 0;
        public uint RawBuf2Addr = 0;
        public uint RawBuf3Addr = 0;
        public uint RawBuf4Addr = 0;
        public List<CamVector> Positions = new List<CamVector>();
        public List<CamVector> Targets = new List<CamVector>();
        public List<float> Zoom = new List<float>();
        public List<float> Fov = new List<float>();
        public List<ushort> TimeFrames = new List<ushort>();
        public int SelectedKeyframe = 0;
    }

    /// <summary>
    /// Table1 link (16 bytes) + Table2 record (48 bytes) pair
    /// </summary>
    public class CamZoneRecord
    {
        public byte TriggerType = 0x03;
        public byte LinkUnk012 = 0;
        public ushort Unk015 = 0;
        public ushort Unk016 = 0;
        public ushort Unk017 = 0;
        public int CameraIndex = -1;
        public byte Unk051 = 1;
        public byte EntryNumber = 1;
        public byte CamTypeTz = 0;
        public byte Subtype = 0x03;
        public ushort[] Unk055 = new ushort[14];
        public float Height = 1000f;
        public float Bottom = 0f;
        public List<CamVector> Points = new List<CamVector>();
        public int SelectedPoint = 0;
        public uint Table2Addr = 0;
        public uint Table3Addr = 0;
    }

    public class File_CAM_Group : BaseGroup
    {
        public const uint Magic = 0x34303442; // 'B404'
        private const int T1Size = 0x10;
        private const int T2Size = 0x30;
        private const int T3Size = 0x34;
        private const int T4Size = 0x10;

        public static readonly string[] CamTypeNames = new string[]
        {
            "Locked Position & Rotation",
            "Align Behind Player (FOV pull)",
            "Overhead Locked",
            "Overhead Locked (tracks player)",
            "Position & Rotation + Pan",
            "Locked Rotation + Player Position",
            "Locked + Animated (Inspection)",
            "Locked on TriggerZone Entry",
            "Over The Shoulder"
        };

        private static readonly ushort[] KnownTriggerTypes = new ushort[]
        {
            0x00, 0x01, 0x03, 0x04, 0x23, 0x3B, 0x41, 0x43, 0x63, 0x81, 0x83
        };

        public byte HdrUnk004 = 0;
        public uint HdrUnk005 = 0;
        public uint HdrUnk006 = 0;
        public List<byte[]> Table4Records = new List<byte[]>();

        public Dictionary<ushort, CamCameraRecord> Cameras { get; private set; }
        public Dictionary<ushort, CamZoneRecord> Zones { get; private set; }

        /// <summary>
        /// one tree node per exported keyframe, following the JADERLINK OBJ rules:
        /// Type 8 cameras expose only Ky08 (index 7), other types expose every keyframe.
        /// </summary>
        public struct CamNodeKey
        {
            public ushort Entry;
            public int Keyframe;
        }

        private readonly List<CamNodeKey> camNodeList = new List<CamNodeKey>();
        public IList<CamNodeKey> CamNodeList { get { return camNodeList; } }

        public void RebuildCamNodeList()
        {
            camNodeList.Clear();
            List<ushort> entries = Zones.Keys.OrderBy(x => x).ToList();
            foreach (ushort e in entries)
            {
                CamZoneRecord z = Zones[e];
                if (z.CameraIndex < 0 || !Cameras.ContainsKey((ushort)z.CameraIndex))
                {
                    continue;
                }
                CamCameraRecord c = Cameras[(ushort)z.CameraIndex];
                if (c.Positions == null || c.Positions.Count == 0)
                {
                    continue;
                }
                if (c.CamType == 8)
                {
                    int ky = c.Positions.Count >= 8 ? 7 : 0;
                    camNodeList.Add(new CamNodeKey { Entry = e, Keyframe = ky });
                }
                else
                {
                    for (int k = 0; k < c.Positions.Count; k++)
                    {
                        camNodeList.Add(new CamNodeKey { Entry = e, Keyframe = k });
                    }
                }
            }
        }

        public ushort IdForNewCamera = 0;
        public ushort IdForNewZone = 0;
        public ushort LastAddedZoneID = 0;

        private byte[] TrailingPadding = new byte[0];

        public File_CAM_Group()
        {
            Cameras = new Dictionary<ushort, CamCameraRecord>();
            Zones = new Dictionary<ushort, CamZoneRecord>();

            DisplayMethods = new NodeDisplayMethods();
            DisplayMethods.GetNodeText = GetNodeTextCamera;
            DisplayMethods.GetNodeColor = GetNodeColor;

            MoveMethods = new NodeMoveMethods();
            MoveMethods.GetObjPostion_ToCamera = GetCameraPos_ToCamera;
            MoveMethods.GetObjAngleY_ToCamera = GetCameraAimYaw;
            MoveMethods.GetObjPostion_ToMove_General = GetCameraPostion_ToMove;
            MoveMethods.SetObjPostion_ToMove_General = SetCameraPostion_ToMove;
        MoveMethods.GetObjRotationAngles_ToMove = GetCameraRotationAngles_ToMove;
        MoveMethods.SetObjRotationAngles_ToMove = SetCameraRotationAngles_ToMove;
            MoveMethods.GetObjScale_ToMove = Utils.GetObjScale_ToMove_Null;
            MoveMethods.SetObjScale_ToMove = Utils.SetObjScale_ToMove_Null;
            MoveMethods.GetTriggerZoneCategory = Utils.GetTriggerZoneCategory_Null;

            ChangeAmountMethods = new NodeChangeAmountMethods();
            ChangeAmountMethods.AddNewLineID = AddNewCameraID;
            ChangeAmountMethods.RemoveLineID = RemoveCameraID;

            ZoneDisplayMethods = new NodeDisplayMethods();
            ZoneDisplayMethods.GetNodeText = GetNodeTextZone;
            ZoneDisplayMethods.GetNodeColor = GetNodeColorZone;

            ZoneMoveMethods = new NodeMoveMethods();
            ZoneMoveMethods.GetObjPostion_ToCamera = GetZonePos_ToCamera;
            ZoneMoveMethods.GetObjAngleY_ToCamera = Utils.GetObjAngleY_ToCamera_Null;
            ZoneMoveMethods.GetObjPostion_ToMove_General = GetZonePostion_ToMove;
            ZoneMoveMethods.SetObjPostion_ToMove_General = SetZonePostion_ToMove;
            ZoneMoveMethods.GetObjRotationAngles_ToMove = Utils.GetObjRotationAngles_ToMove_Null;
            ZoneMoveMethods.SetObjRotationAngles_ToMove = Utils.SetObjRotationAngles_ToMove_Null;
            ZoneMoveMethods.GetObjScale_ToMove = Utils.GetObjScale_ToMove_Null;
            ZoneMoveMethods.SetObjScale_ToMove = Utils.SetObjScale_ToMove_Null;
            ZoneMoveMethods.GetTriggerZoneCategory = GetZoneCategoryGL;

            ZoneChangeAmountMethods = new NodeChangeAmountMethods();
            ZoneChangeAmountMethods.AddNewLineID = AddNewZoneID;
            ZoneChangeAmountMethods.RemoveLineID = RemoveZoneID;

            MethodsForGL = new NewAge_CAM_MethodsForGL();
            MethodsForGL.GetCameraPosition = GetCameraPositionGL;
            MethodsForGL.GetCameraTarget = GetCameraTargetGL;
            MethodsForGL.GetHasData = GetHasDataGL;
            MethodsForGL.GetCameraColor = GetCameraColorGL;

            ZoneMethodsForGL = new NewAge_CAM_Zone_MethodsForGL();
            ZoneMethodsForGL.GetTriggerZoneMatrix4 = GetZoneMatrix4GL;
            ZoneMethodsForGL.GetZoneCategory = GetZoneCategoryGL;
            ZoneMethodsForGL.GetZonePoints = GetZonePointsGL;
            ZoneMethodsForGL.GetZoneBottom = GetZoneBottomGL;
            ZoneMethodsForGL.GetZoneTop = GetZoneTopGL;
            ZoneMethodsForGL.GetZoneColor = GetZoneColorGL;

            Methods = new NewAge_CAM_Methods();
            SetBaseMethods(Methods);
            WireCameraMethods();

            ZoneMethods = new NewAge_CAM_Zone_Methods();
            SetBaseMethods(ZoneMethods);
            WireZoneMethods();
        }

        public NewAge_CAM_Methods Methods { get; }
        public NodeDisplayMethods DisplayMethods { get; }
        public NodeMoveMethods MoveMethods { get; }
        public NodeChangeAmountMethods ChangeAmountMethods { get; }
        public NewAge_CAM_MethodsForGL MethodsForGL { get; }

        public NewAge_CAM_Zone_Methods ZoneMethods { get; }
        public NodeDisplayMethods ZoneDisplayMethods { get; }
        public NodeMoveMethods ZoneMoveMethods { get; }
        public NodeChangeAmountMethods ZoneChangeAmountMethods { get; }
        public NewAge_CAM_Zone_MethodsForGL ZoneMethodsForGL { get; }

        #region parse

        public void Load(byte[] all)
        {
            if (all == null || all.Length < 16)
            {
                throw new InvalidDataException("Invalid CAM file! File is too small.");
            }

            uint magic = BitConverter.ToUInt32(all, 0);
            if (magic != Magic)
            {
                throw new InvalidDataException("Invalid CAM file! Expected magic B404, got 0x" + magic.ToString("X8"));
            }

            Cameras.Clear();
            Zones.Clear();
            Table4Records.Clear();

            byte t3count = all[4];
            byte t1count = all[5];
            byte t4count = all[6];
            HdrUnk004 = all[7];
            HdrUnk005 = BitConverter.ToUInt32(all, 8);
            HdrUnk006 = BitConverter.ToUInt32(all, 12);

            int offset = 0x10;

            for (int i = 0; i < t1count; i++)
            {
                RequireBytes(all, offset, T1Size, "Table1 entry " + (i + 1));
                CamZoneRecord z = new CamZoneRecord();
                z.TriggerType = all[offset];
                z.LinkUnk012 = all[offset + 1];
                z.Unk015 = BitConverter.ToUInt16(all, offset + 2);
                z.Unk016 = BitConverter.ToUInt16(all, offset + 4);
                z.Unk017 = BitConverter.ToUInt16(all, offset + 6);
                z.CameraIndex = -1;
                z.Table2Addr = BitConverter.ToUInt32(all, offset + 8);
                z.Table3Addr = BitConverter.ToUInt32(all, offset + 12);
                Zones.Add((ushort)i, z);
                offset += T1Size;
            }

            List<uint> table2Positions = new List<uint>();
            foreach (var pair in Zones)
            {
                CamZoneRecord z = pair.Value;
                int start = offset;
                table2Positions.Add((uint)start);
                RequireBytes(all, offset, T2Size, "Table2 entry " + (pair.Key + 1));
                z.Unk051 = all[offset];
                z.EntryNumber = all[offset + 1];
                z.CamTypeTz = all[offset + 2];
                z.Subtype = all[offset + 3];
                for (int j = 0; j < 14; j++)
                {
                    z.Unk055[j] = BitConverter.ToUInt16(all, offset + 4 + j * 2);
                }
                z.Height = BitConverter.ToSingle(all, offset + 32);
                z.Bottom = BitConverter.ToSingle(all, offset + 36);
                uint coordCount = BitConverter.ToUInt32(all, offset + 40);
                uint dataAddr = BitConverter.ToUInt32(all, offset + 44);

                if (dataAddr > 0 && coordCount > 0)
                {
                    RequireBytes(all, (int)dataAddr, (int)(coordCount * 12), "TriggerZone points of entry " + (pair.Key + 1));
                    for (int j = 0; j < (int)coordCount; j++)
                    {
                        int addr = (int)dataAddr + j * 12;
                        z.Points.Add(new CamVector(
                            BitConverter.ToSingle(all, addr),
                            BitConverter.ToSingle(all, addr + 4),
                            BitConverter.ToSingle(all, addr + 8)));
                    }
                }
                offset = start + T2Size;
            }

            List<uint> table3Positions = new List<uint>();
            for (int i = 0; i < t3count; i++)
            {
                int start = offset;
                table3Positions.Add((uint)start);
                RequireBytes(all, offset, T3Size, "Table3 camera " + (i + 1));
                CamCameraRecord c = new CamCameraRecord();
                c.Unk021 = all[offset];
                c.CamId = all[offset + 1];
                c.CamType = all[offset + 2];
                c.Flags = all[offset + 3];
                c.Unk025 = BitConverter.ToUInt32(all, offset + 4);
                c.Distance = BitConverter.ToSingle(all, offset + 8);
                c.Unk027 = BitConverter.ToSingle(all, offset + 12);
                c.RawBuf0Addr = BitConverter.ToUInt32(all, offset + 16);
                Array.Copy(all, offset + 20, c.Raw12, 0, 12);
                uint bufCount = BitConverter.ToUInt32(all, offset + 32);
                uint buf1 = BitConverter.ToUInt32(all, offset + 36);
                uint buf2 = BitConverter.ToUInt32(all, offset + 40);
                uint buf3 = BitConverter.ToUInt32(all, offset + 44);
                uint buf4 = BitConverter.ToUInt32(all, offset + 48);
                c.RawBuf1Addr = buf1;
                c.RawBuf2Addr = buf2;
                c.RawBuf3Addr = buf3;
                c.RawBuf4Addr = buf4;

                if (bufCount > 0)
                {
                    if (buf1 > 0)
                    {
                        RequireBytes(all, (int)buf1, (int)(bufCount * 12), "positions buffer of camera " + (i + 1));
                        for (int j = 0; j < (int)bufCount; j++)
                        {
                            int addr = (int)buf1 + j * 12;
                            c.Positions.Add(new CamVector(
                                BitConverter.ToSingle(all, addr),
                                BitConverter.ToSingle(all, addr + 4),
                                BitConverter.ToSingle(all, addr + 8)));
                        }
                    }
                    if (buf2 > 0)
                    {
                        RequireBytes(all, (int)buf2, (int)(bufCount * 12), "targets buffer of camera " + (i + 1));
                        for (int j = 0; j < (int)bufCount; j++)
                        {
                            int addr = (int)buf2 + j * 12;
                            c.Targets.Add(new CamVector(
                                BitConverter.ToSingle(all, addr),
                                BitConverter.ToSingle(all, addr + 4),
                                BitConverter.ToSingle(all, addr + 8)));
                        }
                    }
                    if (buf3 > 0)
                    {
                        RequireBytes(all, (int)buf3, (int)(bufCount * 4), "zoom buffer of camera " + (i + 1));
                        for (int j = 0; j < (int)bufCount; j++)
                        {
                            c.Zoom.Add(BitConverter.ToSingle(all, (int)buf3 + j * 4));
                        }
                    }
                    if (buf4 > 0)
                    {
                        RequireBytes(all, (int)buf4, (int)(bufCount * 4), "fov buffer of camera " + (i + 1));
                        for (int j = 0; j < (int)bufCount; j++)
                        {
                            c.Fov.Add(BitConverter.ToSingle(all, (int)buf4 + j * 4));
                        }
                    }
                    if (c.CamType == 6 && c.RawBuf0Addr > 0)
                    {
                        RequireBytes(all, (int)c.RawBuf0Addr, (int)(bufCount * 2), "timeline buffer of camera " + (i + 1));
                        for (int j = 0; j < (int)bufCount; j++)
                        {
                            c.TimeFrames.Add(BitConverter.ToUInt16(all, (int)c.RawBuf0Addr + j * 2));
                        }
                    }
                }
                Cameras.Add((ushort)i, c);
                offset = start + T3Size;
            }

            for (int i = 0; i < t4count; i++)
            {
                RequireBytes(all, offset, T4Size, "Table4 entry " + (i + 1));
                byte[] rec = new byte[T4Size];
                Array.Copy(all, offset, rec, 0, T4Size);
                Table4Records.Add(rec);
                offset += T4Size;
            }

            foreach (var pair in Zones)
            {
                CamZoneRecord z = pair.Value;
                for (int idx = 0; idx < table3Positions.Count; idx++)
                {
                    if (z.Table3Addr == table3Positions[idx])
                    {
                        z.CameraIndex = idx;
                        break;
                    }
                }
            }

            int maxEnd = offset;
            foreach (var pair in Zones)
            {
                CamZoneRecord z = pair.Value;
                if (z.Points.Count > 0 && z.Table2Addr > 0 && z.Table2Addr + 48 <= all.Length)
                {
                    uint dataAddr = BitConverter.ToUInt32(all, (int)z.Table2Addr + 44);
                    long end = dataAddr + (long)z.Points.Count * 12;
                    if (dataAddr > 0 && end <= all.Length && end > maxEnd) maxEnd = (int)end;
                }
            }
            foreach (var pair in Cameras)
            {
                CamCameraRecord c = pair.Value;
                int n = c.Positions.Count;
                if (n <= 0) continue;
                if (c.RawBuf1Addr > 0) { long e = c.RawBuf1Addr + (long)n * 12; if (e <= all.Length && e > maxEnd) maxEnd = (int)e; }
                if (c.RawBuf2Addr > 0) { long e = c.RawBuf2Addr + (long)n * 12; if (e <= all.Length && e > maxEnd) maxEnd = (int)e; }
                if (c.RawBuf3Addr > 0) { long e = c.RawBuf3Addr + (long)n * 4; if (e <= all.Length && e > maxEnd) maxEnd = (int)e; }
                if (c.RawBuf4Addr > 0) { long e = c.RawBuf4Addr + (long)n * 4; if (e <= all.Length && e > maxEnd) maxEnd = (int)e; }
                if (c.CamType == 6 && c.TimeFrames.Count > 0 && c.RawBuf0Addr > 0)
                {
                    long e = c.RawBuf0Addr + (long)c.TimeFrames.Count * 2;
                    if (e <= all.Length && e > maxEnd) maxEnd = (int)e;
                }
            }

            TrailingPadding = new byte[0];
            if (all.Length > maxEnd)
            {
                TrailingPadding = new byte[all.Length - maxEnd];
                Array.Copy(all, maxEnd, TrailingPadding, 0, TrailingPadding.Length);
            }

            IdForNewCamera = (ushort)Cameras.Count;
            IdForNewZone = (ushort)Zones.Count;
        }

        private static void RequireBytes(byte[] data, int offset, int size, string what)
        {
            if (offset < 0 || size < 0 || offset + size > data.Length)
            {
                throw new InvalidDataException("CAM file is truncated or corrupted near " + what
                    + " (needed bytes at offset 0x" + offset.ToString("X") + ").");
            }
        }

        #endregion

        #region write

        public void WriteTo(Stream stream)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter w = new BinaryWriter(ms);

            int zoneCount = Zones.Count;
            int camCount = Cameras.Count;

            int ptr = 0x10;
            ptr += zoneCount * T1Size;
            int t2Base = ptr;
            ptr += zoneCount * T2Size;
            int t3Base = ptr;
            ptr += camCount * T3Size;
            ptr += Table4Records.Count * T4Size;

            uint[] t2Addrs = new uint[zoneCount];
            uint[] t3Addrs = new uint[zoneCount];
            int zi = 0;
            foreach (var pair in Zones)
            {
                t2Addrs[zi] = (uint)(t2Base + zi * T2Size);
                int ci = pair.Value.CameraIndex;
                t3Addrs[zi] = (ci >= 0 && ci < camCount) ? (uint)(t3Base + ci * T3Size) : 0u;
                zi++;
            }

            uint[] coordAddrs = new uint[zoneCount];
            zi = 0;
            foreach (var pair in Zones)
            {
                if (pair.Value.Points.Count > 0)
                {
                    coordAddrs[zi] = (uint)ptr;
                    ptr += pair.Value.Points.Count * 12;
                }
                else
                {
                    coordAddrs[zi] = 0;
                }
                zi++;
            }

            List<CamVector[]> syncPos = new List<CamVector[]>();
            List<CamVector[]> syncTgt = new List<CamVector[]>();
            List<float[]> syncZoom = new List<float[]>();
            List<float[]> syncFov = new List<float[]>();
            List<ushort> camOrder = new List<ushort>(Cameras.Keys);
            foreach (ushort key in camOrder)
            {
                CamCameraRecord c = Cameras[key];
                int n = c.Positions.Count;
                CamVector[] pos = new CamVector[n];
                CamVector[] tgt = new CamVector[n];
                float[] zoom = new float[n];
                float[] fov = new float[n];
                for (int j = 0; j < n; j++)
                {
                    pos[j] = c.Positions[j];
                }
                for (int j = 0; j < n; j++)
                {
                    tgt[j] = j < c.Targets.Count ? c.Targets[j] : (n > 0 ? pos[Math.Min(j, n - 1)] : new CamVector());
                }
                for (int j = 0; j < n; j++)
                {
                    zoom[j] = j < c.Zoom.Count ? c.Zoom[j] : 0f;
                }
                for (int j = 0; j < n; j++)
                {
                    fov[j] = j < c.Fov.Count ? c.Fov[j] : 50f;
                }
                syncPos.Add(pos);
                syncTgt.Add(tgt);
                syncZoom.Add(zoom);
                syncFov.Add(fov);
            }

            uint[][] bufAddrs = new uint[camCount][];
            for (int ci = 0; ci < camCount; ci++)
            {
                int n = syncPos[ci].Length;
                if (n > 0)
                {
                    uint a1 = (uint)ptr; ptr += n * 12;
                    uint a2 = (uint)ptr; ptr += n * 12;
                    uint a3 = (uint)ptr; ptr += n * 4;
                    uint a4 = (uint)ptr; ptr += n * 4;
                    bufAddrs[ci] = new uint[] { 0, a1, a2, a3, a4 };
                }
                else
                {
                    uint same = (uint)ptr;
                    bufAddrs[ci] = new uint[] { 0, same, same, same, same };
                }
            }

            uint[] buf0Addrs = new uint[camCount];
            List<int> type6Cams = new List<int>();
            for (int ci = 0; ci < camCount; ci++)
            {
                CamCameraRecord c = Cameras[camOrder[ci]];
                // same rules as the CAM tool: Speed_01 = 0, strictly ascending,
                // one Speed entry per keyframe
                SyncAndNormalizeSpeeds(c);
                if (c.CamType == 6 && c.TimeFrames.Count > 0)
                {
                    buf0Addrs[ci] = (uint)ptr;
                    ptr += c.TimeFrames.Count * 2;
                    type6Cams.Add(ci);
                }
                else if (c.CamType == 6)
                {
                    // animated camera without speed data gets no buffer, like the tool
                    buf0Addrs[ci] = 0;
                }
                else
                {
                    buf0Addrs[ci] = c.RawBuf0Addr;
                }
            }

            w.Write(Magic);
            w.Write((byte)camCount);
            w.Write((byte)zoneCount);
            w.Write((byte)Table4Records.Count);
            w.Write(HdrUnk004);
            w.Write(HdrUnk005);
            w.Write(HdrUnk006);

            zi = 0;
            foreach (var pair in Zones)
            {
                CamZoneRecord z = pair.Value;
                w.Write(z.TriggerType);
                w.Write(z.LinkUnk012);
                w.Write(z.Unk015);
                w.Write(z.Unk016);
                w.Write(z.Unk017);
                w.Write(t2Addrs[zi]);
                w.Write(t3Addrs[zi]);
                zi++;
            }

            zi = 0;
            foreach (var pair in Zones)
            {
                CamZoneRecord z = pair.Value;
                w.Write(z.Unk051);
                w.Write(z.EntryNumber);
                w.Write(z.CamTypeTz);
                w.Write(z.Subtype);
                for (int j = 0; j < 14; j++)
                {
                    w.Write(z.Unk055[j]);
                }
                w.Write(z.Height);
                w.Write(z.Bottom);
                w.Write((uint)z.Points.Count);
                w.Write(coordAddrs[zi]);
                zi++;
            }

            for (int ci = 0; ci < camCount; ci++)
            {
                CamCameraRecord c = Cameras[camOrder[ci]];
                w.Write(c.Unk021);
                w.Write(c.CamId);
                w.Write(c.CamType);
                w.Write(c.Flags);
                w.Write(c.Unk025);
                w.Write(c.Distance);
                w.Write(c.Unk027);
                w.Write(buf0Addrs[ci]);
                w.Write(c.Raw12);
                w.Write((uint)syncPos[ci].Length);
                w.Write(bufAddrs[ci][1]);
                w.Write(bufAddrs[ci][2]);
                w.Write(bufAddrs[ci][3]);
                w.Write(bufAddrs[ci][4]);
            }

            foreach (byte[] rec in Table4Records)
            {
                w.Write(rec);
            }

            zi = 0;
            foreach (var pair in Zones)
            {
                if (coordAddrs[zi] != 0)
                {
                    foreach (CamVector v in pair.Value.Points)
                    {
                        w.Write(v.X);
                        w.Write(v.Y);
                        w.Write(v.Z);
                    }
                }
                zi++;
            }

            for (int ci = 0; ci < camCount; ci++)
            {
                int n = syncPos[ci].Length;
                if (n <= 0)
                {
                    continue;
                }
                for (int j = 0; j < n; j++)
                {
                    w.Write(syncPos[ci][j].X);
                    w.Write(syncPos[ci][j].Y);
                    w.Write(syncPos[ci][j].Z);
                }
                for (int j = 0; j < n; j++)
                {
                    w.Write(syncTgt[ci][j].X);
                    w.Write(syncTgt[ci][j].Y);
                    w.Write(syncTgt[ci][j].Z);
                }
                for (int j = 0; j < n; j++)
                {
                    w.Write(syncZoom[ci][j]);
                }
                for (int j = 0; j < n; j++)
                {
                    w.Write(syncFov[ci][j]);
                }
            }

            foreach (int ci in type6Cams)
            {
                CamCameraRecord c = Cameras[camOrder[ci]];
                foreach (ushort v in c.TimeFrames)
                {
                    w.Write(v);
                }
            }

            if (TrailingPadding != null && TrailingPadding.Length > 0)
            {
                w.Write(TrailingPadding);
            }

            ms.WriteTo(stream);
            stream.Flush();
        }

        #endregion

        #region gl and move

        private const float CamUnitScale = 100f;

        private static CamVector KeyframeAt(List<CamVector> list, int index)
        {
            if (list == null || list.Count == 0) return null;
            if (index < 0 || index >= list.Count) index = 0;
            return list[index];
        }

        /// <summary>
        /// resolves a tree-node id into its entry/zone/camera/keyframe tuple
        /// </summary>
        private bool TryCamNode(ushort id, out ushort entry, out CamZoneRecord zone, out CamCameraRecord cam, out int ky)
        {
            entry = 0; zone = null; cam = null; ky = -1;
            if (id >= camNodeList.Count) return false;
            CamNodeKey key = camNodeList[id];
            if (!Zones.ContainsKey(key.Entry)) return false;
            CamZoneRecord z = Zones[key.Entry];
            if (z.CameraIndex < 0 || !Cameras.ContainsKey((ushort)z.CameraIndex)) return false;
            CamCameraRecord c = Cameras[(ushort)z.CameraIndex];
            if (c.Positions == null || c.Positions.Count == 0) return false;
            int k = key.Keyframe;
            if (k < 0 || k >= c.Positions.Count) k = 0;
            entry = key.Entry; zone = z; cam = c; ky = k;
            return true;
        }

        private CamCameraRecord NodeCam(ushort id)
        {
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            return TryCamNode(id, out e, out z, out c, out ky) ? c : null;
        }

        private int NodeKy(ushort id)
        {
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            return TryCamNode(id, out e, out z, out c, out ky) ? ky : -1;
        }

        private byte NodeCamType(ushort id)
        {
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            if (!TryCamNode(id, out e, out z, out c, out ky)) return 0;
            return c.CamType;
        }

        /// <summary>camera type that drives naming/colors of an entry (linked setup first, T2 type as fallback)</summary>
        public byte GetEntryCamType(ushort entry)
        {
            if (!Zones.ContainsKey(entry)) return 0;
            CamZoneRecord z = Zones[entry];
            if (z.CameraIndex >= 0 && Cameras.ContainsKey((ushort)z.CameraIndex))
            {
                return Cameras[(ushort)z.CameraIndex].CamType;
            }
            return Math.Min(z.CamTypeTz, (byte)8);
        }

        // JADERLINK OBJ material palette: base zone color, camera color = +0.2 capped
        internal static readonly float[][] TypeBaseRGB = new float[][]
        {
            new float[] { 0.8f, 0.8f, 0.8f },
            new float[] { 0.2f, 0.8f, 0.2f },
            new float[] { 0.2f, 0.2f, 0.8f },
            new float[] { 0.0f, 0.8f, 0.8f },
            new float[] { 0.8f, 0.8f, 0.0f },
            new float[] { 0.8f, 0.4f, 0.0f },
            new float[] { 0.8f, 0.0f, 0.8f },
            new float[] { 0.4f, 0.4f, 0.8f },
            new float[] { 1.0f, 0.3f, 0.3f }
        };

        public static System.Drawing.Color GetCameraColorOf(byte t)
        {
            float[] b = TypeBaseRGB[Math.Min(t, (byte)(TypeBaseRGB.Length - 1))];
            return System.Drawing.Color.FromArgb(255,
                (int)(Math.Min(b[0] + 0.2f, 1f) * 255f),
                (int)(Math.Min(b[1] + 0.2f, 1f) * 255f),
                (int)(Math.Min(b[2] + 0.2f, 1f) * 255f));
        }

        public static System.Drawing.Color GetZoneColorOf(byte t)
        {
            float[] b = TypeBaseRGB[Math.Min(t, (byte)(TypeBaseRGB.Length - 1))];
            return System.Drawing.Color.FromArgb(255,
                (int)(b[0] * 255f), (int)(b[1] * 255f), (int)(b[2] * 255f));
        }

        private Vector4 GetCameraColorGL(ushort ID)
        {
            System.Drawing.Color c = GetCameraColorOf(NodeCamType(ID));
            return new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, 1f);
        }

        private Vector3 GetCameraPositionGL(ushort ID)
        {
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            if (!TryCamNode(ID, out e, out z, out c, out ky)) return Vector3.Zero;
            CamVector p = KeyframeAt(c.Positions, ky);
            if (p == null) return Vector3.Zero;
            return new Vector3(p.X / CamUnitScale, p.Y / CamUnitScale, p.Z / CamUnitScale);
        }

        private Vector3 GetCameraTargetGL(ushort ID)
        {
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            if (!TryCamNode(ID, out e, out z, out c, out ky)) return Vector3.Zero;
            CamVector p = KeyframeAt(c.Targets, ky);
            if (p == null)
            {
                CamVector pos = KeyframeAt(c.Positions, ky);
                if (pos == null) return Vector3.Zero;
                return new Vector3(pos.X / CamUnitScale, pos.Y / CamUnitScale, pos.Z / CamUnitScale);
            }
            return new Vector3(p.X / CamUnitScale, p.Y / CamUnitScale, p.Z / CamUnitScale);
        }

        private bool GetHasDataGL(ushort ID)
        {
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            return TryCamNode(ID, out e, out z, out c, out ky);
        }

        /// <summary>
        /// Data for the "Enter Camera View" first-person preview:
        /// eye position, look-at target (GL units) and the selected keyframe FOV.
        /// </summary>
        public bool TryGetCamViewData(ushort nodeID, out OpenTK.Vector3 eye, out OpenTK.Vector3 target, out float fovDeg)
        {
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            eye = OpenTK.Vector3.Zero;
            target = OpenTK.Vector3.UnitZ;
            fovDeg = 50f;
            if (!TryCamNode(nodeID, out e, out z, out c, out ky))
            {
                return false;
            }
            eye = GetCameraPositionGL(nodeID);
            target = GetCameraTargetGL(nodeID);
            // use THIS node's own keyframe (not the record's SelectedKeyframe),
            // so Ky02 previews with Ky02's FOV and reacts to its FOV edits
            int k = ky;
            if (k >= c.Fov.Count)
            {
                k = c.Fov.Count - 1;
            }
            if (k >= 0 && k < c.Fov.Count)
            {
                fovDeg = c.Fov[k];
            }
            if (float.IsNaN(fovDeg) || fovDeg < 5f || fovDeg > 170f)
            {
                fovDeg = 50f;
            }
            return true;
        }

        /// <summary>
        /// Points the record's SelectedKeyframe at the clicked tree node's
        /// keyframe, so the property grid edits that exact keyframe.
        /// </summary>
        public void SyncSelectedKeyframeFromNode(ushort nodeID)
        {
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            if (!TryCamNode(nodeID, out e, out z, out c, out ky))
            {
                return;
            }
            if (ky >= 0 && ky < c.Positions.Count)
            {
                c.SelectedKeyframe = ky;
            }
        }

        /// <summary>Applies a stored keyframe (pos/target/zoom/fov/time) to the given camera node.</summary>
        public void ApplyStoredKeyframe(ushort nodeID, OpenTK.Vector3 pos, OpenTK.Vector3 tgt, float zoom, float fov, ushort time)
        {
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            if (!TryCamNode(nodeID, out e, out z, out c, out ky))
            {
                return;
            }
            if (ky < 0 || c.Positions == null || c.Positions.Count == 0)
            {
                return;
            }
            int k = Math.Min(ky, c.Positions.Count - 1);
            if (k < c.Positions.Count) c.Positions[k] = new CamVector(pos.X, pos.Y, pos.Z);
            if (k < c.Targets.Count) c.Targets[k] = new CamVector(tgt.X, tgt.Y, tgt.Z);
            if (k < c.Zoom.Count) c.Zoom[k] = zoom;
            if (k < c.Fov.Count) c.Fov[k] = fov;
            if (k < c.TimeFrames.Count) c.TimeFrames[k] = time;
        }

        /// <summary>Captures a keyframe's values for the given camera node.</summary>
        public bool TryGetStoredKeyframe(ushort nodeID, out OpenTK.Vector3 pos, out OpenTK.Vector3 tgt, out float zoom, out float fov, out ushort time)
        {
            pos = OpenTK.Vector3.Zero;
            tgt = OpenTK.Vector3.UnitZ;
            zoom = 0f;
            fov = 50f;
            time = 0;
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            if (!TryCamNode(nodeID, out e, out z, out c, out ky))
            {
                return false;
            }
            if (ky < 0 || c.Positions == null || c.Positions.Count == 0)
            {
                return false;
            }
            int k = Math.Min(ky, c.Positions.Count - 1);
            pos = new OpenTK.Vector3(c.Positions[k].X, c.Positions[k].Y, c.Positions[k].Z);
            if (k < c.Targets.Count) tgt = new OpenTK.Vector3(c.Targets[k].X, c.Targets[k].Y, c.Targets[k].Z);
            if (k < c.Zoom.Count) zoom = c.Zoom[k];
            if (k < c.Fov.Count) fov = c.Fov[k];
            if (k < c.TimeFrames.Count) time = c.TimeFrames[k];
            return true;
        }

        /// <summary>Resolves a CAM tree node to its full owner zone + camera records (whole track with all keyframes).</summary>
        public bool TryGetEntry(ushort nodeID, out CamZoneRecord zone, out CamCameraRecord cam)
        {
            zone = null; cam = null;
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            if (!TryCamNode(nodeID, out e, out z, out c, out ky)) return false;
            zone = z; cam = c;
            return true;
        }

        /// <summary>
        /// Deep-copies a full camera entry (zone + camera record with all keyframes)
        /// into a brand-new independent entry. Optionally translates every keyframe
        /// position by the given world offset. Returns the first camNodeList ID of
        /// the new entry, or throws on failure.
        /// </summary>
        public ushort AddEntryCopy(CamZoneRecord zoneSrc, CamCameraRecord camSrc, OpenTK.Vector3? translate)
        {
            if (Cameras.Count >= Consts.AmountLimitCAM || Zones.Count >= Consts.AmountLimitCAM)
            {
                throw new InvalidOperationException("Camera limit reached.");
            }

            OpenTK.Vector3 t = translate ?? OpenTK.Vector3.Zero;

            ushort newId = FindFreeID(Cameras.Keys);
            CamCameraRecord c = new CamCameraRecord();
            c.Unk021 = camSrc.Unk021;
            c.CamId = camSrc.CamId;
            c.CamType = camSrc.CamType;
            c.Flags = camSrc.Flags;
            c.Unk025 = camSrc.Unk025;
            c.Distance = camSrc.Distance;
            c.Unk027 = camSrc.Unk027;
            if (camSrc.Raw12 != null) c.Raw12 = (byte[])camSrc.Raw12.Clone();
            c.RawBuf0Addr = 0; c.RawBuf1Addr = 0; c.RawBuf2Addr = 0; c.RawBuf3Addr = 0; c.RawBuf4Addr = 0;
            if (camSrc.Positions != null)
            {
                foreach (var p in camSrc.Positions)
                {
                    c.Positions.Add(new CamVector(p.X + t.X, p.Y + t.Y, p.Z + t.Z));
                }
            }
            if (camSrc.Targets != null)
            {
                foreach (var p in camSrc.Targets)
                {
                    c.Targets.Add(p.Clone());
                }
            }
            if (camSrc.Zoom != null) c.Zoom.AddRange(camSrc.Zoom);
            if (camSrc.Fov != null) c.Fov.AddRange(camSrc.Fov);
            if (camSrc.TimeFrames != null) c.TimeFrames.AddRange(camSrc.TimeFrames);
            c.CamId = newId > 255 ? (byte)255 : (byte)newId;

            Cameras.Add(newId, c);

            ushort zoneId = FindFreeID(Zones.Keys);
            CamZoneRecord z = new CamZoneRecord();
            z.TriggerType = zoneSrc.TriggerType;
            z.LinkUnk012 = zoneSrc.LinkUnk012;
            z.Unk015 = zoneSrc.Unk015;
            z.Unk016 = zoneSrc.Unk016;
            z.Unk017 = zoneSrc.Unk017;
            z.CameraIndex = newId;
            z.Unk051 = zoneSrc.Unk051;
            z.EntryNumber = (byte)Math.Min(255, Zones.Count + 1);
            z.CamTypeTz = zoneSrc.CamTypeTz;
            z.Subtype = zoneSrc.Subtype;
            if (zoneSrc.Unk055 != null)
            {
                z.Unk055 = new ushort[zoneSrc.Unk055.Length];
                Array.Copy(zoneSrc.Unk055, z.Unk055, zoneSrc.Unk055.Length);
            }
            z.Height = zoneSrc.Height;
            z.Bottom = zoneSrc.Bottom;
            if (zoneSrc.Points != null)
            {
                foreach (var p in zoneSrc.Points)
                {
                    z.Points.Add(new CamVector(p.X, p.Y, p.Z));
                }
            }
            z.Table2Addr = 0; z.Table3Addr = 0;
            Zones.Add(zoneId, z);
            LastAddedZoneID = zoneId;

            RebuildCamNodeList();
            SyncTreeNodesToCamNodeList();
            SyncZoneTreeNodesToKeys();

            for (ushort i = 0; i < camNodeList.Count; i++)
            {
                if (camNodeList[i].Entry == zoneId)
                {
                    return i;
                }
            }
            return 0;
        }

        private Vector3 GetCameraPos_ToCamera(ushort ID)
        {
            Vector3 position = GetCameraPositionGL(ID);
            Utils.ToCameraCheckValue(ref position);
            return position;
        }

        private float GetCameraAimYaw(ushort ID)
        {
            Vector3 pos = GetCameraPositionGL(ID);
            Vector3 tgt = GetCameraTargetGL(ID);
            float dx = tgt.X - pos.X;
            float dz = tgt.Z - pos.Z;
            float yaw = (float)Math.Atan2(dx, dz);
            if (float.IsNaN(yaw) || float.IsInfinity(yaw)) yaw = 0f;
            return yaw;
        }

        private Vector3[] GetCameraPostion_ToMove(ushort ID)
        {
            // raw file units, same convention as the AEV/SAR/EAR/FSE zones
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            Vector3[] pos = new Vector3[1];
            if (!TryCamNode(ID, out e, out z, out c, out ky)) { pos[0] = Vector3.Zero; return pos; }
            CamVector p = KeyframeAt(c.Positions, ky);
            if (p == null) { pos[0] = Vector3.Zero; return pos; }
            pos[0] = new Vector3(p.X, p.Y, p.Z);
            return pos;
        }

        private void SetCameraPostion_ToMove(ushort ID, Vector3[] value)
        {
            if (value == null || value.Length < 1) return;
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            if (!TryCamNode(ID, out e, out z, out c, out ky)) return;
            CamVector p = KeyframeAt(c.Positions, ky);
            CamVector t = KeyframeAt(c.Targets, ky);
            if (p == null) return;

            // move the whole camera model (pyramid + aim line + target) as a single rigid unit
            float dx = value[0].X - p.X;
            float dy = value[0].Y - p.Y;
            float dz = value[0].Z - p.Z;

            p.X = value[0].X;
            p.Y = value[0].Y;
            p.Z = value[0].Z;

            if (t != null)
            {
                t.X += dx;
                t.Y += dy;
                t.Z += dz;
            }
        }

        private Vector3[] GetCameraRotationAngles_ToMove(ushort ID)
        {
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            Vector3[] v = new Vector3[1];
            if (!TryCamNode(ID, out e, out z, out c, out ky)) { v[0] = Vector3.Zero; return v; }
            CamVector p = KeyframeAt(c.Positions, ky);
            CamVector t = KeyframeAt(c.Targets, ky);
            if (p == null || t == null) { v[0] = Vector3.Zero; return v; }

            float dx = t.X - p.X;
            float dy = t.Y - p.Y;
            float dz = t.Z - p.Z;
            float hd = (float)Math.Sqrt(dx * dx + dz * dz);

            float yaw = (float)Math.Atan2(dx, dz);
            float pitch = (float)Math.Atan2(dy, hd);
            if (float.IsNaN(yaw)) yaw = 0f;
            if (float.IsNaN(pitch)) pitch = 0f;
            v[0] = new Vector3(pitch, yaw, 0f);
            Utils.ToMoveCheckLimits(ref v);
            return v;
        }

        private void SetCameraRotationAngles_ToMove(ushort ID, Vector3[] value)
        {
            if (value == null || value.Length < 1) return;
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            if (!TryCamNode(ID, out e, out z, out c, out ky)) return;
            CamVector p = KeyframeAt(c.Positions, ky);
            CamVector t = KeyframeAt(c.Targets, ky);
            if (p == null || t == null) return;

            // rotate the whole camera model: rebuild the aim direction on X (pitch) and Y (yaw)
            float pitch = value[0].X;
            float yaw = value[0].Y;

            float dx = t.X - p.X;
            float dy = t.Y - p.Y;
            float dz = t.Z - p.Z;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (dist <= 0f) dist = 100f;

            float cp = (float)Math.Cos(pitch);
            t.X = p.X + ((float)Math.Sin(yaw) * cp * dist);
            t.Y = p.Y + ((float)Math.Sin(pitch) * dist);
            t.Z = p.Z + ((float)Math.Cos(yaw) * cp * dist);
        }

        private Vector3 GetZonePos_ToCamera(ushort ID)
        {
            if (!Zones.ContainsKey(ID)) return Vector3.Zero;
            CamZoneRecord z = Zones[ID];
            if (z.Points.Count == 0) return Vector3.Zero;

            float minX = z.Points[0].X, maxX = z.Points[0].X;
            float minZ = z.Points[0].Z, maxZ = z.Points[0].Z;
            foreach (CamVector p in z.Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Z < minZ) minZ = p.Z;
                if (p.Z > maxZ) maxZ = p.Z;
            }
            float midY = z.Bottom + z.Height * 0.5f;

            Vector3 position = new Vector3(
                (minX + (maxX - minX) * 0.5f) / CamUnitScale,
                midY / CamUnitScale,
                (minZ + (maxZ - minZ) * 0.5f) / CamUnitScale);
            Utils.ToCameraCheckValue(ref position);
            return position;
        }

        private Vector3[] GetZonePostion_ToMove(ushort ID)
        {
            if (!Zones.ContainsKey(ID)) return new Vector3[0];
            CamZoneRecord z = Zones[ID];
            if (z.Points.Count < 4) return new Vector3[0];

            // exact same 7-element layout used by the AEV/SAR/EAR/FSE trigger zones
            // [0] entry position (none here) | [1..4] corners XZ | [5] (radius, Bottom, Height) | [6] center
            Vector3[] pos = new Vector3[7];
            pos[0] = Vector3.Zero;
            pos[1] = new Vector3(z.Points[0].X, 0f, z.Points[0].Z);
            pos[2] = new Vector3(z.Points[1].X, 0f, z.Points[1].Z);
            pos[3] = new Vector3(z.Points[2].X, 0f, z.Points[2].Z);
            pos[4] = new Vector3(z.Points[3].X, 0f, z.Points[3].Z);
            pos[5] = new Vector3(0f, z.Bottom, z.Height);

            float minX = pos[1].X, maxX = pos[1].X, minZ = pos[1].Z, maxZ = pos[1].Z;
            for (int i = 2; i <= 4; i++)
            {
                if (pos[i].X < minX) minX = pos[i].X;
                if (pos[i].X > maxX) maxX = pos[i].X;
                if (pos[i].Z < minZ) minZ = pos[i].Z;
                if (pos[i].Z > maxZ) maxZ = pos[i].Z;
            }
            pos[6] = new Vector3(minX + ((maxX - minX) / 2f), 0f, minZ + ((maxZ - minZ) / 2f));

            Utils.ToMoveCheckLimits(ref pos);
            return pos;
        }

        private void SetZonePostion_ToMove(ushort ID, Vector3[] value)
        {
            if (value == null || value.Length < 6) return;
            if (!Zones.ContainsKey(ID)) return;
            CamZoneRecord z = Zones[ID];
            if (z.Points.Count < 4) return;

            Utils.ToMoveCheckLimits(ref value);

            z.Points[0].X = value[1].X;
            z.Points[0].Z = value[1].Z;
            z.Points[1].X = value[2].X;
            z.Points[1].Z = value[2].Z;
            z.Points[2].X = value[3].X;
            z.Points[2].Z = value[3].Z;
            z.Points[3].X = value[4].X;
            z.Points[3].Z = value[4].Z;

            float bottom = value[5].Y;
            float height = value[5].Z;
            if (height < 0f) height = 0f;
            z.Bottom = bottom;
            z.Height = height;
        }

        private TriggerZoneCategory GetZoneCategoryGL(ushort ID)
        {
            return TriggerZoneCategory.Category01;
        }

        private Vector3[] GetZonePointsGL(ushort ID)
        {
            if (!Zones.ContainsKey(ID)) return new Vector3[0];
            List<CamVector> pts = Zones[ID].Points;
            Vector3[] pos = new Vector3[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                pos[i] = new Vector3(pts[i].X / CamUnitScale, 0f, pts[i].Z / CamUnitScale);
            }
            return pos;
        }

        private float GetZoneBottomGL(ushort ID)
        {
            return Zones.ContainsKey(ID) ? Zones[ID].Bottom / CamUnitScale : 0f;
        }

        private float GetZoneTopGL(ushort ID)
        {
            return Zones.ContainsKey(ID) ? (Zones[ID].Height + Zones[ID].Bottom) / CamUnitScale : 0f;
        }

        private Vector4 GetZoneColorGL(ushort ID)
        {
            byte t = GetEntryCamType(ID);
            System.Drawing.Color c = GetZoneColorOf(t);
            return new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, 1f);
        }

        private Matrix4 GetZoneMatrix4GL(ushort ID)
        {
            if (!Zones.ContainsKey(ID))
            {
                return Matrix4.Identity;
            }
            CamZoneRecord z = Zones[ID];
            if (z.Points.Count < 4)
            {
                return Matrix4.Identity;
            }

            // same layout used by the AEV/SAR/EAR/FSE trigger zone matrix:
            // row0 = Corner0 + TrueY, row1 = Corner1 + MoreHeight,
            // row2 = Corner2 + CircleRadius(unused), row3 = Corner3 + Top
            return new Matrix4(
                z.Points[0].X / CamUnitScale, z.Bottom / CamUnitScale, z.Points[0].Z / CamUnitScale, 0f,
                z.Points[1].X / CamUnitScale, z.Height / CamUnitScale, z.Points[1].Z / CamUnitScale, 0f,
                z.Points[2].X / CamUnitScale, 0f, z.Points[2].Z / CamUnitScale, 0f,
                z.Points[3].X / CamUnitScale, (z.Bottom + z.Height) / CamUnitScale, z.Points[3].Z / CamUnitScale, 0f);
        }

        #endregion

        #region node text

        public string GetNodeTextCamera(ushort ID)
        {
            ushort entry; CamZoneRecord z; CamCameraRecord c; int ky;
            if (!TryCamNode(ID, out entry, out z, out c, out ky))
            {
                return "Camera_" + (ID + 1).ToString("D4") + "_InvalidRef";
            }
            // exact JADERLINK OBJ naming: Camera_0005_CAMType6_ID5_Ky01
            return "Camera_" + (entry + 1).ToString("D4")
                + "_CAMType" + c.CamType
                + "_ID" + c.CamId
                + "_Ky" + (ky + 1).ToString("D2");
        }

        public string GetNodeTextZone(ushort ID)
        {
            if (!Zones.ContainsKey(ID))
            {
                return "TriggerZone_" + (ID + 1).ToString("D4") + "_InvalidRef";
            }
            CamZoneRecord z = Zones[ID];
            byte t = GetEntryCamType(ID);
            // exact JADERLINK OBJ naming: TriggerZone_0001_CAMType8_Trigger0x43
            return "TriggerZone_" + (ID + 1).ToString("D4")
                + "_CAMType" + t
                + "_Trigger0x" + z.TriggerType.ToString("X2");
        }

        public System.Drawing.Color GetNodeColor(ushort ID)
        {
            // white on the dark tree, near-black on the light one
            return Re4QuadExtremeEditor.UiTheme.IsLight
                ? System.Drawing.Color.FromArgb(45, 50, 58)
                : System.Drawing.Color.White;
        }

        public System.Drawing.Color GetNodeColorZone(ushort ID)
        {
            return Re4QuadExtremeEditor.UiTheme.IsLight
                ? System.Drawing.Color.FromArgb(45, 50, 58)
                : System.Drawing.Color.White;
        }

        #endregion

        #region change amount

        private ushort AddNewCameraID(byte initType)
        {
            if (Cameras.Count >= Consts.AmountLimitCAM || Zones.Count >= Consts.AmountLimitCAM)
            {
                throw new InvalidOperationException("Camera limit reached.");
            }

            // brand-new independent setup: one camera record + its own trigger,
            // never touching any existing camera (keys stay gap-free because
            // removal reindexes, so the free ID is always == Count)
            ushort newId = FindFreeID(Cameras.Keys);
            Cameras.Add(newId, CreateDefaultCamera());

            ushort zoneId = FindFreeID(Zones.Keys);
            Zones.Add(zoneId, BuildDefaultZone());
            Zones[zoneId].CameraIndex = newId;
            LastAddedZoneID = zoneId;

            // InitType selects one of the ready-made "From Template" presets
            if (initType < CamTemplates.Length)
            {
                ApplyCamTemplate(Cameras[newId], Zones[zoneId], initType);
            }
            // honest unique ID for naming (avoids "_ID1_" collisions with
            // original records that also carry CamId 1)
            Cameras[newId].CamId = newId > 255 ? (byte)255 : (byte)newId;

            RebuildCamNodeList();
            SyncTreeNodesToCamNodeList();
            SyncZoneTreeNodesToKeys();

            // first tree node belonging to this new entry
            for (ushort i = 0; i < camNodeList.Count; i++)
            {
                if (camNodeList[i].Entry == zoneId)
                {
                    return i;
                }
            }
            return 0;
        }

        private void RemoveCameraID(ushort ID)
        {
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            if (!TryCamNode(ID, out e, out z, out c, out ky))
            {
                return;
            }

            // deleting a keyframe node removes just that keyframe
            if (c.Positions.Count > 1)
            {
                c.Positions.RemoveAt(ky);
                if (ky < c.Targets.Count) c.Targets.RemoveAt(ky);
                if (ky < c.Zoom.Count) c.Zoom.RemoveAt(ky);
                if (ky < c.Fov.Count) c.Fov.RemoveAt(ky);
                if (ky < c.TimeFrames.Count) c.TimeFrames.RemoveAt(ky);
                RebuildCamNodeList();
                SyncTreeNodesToCamNodeList();
                return;
            }

            // last keyframe of the camera -> remove the whole entry
            RemoveEntryInternal(e, z);
        }

        private void RemoveEntryInternal(ushort zoneId, CamZoneRecord z)
        {
            int camKey = z.CameraIndex;
            Zones.Remove(zoneId);
            if (camKey >= 0)
            {
                Cameras.Remove((ushort)camKey);
                List<ushort> keys = Cameras.Keys.OrderBy(x => x).ToList();
                for (int i = 0; i < keys.Count; i++)
                {
                    ushort k = keys[i];
                    if (k != (ushort)i)
                    {
                        Cameras[(ushort)i] = Cameras[k];
                        Cameras.Remove(k);
                    }
                }
                foreach (var pair in Zones)
                {
                    if (pair.Value.CameraIndex >= 0 && pair.Value.CameraIndex > camKey)
                    {
                        pair.Value.CameraIndex--;
                    }
                }
                IdForNewCamera = (ushort)Cameras.Count;
            }
            // keep zone dict keys contiguous 0..N-1: tree node IDs and the
            // writer both assume key == position
            List<ushort> zkeys = Zones.Keys.OrderBy(x => x).ToList();
            for (int i = 0; i < zkeys.Count; i++)
            {
                ushort k = zkeys[i];
                if (k != (ushort)i)
                {
                    Zones[(ushort)i] = Zones[k];
                    Zones.Remove(k);
                }
            }
            IdForNewZone = (ushort)Zones.Count;
            RebuildCamNodeList();
            SyncTreeNodesToCamNodeList();
            SyncZoneTreeNodesToKeys();
        }

        /// <summary>
        /// Default standalone trigger: NOT bound to any camera until linked,
        /// so adding a trigger can never silently attach to an existing cam.
        /// </summary>
        private CamZoneRecord BuildDefaultZone()
        {
            CamZoneRecord z = new CamZoneRecord();
            z.EntryNumber = (byte)Math.Min(255, Zones.Count + 1);
            z.CameraIndex = -1;
            float s = 500f;
            z.Points.Add(new CamVector(-s, 0, -s));
            z.Points.Add(new CamVector(s, 0, -s));
            z.Points.Add(new CamVector(s, 0, s));
            z.Points.Add(new CamVector(-s, 0, s));
            return z;
        }

        private ushort AddNewZoneID(byte initType)
        {
            if (Zones.Count >= Consts.AmountLimitCAM)
            {
                throw new InvalidOperationException("TriggerZone limit reached.");
            }
            ushort newId = FindFreeID(Zones.Keys);
            Zones.Add(newId, BuildDefaultZone());
            IdForNewZone = (ushort)Zones.Count;
            RebuildCamNodeList();
            SyncTreeNodesToCamNodeList();
            SyncZoneTreeNodesToKeys();
            return newId;
        }

        private void RemoveZoneID(ushort ID)
        {
            CamZoneRecord z;
            if (!Zones.TryGetValue(ID, out z))
            {
                return;
            }
            // removing a trigger removes its whole entry (zone + paired camera),
            // same as removing the camera entry in JADERLINK's tool
            RemoveEntryInternal(ID, z);
        }

        private static ushort FindFreeID(ICollection<ushort> used)
        {
            for (ushort i = 0; i < Consts.AmountLimitCAM; i++)
            {
                if (!used.Contains(i))
                {
                    return i;
                }
            }
            throw new InvalidOperationException("No free line ID.");
        }

        public static CamCameraRecord CreateDefaultCamera()
        {
            CamCameraRecord c = new CamCameraRecord();
            c.CamId = 1;
            c.CamType = 0;
            c.Distance = 1000f;
            c.Positions.Add(new CamVector(0f, 1000f, 0f));
            c.Targets.Add(new CamVector(0f, 0f, 0f));
            c.Zoom.Add(0f);
            c.Fov.Add(50f);
            return c;
        }

        #region camera templates

        private sealed class CamTemplateDef
        {
            public string Label;
            public byte CamType;
            public byte TriggerType;
            public byte TzSubtype;
            public float Distance;
            public byte[] Unk027AndRaw12;
            public float[][] Positions;
            public float[][] Targets;
            public float[] Zoom;
            public float Fov;
            public float[] FovList;
            public ushort[] TimeFrames;
            public ushort[] TzUnk055;
            public float TzHeight;
            public float TzBottom;
            public float[][] TzCoords;

            public CamTemplateDef(string label)
            {
                Label = label;
            }
        }

        // same presets as the "From Template" panel of the JADERLINK CAM tool
        private static readonly CamTemplateDef[] CamTemplates = new CamTemplateDef[]
        {
            new CamTemplateDef("Type 0 - Locked Cam (Walk-in, 1 key, FOV 60)")
            {
                CamType = 0,
                TriggerType = 0x03,
                TzSubtype = 0x03,
                Distance = 1000f,
                Fov = 60f,
                Unk027AndRaw12 = new byte[] { 0,0,0,0, 154,153,153,62, 0,0,0,0, 0,0,0,0 },
                Positions = new float[][] { new float[] { 19643.754f, 6337.396f, -11038.534f } },
                Targets   = new float[][] { new float[] { 21792.986f, 3882.930f,  -8863.809f } },
                Zoom      = new float[] { 0f },
                TimeFrames = new ushort[0],
                TzUnk055 = new ushort[] { 0,0,65281,0,0,0,0,0,0,0,0,0,0,0 },
                TzHeight = 3641.589f,
                TzBottom = 3344.228f,
                TzCoords = new float[][]
                {
                    new float[] { 23847.158f, -11374.279f },
                    new float[] { 19072.471f, -11401.656f },
                    new float[] { 19049.271f,  -7422.295f },
                    new float[] { 23845.045f,  -7378.701f }
                }
            },
            new CamTemplateDef("Type 2 - Overhead Cam (Walk-in, 2 keys, FOV 65)")
            {
                CamType = 2,
                TriggerType = 0x03,
                TzSubtype = 0x03,
                Distance = 1000f,
                Fov = 65f,
                Unk027AndRaw12 = new byte[] { 0,0,0,0, 154,153,153,62, 0,0,0,0, 0,0,0,0 },
                Positions = new float[][]
                {
                    new float[] { 25219.541f, 6337.396f, -11120.488f },
                    new float[] { 25073.021f, 6337.396f, -10992.228f }
                },
                Targets = new float[][]
                {
                    new float[] { 26035.488f, 3882.930f, -8173.818f },
                    new float[] { 27611.605f, 3882.930f, -9288.049f }
                },
                Zoom      = new float[] { 0f, 0f },
                TimeFrames = new ushort[0],
                TzUnk055 = new ushort[] { 0,0,65281,0,0,0,0,0,0,0,0,0,0,0 },
                TzHeight = 3641.589f,
                TzBottom = 3344.228f,
                TzCoords = new float[][]
                {
                    new float[] { 29307.906f, -11374.279f },
                    new float[] { 24533.221f, -11401.656f },
                    new float[] { 24510.020f,  -7422.295f },
                    new float[] { 29305.795f,  -7378.701f }
                }
            },
            new CamTemplateDef("Type 6 - Inspection Cam (AEV, 2 keys, FOV 50, Speed 0->30)")
            {
                CamType = 6,
                TriggerType = 0x04,
                TzSubtype = 0x04,
                Distance = 1000f,
                Fov = 50f,
                Unk027AndRaw12 = new byte[16],
                Positions = new float[][]
                {
                    new float[] { 36422.801f, 4297.249f, -11059.189f },
                    new float[] { 36413.820f, 4294.542f, -10979.742f }
                },
                Targets = new float[][]
                {
                    new float[] { 36124.672f, 4321.258f, -8672.860f },
                    new float[] { 36130.277f, 4300.108f, -8672.101f }
                },
                Zoom      = new float[] { 0f, 0f },
                TimeFrames = new ushort[] { 0, 30 },
                TzUnk055 = new ushort[] { 0,0,65281,0,0,0,0,0,0,0,0,0,0,0 },
                TzHeight = 3641.589f,
                TzBottom = 3344.228f,
                TzCoords = new float[][]
                {
                    new float[] { 35832.605f, -11402.449f },
                    new float[] { 35867.738f,  -7378.702f },
                    new float[] { 40447.930f,  -7348.117f },
                    new float[] { 40412.793f, -11371.866f }
                }
            },
            new CamTemplateDef("Type 8 - Shoulder Cam (Walk-in, 24 keys, FOV 90)")
            {
                CamType = 8,
                TriggerType = 0x03,
                TzSubtype = 0x03,
                Distance = 1000f,
                Fov = 90f,
                Unk027AndRaw12 = new byte[] { 0,0,0,0, 154,153,153,62, 0,0,0,0, 0,0,0,0 },
                Positions = BuildRepeat(24, 0f, 1660f, 1000f),
                Targets   = BuildRepeat(12, 0f, 1660f, 1500f),
                Zoom      = BuildRepeatF(24, 0f),
                FovList   = BuildFov24(),
                TimeFrames = new ushort[0],
                TzUnk055 = new ushort[] { 0,0,65281,65281,0,12854,63616,53816,63616,56888,63616,59192,63616,572 },
                TzHeight = 3641.589f,
                TzBottom = 3344.228f,
                // four corners like every other preset (the source tool had a
                // redundant 5th point sitting on the front edge)
                TzCoords = new float[][]
                {
                    new float[] { 34714.094f, -11402.450f },
                    new float[] { 30076.455f, -11374.279f },
                    new float[] { 30109.916f,  -7389.614f },
                    new float[] { 34749.227f,  -7378.701f }
                }
            }
        };

        private static float[][] BuildRepeat(int count, float x, float y, float z)
        {
            List<float[]> l = new List<float[]>(count);
            for (int i = 0; i < count; i++)
            {
                l.Add(new float[] { x, y, z });
            }
            return l.ToArray();
        }

        private static float[] BuildRepeatF(int count, float v)
        {
            float[] a = new float[count];
            for (int i = 0; i < count; i++)
            {
                a[i] = v;
            }
            return a;
        }

        private static float[] BuildFov24()
        {
            // slot 7 = 90, rest = 75 (matches the real file the preset came from)
            float[] a = new float[24];
            for (int i = 0; i < 24; i++)
            {
                a[i] = (i == 7) ? 90f : 75f;
            }
            return a;
        }

        private static void ApplyCamTemplate(CamCameraRecord c, CamZoneRecord z, int tplIdx)
        {
            CamTemplateDef t = CamTemplates[tplIdx];

            c.CamId = 1;
            c.CamType = t.CamType;
            c.Flags = 0;
            c.Distance = t.Distance;
            c.Unk025 = 0;
            c.Unk027 = BitConverter.ToSingle(t.Unk027AndRaw12, 0);
            Array.Copy(t.Unk027AndRaw12, 4, c.Raw12, 0, 12);
            c.Positions.Clear();
            foreach (float[] p in t.Positions)
            {
                c.Positions.Add(new CamVector(p[0], p[1], p[2]));
            }
            c.Targets.Clear();
            foreach (float[] p in t.Targets)
            {
                c.Targets.Add(new CamVector(p[0], p[1], p[2]));
            }
            c.Zoom.Clear();
            foreach (float v in t.Zoom)
            {
                c.Zoom.Add(v);
            }
            if (t.FovList != null && t.FovList.Length > 0)
            {
                c.Fov.Clear();
                foreach (float v in t.FovList)
                {
                    c.Fov.Add(v);
                }
            }
            else
            {
                c.Fov.Clear();
                for (int i = 0; i < c.Positions.Count; i++)
                {
                    c.Fov.Add(t.Fov);
                }
            }
            c.TimeFrames.Clear();
            foreach (ushort v in t.TimeFrames)
            {
                c.TimeFrames.Add(v);
            }

            z.TriggerType = t.TriggerType;
            z.Subtype = t.TzSubtype;
            z.Unk055 = new ushort[t.TzUnk055.Length];
            Array.Copy(t.TzUnk055, z.Unk055, t.TzUnk055.Length);
            z.Height = t.TzHeight;
            z.Bottom = t.TzBottom;
            z.Points.Clear();
            foreach (float[] p in t.TzCoords)
            {
                z.Points.Add(new CamVector(p[0], 0f, p[1]));
            }
        }

        /// <summary>
        /// Copies the selected camera keyframe and appends the copy right after it
        /// (same camera entry). Returns the camNodeList ID of the new keyframe node.
        /// </summary>
        public ushort DuplicateKeyframeCopy(ushort nodeID)
        {
            ushort e; CamZoneRecord z; CamCameraRecord c; int ky;
            if (!TryCamNode(nodeID, out e, out z, out c, out ky))
            {
                throw new InvalidOperationException("Invalid camera node.");
            }

            int at = ky + 1;
            c.Positions.Insert(Math.Min(at, c.Positions.Count), c.Positions[ky].Clone());
            InsertMirror(c.Targets, at, ky, r => r.Clone());
            InsertMirror(c.Zoom, at, ky, r => r);
            InsertMirror(c.Fov, at, ky, r => r);
            InsertMirror(c.TimeFrames, at, ky, r => r);
            SyncAndNormalizeSpeeds(c);

            RebuildCamNodeList();
            for (ushort i = 0; i < camNodeList.Count; i++)
            {
                if (camNodeList[i].Entry == e && camNodeList[i].Keyframe == at)
                {
                    return i;
                }
            }
            throw new InvalidOperationException("Keyframe copy failed.");
        }

        private static void InsertMirror<T>(List<T> list, int at, int from, Func<T, T> clone)
        {
            if (list.Count == 0)
            {
                return;
            }
            int src = Math.Min(from, list.Count - 1);
            list.Insert(Math.Min(at, list.Count), clone(list[src]));
        }

        #endregion

        #endregion

        #region base group plumbing

        protected override byte[] GetInternalLine(ushort ID)
        {
            if (Cameras.ContainsKey(ID))
            {
                return BuildCameraHeader(Cameras[ID]);
            }
            if (Zones.ContainsKey(ID))
            {
                return BuildZoneT2(Zones[ID], 0);
            }
            return new byte[0];
        }

        protected override Endianness GetEndianness()
        {
            return Endianness.LittleEndian;
        }

        private static bool CanUse(Dictionary<ushort, CamCameraRecord> d, ushort id)
        {
            return d != null && d.ContainsKey(id);
        }

        private static bool CanUseZ(Dictionary<ushort, CamZoneRecord> d, ushort id)
        {
            return d != null && d.ContainsKey(id);
        }

        #endregion

        #region camera property engine

        private void WireCameraMethods()
        {
            Methods.ReturnLine = ReturnLineCamera;
            Methods.SetLine = SetLineCamera;
            Methods.ReturnUnk021 = ReturnUnk021Value;
            Methods.SetUnk021 = SetUnk021Value;
            Methods.ReturnCamId = ReturnCamIdValue;
            Methods.SetCamId = SetCamIdValue;
            Methods.ReturnCamType = ReturnCamTypeValue;
            Methods.SetCamType = SetCamTypeValue;
            Methods.ReturnFlags = ReturnFlagsValue;
            Methods.SetFlags = SetFlagsValue;
            Methods.ReturnUnk025 = ReturnUnk025Value;
            Methods.SetUnk025 = SetUnk025Value;
            Methods.ReturnDistance = ReturnDistanceValue;
            Methods.SetDistance = SetDistanceValue;
            Methods.ReturnUnk027 = ReturnUnk027Value;
            Methods.SetUnk027 = SetUnk027Value;
            Methods.ReturnRaw12 = ReturnRaw12;
            Methods.SetRaw12 = SetRaw12;
            Methods.ReturnKeyframeCount = ReturnKeyframeCountValue;
            Methods.SetKeyframeCount = SetKeyframeCountValue;
            Methods.ReturnSelectedKeyframe = ReturnSelectedKeyframeValue;
            Methods.SetSelectedKeyframe = SetSelectedKeyframeValue;
            Methods.ReturnPosX = ReturnPosXValue;
            Methods.SetPosX = SetPosXValue;
            Methods.ReturnPosY = ReturnPosYValue;
            Methods.SetPosY = SetPosYValue;
            Methods.ReturnPosZ = ReturnPosZValue;
            Methods.SetPosZ = SetPosZValue;
            Methods.ReturnTargetX = ReturnTargetXValue;
            Methods.SetTargetX = SetTargetXValue;
            Methods.ReturnTargetY = ReturnTargetYValue;
            Methods.SetTargetY = SetTargetYValue;
            Methods.ReturnTargetZ = ReturnTargetZValue;
            Methods.SetTargetZ = SetTargetZValue;
            Methods.ReturnZoom = ReturnZoomValue;
            Methods.SetZoom = SetZoomValue;
            Methods.ReturnFov = ReturnFovValue;
            Methods.SetFov = SetFovValue;
            Methods.ReturnTimeFrame = ReturnTimeFrameValue;
            Methods.SetTimeFrame = SetTimeFrameValue;
        }

        private byte[] ReturnLineCamera(ushort ID)
        {
            CamCameraRecord c = NodeCam(ID);
            if (c == null)
            {
                return new byte[0];
            }
            return BuildCameraHeader(c);
        }

        private void SetLineCamera(ushort ID, byte[] value)
        {
            CamCameraRecord c = NodeCam(ID);
            if (c == null || value == null || value.Length < T3Size)
            {
                return;
            }
            ParseCameraHeader(c, value);
        }

        private byte ReturnUnk021Value(ushort ID) { CamCameraRecord c = NodeCam(ID); return c != null ? c.Unk021 : (byte)0; }
        private void SetUnk021Value(ushort ID, byte value) { CamCameraRecord c = NodeCam(ID); if (c != null) c.Unk021 = value; }
        private byte ReturnCamIdValue(ushort ID) { CamCameraRecord c = NodeCam(ID); return c != null ? c.CamId : (byte)0; }
        private void SetCamIdValue(ushort ID, byte value) { CamCameraRecord c = NodeCam(ID); if (c != null) c.CamId = value; }
        private byte ReturnCamTypeValue(ushort ID) { CamCameraRecord c = NodeCam(ID); return c != null ? c.CamType : (byte)0; }
        private void SetCamTypeValue(ushort ID, byte value) { CamCameraRecord c = NodeCam(ID); if (c != null) c.CamType = Math.Min(value, (byte)8); }
        private byte ReturnFlagsValue(ushort ID) { CamCameraRecord c = NodeCam(ID); return c != null ? c.Flags : (byte)0; }
        private void SetFlagsValue(ushort ID, byte value) { CamCameraRecord c = NodeCam(ID); if (c != null) c.Flags = value; }
        private uint ReturnUnk025Value(ushort ID) { CamCameraRecord c = NodeCam(ID); return c != null ? c.Unk025 : 0u; }
        private void SetUnk025Value(ushort ID, uint value) { CamCameraRecord c = NodeCam(ID); if (c != null) c.Unk025 = value; }
        private float ReturnDistanceValue(ushort ID) { CamCameraRecord c = NodeCam(ID); return c != null ? c.Distance : 0f; }
        private void SetDistanceValue(ushort ID, float value) { CamCameraRecord c = NodeCam(ID); if (c != null) c.Distance = value; }
        private float ReturnUnk027Value(ushort ID) { CamCameraRecord c = NodeCam(ID); return c != null ? c.Unk027 : 0f; }
        private void SetUnk027Value(ushort ID, float value) { CamCameraRecord c = NodeCam(ID); if (c != null) c.Unk027 = value; }

        private byte[] ReturnRaw12(ushort ID)
        {
            CamCameraRecord c = NodeCam(ID);
            return c != null ? (byte[])c.Raw12.Clone() : new byte[0];
        }

        private void SetRaw12(ushort ID, byte[] value)
        {
            CamCameraRecord c = NodeCam(ID);
            if (c == null || value == null)
            {
                return;
            }
            Array.Copy(value, c.Raw12, Math.Min(12, value.Length));
        }

        private int ReturnKeyframeCountValue(ushort ID)
        {
            CamCameraRecord c = NodeCam(ID);
            return c != null ? c.Positions.Count : 0;
        }

        private void SetKeyframeCountValue(ushort ID, int value)
        {
            CamCameraRecord c = NodeCam(ID);
            if (c == null)
            {
                return;
            }
            if (value < 1 || value > 4096)
            {
                return;
            }
            // Type 8 has a fixed 24-key structure in the file format
            if (c.CamType == 8 && value != 24)
            {
                return;
            }
            ResizeKeyframes(c, value);
            RebuildCamNodeList();
            SyncTreeNodesToCamNodeList();
        }

        /// <summary>
        /// Keeps the CAM tree nodes matching camNodeList after keyframes are
        /// added/removed from the property panel (new "Ky" nodes appear/disappear).
        /// </summary>
        public void SyncTreeNodesToCamNodeList()
        {
            if (Re4QuadExtremeEditor.src.DataBase.NodeCAM == null)
            {
                return;
            }
            System.Windows.Forms.TreeNodeCollection nodes = Re4QuadExtremeEditor.src.DataBase.NodeCAM.Nodes;
            ushort count = (ushort)camNodeList.Count;

            // drop everything and rebuild so IDs always match camNodeList
            bool inSync = nodes.Count == count;
            if (inSync)
            {
                for (ushort i = 0; i < count; i++)
                {
                    Re4QuadExtremeEditor.src.Class.TreeNodeObj.Object3D o =
                        nodes[i] as Re4QuadExtremeEditor.src.Class.TreeNodeObj.Object3D;
                    if (o == null || o.ObjLineRef != i)
                    {
                        inSync = false;
                        break;
                    }
                }
            }
            if (inSync)
            {
                return;
            }

            nodes.Clear();
            for (ushort i = 0; i < count; i++)
            {
                nodes.Add(Re4QuadExtremeEditor.src.Class.TreeNodeObj.Object3D.CreateNewInstance(
                    Re4QuadExtremeEditor.src.Class.Enums.GroupType.CAM, i));
            }
            Re4QuadExtremeEditor.src.DataBase.NodeCAM.Expand();
        }

        /// <summary>
        /// Keeps the CAM_ZONE tree in lockstep with the zone dictionary keys
        /// (key == position, maintained contiguous by add/remove).
        /// </summary>
        public void SyncZoneTreeNodesToKeys()
        {
            if (Re4QuadExtremeEditor.src.DataBase.NodeCAM_Zone == null)
            {
                return;
            }
            System.Windows.Forms.TreeNodeCollection nodes = Re4QuadExtremeEditor.src.DataBase.NodeCAM_Zone.Nodes;
            List<ushort> keys = Zones.Keys.OrderBy(x => x).ToList();

            bool inSync = nodes.Count == keys.Count;
            if (inSync)
            {
                for (int i = 0; i < keys.Count; i++)
                {
                    Re4QuadExtremeEditor.src.Class.TreeNodeObj.Object3D o =
                        nodes[i] as Re4QuadExtremeEditor.src.Class.TreeNodeObj.Object3D;
                    if (o == null || o.ObjLineRef != keys[i])
                    {
                        inSync = false;
                        break;
                    }
                }
            }
            if (inSync)
            {
                return;
            }

            nodes.Clear();
            foreach (ushort k in keys)
            {
                nodes.Add(Re4QuadExtremeEditor.src.Class.TreeNodeObj.Object3D.CreateNewInstance(
                    Re4QuadExtremeEditor.src.Class.Enums.GroupType.CAM_ZONE, k));
            }
            Re4QuadExtremeEditor.src.DataBase.NodeCAM_Zone.Expand();
        }

        private static void ResizeKeyframes(CamCameraRecord c, int value)
        {
            while (c.Positions.Count < value)
            {
                CamVector last = c.Positions.Count > 0 ? c.Positions[c.Positions.Count - 1].Clone() : new CamVector();
                c.Positions.Add(last.Clone());
                c.Targets.Add(c.Targets.Count > 0 ? c.Targets[c.Targets.Count - 1].Clone() : last.Clone());
                c.Zoom.Add(c.Zoom.Count > 0 ? c.Zoom[c.Zoom.Count - 1] : 0f);
                c.Fov.Add(c.Fov.Count > 0 ? c.Fov[c.Fov.Count - 1] : 50f);
            }
            while (c.Positions.Count > value)
            {
                int lastIdx = c.Positions.Count - 1;
                c.Positions.RemoveAt(lastIdx);
                if (c.Targets.Count > lastIdx) c.Targets.RemoveAt(lastIdx);
                if (c.Zoom.Count > lastIdx) c.Zoom.RemoveAt(lastIdx);
                if (c.Fov.Count > lastIdx) c.Fov.RemoveAt(lastIdx);
                if (c.TimeFrames.Count > lastIdx) c.TimeFrames.RemoveAt(lastIdx);
            }
            while (c.Targets.Count > c.Positions.Count && c.Targets.Count > 0) c.Targets.RemoveAt(c.Targets.Count - 1);
            while (c.Zoom.Count > c.Positions.Count && c.Zoom.Count > 0) c.Zoom.RemoveAt(c.Zoom.Count - 1);
            while (c.Fov.Count > c.Positions.Count && c.Fov.Count > 0) c.Fov.RemoveAt(c.Fov.Count - 1);
            while (c.TimeFrames.Count > c.Positions.Count && c.TimeFrames.Count > 0) c.TimeFrames.RemoveAt(c.TimeFrames.Count - 1);
            SyncAndNormalizeSpeeds(c);
            if (c.SelectedKeyframe >= c.Positions.Count)
            {
                c.SelectedKeyframe = Math.Max(0, c.Positions.Count - 1);
            }
        }

        /// <summary>
        /// Same rules as the CAM tool's DATASET_5_Speed validation:
        /// one entry per keyframe, Speed_01 = 0, strictly ascending values
        /// (new entries get +30 frames). Valid data is left untouched.
        /// </summary>
        private static void SyncAndNormalizeSpeeds(CamCameraRecord c)
        {
            if (c.CamType != 6)
            {
                return;
            }
            while (c.TimeFrames.Count < c.Positions.Count)
            {
                c.TimeFrames.Add(c.TimeFrames.Count > 0
                    ? (ushort)(c.TimeFrames[c.TimeFrames.Count - 1] + 30)
                    : (ushort)0);
            }
            while (c.TimeFrames.Count > c.Positions.Count)
            {
                c.TimeFrames.RemoveAt(c.TimeFrames.Count - 1);
            }
            if (c.TimeFrames.Count == 0)
            {
                return;
            }
            c.TimeFrames[0] = 0;
            for (int i = 1; i < c.TimeFrames.Count; i++)
            {
                if (c.TimeFrames[i] <= c.TimeFrames[i - 1])
                {
                    c.TimeFrames[i] = (ushort)(c.TimeFrames[i - 1] + 30);
                }
            }
        }

        private int ClampKey(CamCameraRecord c)
        {
            if (c.Positions.Count == 0)
            {
                return -1;
            }
            if (c.SelectedKeyframe < 0)
            {
                return 0;
            }
            if (c.SelectedKeyframe >= c.Positions.Count)
            {
                return c.Positions.Count - 1;
            }
            return c.SelectedKeyframe;
        }

        private int ReturnSelectedKeyframeValue(ushort ID)
        {
            int k = NodeKy(ID);
            return k >= 0 ? k : 0;
        }

        private void SetSelectedKeyframeValue(ushort ID, int value)
        {
            if (ID >= camNodeList.Count || value < 0)
            {
                return;
            }
            CamNodeKey key = camNodeList[ID];
            key.Keyframe = value;
            camNodeList[ID] = key;
        }

        private float GetVecField(List<CamVector> list, int sel, int field)
        {
            if (sel < 0 || sel >= list.Count)
            {
                return 0f;
            }
            CamVector v = list[sel];
            return field == 0 ? v.X : field == 1 ? v.Y : v.Z;
        }

        private void SetVecField(List<CamVector> list, int sel, int field, float value)
        {
            if (sel < 0 || sel >= list.Count)
            {
                return;
            }
            CamVector v = list[sel];
            if (field == 0) v.X = value;
            else if (field == 1) v.Y = value;
            else v.Z = value;
        }

        private float ReturnPosXValue(ushort ID) { CamCameraRecord c = NodeCam(ID); return c != null ? GetVecField(c.Positions, NodeKy(ID), 0) : 0f; }
        private void SetPosXValue(ushort ID, float v) { CamCameraRecord c = NodeCam(ID); if (c != null) SetVecField(c.Positions, NodeKy(ID), 0, v); }
        private float ReturnPosYValue(ushort ID) { CamCameraRecord c = NodeCam(ID); return c != null ? GetVecField(c.Positions, NodeKy(ID), 1) : 0f; }
        private void SetPosYValue(ushort ID, float v) { CamCameraRecord c = NodeCam(ID); if (c != null) SetVecField(c.Positions, NodeKy(ID), 1, v); }
        private float ReturnPosZValue(ushort ID) { CamCameraRecord c = NodeCam(ID); return c != null ? GetVecField(c.Positions, NodeKy(ID), 2) : 0f; }
        private void SetPosZValue(ushort ID, float v) { CamCameraRecord c = NodeCam(ID); if (c != null) SetVecField(c.Positions, NodeKy(ID), 2, v); }

        private float ReturnTargetXValue(ushort ID) { CamCameraRecord c = NodeCam(ID); return c != null ? GetVecField(c.Targets, NodeKy(ID), 0) : 0f; }
        private void SetTargetXValue(ushort ID, float v) { CamCameraRecord c = NodeCam(ID); if (c != null) SetVecField(c.Targets, NodeKy(ID), 0, v); }
        private float ReturnTargetYValue(ushort ID) { CamCameraRecord c = NodeCam(ID); return c != null ? GetVecField(c.Targets, NodeKy(ID), 1) : 0f; }
        private void SetTargetYValue(ushort ID, float v) { CamCameraRecord c = NodeCam(ID); if (c != null) SetVecField(c.Targets, NodeKy(ID), 1, v); }
        private float ReturnTargetZValue(ushort ID) { CamCameraRecord c = NodeCam(ID); return c != null ? GetVecField(c.Targets, NodeKy(ID), 2) : 0f; }
        private void SetTargetZValue(ushort ID, float v) { CamCameraRecord c = NodeCam(ID); if (c != null) SetVecField(c.Targets, NodeKy(ID), 2, v); }

        private float ReturnZoomValue(ushort ID)
        {
            CamCameraRecord c = NodeCam(ID);
            if (c == null) return 0f;
            int k = NodeKy(ID);
            return (k >= 0 && k < c.Zoom.Count) ? c.Zoom[k] : 0f;
        }

        private void SetZoomValue(ushort ID, float v)
        {
            CamCameraRecord c = NodeCam(ID);
            if (c == null) return;
            int k = NodeKy(ID);
            if (k < 0) return;
            while (c.Zoom.Count <= k) c.Zoom.Add(0f);
            c.Zoom[k] = v;
        }

        private float ReturnFovValue(ushort ID)
        {
            CamCameraRecord c = NodeCam(ID);
            if (c == null) return 0f;
            int k = NodeKy(ID);
            return (k >= 0 && k < c.Fov.Count) ? c.Fov[k] : 0f;
        }

        private void SetFovValue(ushort ID, float v)
        {
            CamCameraRecord c = NodeCam(ID);
            if (c == null) return;
            int k = NodeKy(ID);
            if (k < 0) return;
            while (c.Fov.Count <= k) c.Fov.Add(50f);
            c.Fov[k] = v;
        }

        private ushort ReturnTimeFrameValue(ushort ID)
        {
            CamCameraRecord c = NodeCam(ID);
            if (c == null) return 0;
            int k = NodeKy(ID);
            return (k >= 0 && k < c.TimeFrames.Count) ? c.TimeFrames[k] : (ushort)0;
        }

        private void SetTimeFrameValue(ushort ID, ushort v)
        {
            CamCameraRecord c = NodeCam(ID);
            if (c == null) return;
            int k = NodeKy(ID);
            if (k < 0) return;
            while (c.TimeFrames.Count <= k) c.TimeFrames.Add(0);
            c.TimeFrames[k] = v;
        }

        #endregion

        #region zone property engine

        private void WireZoneMethods()
        {
            ZoneMethods.ReturnLinkLine = ReturnLinkLineValue;
            ZoneMethods.SetLinkLine = SetLinkLineValue;
            ZoneMethods.ReturnLine = ReturnLineZone;
            ZoneMethods.SetLine = SetLineZone;
            ZoneMethods.ReturnTriggerType = ReturnTriggerTypeValue;
            ZoneMethods.SetTriggerType = SetTriggerTypeValue;
            ZoneMethods.ReturnLinkUnk012 = ReturnLinkUnk012Value;
            ZoneMethods.SetLinkUnk012 = SetLinkUnk012Value;
            ZoneMethods.ReturnUnk015 = ReturnUnk015Value;
            ZoneMethods.SetUnk015 = SetUnk015Value;
            ZoneMethods.ReturnUnk016 = ReturnUnk016Value;
            ZoneMethods.SetUnk016 = SetUnk016Value;
            ZoneMethods.ReturnUnk017 = ReturnUnk017Value;
            ZoneMethods.SetUnk017 = SetUnk017Value;
            ZoneMethods.ReturnLinkedCamera = ReturnLinkedCameraValue;
            ZoneMethods.SetLinkedCamera = SetLinkedCameraValue;
            ZoneMethods.ReturnUnk051 = ReturnUnk051Value;
            ZoneMethods.SetUnk051 = SetUnk051Value;
            ZoneMethods.ReturnEntryNumber = ReturnEntryNumberValue;
            ZoneMethods.SetEntryNumber = SetEntryNumberValue;
            ZoneMethods.ReturnCamTypeTz = ReturnCamTypeTzValue;
            ZoneMethods.SetCamTypeTz = SetCamTypeTzValue;
            ZoneMethods.ReturnSubtype = ReturnSubtypeValue;
            ZoneMethods.SetSubtype = SetSubtypeValue;
            ZoneMethods.ReturnHeight = ReturnHeightValue;
            ZoneMethods.SetHeight = SetHeightValue;
            ZoneMethods.ReturnBottom = ReturnBottomValue;
            ZoneMethods.SetBottom = SetBottomValue;
            ZoneMethods.ReturnPointCount = ReturnPointCountValue;
            ZoneMethods.SetPointCount = SetPointCountValue;
            ZoneMethods.ReturnSelectedPoint = ReturnSelectedPointValue;
            ZoneMethods.SetSelectedPoint = SetSelectedPointValue;
            ZoneMethods.ReturnPointX = ReturnPointXValue;
            ZoneMethods.SetPointX = SetPointXValue;
            ZoneMethods.ReturnPointY = ReturnPointYValue;
            ZoneMethods.SetPointY = SetPointYValue;
            ZoneMethods.ReturnPointZ = ReturnPointZValue;
            ZoneMethods.SetPointZ = SetPointZValue;
        }

        private byte[] ReturnLinkLineValue(ushort ID)
        {
            if (!CanUseZ(Zones, ID)) return new byte[0];
            CamZoneRecord z = Zones[ID];
            byte[] b = new byte[16];
            b[0] = z.TriggerType;
            b[1] = z.LinkUnk012;
            Array.Copy(BitConverter.GetBytes(z.Unk015), 0, b, 2, 2);
            Array.Copy(BitConverter.GetBytes(z.Unk016), 0, b, 4, 2);
            Array.Copy(BitConverter.GetBytes(z.Unk017), 0, b, 6, 2);
            Array.Copy(BitConverter.GetBytes(z.CameraIndex >= 0 ? 0xFFFFFFFFu : 0u), 0, b, 8, 4);
            Array.Copy(BitConverter.GetBytes(z.CameraIndex >= 0 ? 0xFFFFFFFFu : 0u), 0, b, 12, 4);
            return b;
        }

        private void SetLinkLineValue(ushort ID, byte[] value)
        {
            if (!CanUseZ(Zones, ID) || value == null || value.Length < 8)
            {
                return;
            }
            CamZoneRecord z = Zones[ID];
            z.TriggerType = value[0];
            z.LinkUnk012 = value[1];
            z.Unk015 = BitConverter.ToUInt16(value, 2);
            z.Unk016 = BitConverter.ToUInt16(value, 4);
            z.Unk017 = BitConverter.ToUInt16(value, 6);
        }

        private byte[] ReturnLineZone(ushort ID)
        {
            if (!CanUseZ(Zones, ID)) return new byte[0];
            return BuildZoneT2(Zones[ID], 0);
        }

        private void SetLineZone(ushort ID, byte[] value)
        {
            if (!CanUseZ(Zones, ID) || value == null || value.Length < T2Size)
            {
                return;
            }
            ParseZoneT2(Zones[ID], value);
        }

        private byte ReturnTriggerTypeValue(ushort ID) { return CanUseZ(Zones, ID) ? Zones[ID].TriggerType : (byte)0; }
        private void SetTriggerTypeValue(ushort ID, byte value) { if (CanUseZ(Zones, ID)) Zones[ID].TriggerType = value; }
        private byte ReturnLinkUnk012Value(ushort ID) { return CanUseZ(Zones, ID) ? Zones[ID].LinkUnk012 : (byte)0; }
        private void SetLinkUnk012Value(ushort ID, byte value) { if (CanUseZ(Zones, ID)) Zones[ID].LinkUnk012 = value; }
        private ushort ReturnUnk015Value(ushort ID) { return CanUseZ(Zones, ID) ? Zones[ID].Unk015 : (ushort)0; }
        private void SetUnk015Value(ushort ID, ushort value) { if (CanUseZ(Zones, ID)) Zones[ID].Unk015 = value; }
        private ushort ReturnUnk016Value(ushort ID) { return CanUseZ(Zones, ID) ? Zones[ID].Unk016 : (ushort)0; }
        private void SetUnk016Value(ushort ID, ushort value) { if (CanUseZ(Zones, ID)) Zones[ID].Unk016 = value; }
        private ushort ReturnUnk017Value(ushort ID) { return CanUseZ(Zones, ID) ? Zones[ID].Unk017 : (ushort)0; }
        private void SetUnk017Value(ushort ID, ushort value) { if (CanUseZ(Zones, ID)) Zones[ID].Unk017 = value; }

        private int ReturnLinkedCameraValue(ushort ID)
        {
            return CanUseZ(Zones, ID) ? Zones[ID].CameraIndex : -1;
        }

        private void SetLinkedCameraValue(ushort ID, int value)
        {
            if (!CanUseZ(Zones, ID)) return;
            if (value < -1) value = -1;
            if (value >= Cameras.Count) value = Cameras.Count - 1;
            Zones[ID].CameraIndex = value;
        }

        private byte ReturnUnk051Value(ushort ID) { return CanUseZ(Zones, ID) ? Zones[ID].Unk051 : (byte)0; }
        private void SetUnk051Value(ushort ID, byte value) { if (CanUseZ(Zones, ID)) Zones[ID].Unk051 = value; }
        private byte ReturnEntryNumberValue(ushort ID) { return CanUseZ(Zones, ID) ? Zones[ID].EntryNumber : (byte)0; }
        private void SetEntryNumberValue(ushort ID, byte value) { if (CanUseZ(Zones, ID)) Zones[ID].EntryNumber = value; }
        private byte ReturnCamTypeTzValue(ushort ID) { return CanUseZ(Zones, ID) ? Zones[ID].CamTypeTz : (byte)0; }
        private void SetCamTypeTzValue(ushort ID, byte value) { if (CanUseZ(Zones, ID)) Zones[ID].CamTypeTz = value; }
        private byte ReturnSubtypeValue(ushort ID) { return CanUseZ(Zones, ID) ? Zones[ID].Subtype : (byte)0; }
        private void SetSubtypeValue(ushort ID, byte value) { if (CanUseZ(Zones, ID)) Zones[ID].Subtype = value; }
        private float ReturnHeightValue(ushort ID) { return CanUseZ(Zones, ID) ? Zones[ID].Height : 0f; }
        private void SetHeightValue(ushort ID, float value) { if (CanUseZ(Zones, ID)) Zones[ID].Height = value; }
        private float ReturnBottomValue(ushort ID) { return CanUseZ(Zones, ID) ? Zones[ID].Bottom : 0f; }
        private void SetBottomValue(ushort ID, float value) { if (CanUseZ(Zones, ID)) Zones[ID].Bottom = value; }

        private int ReturnPointCountValue(ushort ID)
        {
            return CanUseZ(Zones, ID) ? Zones[ID].Points.Count : 0;
        }

        private void SetPointCountValue(ushort ID, int value)
        {
            if (!CanUseZ(Zones, ID)) return;
            if (value < 0 || value > 256) return;
            CamZoneRecord z = Zones[ID];
            while (z.Points.Count < value)
            {
                z.Points.Add(z.Points.Count > 0 ? z.Points[z.Points.Count - 1].Clone() : new CamVector());
            }
            while (z.Points.Count > value)
            {
                z.Points.RemoveAt(z.Points.Count - 1);
            }
            if (z.SelectedPoint >= z.Points.Count)
            {
                z.SelectedPoint = Math.Max(0, z.Points.Count - 1);
            }
        }

        private int ClampPoint(CamZoneRecord z)
        {
            if (z.Points.Count == 0) return -1;
            if (z.SelectedPoint < 0) return 0;
            if (z.SelectedPoint >= z.Points.Count) return z.Points.Count - 1;
            return z.SelectedPoint;
        }

        private int ReturnSelectedPointValue(ushort ID)
        {
            return CanUseZ(Zones, ID) ? ClampPoint(Zones[ID]) : 0;
        }

        private void SetSelectedPointValue(ushort ID, int value)
        {
            if (!CanUseZ(Zones, ID)) return;
            Zones[ID].SelectedPoint = value;
        }

        private float ReturnPointXValue(ushort ID) { return CanUseZ(Zones, ID) ? GetVecField(Zones[ID].Points, ClampPoint(Zones[ID]), 0) : 0f; }
        private void SetPointXValue(ushort ID, float v) { if (CanUseZ(Zones, ID)) SetVecField(Zones[ID].Points, ClampPoint(Zones[ID]), 0, v); }
        private float ReturnPointYValue(ushort ID) { return CanUseZ(Zones, ID) ? GetVecField(Zones[ID].Points, ClampPoint(Zones[ID]), 1) : 0f; }
        private void SetPointYValue(ushort ID, float v) { if (CanUseZ(Zones, ID)) SetVecField(Zones[ID].Points, ClampPoint(Zones[ID]), 1, v); }
        private float ReturnPointZValue(ushort ID) { return CanUseZ(Zones, ID) ? GetVecField(Zones[ID].Points, ClampPoint(Zones[ID]), 2) : 0f; }
        private void SetPointZValue(ushort ID, float v) { if (CanUseZ(Zones, ID)) SetVecField(Zones[ID].Points, ClampPoint(Zones[ID]), 2, v); }

        #endregion

        #region raw header build/parse

        private byte[] BuildCameraHeader(CamCameraRecord c)
        {
            byte[] b = new byte[T3Size];
            b[0] = c.Unk021;
            b[1] = c.CamId;
            b[2] = c.CamType;
            b[3] = c.Flags;
            Array.Copy(BitConverter.GetBytes(c.Unk025), 0, b, 4, 4);
            Array.Copy(BitConverter.GetBytes(c.Distance), 0, b, 8, 4);
            Array.Copy(BitConverter.GetBytes(c.Unk027), 0, b, 12, 4);
            Array.Copy(BitConverter.GetBytes(0u), 0, b, 16, 4);
            Array.Copy(c.Raw12, 0, b, 20, 12);
            Array.Copy(BitConverter.GetBytes((uint)c.Positions.Count), 0, b, 32, 4);
            Array.Copy(BitConverter.GetBytes(0u), 0, b, 36, 4);
            Array.Copy(BitConverter.GetBytes(0u), 0, b, 40, 4);
            Array.Copy(BitConverter.GetBytes(0u), 0, b, 44, 4);
            Array.Copy(BitConverter.GetBytes(0u), 0, b, 48, 4);
            return b;
        }

        private void ParseCameraHeader(CamCameraRecord c, byte[] data)
        {
            c.Unk021 = data[0];
            c.CamId = data[1];
            c.CamType = Math.Min(data[2], (byte)8);
            c.Flags = data[3];
            c.Unk025 = BitConverter.ToUInt32(data, 4);
            c.Distance = BitConverter.ToSingle(data, 8);
            c.Unk027 = BitConverter.ToSingle(data, 12);
            c.RawBuf0Addr = BitConverter.ToUInt32(data, 16);
            Array.Copy(data, 20, c.Raw12, 0, 12);
        }

        private byte[] BuildZoneT2(CamZoneRecord z, uint dataAddrOverride)
        {
            byte[] b = new byte[T2Size];
            b[0] = z.Unk051;
            b[1] = z.EntryNumber;
            b[2] = z.CamTypeTz;
            b[3] = z.Subtype;
            for (int j = 0; j < 14; j++)
            {
                Array.Copy(BitConverter.GetBytes(z.Unk055[j]), 0, b, 4 + j * 2, 2);
            }
            Array.Copy(BitConverter.GetBytes(z.Height), 0, b, 32, 4);
            Array.Copy(BitConverter.GetBytes(z.Bottom), 0, b, 36, 4);
            Array.Copy(BitConverter.GetBytes((uint)dataAddrOverride), 0, b, 40, 4);
            return b;
        }

        private void ParseZoneT2(CamZoneRecord z, byte[] data)
        {
            z.Unk051 = data[0];
            z.EntryNumber = data[1];
            z.CamTypeTz = data[2];
            z.Subtype = data[3];
            for (int j = 0; j < 14; j++)
            {
                z.Unk055[j] = BitConverter.ToUInt16(data, 4 + j * 2);
            }
            z.Height = BitConverter.ToSingle(data, 32);
            z.Bottom = BitConverter.ToSingle(data, 36);
        }

        internal ushort[] GetKnownTriggerTypes()
        {
            return KnownTriggerTypes;
        }

        #endregion
    }
}
