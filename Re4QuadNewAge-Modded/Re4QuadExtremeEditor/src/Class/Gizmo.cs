using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Re4QuadExtremeEditor.src;
using Re4QuadExtremeEditor.src.Class.Enums;
using Re4QuadExtremeEditor.src.Class.TreeNodeObj;
using NsCamera;

namespace Re4QuadExtremeEditor.src.Class
{
    /// <summary>
    /// Advanced gizmo — exact parity with Re4QuadX.
    /// Supports: Move arrows (X/Y/Z) + planar handles (XY/XZ/YZ) + Rotate rings,
    /// World/Local space, distance-scaled, alpha blended, color-picking ready,
    /// while keeping legacy static API (Enabled/Tick/TryBeginDrag...) for compat.
    /// </summary>
    public static class Gizmo
    {
        // ================================================================
        // Legacy-compatible public API (kept for existing callers)
        // ================================================================
        public static bool Enabled = false;
        public static Action TransformApplied = null;
        public static float SnapStep = 0f;
        public static bool IsDragging { get { return dragging; } }

        // New Re4QuadX-style public state
        public static Vector3 Position { get { return GetPivotSafe(); } set { /* pivot is derived from selection, setter is no-op */ } }
        public static float Scale { get; private set; }
        public static GizmoAxis ActiveAxis { get; set; } = GizmoAxis.None;

        public enum GizmoAxis
        {
            None,
            X,
            Y,
            Z,
            XY,
            YZ,
            XZ,
            RotateX,
            RotateY,
            RotateZ
        }

        // Selection colors — identical to Re4QuadX for color-picking path
        public static readonly Vector3 SelectColorMoveX = new Vector3(1.0f, 1.0f / 255.0f, 1.0f / 255.0f);
        public static readonly Vector3 SelectColorMoveY = new Vector3(1.0f, 2.0f / 255.0f, 1.0f / 255.0f);
        public static readonly Vector3 SelectColorMoveZ = new Vector3(1.0f, 3.0f / 255.0f, 1.0f / 255.0f);
        public static readonly Vector3 SelectColorMoveXY = new Vector3(1.0f, 4.0f / 255.0f, 1.0f / 255.0f);
        public static readonly Vector3 SelectColorMoveYZ = new Vector3(1.0f, 5.0f / 255.0f, 1.0f / 255.0f);
        public static readonly Vector3 SelectColorMoveXZ = new Vector3(1.0f, 6.0f / 255.0f, 1.0f / 255.0f);
        public static readonly Vector3 SelectColorRotateX = new Vector3(1.0f, 1.0f / 255.0f, 2.0f / 255.0f);
        public static readonly Vector3 SelectColorRotateY = new Vector3(1.0f, 2.0f / 255.0f, 2.0f / 255.0f);
        public static readonly Vector3 SelectColorRotateZ = new Vector3(1.0f, 3.0f / 255.0f, 2.0f / 255.0f);

        // ================================================================
        // Legacy drag state (ray-plane)
        // ================================================================
        private const int PartNone = -1;
        private const int PartX = 0;
        private const int PartY = 1;
        private const int PartZ = 2;
        private const int PartXY = 3;
        private const int PartXZ = 4;
        private const int PartYZ = 5;
        private const int PartCenterLegacy = 6;
        private const int PartRotateX = 7;
        private const int PartRotateY = 8;
        private const int PartRotateZ = 9;

        private static int hoverPart = PartNone;
        private static bool dragging = false;
        private static int dragPart = PartNone;

        private static Vector3 planePoint = Vector3.Zero;
        private static Vector3 planeNormal = Vector3.UnitZ;
        private static Vector3 planeHitStart = Vector3.Zero;

        private static readonly List<Object3D> dragObjects = new List<Object3D>();
        private static readonly List<Vector3> dragStartPositions = new List<Vector3>();
        private static readonly List<Vector3> dragStartRotations = new List<Vector3>();
        // Extended capture for universal support: every object type + trigger zones
        private static readonly List<Vector3[]> dragStartFullPositions = new List<Vector3[]>();
        private static readonly List<UndoSystem.FullTransformState> dragStartFullStates = new List<UndoSystem.FullTransformState>();

        private static readonly Vector4 ColX = new Vector4(0.95f, 0.25f, 0.25f, 1f);
        private static readonly Vector4 ColY = new Vector4(0.45f, 0.90f, 0.35f, 1f);
        private static readonly Vector4 ColZ = new Vector4(0.35f, 0.50f, 0.95f, 1f);
        private static readonly Vector4 ColXY = new Vector4(0.35f, 0.50f, 0.95f, 0.65f);
        private static readonly Vector4 ColXZ = new Vector4(0.45f, 0.90f, 0.35f, 0.65f);
        private static readonly Vector4 ColYZ = new Vector4(0.95f, 0.25f, 0.25f, 0.65f);
        private static readonly Vector4 ColCenter = new Vector4(0.95f, 0.95f, 0.95f, 1f);

        // glow
        private static readonly float[] partGlow = new float[10];
        private static readonly System.Diagnostics.Stopwatch glowClock = System.Diagnostics.Stopwatch.StartNew();
        private static double glowLastMs = 0;

        public static void Tick()
        {
            double now = glowClock.Elapsed.TotalMilliseconds;
            float dt = (float)(now - glowLastMs) / 1000f;
            glowLastMs = now;
            if (dt <= 0f || dt > 0.25f) dt = 0.016f;
            for (int i = 0; i < partGlow.Length; i++)
            {
                bool hot = hoverPart == i || (dragging && dragPart == i);
                float target = hot ? 1f : 0f;
                float speed = hot ? 15f : 10f; // light, smooth
                partGlow[i] += (target - partGlow[i]) * Math.Min(1f, speed * dt);
                if (partGlow[i] < 0.001f) partGlow[i] = 0f;
                if (partGlow[i] > 0.999f) partGlow[i] = 1f;
            }
        }

        private static float HoverScale(int part)
        {
            float g = partGlow[part];
            return 1f + g * 0.08f; // subtle 8% enlarge
        }

