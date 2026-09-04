using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using Re4QuadExtremeEditor.src;
using Re4QuadExtremeEditor.src.Class;
using Re4QuadExtremeEditor.src.Class.ObjMethods;
using Re4QuadExtremeEditor.src.Class.TreeNodeObj;
using Re4QuadExtremeEditor.src.Class.Enums;
using Re4QuadExtremeEditor.src.Class.Shaders;


namespace NewAgeTheRender
{
    /// <summary>
    /// Classe destinada a renderizar tudo no cenario (No ambiente GL);
    /// </summary>
    public static class TheRender
    {
        /// <summary>
        /// "Enter Camera View" state: renders the scene from the selected
        /// CAM keyframe eye/target with its FOV, like the Blender CAM addon preview.
        /// </summary>
        public static class CameraViewState
        {
            public static bool Enabled;
            public static ushort NodeId;
            public static bool ActiveThisFrame;
        }

        private static readonly Vector3 boundNoneEnemy = new Vector3(3f, 4f, 3f);
        private static readonly Vector3 boundNoneEtcModel = new Vector3(3f, 3f, 3f);
        private static readonly Vector3 boundNoneItem = new Vector3(1.5f, 1.5f, 1.5f);
        private static readonly Vector3 boundNoneExtras = new Vector3(2f, 2f, 2f);
        private static readonly Vector3 boundNoneQuadCustom = new Vector3(2f, 2f, 2f);
        private static readonly Vector3 boundNoneCAM = new Vector3(1.5f, 1.5f, 1.5f);
        private static readonly Vector3 boundNoneRTP = new Vector3(0.68f, 0.68f, 0.68f);