        internal static readonly string LogPath = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "gizmo_debug_log.txt");
        internal static void Log(string msg)
        {
            try { System.IO.File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff ") + msg + "\r\n"); }
            catch { }
        }

        private static Vector3 GetPivotSafe()
        {
            Vector3 acc = Vector3.Zero; int n = 0;
            var sel = Re4QuadExtremeEditor.src.DataBase.SelectedNodes;
            bool trigMode = IsGizmoTriggerMode();
            bool objMode = IsGizmoObjectMode();
            // When panel is in item mode → pivot on object pos[0]; when in trigger mode → pivot on trigger center pos[6]
            bool wantTrigger = trigMode && !objMode;
            bool wantObject = objMode && !trigMode;
            if (!wantTrigger && !wantObject) { wantTrigger = true; wantObject = true; } // AllMove/Null → average existing
            if (sel != null)
            {
                foreach (var kv in sel)
                {
                    Object3D obj = kv.Value as Object3D;
                    if (obj == null) continue;
                    Vector3 camPos = Vector3.Zero;
                    bool got = false;
                    try
                    {
                        Vector3[] full = obj.GetObjPostion_ToMove_General();
                        TriggerZoneCategory cat = TriggerZoneCategory.Disable;
                        try { cat = obj.GetTriggerZoneCategory(); } catch { }
                        bool isTriggerZone = (cat == TriggerZoneCategory.Category01 || cat == TriggerZoneCategory.Category02) && full != null && full.Length >= 7;
                        bool isITA = obj.Group == GroupType.ITA;
                        if (isITA && wantTrigger && !wantObject && isTriggerZone)
                        {
                            // ITA trigger-only mode: pivot at zone center (pos[6]) → camera space
                            Vector3 cen = full[6];
                            camPos = cen / 100f;
                            Utils.ToCameraCheckValue(ref camPos);
                            got = true;
                        }
                        else if (isITA && wantObject && !wantTrigger && isTriggerZone)
                        {
                            // ITA object-only mode even though object has trigger: pivot at object pos[0]
                            Vector3 op = full[0];
                            camPos = op / 100f;
                            Utils.ToCameraCheckValue(ref camPos);
                            got = true;
                        }
                        else if (full != null && full.Length >= 1)
                        {
                            // Universal / non-ITA or both modes: use the object's own camera position
                            if (isTriggerZone && wantObject && wantTrigger)
                            {
                                // Both modes (AllMove): pivot at object pos[0] — matches SetObjPosition both-move semantics
                                Vector3 op = full[0];
                                camPos = op / 100f;
                                Utils.ToCameraCheckValue(ref camPos);
                                got = true;
                            }
                            else if (!isITA)
                            {
                                // Non-ITA keeps universal behavior (no ITA-only separation)
                                // Fall through to fallback GetObjPosition_ToCamera below for correct scaling per group
                            }
                        }
                    }
                    catch { }
                    if (!got)
                    {
                        // Fallback to legacy camera position (covers RTP/LIT/EFF where full is single point, and any error)
                        camPos = obj.GetObjPosition_ToCamera();
                    }
                    acc += camPos; n++;
                }
            }
            return n > 0 ? acc / n : Vector3.Zero;
        }

        private static Vector3 GetPivot()
        {
            return GetPivotSafe();
        }

        public static bool TryGetPivot(out Vector3 pivot)
        {
            pivot = Vector3.Zero;
            if (!HasSelection()) return false;
            pivot = GetPivot();
            return true;
        }

        public static void UpdateScale(Camera camera)
        {
            if (camera == null) return;
            Vector3 pivot = GetPivotSafe();
            float distance = (camera.Position - pivot).Length;
            const float scaleFactor = 0.14f;
            Scale = distance * scaleFactor;
            if (Scale < 0.5f) Scale = 0.5f;
        }

        private static bool HasSelection()
        {
            var sel = Re4QuadExtremeEditor.src.DataBase.SelectedNodes;
            if (sel == null) return false;
            // Every selectable game object is wrapped as Object3D, including all TriggerZones.
            // This covers ESL/ETS/ITA/AEV/QuadCustom/RTP/LIT/EFF/CAM/etc for gizmo visibility.
            foreach (var kv in sel) if (kv.Value is Object3D) return true;
            return false;
        }

        private static bool IsGizmoTriggerMode()
        {
            var cur = Globals.CurrentMoveType;
            if (cur == MoveObjType.Null) return false; // default to object mode when no panel selection yet
            bool isTrigger = cur.HasFlag(MoveObjType._SquareMoveTriggerZone) || cur.HasFlag(MoveObjType._SquareMoveAshleyZone) || cur.HasFlag(MoveObjType._VerticalMoveTriggerZoneY) || cur.HasFlag(MoveObjType._Horizontal2RotationZoneY) || cur.HasFlag(MoveObjType._Horizontal1ChangeTriggerZoneHeight) || cur.HasFlag(MoveObjType._Horizontal3TriggerZoneScaleAll) || cur.HasFlag(MoveObjType._Horizontal3AshleyZoneScaleAll);
            bool isObject = cur.HasFlag(MoveObjType._SquareMoveObjXZ) || cur.HasFlag(MoveObjType._VerticalMoveObjY) || cur.HasFlag(MoveObjType._VerticalScaleObjAll) || cur.HasFlag(MoveObjType._Horizontal1RotationObjX) || cur.HasFlag(MoveObjType._Horizontal2RotationObjY) || cur.HasFlag(MoveObjType._Horizontal3RotationObjZ) || cur.HasFlag(MoveObjType._AllMoveXYZ);
            if (isTrigger && !isObject) return true;
            if (isObject && !isTrigger) return false;
            // Mixed AllMove (object+trigger) or trigger+object combos: decide by which squad flag dominates
            // If current mode explicitly is trigger MoveAll, treat as trigger; if it's AllMove, treat as object+trigger (return false to do object path which moves both via SetObjPosition)
            if (cur.HasFlag(MoveObjType._SquareMoveTriggerZone) && cur.HasFlag(MoveObjType._AllMoveXYZ)) return false;
            // Default: when in doubt, respect the panel's current selection — if trigger flag present without object square, it's trigger
            return isTrigger;
        }

        private static bool IsGizmoObjectMode()
        {
            var cur = Globals.CurrentMoveType;
            if (cur == MoveObjType.Null) return true;
            bool isObject = cur.HasFlag(MoveObjType._SquareMoveObjXZ) || cur.HasFlag(MoveObjType._VerticalMoveObjY) || cur.HasFlag(MoveObjType._VerticalScaleObjAll) || cur.HasFlag(MoveObjType._Horizontal1RotationObjX) || cur.HasFlag(MoveObjType._Horizontal2RotationObjY) || cur.HasFlag(MoveObjType._Horizontal3RotationObjZ) || cur.HasFlag(MoveObjType._AllMoveXYZ);
            bool isTrigger = cur.HasFlag(MoveObjType._SquareMoveTriggerZone) || cur.HasFlag(MoveObjType._VerticalMoveTriggerZoneY) || cur.HasFlag(MoveObjType._Horizontal2RotationZoneY);
            if (isObject && !isTrigger) return true;
            if (isTrigger && !isObject) return false;
            return isObject;
        }

        // ================================================================
        // Math helpers (legacy ray + new helpers)
        // ================================================================
        private static bool BuildRay(int mx, int my, int w, int h, Vector3 camPos, Vector3 camFront, out Vector3 ro, out Vector3 rd)
        {
            ro = camPos; rd = -Vector3.UnitZ;
            if (w <= 1 || h <= 1) return false;
            float nx = 2f * mx / w - 1f;
            float ny = 1f - 2f * my / h;
            Vector3 front = camFront;
            if (front.LengthSquared < 1e-8f) front = -Vector3.UnitZ;
            front.Normalize();
            Vector3 right = Vector3.Cross(front, Vector3.UnitY);
            if (right.LengthSquared < 1e-6f) right = Vector3.Cross(front, Vector3.UnitZ);
            right.Normalize();
            Vector3 up = Vector3.Cross(right, front);
            float tanV = (float)Math.Tan(Globals.FOV * Math.PI / 360.0);
            float aspect = (float)w / h;
            rd = front + right * (nx * tanV * aspect) + up * (ny * tanV);
            if (rd.LengthSquared < 1e-10f) return false;
            rd.Normalize();
            return true;
        }

        private static bool WorldToScreen(Vector3 p, Matrix4 vp, int w, int h, out PointF s)
        {
            s = new PointF();
            Vector4 c = new Vector4(p, 1f) * vp;
            if (c.W <= 1e-6f) return false;
            s.X = (c.X / c.W * 0.5f + 0.5f) * w;
            s.Y = (1f - (c.Y / c.W * 0.5f + 0.5f)) * h;
            return true;
        }
        private struct PointF { public float X, Y; }

        private static float DistToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float lenSq = dx * dx + dy * dy;
            float t = 0f;
            if (lenSq > 1e-6f)
            {
                t = ((px - ax) * dx + (py - ay) * dy) / lenSq;
                if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            }
            float cx = ax + dx * t - px;
            float cy = ay + dy * t - py;
            return (float)Math.Sqrt(cx * cx + cy * cy);
        }

        private static bool RayPlaneHit(Vector3 ro, Vector3 rd, Vector3 point, Vector3 normal, out Vector3 hit)
        {
            hit = Vector3.Zero;
            float denom = Vector3.Dot(normal, rd);
            if (Math.Abs(denom) < 1e-6f) return false;
            float t = Vector3.Dot(point - ro, normal) / denom;
            if (t < 0f) return false;
            hit = ro + rd * t;
            return true;
        }

        private static Vector4 Highlight(Vector4 baseColor)
        {
            return new Vector4(
                baseColor.X + (1f - baseColor.X) * 0.55f,
                baseColor.Y + (1f - baseColor.Y) * 0.55f,
                baseColor.Z + (1f - baseColor.Z) * 0.55f, 1f);
        }

        private static Matrix4 RotForAxis(int axis)
        {
            switch (axis)
            {
                case PartY: return new Matrix4(0f, 1f, 0f, 0f, -1f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f);
                case PartZ: return new Matrix4(0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, -1f, 0f, 0f, 0f, 0f, 0f, 0f, 1f);
                default: return Matrix4.Identity;
            }
        }

        private static Vector3 AxisDir(int part)
        {
            if (part == PartX) return Vector3.UnitX;
            if (part == PartY) return Vector3.UnitY;
            if (part == PartZ) return Vector3.UnitZ;
            return Vector3.Zero;
        }

        private static float L(int w, int h, Matrix4 view, Matrix4 proj, Vector3 pivot)
        {
            float dist = DistanceToCam(view, pivot);
            float len = dist * 0.15f;
            if (len < 1f) len = 1f;
            return len;
        }
        private static float DistanceToCam(Matrix4 view, Vector3 pivot)
        {
            Vector4 v = new Vector4(pivot, 1f) * view;
            return v.Z < 0f ? -v.Z : v.Z;
        }

        private static Matrix4 GetObjectRotation()
        {
            if (Globals.CurrentGizmoSpace == GizmoSpace.Local && DataBase.LastSelectNode is Object3D obj)
            {
                try
                {
                    var ang = obj.GetObjRotarionAngles_ToMove();
                    if (ang != null && ang.Length > 0)
                    {
                        Vector3 a = ang[0];
                        return Matrix4.CreateRotationX(a.X) * Matrix4.CreateRotationY(a.Y) * Matrix4.CreateRotationZ(a.Z);
                    }
                }
                catch { }
            }
            return Matrix4.Identity;
        }