        private static float[] GenerateAuraCylinder(Vector3 basePos, float radius, float height, int segments, float rotAngle, bool spikyTop)
        {
            var tris = new List<float>(segments * 36);
            float y0 = basePos.Y;
            float y1 = basePos.Y + height;
            float spike = 0.55f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments * (float)Math.PI * 2f + rotAngle;
                float a1 = (float)(i + 1) / segments * (float)Math.PI * 2f + rotAngle;
                float x0 = (float)Math.Cos(a0) * radius, z0 = (float)Math.Sin(a0) * radius;
                float x1 = (float)Math.Cos(a1) * radius, z1 = (float)Math.Sin(a1) * radius;
                // spiky top: alternating height
                float yTop0 = y1 + (spikyTop && (i % 2 == 0) ? spike : 0f);
                float yTop1 = y1 + (spikyTop && ((i + 1) % 2 == 0) ? spike : 0f);
                Vector3 p00 = new Vector3(basePos.X + x0, y0, basePos.Z + z0);
                Vector3 p10 = new Vector3(basePos.X + x1, y0, basePos.Z + z1);
                Vector3 p01 = new Vector3(basePos.X + x0, yTop0, basePos.Z + z0);
                Vector3 p11 = new Vector3(basePos.X + x1, yTop1, basePos.Z + z1);
                // side quad as two tris with spiky top
                tris.Add(p00.X); tris.Add(p00.Y); tris.Add(p00.Z);
                tris.Add(p01.X); tris.Add(p01.Y); tris.Add(p01.Z);
                tris.Add(p11.X); tris.Add(p11.Y); tris.Add(p11.Z);
                tris.Add(p00.X); tris.Add(p00.Y); tris.Add(p00.Z);
                tris.Add(p11.X); tris.Add(p11.Y); tris.Add(p11.Z);
                tris.Add(p10.X); tris.Add(p10.Y); tris.Add(p10.Z);
                // spike triangle on top edge
                if (spikyTop)
                {
                    float am = (a0 + a1) * 0.5f;
                    float xm = (float)Math.Cos(am) * radius * 0.92f, zm = (float)Math.Sin(am) * radius * 0.92f;
                    float yTip = y1 + spike + 0.18f;
                    Vector3 tip = new Vector3(basePos.X + xm, yTip, basePos.Z + zm);
                    tris.Add(p01.X); tris.Add(p01.Y); tris.Add(p01.Z);
                    tris.Add(tip.X); tris.Add(tip.Y); tris.Add(tip.Z);
                    tris.Add(p11.X); tris.Add(p11.Y); tris.Add(p11.Z);
                }
            }
            // No top cap — open crown as requested
            return tris.ToArray();
        }
        private static float[] GenerateAuraCylinder(Vector3 basePos, float radius, float height, int segments) => GenerateAuraCylinder(basePos, radius, height, segments, 0f, true);
        private static float[] GenerateAuraDisk(Vector3 center, float radius, int segments)
        {
            var tris = new List<float>(segments * 9);
            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments * (float)Math.PI * 2f;
                float a1 = (float)(i + 1) / segments * (float)Math.PI * 2f;
                float x0 = (float)Math.Cos(a0) * radius, z0 = (float)Math.Sin(a0) * radius;
                float x1 = (float)Math.Cos(a1) * radius, z1 = (float)Math.Sin(a1) * radius;
                Vector3 p0 = new Vector3(center.X + x0, center.Y, center.Z + z0);
                Vector3 p1 = new Vector3(center.X + x1, center.Y, center.Z + z1);
                tris.Add(center.X); tris.Add(center.Y); tris.Add(center.Z);
                tris.Add(p0.X); tris.Add(p0.Y); tris.Add(p0.Z);
                tris.Add(p1.X); tris.Add(p1.Y); tris.Add(p1.Z);
            }
            return tris.ToArray();
        }

        // temporary diagnostics: how many CAM items were drawn on the last frame
        public static int DebugCamCamsDrawn = 0;
        public static int DebugCamZonesDrawn = 0;
        private static readonly Vector3 boundNoneESE = new Vector3(2f, 2f, 2f);
        private static readonly Vector3 boundNoneEMI = new Vector3(2f, 2f, 2f);
        private static readonly Vector3 boundNoneLIT = new Vector3(2f, 2f, 2f);
        private static readonly Vector3 boundNoneEFF = new Vector3(2f, 2f, 2f);
        private static readonly Vector3 boundNoneEFFTable9 = new Vector3(0.3f, 0.3f, 0.3f);

        // ---- view-frustum culling (Gribb-Hartmann) -----------------------
        // objects whose bounding sphere is fully outside the frustum are
        // skipped entirely; with hundreds of enemies this cuts most of the
        // per-frame draw cost while moving around the map.
        private static readonly Vector4[] frustumPlanes = new Vector4[6];

        private static void UpdateFrustum(Matrix4 viewProj)
        {
            // OpenTK builds matrices for the row-vector convention (v * M),
            // so clip.x..w come out as dots against the matrix COLUMNS.
            // The six frustum planes therefore combine Column3 with each
            // other column (transposed Gribb-Hartmann).
            frustumPlanes[0] = NormalizePlane(Column(viewProj, 3) + Column(viewProj, 0)); // left
            frustumPlanes[1] = NormalizePlane(Column(viewProj, 3) - Column(viewProj, 0)); // right
            frustumPlanes[2] = NormalizePlane(Column(viewProj, 3) + Column(viewProj, 1)); // bottom
            frustumPlanes[3] = NormalizePlane(Column(viewProj, 3) - Column(viewProj, 1)); // top
            frustumPlanes[4] = NormalizePlane(Column(viewProj, 3) + Column(viewProj, 2)); // near
            frustumPlanes[5] = NormalizePlane(Column(viewProj, 3) - Column(viewProj, 2)); // far
        }

        private static Vector4 Column(Matrix4 m, int index)
        {
            switch (index)
            {
                case 0: return new Vector4(m.M11, m.M21, m.M31, m.M41);
                case 1: return new Vector4(m.M12, m.M22, m.M32, m.M42);
                case 2: return new Vector4(m.M13, m.M23, m.M33, m.M43);
                default: return new Vector4(m.M14, m.M24, m.M34, m.M44);
            }
        }

        private static Vector4 NormalizePlane(Vector4 p)
        {
            float len = (float)Math.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
            if (len < 1e-8f) return p;
            return new Vector4(p.X / len, p.Y / len, p.Z / len, p.W / len);
        }

        /// <summary>
        /// True when the sphere is at least partially inside the frustum.
        /// </summary>
        public static bool IsSphereVisible(Vector3 center, float radius)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector4 pl = frustumPlanes[i];
                if (pl.X * center.X + pl.Y * center.Y + pl.Z * center.Z + pl.W < -radius)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Culling test for a transformed axis box: cheap sphere approximation.
        /// scaleSpan = half-extents already multiplied by object scale.
        /// </summary>
        public static bool IsBoxVisible(Vector3 position, Vector3 halfExtent)
        {
            return IsSphereVisible(position, halfExtent.Length);
        }



        public static void AllRender(ref Matrix4 camMtx, ref Matrix4 ProjMatrix, Vector3 camPos, float objY, bool IsSelectMode = false)
        {
            DebugCamCamsDrawn = 0;
            DebugCamZonesDrawn = 0;

            // the caller keeps these matrices between frames; since they come
            // by ref, restore the originals before returning so leaving the
            // camera view brings the editor FOV/orbit back untouched
            Matrix4 origCamMtx = camMtx;
            Matrix4 origProjMatrix = ProjMatrix;

            // "Enter Camera View": replace the editor orbit with the selected
            // camera's eye/target and its horizontal FOV (RE4 FOV is horizontal,
            // same conversion the Blender addon does via focal length)
            CameraViewState.ActiveThisFrame = false;
            if (!IsSelectMode && CameraViewState.Enabled && DataBase.NodeCAM != null && DataBase.FileCAM != null)
            {
                Vector3 eye; Vector3 tgt; float fovDeg;
                if (DataBase.FileCAM.TryGetCamViewData(CameraViewState.NodeId, out eye, out tgt, out fovDeg))
                {
                    float aspect = ProjMatrix.M22 / ProjMatrix.M11;
                    if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0.01f) { aspect = 16f / 9f; }

                    float fovX = MathHelper.DegreesToRadians(Math.Max(5f, Math.Min(170f, fovDeg)));
                    float fovY = 2f * (float)Math.Atan(Math.Tan(fovX / 2.0) / aspect);
                    if (float.IsNaN(fovY) || fovY <= 0.001f || fovY >= (float)Math.PI) { fovY = MathHelper.DegreesToRadians(60f); }

                    float nearP = 0.1f; float farP = 10000f;
                    float a = ProjMatrix.M33; float b = ProjMatrix.M43;
                    if (!float.IsNaN(a) && !float.IsNaN(b) && Math.Abs(a - 1f) > 0.0001f)
                    {
                        float n = b / (a - 1f);
                        float f2 = n * (a - 1f) / (a + 1f);
                        if (!float.IsNaN(n) && !float.IsInfinity(n) && n > 0f
                          && !float.IsNaN(f2) && !float.IsInfinity(f2) && f2 > n)
                        {
                            nearP = n; farP = f2;
                        }
                    }

                    Vector3 fwd = tgt - eye;
                    float flen = fwd.Length;
                    if (flen < 0.0001f) { fwd = new Vector3(0f, 0f, 1f); }
                    else { fwd = fwd / flen; }
                    Vector3 upv = new Vector3(0f, 1f, 0f);
                    if (Math.Abs(Vector3.Dot(fwd, upv)) > 0.999f) { upv = new Vector3(0f, 0f, 1f); }

                    camMtx = Matrix4.LookAt(eye, eye + fwd, upv);
                    ProjMatrix = Matrix4.CreatePerspectiveFieldOfView(fovY, aspect, nearP, farP);
                    camPos = eye;
                    CameraViewState.ActiveThisFrame = true;
                }
            }

            //refresh the culling frustum once per frame
            UpdateFrustum(camMtx * ProjMatrix);

            if (IsSelectMode)
            {
                GL.ClearColor(Color.White);
            }
            else 
            {
                GL.ClearColor(Globals.SkyColor);
            }
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            DataShader.ShaderBoundaryBox.Use();
            DataShader.ShaderBoundaryBox.SetMatrix4("view", camMtx);
            DataShader.ShaderBoundaryBox.SetMatrix4("projection", ProjMatrix);

            DataShader.ShaderBoundaryBoxPlus.Use();
            DataShader.ShaderBoundaryBoxPlus.SetMatrix4("view", camMtx);
            DataShader.ShaderBoundaryBoxPlus.SetMatrix4("projection", ProjMatrix);

            DataShader.ShaderTriggerZoneBox.Use();
            DataShader.ShaderTriggerZoneBox.SetMatrix4("view", camMtx);
            DataShader.ShaderTriggerZoneBox.SetMatrix4("projection", ProjMatrix);

            DataShader.ShaderTriggerZoneCircle.Use();
            DataShader.ShaderTriggerZoneCircle.SetMatrix4("view", camMtx);
            DataShader.ShaderTriggerZoneCircle.SetMatrix4("projection", ProjMatrix);

            DataShader.ShaderPlaneZone.Use();
            DataShader.ShaderPlaneZone.SetMatrix4("view", camMtx);
            DataShader.ShaderPlaneZone.SetMatrix4("projection", ProjMatrix);

            DataShader.ShaderLitPoint.Use();
            DataShader.ShaderLitPoint.SetMatrix4("view", camMtx);
            DataShader.ShaderLitPoint.SetMatrix4("projection", ProjMatrix);

            DataShader.ShaderItemTrigggerRadius.Use();
            DataShader.ShaderItemTrigggerRadius.SetMatrix4("view", camMtx);
            DataShader.ShaderItemTrigggerRadius.SetMatrix4("projection", ProjMatrix);

            CamZoneRender.SetupView(camMtx, ProjMatrix);

            if (IsSelectMode == false && Globals.CamGridEnable && Globals.CamGridvalue != 0) //render mode
            {
                DataShader.ShaderGrid.Use();
                DataShader.ShaderGrid.SetMatrix4("view", camMtx);
                DataShader.ShaderGrid.SetMatrix4("projection", ProjMatrix);
                Grid.RenderViewer(objY, Globals.CamGridvalue, Globals.GL_ColorGrid);
            }

            if (IsSelectMode == true && Globals.RenderRoom && DataBase.SelectedRoom != null) // select mode
            {
                DataShader.ShaderRoomSelectMode.Use();
                DataShader.ShaderRoomSelectMode.SetMatrix4("view", camMtx);
                DataShader.ShaderRoomSelectMode.SetMatrix4("projection", ProjMatrix);
                DataBase.SelectedRoom.Render_Solid();
            }

            //select mode box
            if (IsSelectMode)
            {
                RenderEnemyESL(RenderMode.SelectMode);
                RenderExtras(RenderMode.SelectMode);
                RenderFileESE(RenderMode.SelectMode);
                RenderFileEMI(RenderMode.SelectMode);
                RenderFileLIT(RenderMode.SelectMode);
                RenderFileEFF(RenderMode.SelectMode);
                RenderITA_TriggerZone(RenderMode.SelectMode);
                RenderAEV_TriggerZone(RenderMode.SelectMode);

                RenderFileFSE_TriggerZone(RenderMode.SelectMode);
                RenderFileEAR_TriggerZone(RenderMode.SelectMode);
                RenderFileSAR_TriggerZone(RenderMode.SelectMode);
                RenderFileQuadCustom_TriggerZone(RenderMode.SelectMode);

                if (!CameraViewState.ActiveThisFrame)
                {
                    RenderFileCAM_Zone_TriggerZone(RenderMode.SelectMode);
                    RenderFileCAM_Cameras(RenderMode.SelectMode);
                    try { RenderFileRTP_Nodes(RenderMode.SelectMode); }
                    catch (Exception ex) { Re4QuadExtremeEditor.src.DebugLog.Write("RTP Nodes(Select) ex: " + ex); }
                }

                RenderQuadCustomPoint(RenderMode.SelectMode);

                RenderITA_ItemObj(RenderMode.SelectMode);
                RenderAEV_ItemObj(RenderMode.SelectMode);
                RenderEtcModelETS(RenderMode.SelectMode);
            }
            else // box mode
            {
                RenderEnemyESL(RenderMode.BoxMode);
                RenderExtras(RenderMode.BoxMode);
                RenderFileESE(RenderMode.BoxMode);
                RenderFileEMI(RenderMode.BoxMode);
                RenderFileLIT(RenderMode.BoxMode);
                RenderFileEFF(RenderMode.BoxMode);
                RenderITA_TriggerZone(RenderMode.BoxMode);
                RenderAEV_TriggerZone(RenderMode.BoxMode);

                RenderFileFSE_TriggerZone(RenderMode.BoxMode);
                RenderFileEAR_TriggerZone(RenderMode.BoxMode);
                RenderFileSAR_TriggerZone(RenderMode.BoxMode);
                RenderFileQuadCustom_TriggerZone(RenderMode.BoxMode);

                if (!CameraViewState.ActiveThisFrame)
                {
                    RenderFileCAM_Zone_TriggerZone(RenderMode.BoxMode);
                    RenderFileCAM_Cameras(RenderMode.BoxMode);
                    try { RenderFileRTP_Nodes(RenderMode.BoxMode); }
                    catch (Exception ex) { Re4QuadExtremeEditor.src.DebugLog.Write("RTP Nodes(Box) ex: " + ex); }
                }

                RenderQuadCustomPoint(RenderMode.BoxMode);

                RenderITA_ItemObj(RenderMode.BoxMode);
                RenderAEV_ItemObj(RenderMode.BoxMode);
                RenderEtcModelETS(RenderMode.BoxMode);
            }

 
            if (IsSelectMode == false && Globals.RenderRoom && DataBase.SelectedRoom != null)
            {
                DataShader.ShaderRoom.Use();
                DataShader.ShaderRoom.SetMatrix4("view", camMtx);
                DataShader.ShaderRoom.SetMatrix4("projection", ProjMatrix);
                DataShader.ShaderRoom.SetVector3("CameraPosition", camPos);
                DataBase.SelectedRoom.Render();
            }

            if (IsSelectMode == false) // render model
            {
                DataShader.ShaderObjModel.Use();
                DataShader.ShaderObjModel.SetMatrix4("view", camMtx);
                DataShader.ShaderObjModel.SetMatrix4("projection", ProjMatrix);
                DataShader.ShaderObjModel.SetVector3("CameraPosition", camPos);

                DataShader.ShaderObjModelPlus.Use();
                DataShader.ShaderObjModelPlus.SetMatrix4("view", camMtx);
                DataShader.ShaderObjModelPlus.SetMatrix4("projection", ProjMatrix);
                DataShader.ShaderObjModelPlus.SetVector3("CameraPosition", camPos);

                ObjModel3D.PreRender();
                ObjModel3D.PreRenderStep2();
                ObjModel3D.PreRenderStep3();

                RenderQuadCustomPoint(RenderMode.ModelMode);
                RenderExtras(RenderMode.ModelMode);
                RenderFileESE(RenderMode.ModelMode);
                RenderFileEMI(RenderMode.ModelMode);
                RenderFileLIT(RenderMode.ModelMode);
                RenderFileEFF(RenderMode.ModelMode);
                RenderEnemyESL(RenderMode.ModelMode);
                RenderITA_ItemObj(RenderMode.ModelMode);
                RenderAEV_ItemObj(RenderMode.ModelMode);
                RenderEtcModelETS(RenderMode.ModelMode);
                if (!CameraViewState.ActiveThisFrame)
                {
                    RenderFileCAM_Cameras(RenderMode.ModelMode);
                }

                ObjModel3D.PosRender();

                //final, transparencia da triggerzone
                RenderPosTriggerZoneBox();
                // RTP always on top of transparent triggers but still depth-tested against walls - fixes squares/lines disappearing inside trigger zones
                if (!CameraViewState.ActiveThisFrame)
                {
                    try { RenderFileRTP_NodesOverlay(); }
                    catch (Exception ex) { Re4QuadExtremeEditor.src.DebugLog.Write("RTP NodesOverlay ex: " + ex); }
                    try { RenderFileRTP_LinksOverlay(); }
                    catch (Exception ex) { Re4QuadExtremeEditor.src.DebugLog.Write("RTP LinksOverlay ex: " + ex); }
                }
            }

            // while inside the camera view keep the aim line and the small
            // target marker visible, so it is clear where the camera looks
            if (CameraViewState.ActiveThisFrame)
            {
                DrawCamViewAimOverlay();
            }

            // hand the untouched editor matrices back to the caller
            camMtx = origCamMtx;
            ProjMatrix = origProjMatrix;

            GL.Finish();
        }

        private static void DrawCamViewAimOverlay()
        {
            Vector3 eye; Vector3 tgt; float fovDeg;
            if (!DataBase.FileCAM.TryGetCamViewData(CameraViewState.NodeId, out eye, out tgt, out fovDeg))
            {
                return;
            }
            Vector4 c = new Vector4(1f, 0.85f, 0.1f, 1f);
            CamZoneRender.DrawLines(new float[]
            {
                eye.X, eye.Y, eye.Z,
                tgt.X, tgt.Y, tgt.Z,
            }, c);

            float s = 0.6f;
            float x0 = tgt.X - s; float x1 = tgt.X + s;
            float y0 = tgt.Y - s; float y1 = tgt.Y + s;
            float z0 = tgt.Z - s; float z1 = tgt.Z + s;
            List<float> box = new List<float>(24 * 3);
            Action<Vector3, Vector3> seg = delegate (Vector3 p, Vector3 q)
            {
                box.Add(p.X); box.Add(p.Y); box.Add(p.Z);
                box.Add(q.X); box.Add(q.Y); box.Add(q.Z);
            };
            Vector3 p000 = new Vector3(x0, y0, z0); Vector3 p100 = new Vector3(x1, y0, z0);
            Vector3 p010 = new Vector3(x0, y1, z0); Vector3 p110 = new Vector3(x1, y1, z0);
            Vector3 p001 = new Vector3(x0, y0, z1); Vector3 p101 = new Vector3(x1, y0, z1);
            Vector3 p011 = new Vector3(x0, y1, z1); Vector3 p111 = new Vector3(x1, y1, z1);
            seg(p000, p100); seg(p010, p110); seg(p001, p101); seg(p011, p111);
            seg(p000, p010); seg(p100, p110); seg(p001, p011); seg(p101, p111);
            seg(p000, p001); seg(p100, p101); seg(p010, p011); seg(p110, p111);
            CamZoneRender.DrawLines(box.ToArray(), c);
        }

        private static void RenderEnemyESL(RenderMode mode)
        {
            if (Globals.RenderEnemyESL)
            {
                foreach (TreeNode item in DataBase.NodeESL.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    ushort ID = ((Object3D)item).ObjLineRef;
                    ushort EnemiesID = DataBase.NodeESL.MethodsForGL.GetEnemyModelID(ID);
                    ushort EnemyRoom = DataBase.NodeESL.MethodsForGL.GetEnemyRoom(ID);
                    byte EnableState = DataBase.NodeESL.MethodsForGL.GetEnableState(ID);

                    Vector4 useColor = new Vector4((ID & 0xFF) / 255f, ((ID >> 8) & 0xFF) / 255f, (byte)GroupType.ESL / 255f, 1f);

                    if ((Globals.RenderDisabledEnemy || EnableState != 0)
                      && (Globals.RenderDontShowOnlyDefinedRoom || EnemyRoom == Globals.RenderEnemyFromDefinedRoom))
                    {
                        if (mode == RenderMode.BoxMode)
                        {
                            useColor = Globals.GL_ColorESL;
                            if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                            {
                                useColor = Globals.GL_ColorSelected;
                            }
                            else if (DataBase.Extras.IsEnemyLinkedWithIta(ID))
                            {
                                // Enemy tied to an ITA entry: paint its hitbox
                                // with the same blue as the ITA item boxes.
                                useColor = Globals.GL_ColorITA;
                            }
                        }

                        RspFix rspFix = new RspFix(
                        Vector3.One, //scale
                        DataBase.NodeESL.MethodsForGL.GetPosition(ID),
                        DataBase.NodeESL.MethodsForGL.GetRotation(ID));

                        if (!DataBase.EnemiesIDs.List.ContainsKey(EnemiesID))
                        {
                            string eId = EnemiesID.ToString("X4");
                            eId = eId[0].ToString() + eId[1].ToString() + "FF";
                            EnemiesID = ushort.Parse(eId, System.Globalization.NumberStyles.HexNumber);
                        }

                        bool hasEnemyModel = DataBase.EnemiesIDs.List.ContainsKey(EnemiesID)
                            && DataBase.EnemiesModels.ContainsKey(DataBase.EnemiesIDs.List[EnemiesID].ObjectModel);

                        //frustum culling: skip enemies fully outside the view.
                        //never applied in select mode so pixel-perfect picking stays exact.
                        if (mode != RenderMode.SelectMode)
                        {
                            Vector3 halfExtent;
                            if (hasEnemyModel)
                            {
                                var bl = DataBase.EnemiesModels.GetBoundingBoxLimit(DataBase.EnemiesIDs.List[EnemiesID].ObjectModel);
                                halfExtent = new Vector3(
                                    Math.Max(Math.Abs(bl.UpperBoundary.X), Math.Abs(bl.LowerBoundary.X)),
                                    Math.Max(Math.Abs(bl.UpperBoundary.Y), Math.Abs(bl.LowerBoundary.Y)),
                                    Math.Max(Math.Abs(bl.UpperBoundary.Z), Math.Abs(bl.LowerBoundary.Z)));
                            }
                            else
                            {
                                halfExtent = boundNoneEnemy + new Vector3(4f, 4f, 4f);
                            }
                            if (!IsBoxVisible(rspFix.Position, halfExtent))
                            {
                                continue;
                            }
                        }

                        if (hasEnemyModel)
                        {
                            if (mode == RenderMode.ModelMode)
                            {
                                DataBase.EnemiesModels.RenderModel(DataBase.EnemiesIDs.List[EnemiesID].ObjectModel, rspFix);
                            }
                            else if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.BoundingBoxViewer(DataBase.EnemiesModels.GetBoundingBoxLimit(DataBase.EnemiesIDs.List[EnemiesID].ObjectModel), rspFix, useColor);
                            }
                            else if (mode == RenderMode.SelectMode)
                            {
                                RenderAppModel.BoundingBoxToSelect(DataBase.EnemiesModels.GetBoundingBoxLimit(DataBase.EnemiesIDs.List[EnemiesID].ObjectModel), rspFix, useColor);
                            }
                        }
                        else
                        {
                            if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.NoneBoundingBoxViewer(boundNoneEnemy, -boundNoneEnemy, rspFix, useColor);
                            }
                            else if (mode == RenderMode.SelectMode)
                            {
                                RenderAppModel.NoneBoundingBoxToSelect(boundNoneEnemy, -boundNoneEnemy, rspFix, useColor);
                            }

                        }
                    }

                }
            }
        }

        private static void RenderEtcModelETS(RenderMode mode)
        {
            if (Globals.RenderEtcmodelETS)
            {
                foreach (TreeNode item in DataBase.NodeETS.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    ushort ID = ((Object3D)item).ObjLineRef;
                    ushort EtcModelID = DataBase.NodeETS.MethodsForGL.GetEtcModelID(ID);

                    Vector4 useColor = new Vector4((ID & 0xFF) / 255f, ((ID >> 8) & 0xFF) / 255f, (byte)GroupType.ETS / 255f, 1f);

                    if (mode == RenderMode.BoxMode)
                    {
                        useColor = Globals.GL_ColorETS;
                        if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                        {
                            useColor = Globals.GL_ColorSelected;
                        }
                    }

                    RspFix rspFix = new RspFix(
                        DataBase.NodeETS.MethodsForGL.GetScale(ID),
                        DataBase.NodeETS.MethodsForGL.GetPosition(ID),
                        DataBase.NodeETS.MethodsForGL.GetAngle(ID));

                    bool hasEtcModel = DataBase.EtcModelIDs.List.ContainsKey(EtcModelID)
                        && DataBase.EtcModels.ContainsKey(DataBase.EtcModelIDs.List[EtcModelID].ObjectModel);

                    //frustum culling: skip objects fully outside the view.
                    //never applied in select mode so pixel-perfect picking stays exact.
                    if (mode != RenderMode.SelectMode)
                    {
                        Vector3 halfExtent;
                        if (hasEtcModel)
                        {
                            var bl = DataBase.EtcModels.GetBoundingBoxLimit(DataBase.EtcModelIDs.List[EtcModelID].ObjectModel);
                            halfExtent = new Vector3(
                                Math.Max(Math.Abs(bl.UpperBoundary.X), Math.Abs(bl.LowerBoundary.X)),
                                Math.Max(Math.Abs(bl.UpperBoundary.Y), Math.Abs(bl.LowerBoundary.Y)),
                                Math.Max(Math.Abs(bl.UpperBoundary.Z), Math.Abs(bl.LowerBoundary.Z)));
                        }
                        else
                        {
                            halfExtent = boundNoneEtcModel + new Vector3(4f, 4f, 4f);
                        }
                        halfExtent *= new Vector3(
                            Math.Max(Math.Abs(rspFix.Scale.X), 1e-3f),
                            Math.Max(Math.Abs(rspFix.Scale.Y), 1e-3f),
                            Math.Max(Math.Abs(rspFix.Scale.Z), 1e-3f));
                        if (!IsBoxVisible(rspFix.Position, halfExtent))
                        {
                            continue;
                        }
                    }

                    if (hasEtcModel)
                    {
                        if (mode == RenderMode.ModelMode)
                        {
                            DataBase.EtcModels.RenderModel(DataBase.EtcModelIDs.List[EtcModelID].ObjectModel, rspFix);
                        }
                        else if (mode == RenderMode.BoxMode)
                        {
                            RenderAppModel.BoundingBoxViewer(DataBase.EtcModels.GetBoundingBoxLimit(DataBase.EtcModelIDs.List[EtcModelID].ObjectModel), rspFix, useColor);
                        }
                        else if (mode == RenderMode.SelectMode)
                        {
                            RenderAppModel.BoundingBoxToSelect(DataBase.EtcModels.GetBoundingBoxLimit(DataBase.EtcModelIDs.List[EtcModelID].ObjectModel), rspFix, useColor);
                        }

                    }
                    else
                    {
                        if (mode == RenderMode.BoxMode)
                        {
                            RenderAppModel.NoneBoundingBoxViewer(boundNoneEtcModel, -boundNoneEtcModel, rspFix, useColor);
                        }
                        else if (mode == RenderMode.SelectMode)
                        {
                            RenderAppModel.NoneBoundingBoxToSelect(boundNoneEtcModel, -boundNoneEtcModel, rspFix, useColor);
                        }

                    }

                }
            }
        }

        private static void RenderFileESE(RenderMode mode) 
        {
            if (Globals.RenderFileESE)
            {
                foreach (TreeNode item in DataBase.NodeESE.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    ushort ID = ((Object3D)item).ObjLineRef;
                    
                    byte[] partColor = BitConverter.GetBytes(ID);
                    Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.ESE / 255f, 1f);

                    if (mode == RenderMode.BoxMode)
                    {
                        useColor = Globals.GL_ColorESE;
                        if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                        {
                            useColor = Globals.GL_ColorSelected;
                        }
                    }

                    RspFix rspFix = new RspFix(
                        Vector3.One,
                        DataBase.NodeESE.MethodsForGL.GetPosition(ID),
                        Matrix4.Identity);

                    if (DataBase.InternalModels.ContainsKey(Consts.ModelKey_ESE_Point))
                    {
                        if (mode == RenderMode.ModelMode)
                        {
                            DataBase.InternalModels.RenderModel(Consts.ModelKey_ESE_Point, rspFix);
                        }
                        else if (mode == RenderMode.BoxMode)
                        {
                            RenderAppModel.BoundingBoxViewer(DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKey_ESE_Point), rspFix, useColor);
                        }
                        else if (mode == RenderMode.SelectMode)
                        {
                            RenderAppModel.BoundingBoxToSelect(DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKey_ESE_Point), rspFix, useColor);
                        }

                    }
                    else
                    {
                        if (mode == RenderMode.BoxMode)
                        {
                            RenderAppModel.NoneBoundingBoxViewer(boundNoneESE, -boundNoneESE, rspFix, useColor);
                        }
                        else if (mode == RenderMode.SelectMode)
                        {
                            RenderAppModel.NoneBoundingBoxToSelect(boundNoneESE, -boundNoneESE, rspFix, useColor);
                        }

                    }

                }
            }
        }

        private static void RenderFileEMI(RenderMode mode) 
        {
            if (Globals.RenderFileEMI)
            {
                foreach (TreeNode item in DataBase.NodeEMI.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    ushort ID = ((Object3D)item).ObjLineRef;

                    byte[] partColor = BitConverter.GetBytes(ID);
                    Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.EMI / 255f, 1f);

                    if (mode == RenderMode.BoxMode)
                    {
                        useColor = Globals.GL_ColorEMI;
                        if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                        {
                            useColor = Globals.GL_ColorSelected;
                        }
                    }

                    RspFix rspFix = new RspFix(
                        Vector3.One,
                        DataBase.NodeEMI.MethodsForGL.GetPosition(ID),
                        DataBase.NodeEMI.MethodsForGL.GetAngle(ID));

                    if (DataBase.InternalModels.ContainsKey(Consts.ModelKey_EMI_Point))
                    {
                        if (mode == RenderMode.ModelMode)
                        {
                            DataBase.InternalModels.RenderModel(Consts.ModelKey_EMI_Point, rspFix);
                        }
                        else if (mode == RenderMode.BoxMode)
                        {
                            RenderAppModel.BoundingBoxViewer(DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKey_EMI_Point), rspFix, useColor);
                        }
                        else if (mode == RenderMode.SelectMode)
                        {
                            RenderAppModel.BoundingBoxToSelect(DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKey_EMI_Point), rspFix, useColor);
                        }

                    }
                    else
                    {
                        if (mode == RenderMode.BoxMode)
                        {
                            RenderAppModel.NoneBoundingBoxViewer(boundNoneEMI, -boundNoneEMI, rspFix, useColor);
                        }
                        else if (mode == RenderMode.SelectMode)
                        {
                            RenderAppModel.NoneBoundingBoxToSelect(boundNoneEMI, -boundNoneEMI, rspFix, useColor);
                        }

                    }

                }
            }
        }

        private static void RenderFileLIT(RenderMode mode)
        {
            if (Globals.RenderFileLIT)
            {
                foreach (TreeNode item in DataBase.NodeLIT_Entrys.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    ushort ID = ((Object3D)item).ObjLineRef;
                    ushort GroupID = DataBase.NodeLIT_Entrys.MethodsForGL.GetGroupOrderID(ID);

                    if (Globals.LIT_ShowOnlySelectedGroup == false || (Globals.LIT_ShowOnlySelectedGroup && Globals.LIT_SelectedGroup == GroupID))
                    {
                        byte[] partColor = BitConverter.GetBytes(ID);
                        Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.LIT_ENTRYS / 255f, 1f);

                        if (mode == RenderMode.BoxMode)
                        {
                            useColor = Globals.GL_ColorLIT;
                            if (Globals.LIT_EnableLightColor)
                            {
                                useColor = DataBase.NodeLIT_Entrys.MethodsForGL.GetLightColor(ID);
                            }
                            if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                            {
                                useColor = Globals.GL_ColorSelected;
                            }
                        }

                        RspFix rspFix = new RspFix(
                            Vector3.One,
                            DataBase.NodeLIT_Entrys.MethodsForGL.GetPosition(ID),
                            Matrix4.Identity);

                        if (Globals.LIT_EnableLightColor)
                        {
                            if (mode == RenderMode.ModelMode)
                            {
                                RenderAppModel.RenderLitPointBorder(DataBase.NodeLIT_Entrys.MethodsForGL.GetPosition(ID), Globals.GL_ColorLIT);
                                RenderAppModel.RenderLitPointColor(DataBase.NodeLIT_Entrys.MethodsForGL.GetPosition(ID), DataBase.NodeLIT_Entrys.MethodsForGL.GetLightColor(ID));
                            }
                        }
                        if (DataBase.InternalModels.ContainsKey(Consts.ModelKey_LIT_Point))
                        {
                            if (mode == RenderMode.ModelMode && Globals.LIT_EnableLightColor == false)
                            {
                                DataBase.InternalModels.RenderModel(Consts.ModelKey_LIT_Point, rspFix);
                            }
                            else if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.BoundingBoxViewer(DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKey_LIT_Point), rspFix, useColor);
                            }
                            else if (mode == RenderMode.SelectMode)
                            {
                                RenderAppModel.BoundingBoxToSelect(DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKey_LIT_Point), rspFix, useColor);
                            }

                        }
                        else
                        {
                            if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.NoneBoundingBoxViewer(boundNoneLIT, -boundNoneLIT, rspFix, useColor);
                            }
                            else if (mode == RenderMode.SelectMode)
                            {
                                RenderAppModel.NoneBoundingBoxToSelect(boundNoneLIT, -boundNoneLIT, rspFix, useColor);
                            }

                        }

                        if (mode == RenderMode.BoxMode)
                        {
                            //RenderRangeRadius
                            float RangeRadius = DataBase.NodeLIT_Entrys.MethodsForGL.GetRangeRadius(ID);
                            if (Globals.RenderItemTriggerRadius && RangeRadius != 0)
                            {
                                RenderAppModel.ItemTrigggerRadiusViewer(new Vector4(DataBase.NodeLIT_Entrys.MethodsForGL.GetPosition(ID), RangeRadius), useColor);
                            }

                        }
                    }
                }
            }
        }

        private static void RenderFileEFF(RenderMode mode) 
        {
            if (Globals.RenderFileEFFBLOB)
            {
                //(GroupID, TableID), GroupInternalID 
                Dictionary<(ushort GroupID, EffectEntryTableID TableID), ushort> Association = new Dictionary<(ushort GroupID,EffectEntryTableID TableID), ushort>();

                if (Globals.EFF_RenderTable7)
                {
                    foreach (var item in DataBase.NodeEFF_Table7_Effect_0.Nodes)
                    {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked((System.Windows.Forms.TreeNode)item)) continue;
                        ushort ID = ((Object3D)item).ObjLineRef;

                        ushort EntryOrderID = DataBase.NodeEFF_Table7_Effect_0.PropertyMethods.GetEntryOrderID(ID);
                        Association.Add((EntryOrderID, EffectEntryTableID.Table7), ID);

                        if (Globals.EFF_ShowOnlySelectedGroup == false || (Globals.EFF_ShowOnlySelectedGroup && Globals.EFF_SelectedGroup == EntryOrderID))
                        {
                            byte[] partColor = BitConverter.GetBytes(ID);
                            Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.EFF_Table7_Effect_0 / 255f, 1f);

                            if (mode == RenderMode.BoxMode)
                            {
                                useColor = Globals.GL_ColorEFF_Table7;
                                if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                                {
                                    useColor = Globals.GL_ColorSelected;
                                }
                            }

                            RspFix rspFix = new RspFix(
                                  Vector3.One,
                                  DataBase.NodeEFF_Table7_Effect_0.MethodsForGL.GetPosition(ID),
                                  DataBase.NodeEFF_Table7_Effect_0.MethodsForGL.GetAngle(ID));

                            if (DataBase.InternalModels.ContainsKey(Consts.ModelKey_EFF_GroupPoint))
                            {
                                if (mode == RenderMode.ModelMode)
                                {
                                    DataBase.InternalModels.RenderModel(Consts.ModelKey_EFF_GroupPoint, rspFix);
                                }
                                else if (mode == RenderMode.BoxMode)
                                {
                                    RenderAppModel.BoundingBoxViewer(DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKey_EFF_GroupPoint), rspFix, useColor);
                                }
                                else if (mode == RenderMode.SelectMode)
                                {
                                    RenderAppModel.BoundingBoxToSelect(DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKey_EFF_GroupPoint), rspFix, useColor);
                                }

                            }
                            else
                            {
                                if (mode == RenderMode.BoxMode)
                                {
                                    RenderAppModel.NoneBoundingBoxViewer(boundNoneEFF, -boundNoneEFF, rspFix, useColor);
                                }
                                else if (mode == RenderMode.SelectMode)
                                {
                                    RenderAppModel.NoneBoundingBoxToSelect(boundNoneEFF, -boundNoneEFF, rspFix, useColor);
                                }

                            }
                        }
 
                    }
                }

                if (Globals.EFF_RenderTable8)
                {
                    foreach (var item in DataBase.NodeEFF_Table8_Effect_1.Nodes)
                    {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked((System.Windows.Forms.TreeNode)item)) continue;
                        ushort ID = ((Object3D)item).ObjLineRef;

                        ushort EntryOrderID = DataBase.NodeEFF_Table8_Effect_1.PropertyMethods.GetEntryOrderID(ID);
                        Association.Add((EntryOrderID, EffectEntryTableID.Table8), ID);

                        if (Globals.EFF_ShowOnlySelectedGroup == false || (Globals.EFF_ShowOnlySelectedGroup && Globals.EFF_SelectedGroup == EntryOrderID))
                        {
                            byte[] partColor = BitConverter.GetBytes(ID);
                            Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.EFF_Table8_Effect_1 / 255f, 1f);

                            if (mode == RenderMode.BoxMode)
                            {
                                useColor = Globals.GL_ColorEFF_Table8;
                                if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                                {
                                    useColor = Globals.GL_ColorSelected;
                                }
                            }

                            RspFix rspFix = new RspFix(
                                  Vector3.One,
                                  DataBase.NodeEFF_Table8_Effect_1.MethodsForGL.GetPosition(ID),
                                  DataBase.NodeEFF_Table8_Effect_1.MethodsForGL.GetAngle(ID));

                            if (DataBase.InternalModels.ContainsKey(Consts.ModelKey_EFF_GroupPoint))
                            {
                                if (mode == RenderMode.ModelMode)
                                {
                                    DataBase.InternalModels.RenderModel(Consts.ModelKey_EFF_GroupPoint, rspFix);
                                }
                                else if (mode == RenderMode.BoxMode)
                                {
                                    RenderAppModel.BoundingBoxViewer(DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKey_EFF_GroupPoint), rspFix, useColor);
                                }
                                else if (mode == RenderMode.SelectMode)
                                {
                                    RenderAppModel.BoundingBoxToSelect(DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKey_EFF_GroupPoint), rspFix, useColor);
                                }

                            }
                            else
                            {
                                if (mode == RenderMode.BoxMode)
                                {
                                    RenderAppModel.NoneBoundingBoxViewer(boundNoneEFF, -boundNoneEFF, rspFix, useColor);
                                }
                                else if (mode == RenderMode.SelectMode)
                                {
                                    RenderAppModel.NoneBoundingBoxToSelect(boundNoneEFF, -boundNoneEFF, rspFix, useColor);
                                }

                            }
                        }

                    }
                }

                if (Globals.EFF_RenderTable9)
                {
                    foreach (var item in DataBase.NodeEFF_Table9.Nodes)
                    {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked((System.Windows.Forms.TreeNode)item)) continue;
                        ushort ID = ((Object3D)item).ObjLineRef;
                        ushort GroupOrderID = DataBase.NodeEFF_Table9.MethodsForGL.GetGroupOrderID(ID);

                        if (Globals.EFF_ShowOnlySelectedGroup == false || (Globals.EFF_ShowOnlySelectedGroup && Globals.EFF_SelectedGroup == GroupOrderID))
                        {
                            byte[] partColor = BitConverter.GetBytes(ID);
                            Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.EFF_Table9 / 255f, 1f);

                            if (mode == RenderMode.BoxMode)
                            {
                                useColor = Globals.GL_ColorEFF_Table9;
                                if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                                {
                                    useColor = Globals.GL_ColorSelected;
                                }
                            }

                            RspFix rspFix = new RspFix(
                                  Vector3.One,
                                  DataBase.NodeEFF_Table9.MethodsForGL.GetPosition(ID),
                                  Matrix4.Identity);

                            if (DataBase.InternalModels.ContainsKey(Consts.ModelKey_EFF_Table9) && mode == RenderMode.ModelMode)
                            {
                                DataBase.InternalModels.RenderModel(Consts.ModelKey_EFF_Table9, rspFix);
                            }
                            else
                            {
                                if (mode == RenderMode.BoxMode)
                                {
                                    RenderAppModel.NoneBoundingBoxViewer(boundNoneEFFTable9, -boundNoneEFFTable9, rspFix, useColor);
                                }
                                else if (mode == RenderMode.SelectMode)
                                {
                                    RenderAppModel.NoneBoundingBoxToSelect(boundNoneEFFTable9, -boundNoneEFFTable9, rspFix, useColor);
                                }

                            }
                        }  
                    }
                }

                if (Globals.EFF_RenderTable7 || Globals.EFF_RenderTable8) // NodeEFF_EffectEntry so tem entry dessas duas tabelas
                {
                    foreach (TreeNode item in DataBase.NodeEFF_EffectEntry.Nodes)
                    {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                        ushort ID = ((Object3D)item).ObjLineRef;
                        ushort GroupOrderID = DataBase.NodeEFF_EffectEntry.MethodsForGL.GetGroupOrderID(ID);
                        var TableID = DataBase.NodeEFF_EffectEntry.MethodsForGL.GetTableID(ID);

                        bool RenderIsTable7 = Globals.EFF_RenderTable7 && TableID == EffectEntryTableID.Table7;
                        bool RenderIsTable8 = Globals.EFF_RenderTable8 && TableID == EffectEntryTableID.Table8;
                        bool RenderIsOnlySelected = Globals.EFF_ShowOnlySelectedGroup == false || (Globals.EFF_ShowOnlySelectedGroup && Globals.EFF_SelectedGroup == GroupOrderID);

                        if ((RenderIsTable7 || RenderIsTable8) && RenderIsOnlySelected)
                        {
                            byte[] partColor = BitConverter.GetBytes(ID);
                            Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.EFF_EffectEntry / 255f, 1f);

                            if (mode == RenderMode.BoxMode)
                            {
                                if (TableID == EffectEntryTableID.Table7)
                                {
                                    useColor = Globals.GL_ColorEFF_Table7;
                                }
                                else if (TableID == EffectEntryTableID.Table8)
                                {
                                    useColor = Globals.GL_ColorEFF_Table8;
                                }
                                else
                                {
                                    useColor = Globals.GL_ColorEFF_EffectEntry;
                                }

                                if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                                {
                                    useColor = Globals.GL_ColorSelected;
                                }
                            }


                            RspFix rspFix2 = new RspFix(Vector3.One, Vector3.Zero, Matrix4.Identity);
                            if (Association.ContainsKey((GroupOrderID, TableID)) && Globals.EFF_Use_Group_Position)
                            {
                                ushort GroupInternalID = Association[(GroupOrderID, TableID)];
                                if (TableID == EffectEntryTableID.Table7)
                                {
                                    rspFix2 = new RspFix(
                                    Vector3.One,
                                    DataBase.NodeEFF_Table7_Effect_0.MethodsForGL.GetPosition(GroupInternalID),
                                    DataBase.NodeEFF_Table7_Effect_0.MethodsForGL.GetAngle(GroupInternalID));
                                }
                                else if (TableID == EffectEntryTableID.Table8)
                                {
                                    rspFix2 = new RspFix(
                                    Vector3.One,
                                    DataBase.NodeEFF_Table8_Effect_1.MethodsForGL.GetPosition(GroupInternalID),
                                    DataBase.NodeEFF_Table8_Effect_1.MethodsForGL.GetAngle(GroupInternalID));
                                }
                            }

                            RspFix rspFix = new RspFix(
                                   Vector3.One,
                                   DataBase.NodeEFF_EffectEntry.MethodsForGL.GetPosition(ID),
                                   DataBase.NodeEFF_EffectEntry.MethodsForGL.GetAngle(ID));

                            if (DataBase.InternalModels.ContainsKey(Consts.ModelKey_EFF_EntryPoint))
                            {
                                if (mode == RenderMode.ModelMode)
                                {
                                    DataBase.InternalModels.RenderModel(Consts.ModelKey_EFF_EntryPoint, rspFix, rspFix2);
                                }
                                else if (mode == RenderMode.BoxMode)
                                {
                                    RenderAppModel.BoundingBoxViewer(DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKey_EFF_EntryPoint), rspFix, rspFix2, useColor);
                                }
                                else if (mode == RenderMode.SelectMode)
                                {
                                    RenderAppModel.BoundingBoxToSelect(DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKey_EFF_EntryPoint), rspFix, rspFix2, useColor);
                                }

                            }
                            else
                            {
                                if (mode == RenderMode.BoxMode)
                                {
                                    RenderAppModel.NoneBoundingBoxViewer(boundNoneEFF, -boundNoneEFF, rspFix, rspFix2, useColor);
                                }
                                else if (mode == RenderMode.SelectMode)
                                {
                                    RenderAppModel.NoneBoundingBoxToSelect(boundNoneEFF, -boundNoneEFF, rspFix, rspFix2, useColor);
                                }

                            }
                        }

                    }
                }
               
            }
        }


        private static void RenderITA_TriggerZone(RenderMode mode)
        {
            if (Globals.RenderItemsITA)
            {
                foreach (TreeNode item in DataBase.NodeITA.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    RenderSpecial_TriggerZone((Object3D)item, DataBase.NodeITA.MethodsForGL, mode);
                }
            }
        }

        private static void RenderAEV_TriggerZone(RenderMode mode)
        {
            if (Globals.RenderEventsAEV)
            {
                foreach (TreeNode item in DataBase.NodeAEV.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    RenderSpecial_TriggerZone((Object3D)item, DataBase.NodeAEV.MethodsForGL, mode);
                }
            }   
        }

        private static void RenderSpecial_TriggerZone(Object3D item, SpecialMethodsForGL MethodsForGL, RenderMode mode)
        {
            ushort ID = item.ObjLineRef;
            GroupType Group = item.Group;

            if (MethodsForGL.GetSpecialType(ID) == SpecialType.T03_Items)
            {
                if (Globals.RenderItemTriggerZone)
                {
                    Vector4 TriggerZoneColor = Globals.GL_ColorItemTriggerZone;

                    if (mode == RenderMode.BoxMode && DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                    {
                        TriggerZoneColor = Globals.GL_ColorItemTriggerZoneSelected;
                    }

                    Render_Any_TriggerZone(ID, Group, MethodsForGL, mode, TriggerZoneColor);
                }
            }
            else
            {
                if (Globals.RenderSpecialTriggerZone)
                {
                    Vector4 mColor = new Vector4(0f, 0f, 0f, 1f);

                    if (mode == RenderMode.BoxMode)
                    {
                        if (Group == GroupType.ITA)
                        {
                            mColor = Globals.GL_ColorITA;
                        }
                        else if (Group == GroupType.AEV)
                        {
                            mColor = Globals.GL_ColorAEV;
                        }

                        if (Globals.UseMoreSpecialColors)
                        {
                            mColor = ReturnMoreSpecialColor(MethodsForGL.GetSpecialType(ID), mColor);
                        }
                    }

                    if (mode == RenderMode.BoxMode && DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                    {
                        mColor = Globals.GL_ColorSelected;
                    }

                    Render_Any_TriggerZone(ID, Group, MethodsForGL, mode, mColor);
                }
            }
        }

        private static void RenderITA_ItemObj(RenderMode mode)
        {
            if (Globals.RenderItemsITA)
            {
                foreach (TreeNode item in DataBase.NodeITA.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    RenderSpecial_ItemObj((Object3D)item, DataBase.NodeITA.MethodsForGL, mode);
                }
            }         
        }

        private static void RenderAEV_ItemObj(RenderMode mode)
        {
            if (Globals.RenderEventsAEV)
            {
                foreach (TreeNode item in DataBase.NodeAEV.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    RenderSpecial_ItemObj((Object3D)item, DataBase.NodeAEV.MethodsForGL, mode);
                }
            }
        }

        private static void RenderSpecial_ItemObj(Object3D item, SpecialMethodsForGL MethodsForGL, RenderMode mode)
        {
            ushort ID = item.ObjLineRef;
            GroupType Group = item.Group;

            byte[] partColor = BitConverter.GetBytes(ID);

            Vector4 mColor = new Vector4(0, 0, 0, 1f);
            Vector4 useColor = new Vector4(0, 0, 0, 1f);

            if (Group == GroupType.ITA)
            {
                useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.ITA / 255f, 1f);
                mColor = Globals.GL_ColorITA;
            }
            else if (Group == GroupType.AEV)
            {
                useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.AEV / 255f, 1f);
                mColor = Globals.GL_ColorAEV;
            }

            if (MethodsForGL.GetSpecialType(ID) == SpecialType.T03_Items)
            {
                // do objeto item
                if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                {
                    mColor = Globals.GL_ColorSelected;
                }

                RspFix rspFix = new RspFix(Vector3.One, MethodsForGL.GetItemPosition(ID), MethodsForGL.GetItemRotation(ID));

                ushort item_ID = MethodsForGL.GetItemModelID(ID);

                // Snake ITA (ItemNumber 0x1000) rendered as its ESL enemy model instead of the snake item,
                // but only while the View > "Enemy Model (Snake)" toggle is ON.
                if (Group == GroupType.ITA && Globals.RenderSnakeAsEnemy && DataBase.FileITA != null && DataBase.FileITA.Lines.ContainsKey(ID)
                    && DataBase.FileITA.Methods.ReturnItemNumber(ID) == 0x1000)
                {
                    byte[] itaPo = DataBase.FileITA.Methods.ReturnUnknown_PO(ID);
                    ushort EnemiesID = (itaPo != null && itaPo.Length >= 2) ? (ushort)((itaPo[0] << 8) | itaPo[1]) : (ushort)0xFFFF;
                    if (EnemiesID != 0xFFFF && DataBase.EnemiesIDs.List.ContainsKey(EnemiesID))
                    {
                        bool hasEnemyModel = DataBase.EnemiesIDs.List.ContainsKey(EnemiesID)
                            && DataBase.EnemiesModels.ContainsKey(DataBase.EnemiesIDs.List[EnemiesID].ObjectModel);
                        if (hasEnemyModel)
                        {
                            if (mode == RenderMode.ModelMode)
                            {
                                DataBase.EnemiesModels.RenderModel(DataBase.EnemiesIDs.List[EnemiesID].ObjectModel, rspFix);
                            }
                            else if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.BoundingBoxViewer(DataBase.EnemiesModels.GetBoundingBoxLimit(DataBase.EnemiesIDs.List[EnemiesID].ObjectModel), rspFix, mColor);
                            }
                            else if (mode == RenderMode.SelectMode)
                            {
                                RenderAppModel.BoundingBoxToSelect(DataBase.EnemiesModels.GetBoundingBoxLimit(DataBase.EnemiesIDs.List[EnemiesID].ObjectModel), rspFix, useColor);
                            }
                        }
                        else
                        {
                            if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.NoneBoundingBoxViewer(boundNoneEnemy, -boundNoneEnemy, rspFix, mColor);
                            }
                            else if (mode == RenderMode.SelectMode)
                            {
                                RenderAppModel.NoneBoundingBoxToSelect(boundNoneEnemy, -boundNoneEnemy, rspFix, useColor);
                            }
                        }
                        return;
                    }
                }

                if (DataBase.ItemsIDs.List.ContainsKey(item_ID) && DataBase.ItemsModels.ContainsKey(DataBase.ItemsIDs.List[item_ID].ObjectModel))
                {
                    if (mode == RenderMode.ModelMode)
                    {
                        DataBase.ItemsModels.RenderModel(DataBase.ItemsIDs.List[item_ID].ObjectModel, rspFix);
                    }
                    else if (mode == RenderMode.BoxMode)
                    {
                        RenderAppModel.BoundingBoxViewer(
                            DataBase.ItemsModels.GetBoundingBoxLimit(DataBase.ItemsIDs.List[item_ID].ObjectModel),
                            rspFix, mColor);
                    }
                    else if (mode == RenderMode.SelectMode)
                    {
                        RenderAppModel.BoundingBoxToSelect(
                            DataBase.ItemsModels.GetBoundingBoxLimit(DataBase.ItemsIDs.List[item_ID].ObjectModel),
                            rspFix, useColor);
                    }   
                }
                else
                {
                    if (mode == RenderMode.BoxMode)
                    {
                        RenderAppModel.NoneBoundingBoxViewer(boundNoneItem, -boundNoneItem, rspFix, mColor);
                    }
                    else if (mode == RenderMode.SelectMode)
                    {
                        RenderAppModel.NoneBoundingBoxToSelect(boundNoneItem, -boundNoneItem, rspFix, useColor);
                    }
                }

                if (mode == RenderMode.BoxMode)
                {
                    //RenderItemTriggerRadius
                    float ItemTrigggerRadius = MethodsForGL.GetItemTrigggerRadius(ID);
                    if (Globals.RenderItemTriggerRadius && ItemTrigggerRadius != 0)
                    {
                        Vector4 RadiusColor = Globals.GL_ColorItemTrigggerRadius;
                        if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                        {
                            RadiusColor = Globals.GL_ColorItemTrigggerRadiusSelected;
                        }

                        RenderAppModel.ItemTrigggerRadiusViewer(new Vector4(MethodsForGL.GetItemPosition(ID), ItemTrigggerRadius), RadiusColor);
                    }

                }

                // Aura preview — always on when enabled (no need to select), selected is brighter
                if ((mode == RenderMode.BoxMode || mode == RenderMode.ModelMode) && Globals.RenderItemAura)
                {
                    try
                    {
                        ushort aura = 0xFF;
                        if (Group == GroupType.ITA && DataBase.FileITA != null && DataBase.FileITA.Lines.ContainsKey(ID))
                            aura = DataBase.FileITA.Methods.ReturnItemAuraType(ID);
                        else if (Group == GroupType.AEV && DataBase.FileAEV != null && DataBase.FileAEV.Lines.ContainsKey(ID))
                            aura = DataBase.FileAEV.Methods.ReturnItemAuraType(ID);
                        if (aura != 0x00 && aura != 0xFF && aura <= 0x09 && aura != 0x08)
                        {
                            Vector3 ipos = MethodsForGL.GetItemPosition(ID);
                            // Make aura slightly larger than the item's own bounding box so it envelops it
                            float itemExtent = 0.5f;
                            try
                            {
                                var bb = DataBase.ItemsModels.GetBoundingBoxLimit(DataBase.ItemsIDs.List[item_ID].ObjectModel);
                                float ex = Math.Max(Math.Max(Math.Abs(bb.UpperBoundary.X - bb.LowerBoundary.X), Math.Abs(bb.UpperBoundary.Y - bb.LowerBoundary.Y)), Math.Abs(bb.UpperBoundary.Z - bb.LowerBoundary.Z)) * 0.5f;
                                if (ex > 0.1f && ex < 5f) itemExtent = ex * 0.55f;
                            }
                            catch { }
                            Vector4 auraCol = Vector4.Zero; float auraH = 6.8f; float auraR = 0.95f; bool hasSparkle = false; bool renderAura = true;
                            switch (aura)
                            {
                                case 0x01: renderAura = false; hasSparkle = false; break; // Small glint — no aura at all as requested
                                case 0x02: auraCol = new Vector4(1f, 0.92f, 0.38f, 0.14f); auraH = 7.0f; auraR = 0.98f + itemExtent * 0.45f; break;
                                case 0x03: auraCol = new Vector4(0.45f, 0.72f, 1f, 0.12f); auraH = 6.8f; auraR = 0.95f + itemExtent * 0.45f; break;
                                case 0x04: auraCol = new Vector4(0.15f, 1f, 0.38f, 0.14f); auraH = 6.8f; auraR = 0.95f + itemExtent * 0.45f; break;
                                case 0x05:
                                case 0x06: auraCol = new Vector4(1f, 0.28f, 0.28f, 0.14f); auraH = 6.8f; auraR = 0.95f + itemExtent * 0.45f; break;
                                case 0x07: auraCol = new Vector4(0.38f, 0.60f, 1f, 0.10f); auraH = 10.5f; auraR = 1.30f + itemExtent * 0.40f; break;
                                case 0x08: auraCol = new Vector4(1f, 1f, 1f, 0.24f); auraH = 1.55f; auraR = 0.52f + itemExtent * 0.32f; break; // Short but visible as requested
                                case 0x09: auraCol = new Vector4(1f, 0.94f, 0.25f, 0.11f); auraH = 10.5f; auraR = 1.30f + itemExtent * 0.40f; break;
                                default: auraCol = new Vector4(1f, 1f, 1f, 0.10f); break;
                            }
                            bool isSelAura = DataBase.SelectedNodes.ContainsKey(item.GetHashCode());
                            if (!isSelAura) auraCol.W *= 0.62f;
                            float rot = (float)(DateTime.Now.TimeOfDay.TotalSeconds * 0.85);
                            float glowPulse = hasSparkle ? (float)(Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 2.8) * 0.12 + 1.0) : (float)(Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 1.4) * 0.06 + 1.0);
                            auraCol.W *= glowPulse;
                            Vector3 basePos = ipos + new Vector3(0, 0.02f, 0);
                            if (renderAura)
                            {
                                float[] tris = GenerateAuraCylinder(basePos, auraR, auraH, 24, rot, true);
                                CamZoneRender.DrawTrianglesTransparent(tris, auraCol);
                                float[] glowTris = GenerateAuraCylinder(basePos, auraR * 1.18f, auraH * 1.02f, 24, -rot * 0.6f, true);
                                Vector4 glowCol = new Vector4(auraCol.X, auraCol.Y, auraCol.Z, auraCol.W * 0.32f);
                                CamZoneRender.DrawTrianglesTransparent(glowTris, glowCol);
                            }
                            if (hasSparkle)
                            {
                                // Simple spark shower rising upward — minimal, not exaggerated
                                double now = DateTime.Now.TimeOfDay.TotalSeconds;
                                float effH = renderAura ? auraH : 1.4f;
                                float effR = renderAura ? auraR : 0.32f;
                                for (int p = 0; p < 4; p++)
                                {
                                    float t = (float)((now * 0.75 + p * 0.55) % 1.0);
                                    float y = basePos.Y + t * effH + 0.15f;
                                    float ang = (float)(now * 1.4 + p * 1.3);
                                    float rx = (float)Math.Cos(ang) * effR * 0.28f;
                                    float rz = (float)Math.Sin(ang) * effR * 0.28f;
                                    Vector3 sparkPos = new Vector3(basePos.X + rx, y, basePos.Z + rz);
                                    float alpha = (1f - t) * 0.88f * (isSelAura ? 1f : 0.6f);
                                    if (alpha < 0.04f) continue;
                                    RenderAppModel.RenderLitPointColor(sparkPos, new Vector4(1f, 1f, 1f, alpha));
                                    if (p % 2 == 0)
                                        RenderAppModel.RenderLitPointColor(sparkPos + new Vector3(0, 0.06f, 0), new Vector4(auraCol.X, auraCol.Y, auraCol.Z, alpha * 0.45f));
                                }
                            }
                        }
                    }
                    catch { }
                }

            }

        }

        private static void RenderQuadCustomPoint(RenderMode mode) 
        {
            if (Globals.RenderFileQuadCustom)
            {
                foreach (TreeNode item in DataBase.NodeQuadCustom.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    Vector4 mColor = Globals.GL_ColorQuadCustom;
                    ushort ID = ((Object3D)item).ObjLineRef;

                    if (mode == RenderMode.BoxMode && Globals.UseMoreQuadCustomColors)
                    {
                        mColor = DataBase.NodeQuadCustom.MethodsForGL.GetCustomColor(ID);
                    }

                    if (mode == RenderMode.BoxMode && DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                    {
                        mColor = Globals.GL_ColorSelected;
                    }

                    byte[] partColor = BitConverter.GetBytes(ID);
                    Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.QUAD_CUSTOM / 255f, 1f);

                    RspFix rspFix = new RspFix(
                        DataBase.NodeQuadCustom.MethodsForGL.GetScale(ID), 
                        DataBase.NodeQuadCustom.MethodsForGL.GetPosition(ID), 
                        DataBase.NodeQuadCustom.MethodsForGL.GetAngle(ID));

                    var status = DataBase.NodeQuadCustom.MethodsForGL.GetQuadCustomPointStatus(ID);
                    if (status == QuadCustomPointStatus.ArrowPoint01)
                    {

                        if (DataBase.InternalModels.ContainsKey(Consts.ModelKeyQuadCustomPoint))
                        {
                            if (mode == RenderMode.ModelMode)
                            {
                                DataBase.InternalModels.RenderModel(Consts.ModelKeyQuadCustomPoint, rspFix);
                            }
                            else if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.BoundingBoxViewer(
                                    DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyQuadCustomPoint),
                                    rspFix, mColor);
                            }
                            else if (mode == RenderMode.SelectMode)
                            {
                                RenderAppModel.BoundingBoxToSelect(
                                    DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyQuadCustomPoint),
                                    rspFix, useColor);
                            }
                        }
                        else
                        {
                            if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.NoneBoundingBoxViewer(boundNoneQuadCustom, -boundNoneQuadCustom, rspFix, mColor);
                            }
                            else if (mode == RenderMode.SelectMode)
                            {
                                RenderAppModel.NoneBoundingBoxToSelect(boundNoneQuadCustom, -boundNoneQuadCustom, rspFix, useColor);
                            }

                        }

                    }
                    else if (status == QuadCustomPointStatus.CustomModel02)
                    {
                        uint CustomModelID = DataBase.NodeQuadCustom.MethodsForGL.GetPointModelID(ID);

                        if (DataBase.QuadCustomIDs.List.ContainsKey(CustomModelID) && DataBase.QuadCustomModels.ContainsKey(DataBase.QuadCustomIDs.List[CustomModelID].ObjectModel))
                        {
                            if (mode == RenderMode.ModelMode)
                            {
                                DataBase.QuadCustomModels.RenderModel(DataBase.QuadCustomIDs.List[CustomModelID].ObjectModel, rspFix);
                            }
                            else if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.BoundingBoxViewer(
                                    DataBase.QuadCustomModels.GetBoundingBoxLimit(DataBase.QuadCustomIDs.List[CustomModelID].ObjectModel),
                                    rspFix, mColor);
                            }
                            else if (mode == RenderMode.SelectMode)
                            {
                                RenderAppModel.BoundingBoxToSelect(
                                    DataBase.QuadCustomModels.GetBoundingBoxLimit(DataBase.QuadCustomIDs.List[CustomModelID].ObjectModel),
                                    rspFix, useColor);
                            }
                        }
                        else
                        {
                            if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.NoneBoundingBoxViewer(boundNoneQuadCustom, -boundNoneQuadCustom, rspFix, mColor);
                            }
                            else if (mode == RenderMode.SelectMode)
                            {
                                RenderAppModel.NoneBoundingBoxToSelect(boundNoneQuadCustom, -boundNoneQuadCustom, rspFix, useColor);
                            }
                        }
 
                    }

                }
            }

        }


        private static void RenderExtras(RenderMode mode)
        {
            if (Globals.RenderExtraObjs)
            {
                foreach (TreeNode item in DataBase.NodeEXTRAS.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    ushort ID = ((Object3D)item).ObjLineRef;
                    var association = DataBase.Extras.AssociationList[ID];
                    if (association.FileFormat == SpecialFileFormat.AEV && Globals.RenderEventsAEV)
                    {
                        RenderExtrasSubPart((Object3D)item, DataBase.FileAEV.ExtrasMethodsForGL, association.LineID, association.SubObjID, SpecialFileFormat.AEV, mode);
                    }
                    else if (association.FileFormat == SpecialFileFormat.ITA && Globals.RenderItemsITA)
                    {
                        RenderExtrasSubPart((Object3D)item, DataBase.FileITA.ExtrasMethodsForGL, association.LineID, association.SubObjID, SpecialFileFormat.ITA, mode);
                    }

                }
            }

        }

        private static void RenderExtrasSubPart(Object3D item, ExtrasMethodsForGL MethodsForGL, ushort ID, byte SubId, SpecialFileFormat FileFormat, RenderMode mode)
        {
            SpecialType specialType = MethodsForGL.GetSpecialType(ID);
            ushort ExtraID = item.ObjLineRef;
            byte[] partColor = BitConverter.GetBytes(ExtraID);

            Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.EXTRAS / 255f, 1f); // selectmode
            Vector4 mColor = Globals.GL_ColorEXTRAS;

            switch (specialType)
            {
                case SpecialType.T01_WarpDoor:
                    if (Globals.RenderExtraWarpDoor)
                    {
                        if (Globals.UseMoreSpecialColors)
                        {
                            mColor = Globals.GL_MoreColor_T01_DoorWarp;
                        }

                        if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                        {
                            mColor = Globals.GL_ColorSelected;
                        }

                        RspFix rspFix = new RspFix(
                        Vector3.One, //scale
                        MethodsForGL.GetFirstPosition(ID),
                        MethodsForGL.GetWarpRotation(ID));

                        if (DataBase.InternalModels.ContainsKey(Consts.ModelKeyWarpPoint))
                        {
                            if (mode == RenderMode.ModelMode)
                            {
                                DataBase.InternalModels.RenderModel(Consts.ModelKeyWarpPoint, rspFix);
                            }
                            else if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.BoundingBoxViewer(
                                    DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyWarpPoint),
                                    rspFix, mColor);
                            }
                            else if (mode == RenderMode.SelectMode) 
                            {
                                RenderAppModel.BoundingBoxToSelect(
                                    DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyWarpPoint),
                                    rspFix, useColor);
                            }
                        }
                        else
                        {
                            if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.NoneBoundingBoxViewer(boundNoneExtras, -boundNoneExtras, rspFix, mColor);
                            }
                            else if (mode == RenderMode.SelectMode)
                            {
                                RenderAppModel.NoneBoundingBoxToSelect(boundNoneExtras, -boundNoneExtras, rspFix, useColor);
                            }

                        }
                    }
                    break;
                case SpecialType.T13_LocalTeleportation:
                    if (!Globals.HideExtraExceptWarpDoor)
                    {
                        if (Globals.UseMoreSpecialColors)
                        {
                            mColor = Globals.GL_MoreColor_T13_LocalTeleportation;
                        }

                        if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                        {
                            mColor = Globals.GL_ColorSelected;
                        }

                        RspFix rspFix = new RspFix(
                        Vector3.One, //scale
                        MethodsForGL.GetFirstPosition(ID),
                        MethodsForGL.GetLocationAndLadderRotation(ID));

                        if (DataBase.InternalModels.ContainsKey(Consts.ModelKeyLocalTeleportationPoint))
                        {
                            if (mode == RenderMode.ModelMode)
                            {
                                DataBase.InternalModels.RenderModel(Consts.ModelKeyLocalTeleportationPoint, rspFix);
                            }
                            else if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.BoundingBoxViewer(
                                    DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLocalTeleportationPoint),
                                    rspFix, mColor);
                            }
                            else if (mode == RenderMode.SelectMode) 
                            {
                                RenderAppModel.BoundingBoxToSelect(
                                    DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLocalTeleportationPoint),
                                    rspFix, useColor);
                            }
   
                        }
                        else
                        {
                            if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.NoneBoundingBoxViewer(boundNoneExtras, -boundNoneExtras, rspFix, mColor);
                            }
                            else if (mode == RenderMode.SelectMode)
                            {
                                RenderAppModel.NoneBoundingBoxToSelect(boundNoneExtras, -boundNoneExtras, rspFix, useColor);
                            }
                           
                        }
                    }
                    break;
                case SpecialType.T10_FixedLadderClimbUp:
                    if (!Globals.HideExtraExceptWarpDoor)
                    {
                        if (Globals.UseMoreSpecialColors)
                        {
                            mColor = Globals.GL_MoreColor_T10_FixedLadderClimbUp;
                        }

                        if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                        {
                            mColor = Globals.GL_ColorSelected;
                        }

                        RspFix rspFix = new RspFix(
                        Vector3.One, //scale
                        MethodsForGL.GetFirstPosition(ID),
                        MethodsForGL.GetLocationAndLadderRotation(ID));

                        if (DataBase.InternalModels.ContainsKey(Consts.ModelKeyLadderPoint)
                         && DataBase.InternalModels.ContainsKey(Consts.ModelKeyLadderObj))
                        {
                            //renderiza o X que aparece no ch?o
                            if (mode == RenderMode.ModelMode)
                            {
                                DataBase.InternalModels.RenderModel(Consts.ModelKeyLadderPoint, rspFix);
                            }
                            else if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.BoundingBoxViewer(
                                    DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderPoint),
                                    rspFix, mColor);
                            }
                            else if (mode == RenderMode.SelectMode) 
                            {
                                RenderAppModel.BoundingBoxToSelect(
                                    DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderPoint),
                                    rspFix, useColor);
                            }

                            //renderiza a escada
                            sbyte stepCount = MethodsForGL.GetLadderStepCount(ID);
                            if (stepCount >= 2)
                            {
                                if (mode == RenderMode.ModelMode)
                                {
                                    float maxHeight = DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderObj).UpperBoundary.Y;
                                    DataBase.InternalModels.RenderModel(Consts.ModelKeyLadderObj, rspFix);

                                    for (int i = 1; i < stepCount; i++)
                                    {
                                        Vector3 position = new Vector3(MethodsForGL.GetFirstPosition(ID).X,
                                            MethodsForGL.GetFirstPosition(ID).Y + maxHeight,
                                            MethodsForGL.GetFirstPosition(ID).Z);

                                        RspFix irspFix = new RspFix(
                                        rspFix.Scale, //scale
                                        position,
                                        rspFix.Rotation);

                                        DataBase.InternalModels.RenderModel(Consts.ModelKeyLadderObj, irspFix);

                                        maxHeight += DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderObj).UpperBoundary.Y;
                                    }
                                }

                                Vector3 UpperBoundary = new Vector3(
                                    DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderObj).UpperBoundary.X,
                                    DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderObj).UpperBoundary.Y * stepCount, //altura correta
                                    DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderObj).UpperBoundary.Z);
                                Vector3 LowerBoundary = DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderObj).LowerBoundary;

                                if (mode == RenderMode.BoxMode)
                                {
                                    RenderAppModel.BoundingBoxViewer(new BoundingBoxLimit(LowerBoundary, UpperBoundary), rspFix, mColor);
                                }
                                else if (mode == RenderMode.SelectMode)
                                {
                                    RenderAppModel.BoundingBoxToSelect(new BoundingBoxLimit(LowerBoundary, UpperBoundary), rspFix, useColor);
                                }
                              
                            }
                            else if (stepCount <= -2)
                            {
                                if (mode == RenderMode.ModelMode)
                                {
                                    float minHeight = DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderObj).UpperBoundary.Y;
                                    Vector3 position1 = new Vector3(
                                          MethodsForGL.GetFirstPosition(ID).X,
                                          MethodsForGL.GetFirstPosition(ID).Y - minHeight,
                                          MethodsForGL.GetFirstPosition(ID).Z);


                                    RspFix inrspFix = new RspFix(
                                    rspFix.Scale, //scale
                                    position1,
                                    rspFix.Rotation);

                                    DataBase.InternalModels.RenderModel(Consts.ModelKeyLadderObj, inrspFix);

                                    for (int i = 1; i < -stepCount; i++)
                                    {
                                        minHeight += DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderObj).UpperBoundary.Y;

                                        Vector3 position = new Vector3(
                                            MethodsForGL.GetFirstPosition(ID).X,
                                            MethodsForGL.GetFirstPosition(ID).Y - minHeight,
                                            MethodsForGL.GetFirstPosition(ID).Z);

                                        RspFix irspFix = new RspFix(
                                        rspFix.Scale, //scale
                                        position,
                                        rspFix.Rotation);

                                        DataBase.InternalModels.RenderModel(Consts.ModelKeyLadderObj, irspFix);
                                    }
                                }

                                Vector3 UpperBoundary = new Vector3(
                                    DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderObj).UpperBoundary.X,
                               DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderObj).LowerBoundary.Y,
                               DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderObj).UpperBoundary.Z);

                                float _minHeight = DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderObj).UpperBoundary.Y * (-stepCount);
                                Vector3 LowerBoundary = new Vector3(
                                    DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderObj).LowerBoundary.X,
                                   -_minHeight,
                                   DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderObj).LowerBoundary.Z);

                                if (mode == RenderMode.BoxMode)
                                {
                                    RenderAppModel.BoundingBoxViewer(new BoundingBoxLimit(LowerBoundary, UpperBoundary), rspFix, mColor);
                                }
                                else if (mode == RenderMode.SelectMode) 
                                {
                                    RenderAppModel.BoundingBoxToSelect(new BoundingBoxLimit(LowerBoundary, UpperBoundary), rspFix, useColor);
                                }   
                            }
                            else
                            {
                                if (DataBase.InternalModels.ContainsKey(Consts.ModelKeyLadderError))
                                {
                                    if (mode == RenderMode.ModelMode)
                                    {
                                        DataBase.InternalModels.RenderModel(Consts.ModelKeyLadderError, rspFix);
                                    }
                                    else if (mode == RenderMode.BoxMode)
                                    {
                                        RenderAppModel.BoundingBoxViewer(
                                            DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderError),
                                            rspFix, mColor);
                                    }
                                    else if (mode == RenderMode.SelectMode) 
                                    {
                                        RenderAppModel.BoundingBoxToSelect(
                                            DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyLadderError),
                                            rspFix, useColor);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.NoneBoundingBoxViewer(boundNoneExtras, -boundNoneExtras, rspFix, mColor);
                            }
                            else if (mode == RenderMode.SelectMode)
                            {
                                RenderAppModel.NoneBoundingBoxToSelect(boundNoneExtras, -boundNoneExtras, rspFix, useColor);
                            }
                        }
                    }
                    break;
                case SpecialType.T12_AshleyHideCommand:
                    if (!Globals.HideExtraExceptWarpDoor)
                    {
                        if (Globals.UseMoreSpecialColors)
                        {
                            mColor = Globals.GL_MoreColor_T12_AshleyHideCommand;
                        }

                        if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                        {
                            mColor = Globals.GL_ColorSelected;
                        }

                        RspFix rspFix = new RspFix(
                        Vector3.One, //scale
                        MethodsForGL.GetAshleyPoint(ID),
                        Matrix4.Identity); //Rotation

                        if (DataBase.InternalModels.ContainsKey(Consts.ModelKeyAshleyPoint))
                        {
                            if (mode == RenderMode.ModelMode)
                            {
                                DataBase.InternalModels.RenderModel(Consts.ModelKeyAshleyPoint, rspFix);
                            }
                            else if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.BoundingBoxViewer(
                                    DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyAshleyPoint),
                                    rspFix, mColor);
                            }
                            else if (mode == RenderMode.SelectMode) 
                            {
                                RenderAppModel.BoundingBoxToSelect(
                                       DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyAshleyPoint),
                                       rspFix, useColor);
                            }
                        }
                        else
                        {
                            if (mode == RenderMode.BoxMode)
                            {
                                RenderAppModel.NoneBoundingBoxViewer(boundNoneExtras, -boundNoneExtras, rspFix, mColor);
                            }
                            else if (mode == RenderMode.SelectMode)
                            {
                                RenderAppModel.NoneBoundingBoxToSelect(boundNoneExtras, -boundNoneExtras, rspFix, useColor);
                            }
                        }

                        // AshleyZone
                        if (mode == RenderMode.BoxMode)
                        {
                            RenderAppModel.PlaneZoneViewer(MethodsForGL.GetAshleyHidingZoneCornerMatrix4(ID), mColor);
                        }
                        else if (mode == RenderMode.SelectMode)
                        {
                            RenderAppModel.PlaneZoneSolid(MethodsForGL.GetAshleyHidingZoneCornerMatrix4(ID), useColor);
                        }
                    }
                    break;
                case SpecialType.T15_AdaGrappleGun:
                    if (!Globals.HideExtraExceptWarpDoor)
                    {
                        if (SubId == 0)
                        {
                            RenderGrappleGun(item, MethodsForGL, ID, SubId, FileFormat, MethodsForGL.GetFirstPosition(ID), mode);
                        }
                        else if (SubId == 1)
                        {
                            RenderGrappleGun(item, MethodsForGL, ID, SubId, FileFormat, MethodsForGL.GetGrappleGunEndPosition(ID), mode);
                        }
                        else if (SubId == 2 && MethodsForGL.GetGrappleGunParameter3(ID) != 0)
                        {
                            RenderGrappleGun(item, MethodsForGL, ID, SubId, FileFormat, MethodsForGL.GetGrappleGunThirdPosition(ID), mode);
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        private static void RenderGrappleGun(Object3D item, ExtrasMethodsForGL MethodsForGL, ushort ID, byte SubId, SpecialFileFormat FileFormat, Vector3 position, RenderMode mode)
        {
            ushort ExtraID = item.ObjLineRef;
            byte[] partColor = BitConverter.GetBytes(ExtraID);

            Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.EXTRAS / 255f, 1f);

            Vector4 mColor = Globals.GL_ColorEXTRAS;
            if (Globals.UseMoreSpecialColors)
            {
                mColor = Globals.GL_MoreColor_T15_AdaGrappleGun;
            }

            if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
            {
                mColor = Globals.GL_ColorSelected;
            }

            RspFix rspFix = new RspFix(
                Vector3.One, //scale
                position,
                MethodsForGL.GetGrappleGunFacingAngleRotation(ID));

            if (DataBase.InternalModels.ContainsKey(Consts.ModelKeyGrappleGunPoint))
            {
                if (mode == RenderMode.ModelMode)
                {
                    DataBase.InternalModels.RenderModel(Consts.ModelKeyGrappleGunPoint, rspFix);
                }
                else if (mode == RenderMode.BoxMode)
                {
                    RenderAppModel.BoundingBoxViewer(
                               DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyGrappleGunPoint),
                               rspFix, mColor);
                }
                else if (mode == RenderMode.SelectMode) 
                {
                    RenderAppModel.BoundingBoxToSelect(
                         DataBase.InternalModels.GetBoundingBoxLimit(Consts.ModelKeyGrappleGunPoint),
                         rspFix, useColor);
                }
            }
            else
            {
                if (mode == RenderMode.BoxMode)
                {
                    RenderAppModel.NoneBoundingBoxViewer(boundNoneExtras, -boundNoneExtras, rspFix, mColor);
                }
                else if (mode == RenderMode.SelectMode)
                {
                    RenderAppModel.NoneBoundingBoxToSelect(boundNoneExtras, -boundNoneExtras, rspFix, useColor);
                }
            }
        }


        private static void RenderPosTriggerZoneBox()
        {
            if (Globals.RenderItemsITA)
            {
                foreach (Object3D item in DataBase.NodeITA.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    RenderPosTriggerZoneBoxSubPartSpecial(item, DataBase.NodeITA.MethodsForGL, Globals.GL_ColorITA);
                }
            }

            if (Globals.RenderEventsAEV)
            {
                foreach (Object3D item in DataBase.NodeAEV.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    RenderPosTriggerZoneBoxSubPartSpecial(item, DataBase.NodeAEV.MethodsForGL, Globals.GL_ColorAEV);
                }
            }

            if (Globals.RenderFileFSE)
            {
                foreach (Object3D item in DataBase.NodeFSE.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    RenderPosTriggerZoneBoxSubPart(item, DataBase.NodeFSE.MethodsForGL, Globals.GL_ColorFSE);
                }
            }

            if (Globals.RenderFileSAR)
            {
                foreach (Object3D item in DataBase.NodeSAR.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    RenderPosTriggerZoneBoxSubPart(item, DataBase.NodeSAR.MethodsForGL, Globals.GL_ColorSAR);
                }
            }

            if (Globals.RenderFileEAR)
            {
                foreach (Object3D item in DataBase.NodeEAR.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    RenderPosTriggerZoneBoxSubPart(item, DataBase.NodeEAR.MethodsForGL, Globals.GL_ColorEAR);
                }
            }

            if (Globals.RenderFileQuadCustom)
            {
                foreach (Object3D item in DataBase.NodeQuadCustom.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    ushort ID = item.ObjLineRef;

                    Vector4 mColor = Globals.GL_ColorQuadCustom;

                    if (Globals.UseMoreQuadCustomColors)
                    {
                        mColor = DataBase.NodeQuadCustom.MethodsForGL.GetCustomColor(ID);
                    }

                    RenderPosTriggerZoneBoxSubPart(item, DataBase.NodeQuadCustom.MethodsForGL, mColor);
                }
            }

            if (Globals.RenderFileCAM_Zone && !CameraViewState.ActiveThisFrame && DataBase.NodeCAM_Zone != null
                && DataBase.NodeCAM_Zone.MethodsForGL is Re4QuadExtremeEditor.src.Class.ObjMethods.NewAge_CAM_Zone_MethodsForGL camZoneMethods)
            {
                // polygon zones (>4 points) first: their see-through fill must
                // not depth-fight the ghost boxes below, which write depth
                foreach (Object3D item in DataBase.NodeCAM_Zone.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;

                    Vector3[] polyPts = (camZoneMethods.GetZonePoints != null) ? camZoneMethods.GetZonePoints(item.ObjLineRef) : null;
                    if (polyPts != null && polyPts.Length != 4)
                    {
                        // true prism fill here in the late pass; the 4-corner
                        // ghost box would be a wrong volume built from the
                        // first 4 corners only
                        bool isSel = DataBase.SelectedNodes.ContainsKey(item.GetHashCode());
                        RenderCamZonePolygonTransparent(camZoneMethods, item.ObjLineRef, polyPts, isSel);
                    }
                }

                foreach (Object3D item in DataBase.NodeCAM_Zone.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;

                    Vector3[] polyPts = (camZoneMethods.GetZonePoints != null) ? camZoneMethods.GetZonePoints(item.ObjLineRef) : null;
                    if (polyPts != null && polyPts.Length != 4)
                    {
                        continue;
                    }

                    Vector4 mColor = camZoneMethods.GetZoneColor != null ? camZoneMethods.GetZoneColor(item.ObjLineRef) : Globals.GL_ColorCAM_ZONE;

                    RenderPosTriggerZoneBoxSubPart(item, DataBase.NodeCAM_Zone.MethodsForGL, mColor);
                }
            }
        }

        private static void RenderPosTriggerZoneBoxSubPartSpecial(Object3D item, SpecialMethodsForGL MethodsForGL, Vector4 frontColor)
        {
            ushort ID = item.ObjLineRef;
         
            if (MethodsForGL.GetSpecialType(ID) != SpecialType.T03_Items && Globals.RenderSpecialTriggerZone)
            {
                if (Globals.UseMoreSpecialColors)
                {
                    frontColor = ReturnMoreSpecialColor(MethodsForGL.GetSpecialType(ID), frontColor);
                }

                RenderPosTriggerZoneBoxSubPart(item, MethodsForGL, frontColor);
            }
        }

        private static void RenderPosTriggerZoneBoxSubPart(Object3D item, BaseTriggerZoneMethodsForGL MethodsForGL, Vector4 frontColor)
        {
            ushort ID = item.ObjLineRef;

            if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode())) { frontColor = Globals.GL_ColorSelected; }
            frontColor.W = TriggerZoneTransparentLevel;

            Vector4 backColor = TriggerZoneGetBackColor(frontColor);

            TriggerZoneTransparentSolid(ID, MethodsForGL, frontColor, backColor);
        }


        private static Vector4 ReturnMoreSpecialColor(SpecialType specialType, Vector4 color)
        {
            switch (specialType)
            {
                case SpecialType.T00_GeneralPurpose: return Globals.GL_MoreColor_T00_GeneralPurpose;
                case SpecialType.T01_WarpDoor: return Globals.GL_MoreColor_T01_DoorWarp;
                case SpecialType.T02_CutSceneEvents: return Globals.GL_MoreColor_T02_CutSceneEvents;
                case SpecialType.T04_GroupedEnemyTrigger: return Globals.GL_MoreColor_T04_GroupedEnemyTrigger;
                case SpecialType.T05_Message: return Globals.GL_MoreColor_T05_Message;
                case SpecialType.T08_TypeWriter: return Globals.GL_MoreColor_T08_TypeWriter;
                case SpecialType.T0A_DamagesThePlayer: return Globals.GL_MoreColor_T0A_DamagesThePlayer;
                case SpecialType.T0B_FalseCollision: return Globals.GL_MoreColor_T0B_FalseCollision;
                case SpecialType.T0D_FieldInfo: return Globals.GL_MoreColor_T0D_FieldInfo;
                case SpecialType.T0E_Crouch: return Globals.GL_MoreColor_T0E_Crouch;
                case SpecialType.T10_FixedLadderClimbUp: return Globals.GL_MoreColor_T10_FixedLadderClimbUp;
                case SpecialType.T11_ItemDependentEvents: return Globals.GL_MoreColor_T11_ItemDependentEvents;
                case SpecialType.T12_AshleyHideCommand: return Globals.GL_MoreColor_T12_AshleyHideCommand;
                case SpecialType.T13_LocalTeleportation: return Globals.GL_MoreColor_T13_LocalTeleportation;
                case SpecialType.T14_UsedForElevators: return Globals.GL_MoreColor_T14_UsedForElevators;
                case SpecialType.T15_AdaGrappleGun: return Globals.GL_MoreColor_T15_AdaGrappleGun;
            }
            return color;
        }

        private static void RenderFileFSE_TriggerZone(RenderMode mode) 
        {
            if (Globals.RenderFileFSE)
            {
                foreach (TreeNode item in DataBase.NodeFSE.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    Vector4 mColor = Globals.GL_ColorFSE;
                    ushort ID = ((Object3D)item).ObjLineRef;

                    if (mode == RenderMode.BoxMode && DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                    {
                        mColor = Globals.GL_ColorSelected;
                    }

                    Render_Any_TriggerZone(ID, GroupType.FSE, DataBase.NodeFSE.MethodsForGL, mode, mColor);
                }
            }

        }

        private static void RenderFileSAR_TriggerZone(RenderMode mode)
        {
            if (Globals.RenderFileSAR)
            {
                foreach (TreeNode item in DataBase.NodeSAR.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    Vector4 mColor = Globals.GL_ColorSAR;
                    ushort ID = ((Object3D)item).ObjLineRef;

                    if (mode == RenderMode.BoxMode && DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                    {
                        mColor = Globals.GL_ColorSelected;
                    }

                    Render_Any_TriggerZone(ID, GroupType.SAR, DataBase.NodeSAR.MethodsForGL, mode, mColor);
                }
            }

        }

        private static void RenderFileEAR_TriggerZone(RenderMode mode)
        {
            if (Globals.RenderFileEAR)
            {
                foreach (TreeNode item in DataBase.NodeEAR.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    Vector4 mColor = Globals.GL_ColorEAR;
                    ushort ID = ((Object3D)item).ObjLineRef;

                    if (mode == RenderMode.BoxMode && DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                    {
                        mColor = Globals.GL_ColorSelected;
                    }

                    Render_Any_TriggerZone(ID, GroupType.EAR, DataBase.NodeEAR.MethodsForGL, mode, mColor);
                }
            }

        }

        private static void RenderFileQuadCustom_TriggerZone(RenderMode mode)
        {
            if (Globals.RenderFileQuadCustom)
            {
                foreach (TreeNode item in DataBase.NodeQuadCustom.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    ushort ID = ((Object3D)item).ObjLineRef;

                    Vector4 mColor = Globals.GL_ColorQuadCustom;

                    if (mode == RenderMode.BoxMode && Globals.UseMoreQuadCustomColors)
                    {
                        mColor = DataBase.NodeQuadCustom.MethodsForGL.GetCustomColor(ID);
                    }

                    if (mode == RenderMode.BoxMode && DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                    {
                        mColor = Globals.GL_ColorSelected;
                    }

                    Render_Any_TriggerZone(ID, GroupType.QUAD_CUSTOM, DataBase.NodeQuadCustom.MethodsForGL, mode, mColor);
                }
            }

        }


        private static void RenderFileCAM_Zone_TriggerZone(RenderMode mode)
        {
            if (Globals.RenderFileCAM_Zone && DataBase.NodeCAM_Zone != null && DataBase.NodeCAM_Zone.MethodsForGL != null)
            {
                Re4QuadExtremeEditor.src.Class.ObjMethods.NewAge_CAM_Zone_MethodsForGL MethodsForGL =
                    (Re4QuadExtremeEditor.src.Class.ObjMethods.NewAge_CAM_Zone_MethodsForGL)DataBase.NodeCAM_Zone.MethodsForGL;

                foreach (TreeNode item in DataBase.NodeCAM_Zone.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    ushort ID = ((Object3D)item).ObjLineRef;

                    // exact same pipeline as the AEV/SAR/EAR/FSE trigger zones
                    // (4-corner box matrix); polygons with a different point
                    // count (e.g. the 5-point Type 8 preset) get a true prism
                    Vector3[] polyPts = (MethodsForGL.GetZonePoints != null) ? MethodsForGL.GetZonePoints(ID) : null;
                    if (polyPts != null && polyPts.Length != 4)
                    {
                        bool isSel = DataBase.SelectedNodes.ContainsKey(item.GetHashCode());
                        RenderCamZonePolygon(MethodsForGL, ID, polyPts, mode, isSel);
                        continue;
                    }

                    if (mode == RenderMode.SelectMode)
                    {
                        byte[] partColor = BitConverter.GetBytes(ID);
                        Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.CAM_ZONE / 255f, 1f);
                        RenderAppModel.TriggerZoneBoxSolid(MethodsForGL.GetTriggerZoneMatrix4(ID), useColor);
                        DebugCamZonesDrawn++;
                    }
                    else
                    {
                        Vector4 mColor = MethodsForGL.GetZoneColor != null ? MethodsForGL.GetZoneColor(ID) : Globals.GL_ColorCAM_ZONE;
                        if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                        {
                            mColor = Globals.GL_ColorSelected;
                        }
                        RenderAppModel.TriggerZoneBoxViewer(MethodsForGL.GetTriggerZoneMatrix4(ID), mColor);
                        DebugCamZonesDrawn++;
                    }
                }
            }

        }

        /// <summary>
        /// draws a trigger with an arbitrary point count as a real prism
        /// (side walls + top/bottom caps), so presets like Type 8 show their
        /// true footprint instead of a truncated 4-corner box
        /// </summary>
        private static void RenderCamZonePolygon(
            Re4QuadExtremeEditor.src.Class.ObjMethods.NewAge_CAM_Zone_MethodsForGL MethodsForGL,
            ushort ID, Vector3[] pts, RenderMode mode, bool isSelected)
        {
            if (pts == null || pts.Length < 3)
            {
                return;
            }

            float[] trisArr;
            float[] edgesArr;
            BuildCamZonePrismGeometry(MethodsForGL, ID, pts, out trisArr, out edgesArr);

            DebugCamZonesDrawn++;
            if (mode == RenderMode.SelectMode)
            {
                byte[] partColor = BitConverter.GetBytes(ID);
                Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.CAM_ZONE / 255f, 1f);
                CamZoneRender.DrawTrianglesOpaque(trisArr, useColor);
                return;
            }

            // viewer: wireframe only here. The see-through fill is drawn in
            // the late RenderPosTriggerZoneBox pass (after the opaque room),
            // because this fill does not write depth and the room rendered
            // afterwards would erase it completely.
            Vector4 color = MethodsForGL.GetZoneColor != null ? MethodsForGL.GetZoneColor(ID) : Globals.GL_ColorCAM_ZONE;
            if (isSelected)
            {
                color = Globals.GL_ColorSelected;
            }
            CamZoneRender.DrawLines(edgesArr, color);
        }

        /// <summary>
        /// late-pass see-through fill for polygon CAM zones; runs after the
        /// room and models, so opaque geometry can no longer overwrite it
        /// </summary>
        private static void RenderCamZonePolygonTransparent(
            Re4QuadExtremeEditor.src.Class.ObjMethods.NewAge_CAM_Zone_MethodsForGL MethodsForGL,
            ushort ID, Vector3[] pts, bool isSelected)
        {
            if (pts == null || pts.Length < 3)
            {
                return;
            }

            float[] trisArr;
            float[] edgesArr;
            BuildCamZonePrismGeometry(MethodsForGL, ID, pts, out trisArr, out edgesArr);

            Vector4 color = MethodsForGL.GetZoneColor != null ? MethodsForGL.GetZoneColor(ID) : Globals.GL_ColorCAM_ZONE;
            if (isSelected)
            {
                color = Globals.GL_ColorSelected;
            }

            Vector4 fillColor = new Vector4(color.X, color.Y, color.Z, 0.35f);
            CamZoneRender.DrawTrianglesTransparent(trisArr, fillColor);
        }

        /// <summary>
        /// builds the full prism vertex soup (side walls + ear-clipped
        /// bottom/top caps + outline edges) for a CAM zone polygon
        /// </summary>
        private static void BuildCamZonePrismGeometry(
            Re4QuadExtremeEditor.src.Class.ObjMethods.NewAge_CAM_Zone_MethodsForGL MethodsForGL,
            ushort ID, Vector3[] pts, out float[] trisArr, out float[] edgesArr)
        {
            float yb = MethodsForGL.GetZoneBottom != null ? MethodsForGL.GetZoneBottom(ID) : 0f;
            float yt = MethodsForGL.GetZoneTop != null ? MethodsForGL.GetZoneTop(ID) : yb;
            if (yt < yb)
            {
                float t = yb; yb = yt; yt = t;
            }

            int n = pts.Length;
            List<Vector3> tris = new List<Vector3>(n * 4 * 3);
            List<float> edges = new List<float>(n * 3 * 2 * 3);
            Action<Vector3, Vector3, Vector3> tri = delegate (Vector3 p, Vector3 q, Vector3 r)
            {
                tris.Add(p); tris.Add(q); tris.Add(r);
            };
            Action<Vector3, Vector3> seg = delegate (Vector3 p, Vector3 q)
            {
                edges.Add(p.X); edges.Add(p.Y); edges.Add(p.Z);
                edges.Add(q.X); edges.Add(q.Y); edges.Add(q.Z);
            };

            // proper ear-clipping instead of a fixed fan: game zones can be
            // concave or even multi-lobe (r111 zone 3 is a quad+hexagon ring),
            // which a fan fills wrong ("half filled" look)
            List<int> capIndices = TriangulatePolygonXZ(pts);

            for (int i = 0; i < n; i++)
            {
                Vector3 b0 = new Vector3(pts[i].X, yb, pts[i].Z);
                Vector3 b1 = new Vector3(pts[(i + 1) % n].X, yb, pts[(i + 1) % n].Z);
                Vector3 t0 = new Vector3(pts[i].X, yt, pts[i].Z);
                Vector3 t1 = new Vector3(pts[(i + 1) % n].X, yt, pts[(i + 1) % n].Z);

                tri(b0, b1, t1); tri(b0, t1, t0);          // side wall
                seg(b0, b1); seg(t0, t1); seg(b0, t0);     // outline
            }

            for (int k = 0; k + 2 < capIndices.Count; k += 3)
            {
                int ia = capIndices[k];
                int ib = capIndices[k + 1];
                int ic = capIndices[k + 2];
                tri(new Vector3(pts[ia].X, yb, pts[ia].Z),
                    new Vector3(pts[ib].X, yb, pts[ib].Z),
                    new Vector3(pts[ic].X, yb, pts[ic].Z));      // bottom cap
                tri(new Vector3(pts[ia].X, yt, pts[ia].Z),
                    new Vector3(pts[ic].X, yt, pts[ic].Z),
                    new Vector3(pts[ib].X, yt, pts[ib].Z));      // top cap
            }

            trisArr = new float[tris.Count * 3];
            for (int i = 0; i < tris.Count; i++)
            {
                trisArr[i * 3] = tris[i].X;
                trisArr[i * 3 + 1] = tris[i].Y;
                trisArr[i * 3 + 2] = tris[i].Z;
            }
            edgesArr = edges.ToArray();
        }

        /// <summary>
        /// ear-clipping triangulation of a simple polygon projected on the
        /// XZ plane; handles concave and multi-lobe outlines. Winding is
        /// normalized first, and a fan fallback guarantees termination on
        /// degenerate (collinear) input.
        /// </summary>
        private static List<int> TriangulatePolygonXZ(Vector3[] pts)
        {
            List<int> result = new List<int>();
            int n = pts.Length;
            if (n < 3)
            {
                return result;
            }

            // signed area on (x,z); positive means counter-clockwise seen from above
            double area2 = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area2 += (double)pts[i].X * pts[j].Z - (double)pts[j].X * pts[i].Z;
            }

            List<int> ring = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                ring.Add(i);
            }
            if (area2 < 0)
            {
                ring.Reverse();
            }

            int guard = 0;
            int maxGuard = n * n + 16;
            while (ring.Count > 3 && guard++ < maxGuard)
            {
                bool clipped = false;
                for (int k = 0; k < ring.Count; k++)
                {
                    int a = ring[(k + ring.Count - 1) % ring.Count];
                    int b = ring[k];
                    int c = ring[(k + 1) % ring.Count];

                    OpenTK.Vector2 pa = new OpenTK.Vector2(pts[a].X, pts[a].Z);
                    OpenTK.Vector2 pb = new OpenTK.Vector2(pts[b].X, pts[b].Z);
                    OpenTK.Vector2 pc = new OpenTK.Vector2(pts[c].X, pts[c].Z);

                    float cross = (pb.X - pa.X) * (pc.Y - pa.Y) - (pb.Y - pa.Y) * (pc.X - pa.X);
                    if (cross <= 1e-6f)
                    {
                        continue;   // reflex or degenerate corner
                    }

                    bool containsOther = false;
                    foreach (int other in ring)
                    {
                        if (other == a || other == b || other == c) continue;
                        OpenTK.Vector2 p = new OpenTK.Vector2(pts[other].X, pts[other].Z);
                        if (PointInTriangleXZ(p, pa, pb, pc))
                        {
                            containsOther = true;
                            break;
                        }
                    }
                    if (containsOther)
                    {
                        continue;
                    }

                    result.Add(a); result.Add(b); result.Add(c);
                    ring.RemoveAt(k);
                    clipped = true;
                    break;
                }

                if (!clipped)
                {
                    // no valid ear left (collinear rest): fan to guarantee progress
                    for (int k = 1; k + 1 < ring.Count; k++)
                    {
                        result.Add(ring[0]); result.Add(ring[k]); result.Add(ring[k + 1]);
                    }
                    ring.Clear();
                }
            }

            if (ring.Count == 3)
            {
                result.Add(ring[0]); result.Add(ring[1]); result.Add(ring[2]);
            }
            return result;
        }

        private static bool PointInTriangleXZ(OpenTK.Vector2 p, OpenTK.Vector2 a, OpenTK.Vector2 b, OpenTK.Vector2 c)
        {
            float d1 = SignXZ(p, a, b);
            float d2 = SignXZ(p, b, c);
            float d3 = SignXZ(p, c, a);
            bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }

        private static float SignXZ(OpenTK.Vector2 p1, OpenTK.Vector2 p2, OpenTK.Vector2 p3)
        {
            return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        }

        private static void BuildPyramidGeometry(Vector3 pos, Vector3 tgt, out float[] tris, out float[] edges)
        {
            // JADERLINK marker shape (centroid == position) rotated as one rigid body
            // with the aim direction, like a camera object in Blender
            Vector3 fwd = tgt - pos;
            float flen = fwd.Length;
            if (flen < 0.0001f) { fwd = new Vector3(0f, 0f, 1f); }
            else { fwd = fwd / flen; }

            Vector3 right = Vector3.Cross(new Vector3(0f, 1f, 0f), fwd);
            if (right.Length < 0.0001f) { right = new Vector3(1f, 0f, 0f); }
            else { right = right / right.Length; }
            Vector3 up = Vector3.Cross(fwd, right);

            Func<float, float, float, Vector3> local = (x, y, z) =>
                pos + (right * x) + (up * y) + (fwd * z);

            Vector3 A = local(0f, 1.125f, -0.375f);
            Vector3 B = local(1.5f, -0.375f, 1.125f);
            Vector3 C = local(-1.5f, -0.375f, 1.125f);
            Vector3 D = local(0f, -0.375f, -1.875f);

            List<float> tv = new List<float>(12 * 3);
            tv.AddRange(new float[] { A.X, A.Y, A.Z, B.X, B.Y, B.Z, C.X, C.Y, C.Z });
            tv.AddRange(new float[] { A.X, A.Y, A.Z, B.X, B.Y, B.Z, D.X, D.Y, D.Z });
            tv.AddRange(new float[] { A.X, A.Y, A.Z, C.X, C.Y, C.Z, D.X, D.Y, D.Z });
            tv.AddRange(new float[] { B.X, B.Y, B.Z, C.X, C.Y, C.Z, D.X, D.Y, D.Z });

            List<float> ev = new List<float>(6 * 2 * 3);
            Action<Vector3, Vector3> seg = (p, q) => ev.AddRange(new float[] { p.X, p.Y, p.Z, q.X, q.Y, q.Z });
            seg(A, B); seg(A, C); seg(A, D); seg(B, C); seg(B, D); seg(C, D);

            tris = tv.ToArray();
            edges = ev.ToArray();
        }

        private static void RenderFileCAM_Cameras(RenderMode mode)
        {
            if (Globals.RenderFileCAM && DataBase.NodeCAM != null && DataBase.NodeCAM.MethodsForGL != null)
            {
                NewAge_CAM_MethodsForGL MethodsForGL = DataBase.NodeCAM.MethodsForGL;
                foreach (TreeNode item in DataBase.NodeCAM.Nodes)
                {
                        if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    ushort ID = ((Object3D)item).ObjLineRef;

                    if (!MethodsForGL.GetHasData(ID))
                    {
                        continue;
                    }

                    Vector3 pos = MethodsForGL.GetCameraPosition(ID);
                    Vector3 tgt = MethodsForGL.GetCameraTarget(ID);

                    byte[] partColor = BitConverter.GetBytes(ID);
                    Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.CAM / 255f, 1f);

                    Vector4 mColor = MethodsForGL.GetCameraColor != null ? MethodsForGL.GetCameraColor(ID) : Globals.GL_ColorCAM;
                    if (mode == RenderMode.BoxMode && DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                    {
                        mColor = Globals.GL_ColorSelected;
                    }

                    if (mode == RenderMode.SelectMode)
                    {
                        RenderAppModel.NoneBoundingBoxToSelect(boundNoneCAM, -boundNoneCAM, new RspFix(Vector3.One, pos, Matrix4.Identity), useColor);
                        DebugCamCamsDrawn++;
                        continue;
                    }

                    // keyframe marker: JADERLINK-style pyramid + aim line + target marker
                    float[] tris;
                    float[] edges;
                    BuildPyramidGeometry(pos, tgt, out tris, out edges);
                    CamZoneRender.DrawMeshWithEdges(tris, edges, mColor, mColor);
                    DebugCamCamsDrawn++;

                    Vector3 aim = tgt - pos;
                    float length = aim.Length;
                    if (length > 0.01f)
                    {
                        Vector4 lineColor = new Vector4(mColor.X * 0.7f, mColor.Y * 0.7f, mColor.Z * 0.7f, 1f);
                        CamZoneRender.DrawLines(new float[] { pos.X, pos.Y, pos.Z, tgt.X, tgt.Y, tgt.Z }, lineColor);

                        RspFix rspFixTgt = new RspFix(Vector3.One, tgt, Matrix4.Identity);
                        RenderAppModel.NoneBoundingBoxViewer(new Vector3(0.6f, 0.6f, 0.6f), new Vector3(-0.6f, -0.6f, -0.6f), rspFixTgt, mColor);
                    }
                }
            }

        }


        /// <summary>
        /// enemy route network (RTP): small cube per node + link lines between them
        /// </summary>
        private static void RenderFileRTP_Nodes(RenderMode mode)
        {
            if (Globals.RenderFileRTP && DataBase.NodeRTP != null && DataBase.NodeRTP.MethodsForGL != null && DataBase.FileRTP != null)
            {
                NewAge_RTP_MethodsForGL MethodsForGL = DataBase.NodeRTP.MethodsForGL;
                TreeNode[] snap;
                try { snap = new TreeNode[DataBase.NodeRTP.Nodes.Count]; DataBase.NodeRTP.Nodes.CopyTo(snap, 0); }
                catch { return; }
                foreach (TreeNode item in snap)
                {
                    if (item == null) continue;
                    if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                    ushort ID = ((Object3D)item).ObjLineRef;

                    byte[] partColor = BitConverter.GetBytes(ID);
                    Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)GroupType.RTP / 255f, 1f);

                    Vector4 mColor = Globals.GL_ColorRTP;
                    if (mode == RenderMode.BoxMode && DataBase.SelectedNodes.ContainsKey(item.GetHashCode()))
                    {
                        mColor = Globals.GL_ColorSelected;
                    }

                    Vector3 pos = MethodsForGL.GetNodePosition(ID);

                    if (mode == RenderMode.SelectMode)
                    {
                        RenderAppModel.NoneBoundingBoxToSelect(boundNoneRTP, -boundNoneRTP, new RspFix(Vector3.One, pos, Matrix4.Identity), useColor);
                        continue;
                    }

                    RenderAppModel.NoneBoundingBoxViewer(boundNoneRTP, -boundNoneRTP, new RspFix(Vector3.One, pos, Matrix4.Identity), mColor);
                }

                // Links are drawn as a final overlay after the room (see RenderFileRTP_LinksOverlay) so they are never hidden behind terrain
                // Keep only nodes here
            }
        }

        private static void RenderFileRTP_NodesOverlay()
        {
            if (!Globals.RenderFileRTP || DataBase.NodeRTP == null || DataBase.NodeRTP.MethodsForGL == null || DataBase.FileRTP == null) return;
            var MethodsForGL2 = DataBase.NodeRTP.MethodsForGL;
            TreeNode[] snap;
            try { snap = new TreeNode[DataBase.NodeRTP.Nodes.Count]; DataBase.NodeRTP.Nodes.CopyTo(snap, 0); }
            catch { return; }
            foreach (TreeNode item in snap)
            {
                if (item == null) continue;
                if (Re4QuadExtremeEditor.src.Class.IsolateFilter.IsBlocked(item)) continue;
                ushort ID = ((Object3D)item).ObjLineRef;
                Vector4 mColor = Globals.GL_ColorRTP;
                if (DataBase.SelectedNodes.ContainsKey(item.GetHashCode())) mColor = Globals.GL_ColorSelected;
                Vector3 pos = MethodsForGL2.GetNodePosition(ID);
                // Draw again on top of transparent triggers with a tiny depth bias so it never hides inside a trigger
                GL.Enable(EnableCap.PolygonOffsetFill);
                GL.PolygonOffset(-1.0f, -1.0f);
                RenderAppModel.NoneBoundingBoxViewer(boundNoneRTP, -boundNoneRTP, new RspFix(Vector3.One, pos, Matrix4.Identity), mColor);
                GL.Disable(EnableCap.PolygonOffsetFill);
            }
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X) &&
                   !float.IsNaN(v.Y) && !float.IsInfinity(v.Y) &&
                   !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }

        private static void RenderFileRTP_LinksOverlay()
        {
            if (!Globals.RenderFileRTP || DataBase.FileRTP == null) return;
            // FIX: always use FileRTP as ground truth after Force Reload.
            // Previous version used NodeRTP.MethodsForGL which becomes stale after
            // Force Reload (old delegate returns 18 segs while FileRTP has 19).
            // Now fallback is primary.
            List<Vector3[]> segs = null;
            List<Vector3[]> fallback = null;
            if (DataBase.FileRTP.Nodes.Count > 1)
            {
                try
                {
                    fallback = new List<Vector3[]>();
                    for (ushort i = 0; i < DataBase.FileRTP.Nodes.Count; i++)
                    {
                        var entries = DataBase.FileRTP.GetNodeEntries(i);
                        foreach (var e in entries)
                        {
                            if (e.TargetNode >= DataBase.FileRTP.Nodes.Count || e.TargetNode <= i) continue;
                            Vector3 p1 = new Vector3(DataBase.FileRTP.Nodes[i].GameX / 100f, DataBase.FileRTP.Nodes[i].GameY / 100f, DataBase.FileRTP.Nodes[i].GameZ / 100f);
                            Vector3 p2 = new Vector3(DataBase.FileRTP.Nodes[e.TargetNode].GameX / 100f, DataBase.FileRTP.Nodes[e.TargetNode].GameY / 100f, DataBase.FileRTP.Nodes[e.TargetNode].GameZ / 100f);
                            if (!IsFinite(p1) || !IsFinite(p2)) continue;
                            fallback.Add(new Vector3[] { p1, p2 });
                        }
                    }
                }
                catch { fallback = null; }
            }
            // primary is now fallback; keep old path as secondary for compatibility
            segs = fallback;
            if (segs == null || segs.Count == 0)
            {
                try
                {
                    if (DataBase.NodeRTP != null && DataBase.NodeRTP.MethodsForGL != null && DataBase.NodeRTP.MethodsForGL.GetLinkSegments != null)
                        segs = DataBase.NodeRTP.MethodsForGL.GetLinkSegments();
                }
                catch { }
                if ((segs == null || segs.Count == 0) && fallback != null && fallback.Count > 0) segs = fallback;
            }
            if (segs == null || segs.Count == 0) return;
            float[] lines = new float[segs.Count * 6];
            int o = 0;
            foreach (var s in segs) { lines[o++] = s[0].X; lines[o++] = s[0].Y; lines[o++] = s[0].Z; lines[o++] = s[1].X; lines[o++] = s[1].Y; lines[o++] = s[1].Z; }
            // Small lift to avoid z-fighting with ground, keep depth test so walls correctly occlude (no wall-hack)
            float yLift = 0.12f;
            for (int i = 1; i < lines.Length; i += 3) lines[i] += yLift;
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.LineWidth(3.2f);
            CamZoneRender.DrawLines(lines, Globals.GL_ColorRTP_Link);
            GL.LineWidth(1.5f);
        }

        private static void Render_Any_TriggerZone(ushort ID, GroupType groupType, BaseTriggerZoneMethodsForGL MethodsForGL, RenderMode mode, Vector4 mColor)
        {
            byte[] partColor = BitConverter.GetBytes(ID);
            Vector4 useColor = new Vector4(partColor[0] / 255f, partColor[1] / 255f, (byte)groupType / 255f, 1f);

            if (mode == RenderMode.BoxMode)
            {
                RenderTriggerZoneViewer(ID, MethodsForGL, mColor);
            }
            else if (mode == RenderMode.SelectMode)
            {
                RenderTriggerZoneSolid(ID, MethodsForGL, useColor);
            }
        }


        // triggerZone
        private static void RenderTriggerZoneViewer(ushort ID, BaseTriggerZoneMethodsForGL MethodsForGL, Vector4 color) 
        {
            if (MethodsForGL.GetZoneCategory(ID) == TriggerZoneCategory.Category01)
            {
                RenderAppModel.TriggerZoneBoxViewer(MethodsForGL.GetTriggerZoneMatrix4(ID), color);
            }
            else if (MethodsForGL.GetZoneCategory(ID) == TriggerZoneCategory.Category02)
            {
                RenderAppModel.TriggerZoneCircleViewer(MethodsForGL.GetTriggerZoneMatrix4(ID), color);
            }
        }

        private static void RenderTriggerZoneSolid(ushort ID, BaseTriggerZoneMethodsForGL MethodsForGL, Vector4 useColor) 
        {
            if (MethodsForGL.GetZoneCategory(ID) == TriggerZoneCategory.Category01)
            {
                RenderAppModel.TriggerZoneBoxSolid(MethodsForGL.GetTriggerZoneMatrix4(ID), useColor);
            }
            else if (MethodsForGL.GetZoneCategory(ID) == TriggerZoneCategory.Category02)
            {
                RenderAppModel.TriggerZoneCircleSolid(MethodsForGL.GetTriggerZoneMatrix4(ID), useColor);
            }
        }

        private static void TriggerZoneTransparentSolid(ushort ID, BaseTriggerZoneMethodsForGL MethodsForGL, Vector4 frontColor, Vector4 backColor)
        {
            if (MethodsForGL.GetZoneCategory(ID) == TriggerZoneCategory.Category01)
            {
                RenderAppModel.TriggerZoneBoxTransparentSolid(MethodsForGL.GetTriggerZoneMatrix4(ID), frontColor, backColor);
            }
            else if (MethodsForGL.GetZoneCategory(ID) == TriggerZoneCategory.Category02)
            {
                RenderAppModel.TriggerZoneCircleTransparentSolid(MethodsForGL.GetTriggerZoneMatrix4(ID), frontColor, backColor);
            }
        }

        private static Vector4 TriggerZoneGetBackColor(Vector4 frontColor) 
        {
            Vector4 backColor = new Vector4(frontColor.X - 0.4f, frontColor.Y - 0.4f, frontColor.Z - 0.4f, 0.4f);
            backColor.X = backColor.X < 0 ? 0 : backColor.X;
            backColor.Y = backColor.Y < 0 ? 0 : backColor.Y;
            backColor.Z = backColor.Z < 0 ? 0 : backColor.Z;
            return backColor;
        }

        private const float TriggerZoneTransparentLevel = 0.2f;


        private enum RenderMode : byte
        {
            SelectMode,
            BoxMode,
            ModelMode
        }
    }
}