        // ================================================================
        // Picking — extended for XY/XZ/YZ planes + rotate rings
        // ================================================================
        private static int PickPart(int mx, int my, int w, int h, Matrix4 view, Matrix4 proj)
        {
            Vector3 pivot = GetPivot();
            Matrix4 vp = view * proj;
            PointF cs;
            if (!WorldToScreen(pivot, vp, w, h, out cs)) return PartNone;

            float len = L(w, h, view, proj, pivot);
            float dist = DistanceToCam(view, pivot);
            float tanHalf = (float)Math.Tan(Globals.FOV * Math.PI / 360.0);
            float pxPerWorld = (h * 0.5f) / Math.Max(0.001f, dist * tanHalf);

            // rotate takes priority when tool is Rotate
            if (Globals.CurrentTool == EditorTool.Rotate)
            {
                float ringRadius = len; // rings drawn at Scale == len approx
                // project 64 points per ring and test distance to circle
                for (int axis = 0; axis < 3; axis++)
                {
                    int part = axis == 0 ? PartRotateX : axis == 1 ? PartRotateY : PartRotateZ;
                    Vector3 nrm = axis == 0 ? Vector3.UnitX : axis == 1 ? Vector3.UnitY : Vector3.UnitZ;
                    // build ring in world, account for Local space
                    Matrix4 rot = GetObjectRotation();
                    // sample circle
                    float best = 10f;
                    PointF prev = new PointF();
                    bool hasPrev = false;
                    for (int i = 0; i <= 64; i++)
                    {
                        float ang = i / 64f * (float)Math.PI * 2f;
                        Vector3 local = Vector3.Zero;
                        if (axis == 0) // X ring in YZ plane
                            local = new Vector3(0, (float)Math.Cos(ang), (float)Math.Sin(ang)) * len;
                        else if (axis == 1)
                            local = new Vector3((float)Math.Sin(ang), 0, (float)Math.Cos(ang)) * len;
                        else
                            local = new Vector3((float)Math.Cos(ang), (float)Math.Sin(ang), 0) * len;
                        Vector3 world = Vector3.TransformPosition(local, rot) + pivot;
                        PointF sp;
                        if (!WorldToScreen(world, vp, w, h, out sp)) { hasPrev = false; continue; }
                        if (hasPrev)
                        {
                            float d = DistToSegment(mx, my, prev.X, prev.Y, sp.X, sp.Y);
                            if (d < best) best = d;
                        }
                        prev = sp; hasPrev = true;
                    }
                    if (best < 10f) return part;
                }
            }

            // planar squares in move mode — test as screen quads
            if (Globals.CurrentTool == EditorTool.Move)
            {
                Matrix4 rot = GetObjectRotation();
                float planeSize = len * 0.24f;
                float planeOff = len * 0.045f;
                // build 3 quads like Re4QuadX CreateMovePlanesGeometry
                Vector3[] quadXY = new Vector3[] {
                    new Vector3(planeOff, planeOff, 0),
                    new Vector3(planeOff+planeSize, planeOff, 0),
                    new Vector3(planeOff+planeSize, planeOff+planeSize, 0),
                    new Vector3(planeOff, planeOff+planeSize, 0)
                };
                Vector3[] quadYZ = new Vector3[] {
                    new Vector3(0, planeOff, planeOff),
                    new Vector3(0, planeOff+planeSize, planeOff),
                    new Vector3(0, planeOff+planeSize, planeOff+planeSize),
                    new Vector3(0, planeOff, planeOff+planeSize)
                };
                Vector3[] quadXZ = new Vector3[] {
                    new Vector3(planeOff, 0, planeOff),
                    new Vector3(planeOff+planeSize, 0, planeOff),
                    new Vector3(planeOff+planeSize, 0, planeOff+planeSize),
                    new Vector3(planeOff, 0, planeOff+planeSize)
                };
                // flip to face camera in local space (Re4QuadX flips sign based on camDirLocal)
                // for picking we keep fixed orientation — sufficient for hit test
                if (PointInQuadScreen(quadXY, rot, pivot, vp, w, h, mx, my)) return PartXY;
                if (PointInQuadScreen(quadXZ, rot, pivot, vp, w, h, mx, my)) return PartXZ;
                if (PointInQuadScreen(quadYZ, rot, pivot, vp, w, h, mx, my)) return PartYZ;
            }

            // center square
            float screenHalf = len * 0.07f * pxPerWorld;
            if (screenHalf < 8f) screenHalf = 8f;
            if (screenHalf > 80f) screenHalf = 80f;
            if (Math.Abs(mx - cs.X) <= screenHalf && Math.Abs(my - cs.Y) <= screenHalf)
            {
                // when move, center means free plane (camera-facing); keep as PartCenterLegacy for compat
                if (Globals.CurrentTool == EditorTool.Move) return PartCenterLegacy;
            }

            float bestAxis = 12f;
            int found = PartNone;
            Matrix4 rotAxis = GetObjectRotation();
            for (int axis = PartX; axis <= PartZ; axis++)
            {
                Vector3 dir = AxisDir(axis);
                dir = Vector3.TransformVector(dir, rotAxis);
                Vector3 tipW = pivot + dir * len;
                PointF ts;
                if (!WorldToScreen(tipW, vp, w, h, out ts)) continue;
                float d = DistToSegment(mx, my, cs.X, cs.Y, ts.X, ts.Y);
                if (d < bestAxis) { bestAxis = d; found = axis; }
            }
            return found;
        }

        private static bool PointInQuadScreen(Vector3[] quadLocal, Matrix4 rot, Vector3 pivot, Matrix4 vp, int w, int h, int mx, int my)
        {
            PointF[] pts = new PointF[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 wp = Vector3.TransformPosition(quadLocal[i], rot) + pivot;
                if (!WorldToScreen(wp, vp, w, h, out pts[i])) return false;
            }
            // winding check via cross
            bool inside = true;
            for (int i = 0; i < 4; i++)
            {
                PointF a = pts[i];
                PointF b = pts[(i + 1) % 4];
                float cross = (b.X - a.X) * (my - a.Y) - (b.Y - a.Y) * (mx - a.X);
                if (cross < 0) { inside = false; break; }
            }
            // also allow opposite winding (camera flipped)
            if (!inside)
            {
                inside = true;
                for (int i = 0; i < 4; i++)
                {
                    PointF a = pts[i];
                    PointF b = pts[(i + 1) % 4];
                    float cross = (b.X - a.X) * (my - a.Y) - (b.Y - a.Y) * (mx - a.X);
                    if (cross > 0) { inside = false; break; }
                }
            }
            return inside;
        }

        // ================================================================
        // Public interaction API — extended
        // ================================================================
        internal static int dragLogCount = 0;
        public static bool TryBeginDrag(int mx, int my, int w, int h, Matrix4 view, Matrix4 proj, Vector3 camPos, Vector3 camFront)
        {
            if (!Enabled || !HasSelection()) { Log(string.Format("Begin skip enabled={0} sel={1}", Enabled, HasSelection())); return false; }
            int part = PickPart(mx, my, w, h, view, proj);
            if (part == PartNone) { Log("Begin: no part under cursor"); return false; }
            Vector3 ro, rd;
            if (!BuildRay(mx, my, w, h, camPos, camFront, out ro, out rd)) { Log("Begin: ray fail"); return false; }
            Vector3 pivot = GetPivot();
            if (part == PartCenterLegacy)
            {
                planeNormal = camFront;
                if (planeNormal.LengthSquared < 1e-6f) planeNormal = -Vector3.UnitZ;
                planeNormal.Normalize();
            }
            else if (part == PartXY) { Matrix4 r = GetObjectRotation(); planeNormal = Vector3.TransformVector(Vector3.UnitZ, r); planeNormal.Normalize(); }
            else if (part == PartXZ) { Matrix4 r = GetObjectRotation(); planeNormal = Vector3.TransformVector(Vector3.UnitY, r); planeNormal.Normalize(); }
            else if (part == PartYZ) { Matrix4 r = GetObjectRotation(); planeNormal = Vector3.TransformVector(Vector3.UnitX, r); planeNormal.Normalize(); }
            else if (part == PartRotateX || part == PartRotateY || part == PartRotateZ)
            {
                // for rotate, plane is perpendicular to rotation axis (view-facing trick in Re4QuadX uses screen delta, but we approximate with eye plane)
                Vector3 axis = part == PartRotateX ? Vector3.UnitX : part == PartRotateY ? Vector3.UnitY : Vector3.UnitZ;
                axis = Vector3.TransformVector(axis, GetObjectRotation());
                // choose plane facing camera most
                planeNormal = axis;
                // if axis is nearly parallel to view, keep as is; else use camera-facing for stable angle
                if (Math.Abs(Vector3.Dot(axis, camFront)) < 0.85f)
                {
                    // Use plane orthogonal to axis? Keep axis as normal to allow circular motion via ray-plane?
                    // For simplicity use camera front as plane to get screen delta style in UpdateDragRotate
                    planeNormal = camFront;
                    if (planeNormal.LengthSquared < 1e-6f) planeNormal = Vector3.UnitZ;
                    planeNormal.Normalize();
                }
            }
            else
            {
                Vector3 axis = AxisDir(part);
                axis = Vector3.TransformVector(axis, GetObjectRotation());
                Vector3 n = camFront - Vector3.Dot(camFront, axis) * axis;
                if (n.LengthSquared < 1e-6f) n = Vector3.UnitY - Vector3.Dot(Vector3.UnitY, axis) * axis;
                if (n.LengthSquared < 1e-6f) n = Vector3.UnitZ;
                planeNormal = n.Normalized();
            }
            planePoint = pivot;
            Vector3 hit;
            if (!RayPlaneHit(ro, rd, planePoint, planeNormal, out hit)) { Log("Begin: plane fail"); return false; }
            planeHitStart = hit;
            dragObjects.Clear(); dragStartPositions.Clear(); dragStartRotations.Clear();
            dragStartFullPositions.Clear(); dragStartFullStates.Clear();
            var sel = Re4QuadExtremeEditor.src.DataBase.SelectedNodes;
            if (sel != null)
            {
                foreach (var kv in sel)
                {
                    Object3D obj = kv.Value as Object3D;
                    if (obj != null)
                    {
                        dragObjects.Add(obj);
                        dragStartPositions.Add(obj.GetObjPosition_ToCamera());
                        try
                        {
                            Vector3[] r = obj.GetObjRotarionAngles_ToMove();
                            dragStartRotations.Add((r != null && r.Length > 0) ? r[0] : Vector3.Zero);
                        }
                        catch { dragStartRotations.Add(Vector3.Zero); }
                        try
                        {
                            Vector3[] full = obj.GetObjPostion_ToMove_General();
                            dragStartFullPositions.Add(full != null ? (Vector3[])full.Clone() : null);
                        }
                        catch { dragStartFullPositions.Add(null); }
                        try
                        {
                            dragStartFullStates.Add(UndoSystem.CaptureFullTransform(obj));
                        }
                        catch { dragStartFullStates.Add(new UndoSystem.FullTransformState()); }
                    }
                }
            }
            if (dragObjects.Count == 0) { Log("Begin: zero objects"); return false; }
            dragging = true; dragPart = part; hoverPart = part;
            ActiveAxis = PartToAxis(part);
            Log(string.Format("Begin OK part={0} axis={1} objs={2} pivot=({3:F2},{4:F2},{5:F2})",
                part, ActiveAxis, dragObjects.Count, planePoint.X, planePoint.Y, planePoint.Z));
            return true;
        }

        private static GizmoAxis PartToAxis(int part)
        {
            switch (part)
            {
                case PartX: return GizmoAxis.X;
                case PartY: return GizmoAxis.Y;
                case PartZ: return GizmoAxis.Z;
                case PartXY: return GizmoAxis.XY;
                case PartXZ: return GizmoAxis.XZ;
                case PartYZ: return GizmoAxis.YZ;
                case PartRotateX: return GizmoAxis.RotateX;
                case PartRotateY: return GizmoAxis.RotateY;
                case PartRotateZ: return GizmoAxis.RotateZ;
                default: return GizmoAxis.None;
            }
        }

        public static void UpdateDrag(int mx, int my, int w, int h, Vector3 camPos, Vector3 camFront)
        {
            if (!dragging) return;
            // rotate handling — universal for ALL object types + ALL TriggerZones
            if (dragPart == PartRotateX || dragPart == PartRotateY || dragPart == PartRotateZ)
            {
                Vector3 ro, rd;
                if (!BuildRay(mx, my, w, h, camPos, camFront, out ro, out rd)) return;
                Vector3 hit;
                if (!RayPlaneHit(ro, rd, planePoint, planeNormal, out hit)) return;
                Vector3 pivot = planePoint;
                Vector3 axis = dragPart == PartRotateX ? Vector3.UnitX : dragPart == PartRotateY ? Vector3.UnitY : Vector3.UnitZ;
                axis = Vector3.TransformVector(axis, GetObjectRotation());
                if (axis.LengthSquared < 1e-8f) axis = dragPart == PartRotateX ? Vector3.UnitX : dragPart == PartRotateY ? Vector3.UnitY : Vector3.UnitZ;
                axis.Normalize();
                Vector3 v0 = (planeHitStart - pivot); v0 = v0 - Vector3.Dot(v0, axis) * axis;
                Vector3 v1 = (hit - pivot); v1 = v1 - Vector3.Dot(v1, axis) * axis;
                if (v0.LengthSquared < 1e-6f || v1.LengthSquared < 1e-6f) return;
                v0.Normalize(); v1.Normalize();
                float dot = MathHelper.Clamp(Vector3.Dot(v0, v1), -1f, 1f);
                float ang = (float)Math.Acos(dot);
                Vector3 cross = Vector3.Cross(v0, v1);
                if (Vector3.Dot(cross, axis) < 0) ang = -ang;
                float rad = ang; // already in radians
                Matrix4 rotMat;
                try { rotMat = Matrix4.CreateFromAxisAngle(axis, rad); }
                catch { rotMat = Matrix4.Identity; }

                bool gizmoTriggerMode = IsGizmoTriggerMode();
                bool gizmoObjectMode = IsGizmoObjectMode();
                if (!gizmoTriggerMode && !gizmoObjectMode) { gizmoTriggerMode = true; gizmoObjectMode = true; }

                for (int i = 0; i < dragObjects.Count; i++)
                {
                    Object3D obj = dragObjects[i];
                    if (obj == null || obj.Parent == null) continue;
                    Vector3[] baseFull = (i < dragStartFullPositions.Count) ? dragStartFullPositions[i] : null;
                    TriggerZoneCategory cat = TriggerZoneCategory.Disable;
                    try { cat = obj.GetTriggerZoneCategory(); } catch { }
                    bool isTriggerZone = (cat == TriggerZoneCategory.Category01 || cat == TriggerZoneCategory.Category02) && baseFull != null && baseFull.Length >= 7;
                    bool isITA = obj.Group == GroupType.ITA;
                    bool doTrigger = isTriggerZone && (isITA ? gizmoTriggerMode : true);
                    bool doObject = isITA ? gizmoObjectMode : true;
                    bool didTrigger = false;
                    bool didEuler = false;

                    // 1) TriggerZone polygon: rotate its 4 corners around its own center (pos[6]) — ITA-only separation, otherwise universal
                    if (doTrigger)
                    {
                        try
                        {
                            Vector3[] curFull = (Vector3[])baseFull.Clone();
                            Vector3 center = baseFull[6];
                            for (int p = 1; p <= 4; p++)
                            {
                                Vector3 off = baseFull[p] - center;
                                Vector3 rotated = Vector3.TransformVector(off, rotMat);
                                curFull[p] = center + rotated;
                            }
                            // Keep center coherent with rotated corners (xz recomputed, y preserved)
                            {
                                float xmin = curFull[1].X, xmax = curFull[1].X, zmin = curFull[1].Z, zmax = curFull[1].Z;
                                for (int p = 2; p <= 4; p++) { if (curFull[p].X < xmin) xmin = curFull[p].X; if (curFull[p].X > xmax) xmax = curFull[p].X; if (curFull[p].Z < zmin) zmin = curFull[p].Z; if (curFull[p].Z > zmax) zmax = curFull[p].Z; }
                                curFull[6] = new Vector3(xmin + (xmax - xmin) * 0.5f, curFull[6].Y, zmin + (zmax - zmin) * 0.5f);
                            }
                            obj.SetObjPostion_ToMove_General(curFull);
                            didTrigger = true;
                        }
                        catch { }
                        if (isITA && gizmoTriggerMode && !gizmoObjectMode) continue; // trigger-only mode for ITA: don't also rotate object
                    }

                    // 2) Euler angles: works for Enemies, EtcModel, Items, QuadCustom, EFF, etc.
                    if (doObject)
                    {
                        bool hasRot = false;
                        try
                        {
                            Vector3[] test = obj.GetObjRotarionAngles_ToMove();
                            hasRot = (test != null && test.Length > 0);
                        }
                        catch { hasRot = false; }
                        if (hasRot)
                        {
                            try
                            {
                                Vector3 baseRot = (i < dragStartRotations.Count) ? dragStartRotations[i] : Vector3.Zero;
                                Vector3 cur = baseRot;
                                if (dragPart == PartRotateX) cur.X += rad;
                                else if (dragPart == PartRotateY) cur.Y += rad;
                                else if (dragPart == PartRotateZ) cur.Z += rad;
                                obj.SetObjRotarionAngles_ToMove(new Vector3[] { cur });
                                didEuler = true;
                            }
                            catch { }
                        }

                        // 3) Fallback orbital: pure position objects (RTP, LIT, ESE, Emi without angles, etc.)
                        if (!didTrigger && !didEuler)
                        {
                            try
                            {
                                Vector3 baseCam = dragStartPositions[i];
                                Vector3 off = baseCam - pivot;
                                Vector3 rotatedOff = Vector3.TransformVector(off, rotMat);
                                Vector3 newCam = pivot + rotatedOff;
                                obj.SetObjPosition_ToCamera(newCam);
                            }
                            catch { }
                        }
                    }
                }
                return;
            }

            Vector3 ro2, rd2;
            if (!BuildRay(mx, my, w, h, camPos, camFront, out ro2, out rd2)) return;
            Vector3 hit2;
            if (!RayPlaneHit(ro2, rd2, planePoint, planeNormal, out hit2)) return;
            Vector3 delta = hit2 - planeHitStart;

            // axis-constrained
            if (dragPart == PartX || dragPart == PartY || dragPart == PartZ)
            {
                Vector3 axis = AxisDir(dragPart);
                axis = Vector3.TransformVector(axis, GetObjectRotation());
                delta = axis * Vector3.Dot(delta, axis);
            }
            else if (dragPart == PartXY)
            {
                Matrix4 r = GetObjectRotation();
                Vector3 ax = Vector3.TransformVector(Vector3.UnitX, r);
                Vector3 ay = Vector3.TransformVector(Vector3.UnitY, r);
                // project delta onto XY plane (remove Z along normal)
                // already on plane Z, but ensure
            }
            else if (dragPart == PartXZ)
            {
                // keep as is on Y plane
            }
            else if (dragPart == PartYZ)
            {
                // keep as is on X plane
            }
            // center/legacy also free
            if (SnapStep > 0f)
            {
                delta.X = (float)Math.Round(delta.X / SnapStep) * SnapStep;
                delta.Y = (float)Math.Round(delta.Y / SnapStep) * SnapStep;
                delta.Z = (float)Math.Round(delta.Z / SnapStep) * SnapStep;
            }
            bool gizmoTriggerMove = IsGizmoTriggerMode();
            bool gizmoObjectMove = IsGizmoObjectMode();
            if (!gizmoTriggerMove && !gizmoObjectMove) { gizmoTriggerMove = true; gizmoObjectMove = true; }
            for (int i = 0; i < dragObjects.Count; i++)
            {
                Object3D obj = dragObjects[i];
                if (obj == null || obj.Parent == null) continue;
                Vector3[] baseFull = (i < dragStartFullPositions.Count) ? dragStartFullPositions[i] : null;
                TriggerZoneCategory cat = TriggerZoneCategory.Disable;
                try { cat = obj.GetTriggerZoneCategory(); } catch { }
                bool isTriggerZone = (cat == TriggerZoneCategory.Category01 || cat == TriggerZoneCategory.Category02) && baseFull != null && baseFull.Length >= 7;
                bool isITA = obj.Group == GroupType.ITA;

                if (isITA && gizmoTriggerMove && !gizmoObjectMove && isTriggerZone)
                {
                    // ITA trigger-only move: keep object pos[0] untouched, move quad + TrueY (panel: TriggerZone; Squad)
                    try
                    {
                        Vector3[] curFull = (Vector3[])baseFull.Clone();
                        float scale = 100f;
                        try
                        {
                            Vector3 camStart = dragStartPositions[i];
                            Vector3 cen = baseFull[6];
                            if (camStart.LengthSquared > 1e-6f && cen.LengthSquared > 1e-6f)
                            {
                                if (Math.Abs(camStart.X) > 0.01f && Math.Abs(cen.X) > 0.01f)
                                {
                                    float r = cen.X / camStart.X;
                                    if (Math.Abs(r - 10f) < 2f) scale = 10f;
                                    else if (Math.Abs(r - 1f) < 0.2f) scale = 1f;
                                }
                            }
                        }
                        catch { }
                        Vector3 dMove = delta * scale;
                        curFull[1].X += dMove.X; curFull[1].Z += dMove.Z;
                        curFull[2].X += dMove.X; curFull[2].Z += dMove.Z;
                        curFull[3].X += dMove.X; curFull[3].Z += dMove.Z;
                        curFull[4].X += dMove.X; curFull[4].Z += dMove.Z;
                        curFull[6].X += dMove.X; curFull[6].Z += dMove.Z;
                        curFull[5].Y += dMove.Y;
                        obj.SetObjPostion_ToMove_General(curFull);
                    }
                    catch
                    {
                        obj.SetObjPosition_ToCamera(dragStartPositions[i] + delta);
                    }
                }
                else if (isITA && gizmoObjectMove && !gizmoTriggerMove && isTriggerZone)
                {
                    // ITA object-only move: move pos[0] only, keep quad untouched (panel: item: Squad = object only)
                    try
                    {
                        Vector3[] curFull = (Vector3[])baseFull.Clone();
                        float scale = 100f;
                        try
                        {
                            Vector3 camStart = dragStartPositions[i];
                            Vector3 cen = baseFull[6];
                            if (camStart.LengthSquared > 1e-6f && cen.LengthSquared > 1e-6f)
                            {
                                if (Math.Abs(camStart.X) > 0.01f && Math.Abs(cen.X) > 0.01f)
                                {
                                    float r = cen.X / camStart.X;
                                    if (Math.Abs(r - 10f) < 2f) scale = 10f;
                                    else if (Math.Abs(r - 1f) < 0.2f) scale = 1f;
                                }
                            }
                        }
                        catch { }
                        Vector3 dMove = delta * scale;
                        curFull[0].X += dMove.X; curFull[0].Y += dMove.Y; curFull[0].Z += dMove.Z;
                        obj.SetObjPostion_ToMove_General(curFull);
                    }
                    catch
                    {
                        obj.SetObjPosition_ToCamera(dragStartPositions[i] + delta);
                    }
                }
                else
                {
                    // Non-ITA or universal / AllMove: move object — for trigger zones this also drags the zone with it via SetObjPosition_ToCamera (both)
                    obj.SetObjPosition_ToCamera(dragStartPositions[i] + delta);
                }
            }
        }

        public static void EndDrag()
        {
            if (!dragging) return;
            int finishedPart = dragPart;
            dragging = false; dragPart = PartNone; ActiveAxis = GizmoAxis.None;
            if (dragObjects.Count > 0)
            {
                if (finishedPart == PartRotateX || finishedPart == PartRotateY || finishedPart == PartRotateZ)
                {
                    // Universal undo: captures Position + Rotation + Scale + TriggerZone polygon
                    var objs = new List<Object3D>(dragObjects);
                    var startStates = new List<UndoSystem.FullTransformState>(dragStartFullStates);
                    if (startStates.Count != objs.Count)
                    {
                        startStates.Clear();
                        for (int i = 0; i < objs.Count; i++)
                        {
                            try { startStates.Add(UndoSystem.CaptureFullTransform(objs[i])); }
                            catch { startStates.Add(new UndoSystem.FullTransformState()); }
                        }
                        if (objs.Count == dragStartRotations.Count)
                        {
                            for (int i = 0; i < startStates.Count && i < dragStartRotations.Count; i++)
                            {
                                if (startStates[i].Rotation == null) startStates[i].Rotation = new Vector3[] { dragStartRotations[i] };
                                else if (startStates[i].Rotation.Length > 0) startStates[i].Rotation[0] = dragStartRotations[i];
                            }
                        }
                    }
                    UndoSystem.PushFullTransform(objs, startStates, delegate () {
                        var arr = new UndoSystem.FullTransformState[objs.Count];
                        for (int i = 0; i < objs.Count; i++)
                        {
                            try { arr[i] = UndoSystem.CaptureFullTransform(objs[i]); }
                            catch { arr[i] = new UndoSystem.FullTransformState(); }
                        }
                        return arr;
                    });
                }
                else
                {
                    // Move (including planar/vertical and trigger zones) — use FullTransform so trigger height/radius/points are undoable
                    bool pushed = false;
                    try
                    {
                        var objs = new List<Object3D>(dragObjects);
                        var startStates = new List<UndoSystem.FullTransformState>(dragStartFullStates);
                        if (startStates.Count == objs.Count && startStates.Count > 0)
                        {
                            UndoSystem.PushFullTransform(objs, startStates, delegate () {
                                var arr = new UndoSystem.FullTransformState[objs.Count];
                                for (int i = 0; i < objs.Count; i++) arr[i] = UndoSystem.CaptureFullTransform(objs[i]);
                                return arr;
                            });
                            pushed = true;
                        }
                    }
                    catch { }
                    if (!pushed)
                    {
                        UndoSystem.PushMove(dragObjects, dragStartPositions, delegate () {
                            Vector3[] cur = new Vector3[dragObjects.Count];
                            for (int i = 0; i < cur.Length; i++) cur[i] = dragObjects[i].GetObjPosition_ToCamera();
                            return cur;
                        });
                    }
                }
            }
            dragObjects.Clear(); dragStartPositions.Clear(); dragStartRotations.Clear();
            dragStartFullPositions.Clear(); dragStartFullStates.Clear();
            if (TransformApplied != null) TransformApplied.Invoke();
        }

        public static int UpdateHover(int mx, int my, int w, int h, Matrix4 view, Matrix4 proj)
        {
            if (!Enabled || !HasSelection()) { hoverPart = PartNone; ActiveAxis = GizmoAxis.None; return PartNone; }
            hoverPart = PickPart(mx, my, w, h, view, proj);
            ActiveAxis = PartToAxis(hoverPart);
            return hoverPart;
        }

        // ================================================================
        // Rendering — now includes planes + rotate rings, World/Local,
        // alpha blending, and selection-color path (for future color picking)
        // ================================================================
        private static int gizmoProgram = 0;
        private static int gizmoVao = 0;
        private static int gizmoVbo = 0;
        private static int uMvpLocation = -1;
        private static int uColorLocation = -1;
        private static readonly List<float> partVerts = new List<float>(2048);
        private static readonly List<float> allVerts = new List<float>(8192);
        private static float[] uploadArr = null;
        private static readonly int[] rangeStart = new int[10];
        private static readonly int[] rangeCount = new int[10];

        // second program for Re4QuadX-style (model/view/proj + alpha) — reuse same program but add uniforms when needed
        private static int advProgram = 0;
        private static int advVaoMove = 0, advVboMove = 0;
        private static int advVaoPlanes = 0, advVboPlanes = 0;
        private static int advVaoRotate = 0, advVboRotate = 0;
        private static readonly List<float> vertsMove = new List<float>(512);
        private static readonly List<float> vertsPlanes = new List<float>(512);
        private static readonly List<float> vertsRotate = new List<float>(512);
        private static bool advBuilt = false;
        private const float ArrowLength = 1f;
        private const float ConeHeight = 0.25f;
        private const float ConeRadius = 0.0625f;
        private const int ConeSegments = 12;
        private const int CircleSegments = 64;
        private static readonly Vector3[] axisColors = {
            new Vector3(1.0f,0.0f,0.0f),
            new Vector3(0.0f,1.0f,0.0f),
            new Vector3(0.0f,0.0f,1.0f)
        };

        private static void EnsureGizmoGlObjects()
        {
            if (gizmoProgram != 0) return;
            const string vertSrc = "#version 330 core\nlayout(location = 0) in vec3 aPos;\nuniform mat4 uMVP;\nvoid main(){ gl_Position = vec4(aPos, 1.0) * uMVP; }\n";
            const string fragSrc = "#version 330\nuniform vec4 uColor;\nout vec4 fragColor;\nvoid main(){ fragColor = uColor; }\n";
            int vs = GL.CreateShader(ShaderType.VertexShader); GL.ShaderSource(vs, vertSrc); GL.CompileShader(vs);
            int fs = GL.CreateShader(ShaderType.FragmentShader); GL.ShaderSource(fs, fragSrc); GL.CompileShader(fs);
            gizmoProgram = GL.CreateProgram(); GL.AttachShader(gizmoProgram, vs); GL.AttachShader(gizmoProgram, fs); GL.LinkProgram(gizmoProgram);
            GL.DetachShader(gizmoProgram, vs); GL.DetachShader(gizmoProgram, fs); GL.DeleteShader(vs); GL.DeleteShader(fs);
            uMvpLocation = GL.GetUniformLocation(gizmoProgram, "uMVP");
            uColorLocation = GL.GetUniformLocation(gizmoProgram, "uColor");
            gizmoVao = GL.GenVertexArray(); gizmoVbo = GL.GenBuffer();
            GL.BindVertexArray(gizmoVao); GL.BindBuffer(BufferTarget.ArrayBuffer, gizmoVbo);
            GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.BindVertexArray(0); GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        private static void EnsureAdvanced()
        {
            if (advBuilt) return;
            advBuilt = true;
            // build move geometry (lines + cones)
            vertsMove.Clear(); vertsPlanes.Clear(); vertsRotate.Clear();
            // lines
            AddLine(Vector3.Zero, Vector3.UnitX * ArrowLength);
            AddLine(Vector3.Zero, Vector3.UnitY * ArrowLength);
            AddLine(Vector3.Zero, Vector3.UnitZ * ArrowLength);
            AddCone(Vector3.UnitX * ArrowLength);
            AddCone(Vector3.UnitY * ArrowLength);
            AddCone(Vector3.UnitZ * ArrowLength);
            // planes
            const float planeSize = 0.24f; const float planeOffset = 0.045f;
            Vector3 xOff = Vector3.UnitX * planeOffset, yOff = Vector3.UnitY * planeOffset, zOff = Vector3.UnitZ * planeOffset;
            Vector3 xSize = Vector3.UnitX * planeSize, ySize = Vector3.UnitY * planeSize, zSize = Vector3.UnitZ * planeSize;
            AddQuadPlane(xOff + yOff, xOff + yOff + xSize, xOff + yOff + xSize + ySize, xOff + yOff + ySize);
            AddQuadPlane(yOff + zOff, yOff + zOff + ySize, yOff + zOff + ySize + zSize, yOff + zOff + zSize);
            AddQuadPlane(xOff + zOff, xOff + zOff + xSize, xOff + zOff + xSize + zSize, xOff + zOff + zSize);
            // rings
            AddRing(Vector3.UnitX, Vector3.UnitY);
            AddRing(Vector3.UnitY, Vector3.UnitZ);
            AddRing(Vector3.UnitZ, Vector3.UnitX);
            // shaders
            string vs = @"#version 330 core
layout (location = 0) in vec3 aPosition;
uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
void main(){ gl_Position = projection * view * model * vec4(aPosition, 1.0); }";
            string fs = @"#version 330 core
out vec4 FragColor;
uniform vec3 u_Color;
uniform float u_Alpha;
void main(){ FragColor = vec4(u_Color, u_Alpha); }";
            int v = GL.CreateShader(ShaderType.VertexShader); GL.ShaderSource(v, vs); GL.CompileShader(v);
            int f = GL.CreateShader(ShaderType.FragmentShader); GL.ShaderSource(f, fs); GL.CompileShader(f);
            advProgram = GL.CreateProgram(); GL.AttachShader(advProgram, v); GL.AttachShader(advProgram, f); GL.LinkProgram(advProgram);
            GL.DeleteShader(v); GL.DeleteShader(f);
            advVaoMove = GL.GenVertexArray(); advVboMove = GL.GenBuffer();
            GL.BindVertexArray(advVaoMove); GL.BindBuffer(BufferTarget.ArrayBuffer, advVboMove);
            GL.BufferData(BufferTarget.ArrayBuffer, vertsMove.Count * sizeof(float), vertsMove.ToArray(), BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3*sizeof(float), 0); GL.EnableVertexAttribArray(0); GL.BindVertexArray(0);
            advVaoRotate = GL.GenVertexArray(); advVboRotate = GL.GenBuffer();
            GL.BindVertexArray(advVaoRotate); GL.BindBuffer(BufferTarget.ArrayBuffer, advVboRotate);
            GL.BufferData(BufferTarget.ArrayBuffer, vertsRotate.Count * sizeof(float), vertsRotate.ToArray(), BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3*sizeof(float), 0); GL.EnableVertexAttribArray(0); GL.BindVertexArray(0);
            advVaoPlanes = GL.GenVertexArray(); advVboPlanes = GL.GenBuffer();
            GL.BindVertexArray(advVaoPlanes); GL.BindBuffer(BufferTarget.ArrayBuffer, advVboPlanes);
            GL.BufferData(BufferTarget.ArrayBuffer, vertsPlanes.Count * sizeof(float), vertsPlanes.ToArray(), BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3*sizeof(float), 0); GL.EnableVertexAttribArray(0); GL.BindVertexArray(0);
        }

        private static void AddLine(Vector3 a, Vector3 b){ vertsMove.Add(a.X); vertsMove.Add(a.Y); vertsMove.Add(a.Z); vertsMove.Add(b.X); vertsMove.Add(b.Y); vertsMove.Add(b.Z); }
        private static void AddCone(Vector3 tip){
            Vector3 dir = tip.Normalized(); Vector3 coneBase = tip - dir * ConeHeight;
            Vector3 tmp = Vector3.UnitY; if (Math.Abs(Vector3.Dot(dir, tmp)) > 0.999f) tmp = Vector3.UnitZ;
            Vector3 right = Vector3.Cross(dir, tmp).Normalized(); Vector3 up = Vector3.Cross(right, dir);
            for(int i=0;i<ConeSegments;i++){ float a1 = i/(float)ConeSegments*MathHelper.TwoPi; float a2=(i+1)/(float)ConeSegments*MathHelper.TwoPi;
                Vector3 p1 = coneBase + right*(float)Math.Cos(a1)*ConeRadius + up*(float)Math.Sin(a1)*ConeRadius;
                Vector3 p2 = coneBase + right*(float)Math.Cos(a2)*ConeRadius + up*(float)Math.Sin(a2)*ConeRadius;
                vertsMove.Add(tip.X);vertsMove.Add(tip.Y);vertsMove.Add(tip.Z);
                vertsMove.Add(p1.X);vertsMove.Add(p1.Y);vertsMove.Add(p1.Z);
                vertsMove.Add(p2.X);vertsMove.Add(p2.Y);vertsMove.Add(p2.Z);
            }
        }
        private static void AddQuadPlane(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3){
            vertsPlanes.Add(p0.X);vertsPlanes.Add(p0.Y);vertsPlanes.Add(p0.Z);
            vertsPlanes.Add(p1.X);vertsPlanes.Add(p1.Y);vertsPlanes.Add(p1.Z);
            vertsPlanes.Add(p2.X);vertsPlanes.Add(p2.Y);vertsPlanes.Add(p2.Z);
            vertsPlanes.Add(p0.X);vertsPlanes.Add(p0.Y);vertsPlanes.Add(p0.Z);
            vertsPlanes.Add(p2.X);vertsPlanes.Add(p2.Y);vertsPlanes.Add(p2.Z);
            vertsPlanes.Add(p3.X);vertsPlanes.Add(p3.Y);vertsPlanes.Add(p3.Z);
        }
        private static void AddRing(Vector3 axis, Vector3 start){
            for(int i=0;i<CircleSegments;i++){ float a1=i/(float)CircleSegments*MathHelper.TwoPi; float a2=(i+1)/(float)CircleSegments*MathHelper.TwoPi;
                Quaternion q1=Quaternion.FromAxisAngle(axis,a1); Quaternion q2=Quaternion.FromAxisAngle(axis,a2);
                Vector3 p1=Vector3.Transform(start,q1); Vector3 p2=Vector3.Transform(start,q2);
                vertsRotate.Add(p1.X);vertsRotate.Add(p1.Y);vertsRotate.Add(p1.Z);
                vertsRotate.Add(p2.X);vertsRotate.Add(p2.Y);vertsRotate.Add(p2.Z);
            }
        }

        private static void EmitTri(List<float> list, Vector3 a, Vector3 b, Vector3 c){ list.Add(a.X); list.Add(a.Y); list.Add(a.Z); list.Add(b.X); list.Add(b.Y); list.Add(b.Z); list.Add(c.X); list.Add(c.Y); list.Add(c.Z); }
        private static void EmitPrism(List<float> list, Vector3 p0, Vector3 p1, Vector3 u, Vector3 v, float t){
            Vector3 a0=p0-u*t-v*t, b0=p0+u*t-v*t, c0=p0+u*t+v*t, d0=p0-u*t+v*t;
            Vector3 a1=p1-u*t-v*t, b1=p1+u*t-v*t, c1=p1+u*t+v*t, d1=p1-u*t+v*t;
            EmitTri(list,a0,a1,b1); EmitTri(list,a0,b1,b0); EmitTri(list,b0,b1,c1); EmitTri(list,b0,c1,c0);
            EmitTri(list,c0,c1,d1); EmitTri(list,c0,d1,d0); EmitTri(list,d0,d1,a1); EmitTri(list,d0,a1,a0);
            EmitTri(list,a0,c0,b0); EmitTri(list,a0,d0,c0); EmitTri(list,a1,b1,c1); EmitTri(list,a1,c1,d1);
        }
        private static void EmitPyramid(List<float> list, Vector3[] ring, Vector3 apex){
            for(int i=0;i<4;i++) EmitTri(list, ring[i], ring[(i+1)&3], apex);
            EmitTri(list, ring[0], ring[2], ring[1]); EmitTri(list, ring[0], ring[3], ring[2]);
        }
        private static void EmitArrow(List<float> list, Vector3 origin, Vector3 dir, float len, float shaftW, float headLen, float headW){
            Vector3 front=dir.Normalized(); Vector3 helper=Math.Abs(Vector3.Dot(front,Vector3.UnitY))>0.95f?Vector3.UnitZ:Vector3.UnitY;
            Vector3 u=Vector3.Cross(front,helper).Normalized(); Vector3 v=Vector3.Cross(u,front);
            float shaftLen=len-headLen; Vector3 ringC=origin+front*shaftLen; Vector3 apex=origin+front*len;
            EmitPrism(list, origin, ringC, u, v, shaftW);
            Vector3[] ring=new Vector3[]{ringC-u*headW-v*headW, ringC+u*headW-v*headW, ringC+u*headW+v*headW, ringC-u*headW+v*headW};
            EmitPyramid(list, ring, apex);
        }
        private static void EmitGuideLine(List<float> list, Vector3 origin, Vector3 dir, float len){
            Vector3 front=dir.Normalized(); Vector3 helper=Math.Abs(Vector3.Dot(front,Vector3.UnitY))>0.95f?Vector3.UnitZ:Vector3.UnitY;
            Vector3 u=Vector3.Cross(front,helper).Normalized(); Vector3 v=Vector3.Cross(u,front);
            EmitPrism(list, origin-front*len*2.5f, origin+front*len*4.0f, u, v, len*0.006f);
        }
        private static void EmitQuad(List<float> list, Vector3 center, Vector3 u, Vector3 v, float half){
            Vector3 a=center-u*half-v*half; Vector3 b=center+u*half-v*half; Vector3 c=center+u*half+v*half; Vector3 d=center-u*half+v*half;
            EmitTri(list,a,b,c); EmitTri(list,a,c,d);
        }
        private static Vector4 ColorForPart(int part){
            Vector4 col; if(part==PartX) col=ColX; else if(part==PartY) col=ColY; else if(part==PartZ) col=ColZ;
            else if(part==PartXY) col=ColXY; else if(part==PartXZ) col=ColXZ; else if(part==PartYZ) col=ColYZ;
            else if(part==PartRotateX) col=ColX; else if(part==PartRotateY) col=ColY; else if(part==PartRotateZ) col=ColZ;
            else col=ColCenter;
            float g=partGlow[part]; if(g>0.001f){ Vector4 hot=Highlight(col); col=new Vector4(col.X+(hot.X-col.X)*g, col.Y+(hot.Y-col.Y)*g, col.Z+(hot.Z-col.Z)*g, col.W); }
            return col;
        }

        // New advanced render — used when tool/space matters; falls back to legacy if not ready
        public static void Render(Matrix4 view, Matrix4 proj, Vector3 camPos, Camera camera, EditorTool tool, GizmoSpace space, bool selectionMode = false)
        {
            if (!Enabled || !HasSelection()) return;
            EnsureAdvanced();
            Vector3 pivot = GetPivot();
            // scale
            float dist = (camera.Position - pivot).Length; float scale = dist * 0.14f; if (scale < 0.5f) scale = 0.5f; Scale = scale;
            Matrix4 objRot = GetObjectRotation();
            GL.Disable(EnableCap.DepthTest); GL.Enable(EnableCap.Blend); GL.Disable(EnableCap.CullFace);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.UseProgram(advProgram);
            int modelLoc=GL.GetUniformLocation(advProgram,"model"), viewLoc=GL.GetUniformLocation(advProgram,"view"), projLoc=GL.GetUniformLocation(advProgram,"projection"),
                colLoc=GL.GetUniformLocation(advProgram,"u_Color"), alphaLoc=GL.GetUniformLocation(advProgram,"u_Alpha");
            GL.UniformMatrix4(viewLoc,false,ref view); GL.UniformMatrix4(projLoc,false,ref proj); GL.Uniform1(alphaLoc,1.0f);
            if(selectionMode) GL.LineWidth(10f);
            if(tool==EditorTool.Move){
                int coneVerts=ConeSegments*3, lineVerts=2;
                // per-axis hover scale + line width + glow color
                for(int ax=0; ax<3; ax++){
                    float hs = HoverScale(ax==0?PartX:ax==1?PartY:PartZ);
                    float s = scale * hs;
                    Matrix4 m = Matrix4.CreateScale(s)*objRot*Matrix4.CreateTranslation(pivot);
                    GL.BindVertexArray(advVaoMove); GL.UniformMatrix4(modelLoc,false,ref m);
                    Vector3 baseCol = axisColors[ax];
                    Vector3 drawCol = baseCol;
                    float g = partGlow[ax==0?PartX:ax==1?PartY:PartZ];
                    if(g>0.001f){
                        Vector4 bc = new Vector4(baseCol.X,baseCol.Y,baseCol.Z,1f);
                        Vector4 hc = Highlight(bc);
                        drawCol = new Vector3(bc.X+(hc.X-bc.X)*g, bc.Y+(hc.Y-bc.Y)*g, bc.Z+(hc.Z-bc.Z)*g);
                        GL.LineWidth(2.2f + g*1.2f);
                    } else GL.LineWidth(selectionMode?10f:2.2f);
                    Vector3 sel = ax==0?SelectColorMoveX:ax==1?SelectColorMoveY:SelectColorMoveZ;
                    GL.Uniform3(colLoc, selectionMode?sel:drawCol);
                    int lineOff = ax*lineVerts;
                    int triOff = 6 + ax*coneVerts;
                    GL.DrawArrays(PrimitiveType.Lines,lineOff,lineVerts);
                    GL.DrawArrays(PrimitiveType.Triangles,triOff,coneVerts);
                }
                GL.LineWidth(selectionMode?10f:2.2f);
                // planes — face camera with hover scale and brightening
                Matrix4 camWorld = Matrix4.Invert(view); Vector3 camWorldPos = camWorld.Row3.Xyz;
                Vector3 toGizmo = camWorldPos - pivot; Matrix4 invRot = Matrix4.Invert(objRot); Vector3 camLocal = Vector3.TransformVector(toGizmo, invRot);
                float xs = camLocal.X>=0?1:-1, ys=camLocal.Y>=0?1:-1, zs=camLocal.Z>=0?1:-1;
                GL.BindVertexArray(advVaoPlanes);
                for(int pi=0; pi<3; pi++){
                    int part = pi==0?PartXY:pi==1?PartYZ:PartXZ;
                    float hs = HoverScale(part);
                    float pa = selectionMode?1f:0.42f + partGlow[part]*0.15f;
                    GL.Uniform1(alphaLoc, pa);
                    Vector3 pc = pi==0?axisColors[2]:pi==1?axisColors[0]:axisColors[1];
                    float pg = partGlow[part];
                    if(pg>0.001f){ Vector4 bc=new Vector4(pc.X,pc.Y,pc.Z,1f); Vector4 hc=Highlight(bc); pc=new Vector3(bc.X+(hc.X-bc.X)*pg, bc.Y+(hc.Y-bc.Y)*pg, bc.Z+(hc.Z-bc.Z)*pg); }
                    Vector3 sel = pi==0?SelectColorMoveXY:pi==1?SelectColorMoveYZ:SelectColorMoveXZ;
                    GL.Uniform3(colLoc, selectionMode?sel:pc);
                    Matrix4 pm;
                    if(pi==0) pm = Matrix4.CreateScale(scale*xs*hs, scale*ys*hs, scale*hs)*objRot*Matrix4.CreateTranslation(pivot);
                    else if(pi==1) pm = Matrix4.CreateScale(scale*hs, scale*ys*hs, scale*zs*hs)*objRot*Matrix4.CreateTranslation(pivot);
                    else pm = Matrix4.CreateScale(scale*xs*hs, scale*hs, scale*zs*hs)*objRot*Matrix4.CreateTranslation(pivot);
                    GL.UniformMatrix4(modelLoc,false,ref pm);
                    GL.DrawArrays(PrimitiveType.Triangles,pi*6,6);
                }
                GL.Uniform1(alphaLoc,1f);
            } else if(tool==EditorTool.Rotate){
                int ringVerts=CircleSegments*2;
                for(int ri=0; ri<3; ri++){
                    int part = ri==0?PartRotateX:ri==1?PartRotateY:PartRotateZ;
                    float hs = HoverScale(part);
                    float s = scale * hs;
                    Matrix4 rm = Matrix4.CreateScale(s)*objRot*Matrix4.CreateTranslation(pivot);
                    GL.BindVertexArray(advVaoRotate); GL.UniformMatrix4(modelLoc,false,ref rm);
                    Vector3 baseCol = axisColors[ri];
                    float g = partGlow[part];
                    Vector3 drawCol = baseCol;
                    if(g>0.001f){ Vector4 bc=new Vector4(baseCol.X,baseCol.Y,baseCol.Z,1f); Vector4 hc=Highlight(bc); drawCol=new Vector3(bc.X+(hc.X-bc.X)*g, bc.Y+(hc.Y-bc.Y)*g, bc.Z+(hc.Z-bc.Z)*g); GL.LineWidth(2.6f+g*1.4f); } else GL.LineWidth(selectionMode?10f:2.6f);
                    Vector3 sel = ri==0?SelectColorRotateX:ri==1?SelectColorRotateY:SelectColorRotateZ;
                    GL.Uniform3(colLoc, selectionMode?sel:drawCol);
                    GL.DrawArrays(PrimitiveType.Lines,ri*ringVerts,ringVerts);
                }
            }
            if(selectionMode) GL.LineWidth(1.5f);
            GL.Disable(EnableCap.Blend); GL.BindVertexArray(0); GL.Enable(EnableCap.CullFace); GL.Enable(EnableCap.DepthTest);
        }

        // Legacy render — kept for existing MainForm call (will now delegate to new)
        public static void Render(Matrix4 view, Matrix4 proj, Vector3 camFront, Vector3 camRight, Vector3 camUp)
        {
            if (!Enabled || !HasSelection()) return;
            // if advanced geometry is ready, use new path for visual parity
            if (advBuilt || true)
            {
                // need camera for scale — try to find from DataBase? fallback to legacy L()
                // Use legacy distance scale path if camera not available
                EnsureGizmoGlObjects();
                Vector3 pivot = GetPivot();
                float len = L(0,0,view,proj,pivot);
                float shaftW=len*0.022f, headLen=len*0.18f, headW=len*0.055f, sqHalf=len*0.085f;
                GL.Clear(ClearBufferMask.DepthBufferBit); GL.Disable(EnableCap.DepthTest); GL.Disable(EnableCap.CullFace); GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                GL.UseProgram(gizmoProgram);
                Matrix4 mvp=view*proj; GL.UniformMatrix4(uMvpLocation,true,ref mvp);
                allVerts.Clear();
                Matrix4 objRot = GetObjectRotation();
                // For legacy call we still show advanced visuals via legacy shader fallback — reuse advanced if tool==Move
                // Build legacy verts with advanced colors when possible
                for(int p=0;p<10;p++){
                    partVerts.Clear();
                    if(p==PartX) EmitArrow(partVerts, pivot, Vector3.TransformVector(Vector3.UnitX, objRot), len, shaftW, headLen, headW);
                    else if(p==PartY) EmitArrow(partVerts, pivot, Vector3.TransformVector(Vector3.UnitY, objRot), len, shaftW, headLen, headW);
                    else if(p==PartZ) EmitArrow(partVerts, pivot, Vector3.TransformVector(Vector3.UnitZ, objRot), len, shaftW, headLen, headW);
                    else if(p==PartXY && Globals.CurrentTool==EditorTool.Move) EmitQuad(partVerts, pivot + Vector3.TransformVector(new Vector3(0.16f,0.16f,0), objRot)*len, Vector3.TransformVector(Vector3.UnitX, objRot), Vector3.TransformVector(Vector3.UnitY, objRot), len*0.07f);
                    else if(p==PartXZ && Globals.CurrentTool==EditorTool.Move) EmitQuad(partVerts, pivot + Vector3.TransformVector(new Vector3(0.16f,0,0.16f), objRot)*len, Vector3.TransformVector(Vector3.UnitX, objRot), Vector3.TransformVector(Vector3.UnitZ, objRot), len*0.07f);
                    else if(p==PartYZ && Globals.CurrentTool==EditorTool.Move) EmitQuad(partVerts, pivot + Vector3.TransformVector(new Vector3(0,0.16f,0.16f), objRot)*len, Vector3.TransformVector(Vector3.UnitY, objRot), Vector3.TransformVector(Vector3.UnitZ, objRot), len*0.07f);
                    else if(p>=PartRotateX && Globals.CurrentTool==EditorTool.Rotate){
                        // draw torus segment approximation as lines
                        Vector3 axis = p==PartRotateX?Vector3.UnitX:p==PartRotateY?Vector3.UnitY:Vector3.UnitZ;
                        axis = Vector3.TransformVector(axis, objRot);
                        // Emit circle as quad strip approximating ring thickness via prism? Simplified as line loop via EmitQuad thin?
                        // Use EmitArrow guide fallback
                        EmitGuideLine(partVerts, pivot, axis, len);
                    } else if(p==PartCenterLegacy && Globals.CurrentTool==EditorTool.Move){
                        EmitQuad(partVerts, pivot, camRight.LengthSquared>1e-6f?camRight.Normalized():Vector3.UnitX, camUp.LengthSquared>1e-6f?camUp.Normalized():Vector3.UnitY, sqHalf);
                    } else continue;
                    if(dragging && dragPart==p) EmitGuideLine(partVerts, pivot, AxisDir(p), len);
                    rangeStart[p]=allVerts.Count/3; rangeCount[p]=partVerts.Count/3; allVerts.AddRange(partVerts);
                }
                GL.BindVertexArray(gizmoVao); GL.BindBuffer(BufferTarget.ArrayBuffer, gizmoVbo);
                if(uploadArr==null||uploadArr.Length<allVerts.Count) uploadArr=new float[Math.Max(8192, allVerts.Count*2)];
                allVerts.CopyTo(uploadArr); GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(allVerts.Count*sizeof(float)), uploadArr, BufferUsageHint.DynamicDraw);
                for(int p=0;p<10;p++){ if(rangeCount[p]<=0) continue; GL.Uniform4(uColorLocation, ColorForPart(p)); GL.DrawArrays(PrimitiveType.Triangles, rangeStart[p], rangeCount[p]); }
                GL.BindVertexArray(0); GL.BindBuffer(BufferTarget.ArrayBuffer,0); GL.UseProgram(0);
                GL.Enable(EnableCap.DepthTest); GL.Enable(EnableCap.CullFace);
                return;
            }
        }
    }
}
