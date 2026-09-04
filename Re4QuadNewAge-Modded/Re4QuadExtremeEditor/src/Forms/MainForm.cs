using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Re4QuadExtremeEditor.src;
using Re4QuadExtremeEditor.src.Class;
using Re4QuadExtremeEditor.src.Class.TreeNodeObj;
using Re4QuadExtremeEditor.src.Forms;
using Re4QuadExtremeEditor.src.Class.Enums;
using Re4QuadExtremeEditor.src.Class.MyProperty;
using Re4QuadExtremeEditor.src.Class.MyProperty.CustomAttribute;
using Re4QuadExtremeEditor.src.Class.ObjMethods;
using Re4QuadExtremeEditor.src.Class.Files;
using Re4QuadExtremeEditor.src.Class.Shaders;
using Re4QuadExtremeEditor.src.Controls;
using Re4QuadExtremeEditor.src.Class.MyProperty._EFF_Property;
using NsCamera;
using System.IO;
using SimpleEndianBinaryIO;


namespace Re4QuadExtremeEditor
{
    public partial class MainForm : Form
    {
        GLControl glControl;
        readonly Timer myTimer = new Timer();

        CameraMoveControl cameraMove;
        ObjectMoveControl objectMove;
        Advertising1Control advertising1Control;
        Advertising2Control advertising2Control;

        #region Camera // variaveis para a camera
        Camera camera = new Camera();

        /// <summary>Camera access for the .quad project system.</summary>
        public Camera ActiveCamera => camera;
        Matrix4 camMtx = Matrix4.Identity;
        Matrix4 ProjMatrix;
        // movimentação da camera
        bool isShiftDown = false, isControlDown = false, isSpaceDown = false;

        // C descends in FLY mode; Shift was freed up for box selection
        bool isCDown = false;
        bool isMouseDown = false, isMouseMove = false;
        //per-frame camera input buffering: mouse-look deltas are accumulated
        //as they arrive and applied once per rendered frame with dt-scaled
        //WASD steps, so camera motion stays perfectly even at any framerate
        private float pendingLookDX = 0f, pendingLookDY = 0f;
        private Point lastRotateMousePos;
        private bool rotateTrackingValid = false;
        private long lastCameraFrameTick = -1;
        private bool altSlowWasOn = false;
        //arrow-key nudging of the selected objects while the select button is held
        private bool arrowNudgeActive = false;
        private List<Re4QuadExtremeEditor.src.Class.TreeNodeObj.Object3D> arrowNudgeObjects;
        private List<Vector3> arrowNudgeStartPositions;
        bool isWDown = false, isSDown = false, isADown = false, isDDown = false;
        //movimentação camera no glControl
        MouseButtons MouseButtonsLeft = MouseButtons.Right; //botão para movimentação camera
        MouseButtons MouseButtonsRight = MouseButtons.Left; // botão para selecionar objeto
        public void ApplyInvertedMouseButtons()
        {
            if (Globals.BackupConfigs != null && Globals.BackupConfigs.UseInvertedMouseButtons)
            {
                MouseButtonsLeft = MouseButtons.Left;
                MouseButtonsRight = MouseButtons.Right;
            }
            else
            {
                MouseButtonsLeft = MouseButtons.Right;
                MouseButtonsRight = MouseButtons.Left;
            }
        }
        #endregion

        // Property que fica no PropertyGrid quando não tem nada selecionado;
        readonly NoneProperty none = new NoneProperty();

        // define se esta com o PropertyGrid selecionado;
        bool InPropertyGrid = false;

        UpdateMethods updateMethods;

        public MainForm()
        {
            SplashScreen.StartSplashScreen();

            InitializeComponent();
            try { this.Text = "RE4 QUAD EXTREME EDITOR [NEW AGE] | V1.2.5 FIX | YOUTUBE.COM/@JADERLINK"; } catch { }

            propertyGridObjs.SelectedObject = none;
            DataBase.SelectedNodes = treeViewObjs.SelectedNodes; // vinculo de referencia entra as listas

            Re4QuadExtremeEditor.src.Class.Gizmo.TransformApplied += UpdatePropertyGrid;

            // Undo support for edits made through the property grid ("control
            // panel"): snapshot the object position whenever a row is entered,
            // and on commit push a move command if the value really changed.
            propertyGridObjs.SelectedGridItemChanged += delegate { SnapshotGridUndoPosition(); };
            propertyGridObjs.SelectedObjectsChanged += delegate { SnapshotGridUndoPosition(); };
            propertyGridObjs.PropertyValueChanged += PropertyGridObjs_PropertyValueChanged;

            // Editor action console (ported concept from Re4QuadX):
            // a collapsible bottom panel logging every relevant action.
            BuildConsolePanel();
            SetupRoomToolsMenu();
            EditorConsole.Log("Editor started");

            // Undo/redo notifications already surface through this callback.
            Re4QuadExtremeEditor.src.Class.UndoSystem.Notify += delegate (string msg)
            {
                EditorConsole.Log(msg);
            };

            SetupBulkFileMenuItems();
            WireSetupWizardMenu();

            //Blender-style .quad project support (File menu)
            WireProjectMenu();

            glControl = new OpenTK.GLControl();
            glControl.Dock = DockStyle.Fill;
            glControl.Name = "glControl";
            glControl.TabIndex = 999;
            glControl.TabStop = false;
            glControl.Paint += GlControl_Paint;
            glControl.Load += GlControl_Load;
            glControl.KeyDown += GlControl_KeyDown;
            glControl.KeyUp += GlControl_KeyUp;
            glControl.Leave += GlControl_Leave;
            glControl.MouseWheel += GlControl_MouseWheel;
            glControl.MouseMove += GlControl_MouseMove;
            glControl.MouseDown += GlControl_MouseDown;
            glControl.MouseUp += GlControl_MouseUp;
            glControl.MouseLeave += GlControl_MouseLeave;
            glControl.Resize += GlControl_Resize;
            splitContainerRight.Panel1.Controls.Add(glControl);

            camera.getSelectedObject = getSelectedObject;

            cameraMove = new CameraMoveControl(ref camera, UpdateGL, UpdateCameraMatrix);
            cameraMove.EnterCamViewRequested += ToggleEnterCamView;
            cameraMove.FovTargetSetter = SetFovTarget;
            cameraMove.InitFov(Globals.FOV);
            cameraMove.Location = new Point(splitContainerRight.Panel2.Width - cameraMove.Width, 0);
            cameraMove.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            cameraMove.Name = "cameraMove";
            cameraMove.TabIndex = 998;
            cameraMove.TabStop = false;
           
            objectMove = new ObjectMoveControl(ref camera, UpdateGL, UpdateCameraMatrix, UpdatePropertyGrid, UpdateTreeViewObjs);
            objectMove.Location = new Point(0, 0);
            objectMove.Anchor = AnchorStyles.Right | AnchorStyles.Bottom | AnchorStyles.Left;
            objectMove.Name = "objectMove";
            objectMove.TabIndex = 995;
            objectMove.TabStop = false;
           
            advertising1Control = new Advertising1Control();
            advertising1Control.Location = new Point(0, 0);
            advertising1Control.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            advertising1Control.Name = "advertising1Control";
            advertising1Control.TabIndex = 997;
            advertising1Control.TabStop = false;
            advertising1Control.Hide();

            advertising2Control = new Advertising2Control();
            advertising2Control.Location = new Point(0, 0);
            advertising2Control.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            advertising2Control.Name = "advertising2Control";
            advertising2Control.TabIndex = 996;
            advertising2Control.TabStop = false;
            advertising2Control.Hide();

            controlsTab.Controls.Add(cameraMove);
            controlsTab.Controls.Add(advertising1Control);
            controlsTab.Controls.Add(advertising2Control);
            controlsTab.Controls.Add(objectMove);
            enable_splitContainerRight_Panel2_Resize = true;

            KeyPreview = true;

            myTimer.Tick += updateWASDControls;
            myTimer.Interval = 10;
            myTimer.Enabled = false;

            camMtx = camera.GetViewMatrix();
            ProjMatrix = ReturnNewProjMatrix();

            // todos os metodos listados abaixos, tem que seguir a sequencia abaixo, se não dara erro.

            Lang.StartAttributeTexts();
            Lang.StartTexts();
            Lang.StartOthersTextsSafe();
            Lang.SnapshotEnglishDefaults();

            src.JSON.Configs.StartLoadConfigs();
            Utils.StartReloadDirectoryDic();
            Utils.StartLoadObjsInfoLists();
            Utils.StartLoadPromptMessageList();
            Globals.BackupConfigs.LoadLangTranslation = false;
            Utils.StartLoadLangFile();
            Utils.StartEnemyExtraSegmentList();
            Utils.StartSetListBoxsProperty();
            Utils.StartSetListBoxsPropertybjsInfoLists();
            if (Lang.LoadedTranslation) 
            { 
                StartUpdateTranslation();
                cameraMove.StartUpdateTranslation();
                objectMove.StartUpdateTranslation();
            }

            Utils.StartCreateNodes();
            Utils.StartExtraGroup();
            treeViewObjs.Nodes.Add(DataBase.NodeESL);
            treeViewObjs.Nodes.Add(DataBase.NodeETS);
            treeViewObjs.Nodes.Add(DataBase.NodeITA);
            treeViewObjs.Nodes.Add(DataBase.NodeAEV);
            treeViewObjs.Nodes.Add(DataBase.NodeEXTRAS);
            treeViewObjs.Nodes.Add(DataBase.NodeDSE);
            treeViewObjs.Nodes.Add(DataBase.NodeSMX);
            treeViewObjs.Nodes.Add(DataBase.NodeAVL);
            treeViewObjs.Nodes.Add(DataBase.NodeCAM);
            treeViewObjs.Nodes.Add(DataBase.NodeCAM_Zone);
            treeViewObjs.Nodes.Add(DataBase.NodeRTP);
            treeViewObjs.Nodes.Add(DataBase.NodeFSE);
            treeViewObjs.Nodes.Add(DataBase.NodeEAR);
            treeViewObjs.Nodes.Add(DataBase.NodeSAR);
            treeViewObjs.Nodes.Add(DataBase.NodeEMI);
            treeViewObjs.Nodes.Add(DataBase.NodeESE);
            treeViewObjs.Nodes.Add(DataBase.NodeQuadCustom);
            treeViewObjs.Nodes.Add(DataBase.NodeLIT_Groups);
            treeViewObjs.Nodes.Add(DataBase.NodeLIT_Entrys);
            treeViewObjs.Nodes.Add(DataBase.NodeEFF_Table0);
            treeViewObjs.Nodes.Add(DataBase.NodeEFF_Table1);
            treeViewObjs.Nodes.Add(DataBase.NodeEFF_Table2);
            treeViewObjs.Nodes.Add(DataBase.NodeEFF_Table3);
            treeViewObjs.Nodes.Add(DataBase.NodeEFF_Table4);
            treeViewObjs.Nodes.Add(DataBase.NodeEFF_Table6);
            treeViewObjs.Nodes.Add(DataBase.NodeEFF_Table7_Effect_0);
            treeViewObjs.Nodes.Add(DataBase.NodeEFF_Table8_Effect_1);
            treeViewObjs.Nodes.Add(DataBase.NodeEFF_EffectEntry);
            treeViewObjs.Nodes.Add(DataBase.NodeEFF_Table9);

            updateMethods = new UpdateMethods();
            updateMethods.UpdateGL = UpdateGL;
            updateMethods.UpdatePropertyGrid = UpdatePropertyGrid;
            updateMethods.UpdateTreeViewObjs = UpdateTreeViewObjs;
            updateMethods.UpdateMoveObjSelection = objectMove.UpdateSelection;
            updateMethods.UpdateOrbitCamera = UpdateOrbitCamera;

            // UX pack: undo/redo, isolate, duplicate, snap keys, rubber band,
            // recent files, FPS limiter menu and periodic backups.
            WireUxPack();

            // Drag-and-drop: accept RE4 mod files dropped onto the viewport.
            AllowDrop = true;
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;

            ApplyInvertedMouseButtons();
            try { toolStripMenuItemHideItemAura.Checked = !Globals.RenderItemAura; } catch { }

            //apenas para testes, cria um arquivo para tradução
            //src.JSON.LangFile.WriteToLangFile("SourceLang.json");
            //int finish = 0;

            // Build and theme the modern dark shell before the form is first painted.
            // This removes the visible light-theme -> dark-theme transition.
            MainForm_ShownModernUI(this, EventArgs.Empty);
        }

        #region GlControl Events

        // smooth FOV transition state (degrees)
        private float fovCurrent = 60f;
        private float fovTarget = 60f;
        private long lastPaintStopwatchMs = -1;

        private void SetFovTarget(float degrees)
        {
            if (degrees < 20f) degrees = 20f;
            if (degrees > 130f) degrees = 130f;
            fovTarget = degrees;
        }

        private Matrix4 ReturnNewProjMatrix()
        {
            //near plane of 0.01 with far = 1M destroyed depth-buffer precision:
            //surfaces started z-fighting and objects visibly shimmered/jittered
            //whenever the camera moved. 0.5 keeps a huge usable range while
            //making depth precision ~50x better, without clipping geometry
            //when zoomed right up against a bounding box.
            return Matrix4.CreatePerspectiveFieldOfView(Globals.FOV * ((float)Math.PI / 180.0f), (float)glControl.Width / (float)glControl.Height, 0.5f, 1_000_000f);
        }

        private void GlControl_Resize(object sender, EventArgs e)
        {
            glControl.Context.Update(glControl.WindowInfo);
            GL.Viewport(0, 0, glControl.Width, glControl.Height);
            ProjMatrix = ReturnNewProjMatrix();
            glControl.Invalidate();
        }

        public void InvalidateViewport()
        {
            if (glControl != null && !glControl.IsDisposed)
            {
                glControl.Invalidate();
                glControl.Update();
            }
        }

        public void ApplyTranslationLive()
        {
            StartUpdateTranslation();
            cameraMove.StartUpdateTranslation();
            objectMove.StartUpdateTranslation();
            Utils.UpdateNodeTextsLive();
            Utils.StartSetListBoxsProperty();
            treeViewObjs.Refresh();
            if (DataBase.LastSelectNode != null && treeViewObjs.SelectedNode != null)
            {
                treeViewObjs_AfterSelect(treeViewObjs, new TreeViewEventArgs(treeViewObjs.SelectedNode));
            }
        }

        private void splitContainerMain_SplitterMoving(object sender, SplitterCancelEventArgs e)
        {
            glControl.Invalidate();
        }

        private void GlControl_MouseLeave(object sender, EventArgs e)
        {
            // a rubber band interrupted by leaving the viewport is simply cancelled
            if (rbActive)
            {
                EraseRubberBandFrame();
                rbActive = false;
            }
            camera.resetMouseStuff();
            isMouseDown = false;
            isMouseMove = false;
        }

        private void GlControl_MouseUp(object sender, MouseEventArgs e)
        {
            // finish a rubber-band box selection before anything else
            if (e.Button == MouseButtonsRight && rbActive)
            {
                FinishRubberBandSelect(e.Location);
                camera.resetMouseStuff();
                return;
            }

            if ((e.Button == MouseButtonsRight || e.Button == MouseButtonsLeft) && Re4QuadExtremeEditor.src.Class.Gizmo.IsDragging)
            {
                Re4QuadExtremeEditor.src.Class.Gizmo.EndDrag();
                UpdatePropertyGrid();
                glControl.Invalidate();
                return;
            }
            if (e.Button == MouseButtonsLeft)
            {
                camera.resetMouseStuff();
                isMouseDown = false;
                isMouseMove = false;
                rotateTrackingValid = false;
                FinishArrowNudge();
                camera.SaveCameraPosition();
                if (!isWDown && !isSDown && !isADown && !isDDown && !isMouseMove && !isCDown && !isSpaceDown)
                {
                    myTimer.Enabled = false;
                }
            }    
        }

        private void GlControl_MouseDown(object sender, MouseEventArgs e)
        {
            //the gizmo arrows can be grabbed with either mouse button: with the
            //select button (as before) and also with the camera-rotate button,
            //so left-drag works on them in both mouse mappings
            if ((e.Button == MouseButtonsRight || e.Button == MouseButtonsLeft) && Re4QuadExtremeEditor.src.Class.Gizmo.Enabled)
            {
                bool consumed = Re4QuadExtremeEditor.src.Class.Gizmo.TryBeginDrag(e.X, e.Y, glControl.Width, glControl.Height,
                    camMtx, ProjMatrix, camera.Position, camera.Front);
                if (consumed)
                {
                    Re4QuadExtremeEditor.src.Class.Gizmo.dragLogCount = 0;
                    return;
                }
            }
            // Shift + left-drag on empty space = rubber-band box selection
            if (e.Button == MouseButtonsRight && (ModifierKeys & Keys.Shift) != 0)
            {
                rbActive = true;
                rbStartPoint = e.Location;
                rbLastFrame = Rectangle.Empty;
                UpdateRubberBandFrame(e.Location);
                return;
            }
            if (e.Button == MouseButtonsLeft)
            {
                camera.resetMouseStuff();
                lastRotateMousePos = e.Location;
                pendingLookDX = 0f;
                pendingLookDY = 0f;
                rotateTrackingValid = true;
                isMouseDown = true;
                isMouseMove = true;
                camera.SaveCameraPosition();
                myTimer.Enabled = true;
            }
            if (e.Button == MouseButtonsRight)
            {
                selectObject(e.X, e.Y);
                glControl.Invalidate();
            }
        }

        /// <summary>
        /// metodo destinado para a seleção dos objetos no ambiente GL
        /// </summary>
        private void selectObject(int mx, int my)
        {
            NewAgeTheRender.TheRender.AllRender(ref camMtx, ref ProjMatrix, camera.Position, camera.SelectedObjPosY(), true); // renderiza o ambiente GL no modo seleção.

            int h = glControl.Height;
            byte[] pixel = new byte[4];
            GL.ReadPixels(mx, h - my, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, pixel);

            //Console.WriteLine("pixel[0]: " + pixel[0]); // lineID
            //Console.WriteLine("pixel[1]: " + pixel[1]); // lineID
            //Console.WriteLine("pixel[2]: " + pixel[2]); // id da lista
            //Console.WriteLine("pixel[3]: " + pixel[3]);

            // listas
            // aviso: proibido usar os valores 0 e 255, pois fazem parte das cores preta (renderização do cenario) e da cor branca (fundo);
            if (pixel[2] > 0 && pixel[2] < 255)
            {
                ushort LineID = BitConverter.ToUInt16(pixel, 0);

                TreeNode selected = null;
                switch (pixel[2])
                {
                    case (byte)GroupType.ESL:
                        int index1 = DataBase.NodeESL.Nodes.IndexOfKey(LineID.ToString());
                        if (index1 > -1)
                        {
                            selected = DataBase.NodeESL.Nodes[index1];
                        }
                        break;
                    case (byte)GroupType.ETS:
                        int index2 = DataBase.NodeETS.Nodes.IndexOfKey(LineID.ToString());
                        if (index2 > -1)
                        {
                            selected = DataBase.NodeETS.Nodes[index2];
                        }
                        break;
                    case (byte)GroupType.ITA:
                        int index3 = DataBase.NodeITA.Nodes.IndexOfKey(LineID.ToString());
                        if (index3 > -1)
                        {
                            selected = DataBase.NodeITA.Nodes[index3];
                        }
                        break;
                    case (byte)GroupType.AEV:
                        int index4 = DataBase.NodeAEV.Nodes.IndexOfKey(LineID.ToString());
                        if (index4 > -1)
                        {
                            selected = DataBase.NodeAEV.Nodes[index4];
                        }
                        break;
                    case (byte)GroupType.EXTRAS:
                        int index5 = DataBase.NodeEXTRAS.Nodes.IndexOfKey(LineID.ToString());
                        if (index5 > -1)
                        {
                            selected = DataBase.NodeEXTRAS.Nodes[index5];
                        }
                        break;
                    case (byte)GroupType.EAR:
                        int index6 = DataBase.NodeEAR.Nodes.IndexOfKey(LineID.ToString());
                        if (index6 > -1)
                        {
                            selected = DataBase.NodeEAR.Nodes[index6];
                        }
                        break;
                    case (byte)GroupType.SAR:
                        int index7 = DataBase.NodeSAR.Nodes.IndexOfKey(LineID.ToString());
                        if (index7 > -1)
                        {
                            selected = DataBase.NodeSAR.Nodes[index7];
                        }
                        break;
                    case (byte)GroupType.EMI:
                        int index8 = DataBase.NodeEMI.Nodes.IndexOfKey(LineID.ToString());
                        if (index8 > -1)
                        {
                            selected = DataBase.NodeEMI.Nodes[index8];
                        }
                        break;
                    case (byte)GroupType.ESE:
                        int index9 = DataBase.NodeESE.Nodes.IndexOfKey(LineID.ToString());
                        if (index9 > -1)
                        {
                            selected = DataBase.NodeESE.Nodes[index9];
                        }
                        break;
                    case (byte)GroupType.FSE:
                        int index10 = DataBase.NodeFSE.Nodes.IndexOfKey(LineID.ToString());
                        if (index10 > -1)
                        {
                            selected = DataBase.NodeFSE.Nodes[index10];
                        }
                        break;
                    case (byte)GroupType.QUAD_CUSTOM:
                        int index11 = DataBase.NodeQuadCustom.Nodes.IndexOfKey(LineID.ToString());
                        if (index11 > -1)
                        {
                            selected = DataBase.NodeQuadCustom.Nodes[index11];
                        }
                        break;
                    case (byte)GroupType.LIT_ENTRYS:
                        int index12 = DataBase.NodeLIT_Entrys.Nodes.IndexOfKey(LineID.ToString());
                        if (index12 > -1)
                        {
                            selected = DataBase.NodeLIT_Entrys.Nodes[index12];
                        }
                        break;
                    case (byte)GroupType.EFF_EffectEntry:
                        int index13 = DataBase.NodeEFF_EffectEntry.Nodes.IndexOfKey(LineID.ToString());
                        if (index13 > -1)
                        {
                            selected = DataBase.NodeEFF_EffectEntry.Nodes[index13];
                        }
                        break;
                    case (byte)GroupType.EFF_Table7_Effect_0:
                        int index14 = DataBase.NodeEFF_Table7_Effect_0.Nodes.IndexOfKey(LineID.ToString());
                        if (index14 > -1)
                        {
                            selected = DataBase.NodeEFF_Table7_Effect_0.Nodes[index14];
                        }
                        break;
                    case (byte)GroupType.EFF_Table8_Effect_1:
                        int index15 = DataBase.NodeEFF_Table8_Effect_1.Nodes.IndexOfKey(LineID.ToString());
                        if (index15 > -1)
                        {
                            selected = DataBase.NodeEFF_Table8_Effect_1.Nodes[index15];
                        }
                        break;
                    case (byte)GroupType.EFF_Table9:
                        int index16 = DataBase.NodeEFF_Table9.Nodes.IndexOfKey(LineID.ToString());
                        if (index16 > -1)
                        {
                            selected = DataBase.NodeEFF_Table9.Nodes[index16];
                        }
                        break;
                    case (byte)GroupType.CAM:
                        int index17 = DataBase.NodeCAM.Nodes.IndexOfKey(LineID.ToString());
                        if (index17 > -1)
                        {
                            selected = DataBase.NodeCAM.Nodes[index17];
                        }
                        break;
                    case (byte)GroupType.CAM_ZONE:
                        int index18 = DataBase.NodeCAM_Zone.Nodes.IndexOfKey(LineID.ToString());
                        if (index18 > -1)
                        {
                            selected = DataBase.NodeCAM_Zone.Nodes[index18];
                        }
                        break;
                    case (byte)GroupType.RTP:
                        int index19 = DataBase.NodeRTP.Nodes.IndexOfKey(LineID.ToString());
                        if (index19 > -1)
                        {
                            selected = DataBase.NodeRTP.Nodes[index19];
                        }
                        break;
                }

                if (selected != null)
                {
                    if (isControlDown) // add ou remove da seleção
                    {
                        treeViewObjs.ToSelectMultiNode(selected);
                    }
                    else // seleciona so esse
                    {
                        treeViewObjs.ToSelectSingleNode(selected);
                    }

                }
            }
        }

        private void GlControl_MouseMove(object sender, MouseEventArgs e)
        {
            // live rubber-band rectangle
            if (rbActive)
            {
                UpdateRubberBandFrame(e.Location);
                return;
            }
            if (Re4QuadExtremeEditor.src.Class.Gizmo.IsDragging)
            {
                Re4QuadExtremeEditor.src.Class.Gizmo.UpdateDrag(e.X, e.Y, glControl.Width, glControl.Height,
                    camera.Position, camera.Front);
                return;
            }
            if (isMouseDown && e.Button == MouseButtonsLeft)
            {
                if (!isControlDown && camera.CamMode == Camera.CameraMode.FLY)
                {
                    //buffer the rotation and apply it once per rendered frame -
                    //WM_MOUSEMOVE arrives at an irregular rate which made the
                    //view judder while flying around
                    if (rotateTrackingValid)
                    {
                        pendingLookDX += e.X - lastRotateMousePos.X;
                        pendingLookDY += e.Y - lastRotateMousePos.Y;
                    }
                    lastRotateMousePos = e.Location;
                    rotateTrackingValid = true;
                }
                else
                {
                    //LOOK/ORBIT (ctrl-drag) keep the legacy immediate path
                    camera.updateCameraOffsetMatrixWithMouse(isControlDown, e.X, e.Y);
                    camMtx = camera.GetViewMatrix();
                }
            }
            else if (Re4QuadExtremeEditor.src.Class.Gizmo.Enabled)
            {
                int hov = Re4QuadExtremeEditor.src.Class.Gizmo.UpdateHover(e.X, e.Y, glControl.Width, glControl.Height,
                    camMtx, ProjMatrix);
                glControl.Cursor = hov >= 0 ? Cursors.Hand : Cursors.Default;
            }
        }

        private void GlControl_MouseWheel(object sender, MouseEventArgs e)
        {
            // Ctrl + wheel = smooth FOV zoom (pro-editor style), does not move the camera
            if ((ModifierKeys & Keys.Control) != 0)
            {
                SetFovTarget(fovTarget - e.Delta * 0.04f);
                cameraMove.SyncFovDisplay(fovTarget);
                glControl.Invalidate();
                return;
            }
            camera.resetMouseStuff();
            camera.updateCameraMatrixWithScrollWheel((int)(e.Delta * 0.5f));
            camMtx = camera.GetViewMatrix();
            camera.SaveCameraPosition();
            glControl.Invalidate();
        }

        private void GlControl_Leave(object sender, EventArgs e)
        {
            isWDown = false;
            isSDown = false;
            isADown = false;
            isDDown = false;
            isSpaceDown = false;
            isShiftDown = false;
            isCDown = false;
            isControlDown = false;
            isMouseDown = false;
            isMouseMove = false;
            rotateTrackingValid = false;
            pendingLookDX = 0f;
            pendingLookDY = 0f;
            FinishArrowNudge();
            myTimer.Enabled = false;
        }

        private void GlControl_KeyUp(object sender, KeyEventArgs e)
        {
            isShiftDown = e.Shift;
            isControlDown = e.Control;
            switch (e.KeyCode)
            {
                case Keys.W: isWDown = false; break;
                case Keys.S: isSDown = false; break;
                case Keys.A: isADown = false; break;
                case Keys.D: isDDown = false; break;
                case Keys.Space: isSpaceDown = false; break;
                case Keys.C: isCDown = false; break;
            }
            if (!isWDown && !isSDown && !isADown && !isDDown && !isMouseMove && !isCDown && !isSpaceDown)
            {
                myTimer.Enabled = false;
            }
            if (isControlDown)
            {
                camera.SaveCameraPosition();
                camera.resetMouseStuff();
            }
        }

        private void GlControl_KeyDown(object sender, KeyEventArgs e)
        {
            isShiftDown = e.Shift;
            isControlDown = e.Control;
            switch (e.KeyCode)
            {
                case Keys.W:
                    isWDown = true;
                    myTimer.Enabled = true;
                    break;
                case Keys.S:
                    isSDown = true;
                    myTimer.Enabled = true;
                    break;
                case Keys.A:
                    isADown = true;
                    myTimer.Enabled = true;
                    break;
                case Keys.D:
                    isDDown = true;
                    myTimer.Enabled = true;
                    break;
                case Keys.Space:
                    isSpaceDown = true;
                    myTimer.Enabled = true;
                    break;
                case Keys.C:
                    isCDown = true;
                    myTimer.Enabled = true;
                    break;
                case Keys.F8:
                    e.Handled = true;
                    OpenElementLibrary();
                    break;
            }

            //holding the select button + arrow keys nudges the selected objects
            if (isMouseDown)
            {
                float step = 10f * Re4QuadExtremeEditor.src.Class.MoveObj.objSpeedMultiplier;
                float dx = 0f, dy = 0f;
                switch (e.KeyCode)
                {
                    case Keys.Left: dx = -step; break;
                    case Keys.Right: dx = step; break;
                    case Keys.Up: dy = step; break;
                    case Keys.Down: dy = -step; break;
                }
                if (dx != 0f || dy != 0f)
                {
                    e.Handled = true;
                    StartOrContinueArrowNudge();
                    ApplyArrowNudge(dx, dy);
                    glControl.Invalidate();
                    return;
                }
            }

            if (isControlDown)
            {
                camera.SaveCameraPosition();
                camera.resetMouseStuff();
            }

        }

        /// <summary>
        /// Captures the selection state the first time an arrow nudge happens
        /// inside one press-hold session, so the whole session becomes a
        /// single undo step when the select button is released.
        /// </summary>
        private void StartOrContinueArrowNudge()
        {
            if (arrowNudgeActive) return;
            arrowNudgeActive = true;
            arrowNudgeObjects = new List<Re4QuadExtremeEditor.src.Class.TreeNodeObj.Object3D>();
            arrowNudgeStartPositions = new List<Vector3>();
            foreach (var item in DataBase.SelectedNodes.Values)
            {
                if (item is Re4QuadExtremeEditor.src.Class.TreeNodeObj.Object3D obj && item.Parent is TreeNodeGroup)
                {
                    Vector3[] p = obj.GetObjPostion_ToMove_General();
                    arrowNudgeObjects.Add(obj);
                    arrowNudgeStartPositions.Add(p != null && p.Length > 0 ? p[0] : Vector3.Zero);
                }
            }
        }

        private void ApplyArrowNudge(float dx, float dy)
        {
            if (arrowNudgeObjects == null || arrowNudgeObjects.Count == 0) return;
            for (int i = 0; i < arrowNudgeObjects.Count; i++)
            {
                var obj = arrowNudgeObjects[i];
                Vector3[] pos = obj.GetObjPostion_ToMove_General();
                if (pos == null || pos.Length < 1) continue;
                pos = (Vector3[])pos.Clone();
                pos[0] = new Vector3(pos[0].X + dx, pos[0].Y + dy, pos[0].Z);
                if (Re4QuadExtremeEditor.src.Class.MoveObj.KeepOnGround && DataBase.SelectedRoom != null)
                {
                    pos[0].Y = DataBase.SelectedRoom.DropToGround(pos[0]);
                }
                obj.SetObjPostion_ToMove_General(pos);
            }
            camera.UpdateCameraOrbitOnChangeValue();
        }

        /// <summary>
        /// Ends the arrow-nudge session and registers a single undo move.
        /// </summary>
        private void FinishArrowNudge()
        {
            if (!arrowNudgeActive) return;
            arrowNudgeActive = false;
            var objs = arrowNudgeObjects;
            var starts = arrowNudgeStartPositions;
            arrowNudgeObjects = null;
            arrowNudgeStartPositions = null;
            if (objs == null || objs.Count == 0) return;

            UndoSystem.PushMove(objs, starts, delegate ()
            {
                List<Vector3> current = new List<Vector3>();
                foreach (var o in objs)
                {
                    Vector3[] p = o.GetObjPostion_ToMove_General();
                    current.Add(p != null && p.Length > 0 ? p[0] : Vector3.Zero);
                }
                return current.ToArray();
            });

            UpdatePropertyGrid();
            glControl.Invalidate();
        }

        /// <summary>
        /// Advances all camera input exactly once per rendered frame:
        /// WASD/Space/C translation (delta-time scaled) then any buffered
        /// mouse-look rotation. Motion can never desync from the presentation
        /// rate, which keeps flying around perfectly smooth.
        /// </summary>
        private void UpdateCameraPerFrame(double dtSeconds)
        {
            if (!isControlDown && camera.CamMode == Camera.CameraMode.FLY)
            {
                //192 units/s at multiplier 1 - matches the feel of the old
                //15.6ms WinForms timer ticks (3 units each)
                float step = 192f * camera.CamSpeedMultiplier * (float)dtSeconds;

                //hold Alt for slow, precise movement (~10% speed)
                bool altHeld = (Control.ModifierKeys & Keys.Alt) != 0;
                if (altHeld)
                {
                    step *= 0.1f;
                }
                if (altHeld != altSlowWasOn)
                {
                    altSlowWasOn = altHeld;
                    if (altHeld) Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Precision move");
                }

                bool moved = false;
                if (isWDown) { camera.MoveFront(step); moved = true; }
                if (isSDown) { camera.MoveBack(step); moved = true; }
                if (isDDown) { camera.MoveRight(step); moved = true; }
                if (isADown) { camera.MoveLeft(step); moved = true; }
                if (isSpaceDown) { camera.MoveUpCam(step); moved = true; }
                if (isCDown) { camera.MoveDownCam(step); moved = true; }
                if (moved) { camMtx = camera.GetViewMatrix(); }
            }

            if (pendingLookDX != 0f || pendingLookDY != 0f)
            {
                camera.ApplyLookDelta(pendingLookDX, pendingLookDY);
                pendingLookDX = 0f;
                pendingLookDY = 0f;
                camMtx = camera.GetViewMatrix();
            }
        }

        /// <summary>
        /// Atualiza a movimentação de wasd, e cria os "frames" da renderização.
        /// </summary>
        private void updateWASDControls(object sender, EventArgs e)
        {
            //camera input now advances inside the render loop (see
            //UpdateCameraPerFrame). This legacy WinForms-timer tick only fired
            //at an irregular ~15ms cadence and caused visible stutter.
        }

        private bool theAppLoadedWell = true; //o app carregou corretamente, sem erro na versão do openGL 

        private void GlControl_Load(object sender, EventArgs e)
        {
            try
            {
                Globals.OpenGLVersion = GL.GetString(StringName.Version)?.Trim() ?? "";

                if (Globals.OpenGLVersion.StartsWith("1.")
                    || Globals.OpenGLVersion.StartsWith("2.")
                    || Globals.OpenGLVersion.StartsWith("3.0")
                    || Globals.OpenGLVersion.StartsWith("3.1")
                    || Globals.OpenGLVersion.StartsWith("3.2")
                    )
                {
                    SplashScreen.Conteiner?.Close?.Invoke();
                    this.TopMost = true;
                    MessageBox.Show(
                        "Error: You have an outdated version of OpenGL, which is not supported by this program." +
                        " The program will now exit.\n\n" +
                        "OpenGL version: [" + Globals.OpenGLVersion + "]\n",
                        "OpenGL version error:",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    theAppLoadedWell = false;
                    this.Close();
                    return;
                }
            }
            catch (Exception ex)
            {
                SplashScreen.Conteiner?.Close?.Invoke();
                this.TopMost = true;
                MessageBox.Show(
                      "Error: " +
                      ex.Message,
                      "Error detecting OpenGL version:",
                      MessageBoxButtons.OK,
                     MessageBoxIcon.Error);
                theAppLoadedWell = false;
                this.Close();
                return;
            }

            if (theAppLoadedWell)
            {
                // Sync presentation to the monitor refresh rate. Without this
                // frames tear (visible shear/distortion while moving) and the
                // GPU burns power rendering frames that never get shown.
                // If a specific FPS limit is active, disable VSync so the
                // software limiter in RenderLoop_Idle can throttle properly.
                glControl.VSync = (Globals.FpsLimit == 0);

                GL.Viewport(0, 0, glControl.Width, glControl.Height);
                GL.ClearColor(Globals.SkyColor);

                GL.Enable(EnableCap.DepthTest);
                GL.Disable(EnableCap.Texture2D);
                GL.LineWidth(1.5f);

                DataShader.StartLoad();
                Utils.StartLoadObjsModels();

                // Finish all JSON/preload work behind the splash so Select Room
                // and Options do not perform their first disk scan while visible.
                Utils.StartPreloadFormCaches();

                glControl.SwapBuffers();

                // Drive continuous redraws at a fixed 120 fps instead of the
                // low-resolution WinForms timer (which caused choppy camera).
                StartRenderLoop();

                // viewport HUD overlay (FPS / mode / selection) + toast target
                CreateViewportHud();
                Re4QuadExtremeEditor.src.Class.ViewAnim.Cam = camera;

                SplashScreen.Conteiner?.Close?.Invoke();
                // faz a janela ficar no topo
                this.TopMost = true;
                this.TopMost = false;
            }

        }
      

        private void GlControl_Paint(object sender, PaintEventArgs e)
        {
            Globals.UpdateRenderFps();

            // per-frame delta time (drives the FOV smoothing below)
            long nowMs = renderLoopClock.ElapsedMilliseconds;
            float dt = lastPaintStopwatchMs < 0 ? 0.016f : (nowMs - lastPaintStopwatchMs) / 1000f;
            lastPaintStopwatchMs = nowMs;
            if (dt <= 0f) dt = 1e-4f;
            else if (dt > 0.25f) dt = 0.25f;

            // advance per-frame animations (selection pulse, gizmo glow)
            Re4QuadExtremeEditor.src.Class.ViewAnim.Tick();
            Re4QuadExtremeEditor.src.Class.Gizmo.Tick();
            try { Re4QuadExtremeEditor.src.Class.Gizmo.UpdateScale(camera); } catch { }

            // smooth FOV transition: exponential ease toward the target,
            // frame-rate independent, rebuilds the projection while moving
            if (Math.Abs(fovTarget - fovCurrent) > 0.02f)
            {
                float k = 1f - (float)Math.Exp(-16.0 * dt);
                fovCurrent += (fovTarget - fovCurrent) * k;
                Globals.FOV = fovCurrent;
                ProjMatrix = ReturnNewProjMatrix();
            }

            if (RenderSelectViewer)
            {
                NewAgeTheRender.TheRender.AllRender(ref camMtx, ref ProjMatrix, camera.Position, camera.SelectedObjPosY(), true); // este é da seleção
            }
            else
            {
                NewAgeTheRender.TheRender.AllRender(ref camMtx, ref ProjMatrix, camera.Position, camera.SelectedObjPosY()); // rederiza todos os objetos do GL;
            }

            if (!RenderSelectViewer)
            {
                // exact Re4QuadX path: Move (arrows+planes) or Rotate (rings) with World/Local handling
                try { Re4QuadExtremeEditor.src.Class.Gizmo.Render(camMtx, ProjMatrix, camera.Position, camera, Globals.CurrentTool, Globals.CurrentGizmoSpace); }
                catch { Re4QuadExtremeEditor.src.Class.Gizmo.Render(camMtx, ProjMatrix, camera.Front, camera.Right, camera.Up); }

                // bottom-right axis widget
                Re4QuadExtremeEditor.src.Class.SelectionOverlay.RenderAxisWidget(camMtx, glControl.Width, glControl.Height);
            }

            glControl.SwapBuffers();
            UpdateHudLabels();
        }

        #region Viewport HUD / focus animation / screenshot

        private Label hudStatsLabel = null;
        private Label hudToastLabel = null;
        private Label hudPosLabel = null;
        private string hudLastStatsText = "";
        private string hudLastPosText = "";
        //HUD text is throttled to ~10 Hz: updating WinForms labels every frame
        //invalidates overlay regions on top of the GL control and costs frames
        private long lastHudUpdateTickMs = int.MinValue;

        // hardware usage sampling (throttled to 1 Hz, values shown in the HUD)
        private System.Diagnostics.PerformanceCounter hudCpuCounter = null;
        private int hudCpuSamples = 0;
        private float hudCpuValue = -1f;

        private System.Diagnostics.PerformanceCounter[] hudGpuCounters = null;
        private bool hudGpuProbed = false;
        private float hudGpuValue = -1f;
        private long hudHwLastSampleTickMs = int.MinValue;

        private void ApplyHudThemeColors()
        {
            bool light = Re4QuadExtremeEditor.UiTheme.IsLight;
            if (hudStatsLabel != null)
            {
                hudStatsLabel.BackColor = light
                    ? Color.FromArgb(255, 245, 247, 250)
                    : Color.FromArgb(255, 24, 26, 32);
                hudStatsLabel.ForeColor = light
                    ? Color.FromArgb(255, 45, 50, 58)
                    : Color.FromArgb(255, 225, 228, 232);
            }
            if (hudPosLabel != null)
            {
                hudPosLabel.BackColor = light
                    ? Color.FromArgb(200, 250, 251, 253)
                    : Color.FromArgb(200, 20, 24, 30);
                hudPosLabel.ForeColor = light
                    ? Color.FromArgb(255, 0, 110, 145)
                    : Color.FromArgb(255, 140, 235, 255);
            }
        }

        /// <summary>
        /// Samples total CPU load and GPU engine (3D) utilization at most once
        /// per second. Values stay -1 ("--") when the counters are unavailable.
        /// </summary>
        private void SampleHardwareUsage(long nowTickMs)
        {
            if (nowTickMs - hudHwLastSampleTickMs < 1000) return;
            hudHwLastSampleTickMs = nowTickMs;

            try
            {
                if (hudCpuCounter == null)
                {
                    hudCpuCounter = new System.Diagnostics.PerformanceCounter(
                        "Processor", "% Processor Time", "_Total", true);
                    hudCpuCounter.NextValue(); // first sample is always invalid
                    hudCpuSamples = 0;
                }
                float cpu = hudCpuCounter.NextValue();
                hudCpuSamples++;
                if (hudCpuSamples >= 2 && cpu >= 0f) hudCpuValue = Math.Min(cpu, 100f);
            }
            catch { hudCpuValue = -1f; }

            try
            {
                if (!hudGpuProbed)
                {
                    hudGpuProbed = true;
                    System.Diagnostics.PerformanceCounterCategory cat =
                        new System.Diagnostics.PerformanceCounterCategory("GPU Engine");
                    List<System.Diagnostics.PerformanceCounter> list =
                        new List<System.Diagnostics.PerformanceCounter>();
                    foreach (string inst in cat.GetInstanceNames())
                    {
                        // sum of all 3D engine instances approximates total GPU load
                        if (inst.IndexOf("engtype_3D", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            try
                            {
                                list.Add(new System.Diagnostics.PerformanceCounter(
                                    "GPU Engine", "Utilization %", inst, true));
                            }
                            catch { }
                        }
                    }
                    foreach (System.Diagnostics.PerformanceCounter pc in list)
                    {
                        try { pc.NextValue(); } catch { } // prime first sample
                    }
                    hudGpuCounters = list.ToArray();
                }

                if (hudGpuCounters != null && hudGpuCounters.Length > 0)
                {
                    float sum = 0f;
                    foreach (System.Diagnostics.PerformanceCounter pc in hudGpuCounters)
                    {
                        try { sum += pc.NextValue(); }
                        catch { /* process ended, instance vanished */ }
                    }
                    hudGpuValue = Math.Max(0f, Math.Min(sum, 100f));
                }
                else
                {
                    hudGpuValue = -1f;
                }
            }
            catch { hudGpuValue = -1f; }
        }

        private void CreateViewportHud()
        {
            hudStatsLabel = new Label();
            hudStatsLabel.AutoSize = true;
            hudStatsLabel.BackColor = Color.FromArgb(255, 24, 26, 32);
            hudStatsLabel.ForeColor = Color.FromArgb(225, 228, 232);
            hudStatsLabel.Font = new Font("Consolas", 8.25f, FontStyle.Bold);
            hudStatsLabel.Padding = new Padding(6, 3, 6, 3);
            hudStatsLabel.Visible = false;

            hudToastLabel = new Label();
            hudToastLabel.AutoSize = true;
            hudToastLabel.BackColor = Color.FromArgb(255, 16, 64, 96);
            hudToastLabel.ForeColor = Color.White;
            hudToastLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            hudToastLabel.Padding = new Padding(10, 5, 10, 5);
            hudToastLabel.Visible = false;

            hudPosLabel = new Label();
            hudPosLabel.AutoSize = true;
            hudPosLabel.BackColor = Color.FromArgb(200, 20, 24, 30);
            hudPosLabel.ForeColor = Color.FromArgb(140, 235, 255);
            hudPosLabel.Font = new Font("Consolas", 8.25f, FontStyle.Bold);
            hudPosLabel.Padding = new Padding(6, 3, 6, 3);
            hudPosLabel.Visible = false;

            glControl.Controls.Add(hudStatsLabel);
            glControl.Controls.Add(hudToastLabel);
            glControl.Controls.Add(hudPosLabel);
            ApplyHudThemeColors();
            hudStatsLabel.Location = new Point(10, 8);
        }

        private void UpdateHudLabels()
        {
            if (hudStatsLabel == null) return;

            long tick = renderLoopClock.ElapsedMilliseconds;
            if (lastHudUpdateTickMs != int.MinValue && tick - lastHudUpdateTickMs < 100) return;
            lastHudUpdateTickMs = tick;

            SampleHardwareUsage(Environment.TickCount);

            string cpu = hudCpuValue < 0f ? "--" : ((int)Math.Round(hudCpuValue)).ToString();
            string gpu = hudGpuValue < 0f ? "--" : ((int)Math.Round(hudGpuValue)).ToString();
            string stats = string.Format("FPS {0}   CPU {1}%   GPU {2}%",
                Globals.CurrentFps, cpu, gpu);

            if (stats != hudLastStatsText)
            {
                hudLastStatsText = stats;
                hudStatsLabel.Text = stats;
                hudStatsLabel.Visible = true;
            }

            // live selection coordinates + snap status, bottom-left of the viewport
            Vector3 hudPivot;
            if (hudPosLabel != null && Re4QuadExtremeEditor.src.Class.Gizmo.TryGetPivot(out hudPivot))
            {
                string snapText = Re4QuadExtremeEditor.src.Class.Gizmo.SnapStep > 0f
                    ? "   SNAP " + Re4QuadExtremeEditor.src.Class.Gizmo.SnapStep.ToString("0.##")
                    : "";
                string gizmoText = Re4QuadExtremeEditor.src.Class.Gizmo.Enabled
                    ? string.Format("   {0} | {1}", Globals.CurrentTool, Globals.CurrentGizmoSpace)
                    : "   GIZMO OFF";
                string posText = string.Format("X {0:F2}   Y {1:F2}   Z {2:F2}{3}{4}",
                    hudPivot.X, hudPivot.Y, hudPivot.Z, snapText, gizmoText);
                if (posText != hudLastPosText)
                {
                    hudLastPosText = posText;
                    hudPosLabel.Text = posText;
                }
                int py = glControl.Height - hudPosLabel.PreferredHeight - 8;
                if (py < 4) py = 4;
                hudPosLabel.Location = new Point(10, py);
                hudPosLabel.Visible = true;
            }
            else if (hudPosLabel != null)
            {
                hudPosLabel.Visible = false;
                hudLastPosText = "";
            }

            if (Re4QuadExtremeEditor.src.Class.ViewAnim.ToastVisible)
            {
                string toast = Re4QuadExtremeEditor.src.Class.ViewAnim.ToastText;
                if (hudToastLabel.Text != toast) hudToastLabel.Text = toast;
                int x = (glControl.Width - hudToastLabel.PreferredWidth) / 2;
                int y = glControl.Height - hudToastLabel.PreferredHeight - 28;
                if (x < 4) x = 4;
                if (y < 4) y = 4;
                hudToastLabel.Location = new Point(x, y);
                hudToastLabel.Visible = true;
            }
            else
            {
                hudToastLabel.Visible = false;
            }
        }

        private void FocusOnSelection()
        {
            Vector3 pivot;
            if (!Re4QuadExtremeEditor.src.Class.Gizmo.TryGetPivot(out pivot))
            {
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Nothing selected - select an object first");
                return;
            }
            Re4QuadExtremeEditor.src.Class.ViewAnim.FocusNow(pivot);
            camMtx = camera.GetViewMatrix();
            glControl.Invalidate();
        }

        #endregion

        #region UX pack // isolate, duplicate, undo, snap, rubber band, recents, fps limit, autosave

        // ---------------- state ----------------

        private bool rbActive = false;
        private Point rbStartPoint;
        private Rectangle rbLastFrame = Rectangle.Empty;

        private ToolStripMenuItem recentMenu = null;

        private void WireUxPack()
        {
            Re4QuadExtremeEditor.src.Class.UndoSystem.Notify = delegate (string s)
            {
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast(s);
            };

            treeViewObjs.MouseDoubleClick += TreeViewObjs_DoubleClick;
            toolStripMenuItemFile.DropDownOpening += ToolStripMenuItemFile_DropDownOpening;

            BuildFpsLimitMenu();
            BuildEditUndoMenu();
            BuildMiscExtrasMenu();

            this.FormClosing += delegate (object s, System.Windows.Forms.FormClosingEventArgs ev)
            {
                PersistUxConfig();
            };
        }

        private void PersistUxConfig()
        {
            try
            {
                if (Globals.BackupConfigs == null) return;
                Globals.BackupConfigs.RecentFiles = Re4QuadExtremeEditor.src.Class.RecentFiles.ToStoredList();
                Globals.BackupConfigs.FpsLimit = Globals.FpsLimit;
                Re4QuadExtremeEditor.src.JSON.ConfigsFile.writeConfigsFile(Consts.ConfigsFileDirectory, Globals.BackupConfigs);
            }
            catch { }
        }

        // ---------------- double-click tree -> focus ----------------

        private void TreeViewObjs_DoubleClick(object sender, MouseEventArgs e)
        {
            TreeNode hit = treeViewObjs.GetNodeAt(e.Location);
            if (!(hit is Object3D)) return;

            var selected = DataBase.SelectedNodes;
            bool alreadySelected = selected != null && selected.ContainsKey(hit.GetHashCode());
            if (!alreadySelected)
            {
                treeViewObjs.ToSelectSingleNode(hit);
            }
            FocusOnSelection();
        }

        // ---------------- H : per-object isolation ----------------
        // H hides EVERY object except the exact selection (render-time filter);
        // Shift+H (or H again) restores the full scene. Group visibility flags
        // in Globals are left untouched.

        private void ToggleIsolateFromSelection()
        {
            if (Re4QuadExtremeEditor.src.Class.IsolateFilter.Active)
            {
                RestoreIsolateFromSelection();
                return;
            }

            var nodes = new List<TreeNode>();
            foreach (TreeNode n in TreeSelectionSnapshot())
            {
                if (n is Object3D) nodes.Add(n);
            }

            if (nodes.Count == 0)
            {
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Nothing to isolate - select an object first");
                return;
            }

            Re4QuadExtremeEditor.src.Class.IsolateFilter.Set(nodes);
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Isolated " + nodes.Count + " object(s)   (H again to restore)");
            glControl.Invalidate();
        }

        private void RestoreIsolateFromSelection()
        {
            if (!Re4QuadExtremeEditor.src.Class.IsolateFilter.Active) return;
            Re4QuadExtremeEditor.src.Class.IsolateFilter.Clear();
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Isolate cleared");
            glControl.Invalidate();
        }

        private System.Collections.Generic.IList<TreeNode> TreeSelectionSnapshot()
        {
            var list = new List<TreeNode>();
            var sel = DataBase.SelectedNodes;
            if (sel != null)
            {
                foreach (KeyValuePair<int, TreeNode> kv in sel) list.Add(kv.Value);
            }
            return list;
        }

        // ---------------- Ctrl+D : duplicate selection ----------------

        private static bool DupSupported(GroupType g)
        {
            switch (g)
            {
                case GroupType.ETS:
                case GroupType.ITA:
                case GroupType.AEV:
                case GroupType.DSE:
                case GroupType.SMX:
                case GroupType.AVL:
                case GroupType.FSE:
                case GroupType.SAR:
                case GroupType.EAR:
                case GroupType.ESE:
                case GroupType.EMI:
                case GroupType.QUAD_CUSTOM:
                case GroupType.LIT_GROUPS:
                case GroupType.LIT_ENTRYS:
                case GroupType.EFF_Table0:
                case GroupType.EFF_Table1:
                case GroupType.EFF_Table2:
                case GroupType.EFF_Table3:
                case GroupType.EFF_Table4:
                case GroupType.EFF_Table6:
                case GroupType.EFF_Table9:
                case GroupType.EFF_Table7_Effect_0:
                case GroupType.EFF_Table8_Effect_1:
                case GroupType.EFF_EffectEntry:
                case GroupType.CAM:
                case GroupType.RTP:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Toggles the first-person "Enter Camera View" on the selected
        /// CAM keyframe node (panel dropdown item or the E shortcut).
        /// </summary>
        public void ToggleEnterCamView()
        {
            Object3D selectedCam = null;
            foreach (TreeNode n in TreeSelectionSnapshot())
            {
                Object3D o = n as Object3D;
                if (o != null && o.Group == GroupType.CAM)
                {
                    selectedCam = o;
                    break;
                }
            }

            if (selectedCam == null)
            {
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Select a camera keyframe node first");
                return;
            }

            if (NewAgeTheRender.TheRender.CameraViewState.Enabled
              && NewAgeTheRender.TheRender.CameraViewState.NodeId == selectedCam.ObjLineRef)
            {
                NewAgeTheRender.TheRender.CameraViewState.Enabled = false;
                glControl.Invalidate();
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Camera view OFF");
                return;
            }

            NewAgeTheRender.TheRender.CameraViewState.Enabled = true;
            NewAgeTheRender.TheRender.CameraViewState.NodeId = selectedCam.ObjLineRef;
            glControl.Invalidate();
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Camera view ON - ESC to exit");
        }

        private void ToolStripMenuItemEnterCamView_Click(object sender, EventArgs e)
        {
            ToggleEnterCamView();
        }

        private void DuplicateSelection()
        {
            var snapshot = new List<Object3D>();
            bool hasEsl = false;
            bool hasCamZone = false;
            foreach (TreeNode n in TreeSelectionSnapshot())
            {
                Object3D o = n as Object3D;
                if (o == null) continue;
                if (o.Group == GroupType.ESL) { hasEsl = true; continue; }
                if (o.Group == GroupType.CAM_ZONE) { hasCamZone = true; continue; }
                if (DupSupported(o.Group)) snapshot.Add(o);
            }

            if (hasEsl)
            {
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ESL enemies cannot be duplicated");
            }
            if (hasCamZone)
            {
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Duplicate a camera keyframe node instead (its trigger zone is part of the camera)");
            }

            if (snapshot.Count == 0)
            {
                if (!hasEsl)
                {
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Duplicate: select a duplicable object first");
                }
                return;
            }

            var created = new List<Object3D>();
            int fullCopies = 0;
            foreach (Object3D item in snapshot)
            {
                TreeNode parent = item.Parent;
                if (parent == null) continue;

                // CAM: duplicate = copy this keyframe into the SAME camera
                // (a new "Camera_xxxx_CAMTypeN_IDn_KyNN" node appears in the tree)
                if (item.Group == GroupType.CAM && DataBase.FileCAM != null)
                {
                    try
                    {
                        ushort newKyId = DataBase.FileCAM.DuplicateKeyframeCopy(item.ObjLineRef);
                        Object3D clone = Object3D.CreateNewInstance(GroupType.CAM, newKyId);
                        int at = parent.Nodes.IndexOf(item);
                        parent.Nodes.Insert(at + 1, clone);
                        fullCopies++;
                        created.Add(clone);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("CAM keyframe duplicate failed: " + ex.Message);
                    }
                    continue;
                }

                // RTP: duplicate = clone the waypoint right next to its source,
                // already linked to it with a real two-way route edge
                if (item.Group == GroupType.RTP && DataBase.FileRTP != null)
                {
                    try
                    {
                        ushort newNodeId = DataBase.FileRTP.DuplicateNode(item.ObjLineRef);
                        if (newNodeId == ushort.MaxValue) throw new InvalidOperationException("RTP limit reached");
                        Object3D clone = Object3D.CreateNewInstance(GroupType.RTP, newNodeId);
                        int at = parent.Nodes.IndexOf(item);
                        parent.Nodes.Insert(at + 1, clone);
                        fullCopies++;
                        created.Add(clone);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("RTP node duplicate failed: " + ex.Message);
                    }
                    continue;
                }

                var change = parent as src.Class.Interfaces.INodeChangeAmount;
                if (change == null || change.ChangeAmountMethods == null || change.ChangeAmountMethods.AddNewLineID == null) continue;

                try
                {
                    ushort newId = change.ChangeAmountMethods.AddNewLineID(0);
                    Object3D clone = Object3D.CreateNewInstance(item.Group, newId);
                    parent.Nodes.Add(clone);

                    // clone EVERY property of the source line (raw byte copy),
                    // so all known and unknown fields match the original exactly
                    if (TryCopyWholeLine(parent, item.ObjLineRef, newId))
                    {
                        fullCopies++;
                    }

                    // place next to the source (2 world units along +X)
                    try
                    {
                        clone.SetObjPosition_ToCamera(item.GetObjPosition_ToCamera() + new Vector3(2f, 0f, 0f));
                    }
                    catch { }

                    created.Add(clone);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Duplicate failed: " + ex.Message);
                }
            }

            if (created.Count == 0)
            {
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Duplicate failed for this object type");
                return;
            }

            TreeViewUpdateSelectedsClear();
            treeViewObjs.ToSelectNodesBatch(created);
            treeViewObjs.Refresh();
            UpdatePropertyGrid();

            Re4QuadExtremeEditor.src.Class.UndoSystem.PushAdd(created,
                created.Count == 1 ? "duplicate #" + created[0].ObjLineRef : "duplicate " + created.Count + " objects");

            if (fullCopies == created.Count)
            {
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Duplicated " + created.Count + " object(s) with ALL settings");
            }
            else
            {
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Duplicated " + created.Count + " object(s) (" + fullCopies + " full copy)");
            }
            glControl.Invalidate();
        }

        /// <summary>
        /// Copies the raw data line of an object to another line through the
        /// group's PropertyMethods ReturnLine/SetLine delegates, cloning every
        /// field bit-for-bit. Only applied when both lines have equal length.
        /// </summary>
        private static bool TryCopyWholeLine(TreeNode parent, ushort srcId, ushort dstId)
        {
            try
            {
                System.Reflection.PropertyInfo pi = parent.GetType().GetProperty("PropertyMethods");
                object methods = pi != null ? pi.GetValue(parent, null) : null;
                if (methods == null) return false;

                System.Reflection.FieldInfo retField = methods.GetType().GetField("ReturnLine");
                System.Reflection.FieldInfo setField = methods.GetType().GetField("SetLine");
                if (retField == null || setField == null) return false;

                Delegate retDel = retField.GetValue(methods) as Delegate;
                Delegate setDel = setField.GetValue(methods) as Delegate;
                if (retDel == null || setDel == null) return false;

                byte[] srcLine = retDel.DynamicInvoke(srcId) as byte[];
                byte[] dstLine = retDel.DynamicInvoke(dstId) as byte[];
                if (srcLine == null || dstLine == null) return false;
                if (srcLine.Length != dstLine.Length) return false;

                setDel.DynamicInvoke(dstId, srcLine);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ---------------- Shift+drag : rubber-band selection ----------------

        private static Rectangle MakeNormalizedRect(Point a, Point b)
        {
            return new Rectangle(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y),
                Math.Abs(a.X - b.X),
                Math.Abs(a.Y - b.Y));
        }

        private void UpdateRubberBandFrame(Point current)
        {
            Rectangle rect = MakeNormalizedRect(rbStartPoint, current);
            if (!rbLastFrame.IsEmpty)
            {
                ControlPaint.DrawReversibleFrame(glControl.RectangleToScreen(rbLastFrame), Color.Cyan, FrameStyle.Dashed);
            }
            ControlPaint.DrawReversibleFrame(glControl.RectangleToScreen(rect), Color.Cyan, FrameStyle.Dashed);
            rbLastFrame = rect;
        }

        private void EraseRubberBandFrame()
        {
            if (!rbLastFrame.IsEmpty)
            {
                ControlPaint.DrawReversibleFrame(glControl.RectangleToScreen(rbLastFrame), Color.Cyan, FrameStyle.Dashed);
                rbLastFrame = Rectangle.Empty;
            }
        }

        private void FinishRubberBandSelect(Point current)
        {
            EraseRubberBandFrame();
            rbActive = false;
            SelectObjectsInScreenRect(MakeNormalizedRect(rbStartPoint, current));
        }

        /// <summary>
        /// Selects every object whose camera-space position projects into the
        /// given viewport rectangle.
        /// </summary>
        private void SelectObjectsInScreenRect(Rectangle rect)
        {
            if (rect.Width < 2 && rect.Height < 2) return;

            int w = glControl.Width;
            int h = glControl.Height;
            Matrix4 vp = camMtx * ProjMatrix;

            TreeNode[] roots = new TreeNode[]
            {
                DataBase.NodeESL, DataBase.NodeETS, DataBase.NodeITA, DataBase.NodeAEV,
                DataBase.NodeDSE, DataBase.NodeFSE, DataBase.NodeEAR, DataBase.NodeSAR,
                DataBase.NodeAVL,
                DataBase.NodeCAM, DataBase.NodeCAM_Zone, DataBase.NodeRTP,
                DataBase.NodeEMI, DataBase.NodeESE, DataBase.NodeQuadCustom,
                DataBase.NodeLIT_Groups, DataBase.NodeLIT_Entrys,
                DataBase.NodeEFF_Table0, DataBase.NodeEFF_Table1, DataBase.NodeEFF_Table2,
                DataBase.NodeEFF_Table3, DataBase.NodeEFF_Table4, DataBase.NodeEFF_Table6,
                DataBase.NodeEFF_Table7_Effect_0, DataBase.NodeEFF_Table8_Effect_1,
                DataBase.NodeEFF_EffectEntry, DataBase.NodeEFF_Table9
            };

            var found = new List<TreeNode>();
            foreach (TreeNode root in roots)
            {
                if (root == null) continue;
                foreach (TreeNode child in root.Nodes)
                {
                    Object3D obj = child as Object3D;
                    if (obj == null) continue;

                    Vector3 p = obj.GetObjPosition_ToCamera();
                    Vector4 clip = new Vector4(p, 1f) * vp;
                    if (clip.W <= 1e-6f) continue; // behind the camera

                    float sx = (clip.X / clip.W * 0.5f + 0.5f) * w;
                    float sy = (1f - (clip.Y / clip.W * 0.5f + 0.5f)) * h;
                    if (sx >= rect.Left && sx <= rect.Right && sy >= rect.Top && sy <= rect.Bottom)
                    {
                        found.Add(obj);
                    }
                }
            }

            if (found.Count == 0) return;

            TreeViewUpdateSelectedsClear();
            treeViewObjs.ToSelectNodesBatch(found);
            treeViewObjs.Refresh();

            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Box-selected " + found.Count + " object(s)");
            glControl.Invalidate();
        }

        // ---------------- snap presets ----------------

        private void ApplySnapStep(float step)
        {
            Re4QuadExtremeEditor.src.Class.Gizmo.SnapStep = step;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast(step > 0f
                ? "Snap grid: " + step.ToString("0.##")
                : "Snap grid: off");
            glControl.Invalidate();
        }

        // ---------------- menus: FPS limiter / undo / extras ----------------

        private void BuildFpsLimitMenu()
        {
            try
            {
                var fpsMenu = new ToolStripMenuItem("FPS Limit");

                int[] limits = new int[] { 30, 60, 120, 144, 240, 0 };
                foreach (int v in limits)
                {
                    string text = v == 0 ? "Unlimited" : v + " fps";
                    var item = new ToolStripMenuItem(text);
                    item.Tag = v;
                    item.Checked = Globals.FpsLimit == v;
                    ToolStripMenuItem captured = item;
                    item.Click += delegate (object s, EventArgs ev)
                    {
                        int val = (int)captured.Tag;
                        Globals.FpsLimit = val;
                        foreach (ToolStripMenuItem x in fpsMenu.DropDownItems)
                        {
                            x.Checked = ReferenceEquals(x, captured);
                        }
                        // When a specific limit is set, disable VSync so the
                        // software limiter in RenderLoop_Idle can throttle below
                        // the monitor refresh rate. Unlimited keeps VSync on.
                        try
                        {
                            if (glControl != null && !glControl.IsDisposed)
                                glControl.VSync = (val == 0);
                        }
                        catch { }
                        Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("FPS limit: " + (val == 0 ? "Unlimited" : val.ToString()));
                    };
                    fpsMenu.DropDownItems.Add(item);
                }

                toolStripMenuItemView.DropDownItems.Add(fpsMenu);
            }
            catch { }
        }

        private void BuildEditUndoMenu()
        {
            try
            {
                var sep = new ToolStripSeparator();

                var undoItem = new ToolStripMenuItem("Undo");
                undoItem.ShortcutKeys = Keys.Control | Keys.Z;
                undoItem.Click += delegate { UndoSystem.Undo(); glControl.Invalidate(); treeViewObjs.Refresh(); };

                var redoItem = new ToolStripMenuItem("Redo");
                redoItem.ShortcutKeys = Keys.Control | Keys.Y;
                redoItem.Click += delegate { UndoSystem.Redo(); glControl.Invalidate(); treeViewObjs.Refresh(); };

                toolStripMenuItemEdit.DropDownItems.Add(sep);
                toolStripMenuItemEdit.DropDownItems.Add(undoItem);
                toolStripMenuItemEdit.DropDownItems.Add(redoItem);
            }
            catch { }
        }

        private void BuildMiscExtrasMenu()
        {
            try
            {
                var backupItem = new ToolStripMenuItem("Create backup now");
                backupItem.Click += delegate { AutosaveNow(true); };
                toolStripMenuItemMisc.DropDownItems.Add(backupItem);
            }
            catch { }
        }

        // ---------------- recent files menu ----------------

        private void EnsureRecentMenu()
        {
            if (recentMenu != null) return;
            recentMenu = new ToolStripMenuItem("Recent Files");
            try
            {
                toolStripMenuItemFile.DropDownItems.Insert(0, recentMenu);
                toolStripMenuItemFile.DropDownItems.Insert(1, new ToolStripSeparator());
            }
            catch
            {
                toolStripMenuItemFile.DropDownItems.Add(recentMenu);
            }
        }

        private void ToolStripMenuItemFile_DropDownOpening(object sender, EventArgs e)
        {
            EnsureRecentMenu();
            recentMenu.DropDownItems.Clear();

            var items = Re4QuadExtremeEditor.src.Class.RecentFiles.Items;
            if (items.Count == 0)
            {
                var empty = new ToolStripMenuItem("(none yet)");
                empty.Enabled = false;
                recentMenu.DropDownItems.Add(empty);
                return;
            }

            foreach (KeyValuePair<string, string> kv in items)
            {
                string kind = kv.Key;
                string path = kv.Value;
                var item = new ToolStripMenuItem(Path.GetFileName(path) + "    [" + kind + "]");
                item.ToolTipText = path;
                ToolStripMenuItem captured = item;
                item.Click += delegate
                {
                    OpenRecentFile(kind, path);
                };
                recentMenu.DropDownItems.Add(item);
            }
        }

        private void OpenRecentFile(string kind, string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("File no longer exists");
                return;
            }

            FileInfo fileInfo;
            FileStream stream;
            try
            {
                fileInfo = new FileInfo(path);
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    return;
                }
                if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    return;
                }
                stream = fileInfo.OpenRead();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                return;
            }

            Action<FileStream, FileInfo> loader = null;
            Action<string> setPath;
            Action clear;
            SimpleEndianBinaryIO.Endianness endian = SimpleEndianBinaryIO.Endianness.LittleEndian;
            bool effBlob = false;

            switch (kind)
            {
                case "ESL": loader = FileManager.LoadFileESL; setPath = v => Globals.FilePathESL = v; clear = FileManager.ClearESL; break;
                case "ETS_2007": loader = FileManager.LoadFileETS_2007_PS2; setPath = v => Globals.FilePathETS = v; clear = FileManager.ClearETS; break;
                case "ETS_UHD": loader = FileManager.LoadFileETS_UHD; setPath = v => Globals.FilePathETS = v; clear = FileManager.ClearETS; break;
                case "ITA_2007": loader = FileManager.LoadFileITA_2007_PS2; setPath = v => Globals.FilePathITA = v; clear = FileManager.ClearITA; break;
                case "ITA_UHD": loader = FileManager.LoadFileITA_UHD; setPath = v => Globals.FilePathITA = v; clear = FileManager.ClearITA; break;
                case "ITA_PS4NS": loader = FileManager.LoadFileITA_PS4_NS; setPath = v => Globals.FilePathITA = v; clear = FileManager.ClearITA; break;
                case "AEV_2007": loader = FileManager.LoadFileAEV_2007_PS2; setPath = v => Globals.FilePathAEV = v; clear = FileManager.ClearAEV; break;
                case "AEV_UHD": loader = FileManager.LoadFileAEV_UHD; setPath = v => Globals.FilePathAEV = v; clear = FileManager.ClearAEV; break;
                case "DSE": loader = FileManager.LoadFileDSE; setPath = v => Globals.FilePathDSE = v; clear = FileManager.ClearDSE; break;
                case "AVL": loader = FileManager.LoadFileAVL; setPath = v => Globals.FilePathAVL = v; clear = FileManager.ClearAVL; break;
                case "CAM_UHD": loader = (s, fi) => FileManager.LoadFileCAM(s, IsRe4Version.UHD); setPath = v => Globals.FilePathCAM = v; clear = FileManager.ClearCAM; break;
                case "RTP": loader = (s, fi) => FileManager.LoadFileRTP(s); setPath = v => Globals.FilePathRTP = v; clear = FileManager.ClearRTP; break;
                case "FSE": loader = FileManager.LoadFileFSE; setPath = v => Globals.FilePathFSE = v; clear = FileManager.ClearFSE; break;
                case "SAR": loader = FileManager.LoadFileSAR; setPath = v => Globals.FilePathSAR = v; clear = FileManager.ClearSAR; break;
                case "EAR": loader = FileManager.LoadFileEAR; setPath = v => Globals.FilePathEAR = v; clear = FileManager.ClearEAR; break;
                case "EMI_2007": loader = FileManager.LoadFileEMI_2007_PS2; setPath = v => Globals.FilePathEMI = v; clear = FileManager.ClearEMI; break;
                case "EMI_UHD": loader = FileManager.LoadFileEMI_UHD; setPath = v => Globals.FilePathEMI = v; clear = FileManager.ClearEMI; break;
                case "ESE_2007": loader = FileManager.LoadFileESE_2007_PS2; setPath = v => Globals.FilePathESE = v; clear = FileManager.ClearESE; break;
                case "ESE_UHD": loader = FileManager.LoadFileESE_UHD; setPath = v => Globals.FilePathESE = v; clear = FileManager.ClearESE; break;
                case "QUAD": loader = FileManager.LoadFileQuadCustom; setPath = v => Globals.FilePathQuadCustom = v; clear = FileManager.ClearQuadCustom; break;
                case "LIT_2007": loader = FileManager.LoadFileLIT_2007_PS2; setPath = v => Globals.FilePathLIT = v; clear = FileManager.ClearLIT; break;
                case "LIT_UHD": loader = FileManager.LoadFileLIT_UHD; setPath = v => Globals.FilePathLIT = v; clear = FileManager.ClearLIT; break;
                case "EFFBLOB_LE": effBlob = true; endian = SimpleEndianBinaryIO.Endianness.LittleEndian; setPath = v => Globals.FilePathEFFBLOB = v; clear = FileManager.ClearEFFBLOB; break;
                case "EFFBLOB_BE": effBlob = true; endian = SimpleEndianBinaryIO.Endianness.BigEndian; setPath = v => Globals.FilePathEFFBLOB = v; clear = FileManager.ClearEFFBLOB; break;
                default:
                    stream.Close();
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Unknown file kind: " + kind);
                    return;
            }

            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            try
            {
                if (effBlob)
                {
                    FileManager.LoadFileEFFBLOB(stream, endian);
                }
                else if (loader != null)
                {
                    loader(stream, fileInfo);
                }
                setPath(path);
                Re4QuadExtremeEditor.src.Class.RecentFiles.Note(kind, path);
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast(kind + " opened");
            }
            catch (Exception ex)
            {
                try { clear(); } catch { }
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
            }
            finally
            {
                stream.Close();
                glControl.Invalidate();
                TreeViewEnableDrawNode();
            }
        }

        // ---------------- manual backups (Misc > Create backup now) ----------------

        private void AutosaveNow(bool manual)
        {
            try
            {
                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Re4QuadExtremeEditor", "Backups", stamp);
                Directory.CreateDirectory(baseDir);

                var savers = new List<KeyValuePair<string, KeyValuePair<Action<FileStream>, string>>>();
                AddSaver(savers, "ESL", Globals.FilePathESL, FileManager.SaveFileESL);
                AddSaver(savers, "ETS", Globals.FilePathETS, FileManager.SaveFileETS);
                AddSaver(savers, "ITA", Globals.FilePathITA, FileManager.SaveFileITA);
                AddSaver(savers, "AEV", Globals.FilePathAEV, FileManager.SaveFileAEV);
                AddSaver(savers, "DSE", Globals.FilePathDSE, FileManager.SaveFileDSE);
                AddSaver(savers, "AVL", Globals.FilePathAVL, FileManager.SaveFileAVL);
                AddSaver(savers, "CAM", Globals.FilePathCAM, FileManager.SaveFileCAM);
                AddSaver(savers, "RTP", Globals.FilePathRTP, FileManager.SaveFileRTP);
                AddSaver(savers, "FSE", Globals.FilePathFSE, FileManager.SaveFileFSE);
                AddSaver(savers, "SAR", Globals.FilePathSAR, FileManager.SaveFileSAR);
                AddSaver(savers, "EAR", Globals.FilePathEAR, FileManager.SaveFileEAR);
                AddSaver(savers, "EMI", Globals.FilePathEMI, FileManager.SaveFileEMI);
                AddSaver(savers, "ESE", Globals.FilePathESE, FileManager.SaveFileESE);
                AddSaver(savers, "QUAD", Globals.FilePathQuadCustom, FileManager.SaveFileQuadCustom);
                AddSaver(savers, "LIT", Globals.FilePathLIT, FileManager.SaveFileLIT);
                AddSaver(savers, "EFFBLOB", Globals.FilePathEFFBLOB, FileManager.SaveFileEFFBLOB);

                if (savers.Count == 0)
                {
                    if (manual) Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Backup: nothing loaded to save");
                    return;
                }

                int saved = 0;
                StringBuilder manifest = new StringBuilder();
                manifest.AppendLine("RE4 Quad Extreme Editor - automatic backup " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                manifest.AppendLine("Restore: copy each file back over its original path (or open it directly).");
                manifest.AppendLine();

                foreach (KeyValuePair<string, KeyValuePair<Action<FileStream>, string>> entry in savers)
                {
                    string label = entry.Key;
                    Action<FileStream> saver = entry.Value.Key;
                    string originalPath = entry.Value.Value;
                    try
                    {
                        string target = Path.Combine(baseDir, label + "_" + Path.GetFileName(originalPath));
                        using (FileStream fs = File.Create(target))
                        {
                            saver(fs);
                        }
                        saved++;
                        manifest.AppendLine(label + "\t" + originalPath);
                    }
                    catch { /* keep saving the rest */ }
                }

                try { File.WriteAllText(Path.Combine(baseDir, "manifest.txt"), manifest.ToString()); } catch { }

                PruneOldBackups(Path.GetDirectoryName(baseDir));

                EditorConsole.Log((manual ? "Backup saved" : "Autosaved") + " (" + saved + " files) -> " + baseDir);

                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast(manual
                    ? "Backup saved (" + saved + " files)"
                    : "Autosaved " + saved + " file(s)");
            }
            catch { }
        }

        private static void AddSaver(List<KeyValuePair<string, KeyValuePair<Action<FileStream>, string>>> list,
            string label, string path, Action<FileStream> saver)
        {
            if (string.IsNullOrEmpty(path)) return;
            list.Add(new KeyValuePair<string, KeyValuePair<Action<FileStream>, string>>(
                label, new KeyValuePair<Action<FileStream>, string>(saver, path)));
        }

        private static void PruneOldBackups(string backupsRoot)
        {
            try
            {
                if (backupsRoot == null || !Directory.Exists(backupsRoot)) return;
                var dirs = Directory.GetDirectories(backupsRoot);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase); // timestamp names sort chronologically
                int keep = 15;
                for (int i = 0; i < dirs.Length - keep; i++)
                {
                    try { Directory.Delete(dirs[i], true); } catch { }
                }
            }
            catch { }
        }

        // ---------------- drag-and-drop ----------------

        private static readonly HashSet<string> DragDropExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".esl", ".ets", ".ita", ".aev", ".dse", ".smx", ".avl",
            ".fse", ".sar", ".ear", ".emi", ".ese", ".quadcustom",
            ".lit", ".effblob", ".effblobbig", ".cam", ".rtp"
        };

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string ext = Path.GetExtension(files[0]);
                    if (DragDropExtensions.Contains(ext))
                    {
                        e.Effect = DragDropEffects.Copy;
                        return;
                    }
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            string path = files[0];
            string ext = Path.GetExtension(path).ToLowerInvariant();

            try
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Length == 0 || fileInfo.Length > 0x1000000)
                {
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("File too large or empty");
                    return;
                }

                TreeViewUpdateSelectedsClear();
                TreeViewDisableDrawNode();

                switch (ext)
                {
                    case ".esl":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileESL(fs, fileInfo);
                        Globals.FilePathESL = path;
                        break;
                    case ".ets":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileETS_UHD(fs, fileInfo);
                        Globals.FilePathETS = path;
                        break;
                    case ".ita":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileITA_UHD(fs, fileInfo);
                        Globals.FilePathITA = path;
                        break;
                    case ".aev":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileAEV_UHD(fs, fileInfo);
                        Globals.FilePathAEV = path;
                        break;
                    case ".dse":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileDSE(fs, fileInfo);
                        Globals.FilePathDSE = path;
                        break;
                    case ".smx":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileSMX(fs, fileInfo);
                        Globals.FilePathSMX = path;
                        break;
                    case ".avl":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileAVL(fs, fileInfo);
                        Globals.FilePathAVL = path;
                        break;
                    case ".fse":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileFSE(fs, fileInfo);
                        Globals.FilePathFSE = path;
                        break;
                    case ".sar":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileSAR(fs, fileInfo);
                        Globals.FilePathSAR = path;
                        break;
                    case ".ear":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileEAR(fs, fileInfo);
                        Globals.FilePathEAR = path;
                        break;
                    case ".emi":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileEMI_UHD(fs, fileInfo);
                        Globals.FilePathEMI = path;
                        break;
                    case ".ese":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileESE_UHD(fs, fileInfo);
                        Globals.FilePathESE = path;
                        break;
                    case ".quadcustom":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileQuadCustom(fs, fileInfo);
                        Globals.FilePathQuadCustom = path;
                        break;
                    case ".lit":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileLIT_UHD(fs, fileInfo);
                        Globals.FilePathLIT = path;
                        break;
                    case ".effblob":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileEFFBLOB(fs, SimpleEndianBinaryIO.Endianness.LittleEndian);
                        Globals.FilePathEFFBLOB = path;
                        break;
                    case ".effblobbig":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileEFFBLOB(fs, SimpleEndianBinaryIO.Endianness.BigEndian);
                        Globals.FilePathEFFBLOB = path;
                        break;
                    case ".cam":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileCAM(fs, IsRe4Version.UHD);
                        Globals.FilePathCAM = path;
                        break;
                    case ".rtp":
                        using (var fs = fileInfo.OpenRead())
                            FileManager.LoadFileRTP(fs);
                        Globals.FilePathRTP = path;
                        break;
                    default:
                        Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Unsupported file type: " + ext);
                        return;
                }

                string name = Path.GetFileName(path);
                EditorConsole.Log("Loaded via drag-drop: " + name);
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast(name + " loaded");
            }
            catch (Exception ex)
            {
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Failed to load: " + ex.Message);
            }
            finally
            {
                glControl.Invalidate();
                TreeViewEnableDrawNode();
            }
        }

        #endregion

        #region Continuous render loop // high-fps redraw driver

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool PeekMessage(out System.Windows.Forms.Message msg, System.IntPtr hWnd, uint messageFilterMin, uint messageFilterMax, uint removeMsg);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern System.IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int GetClassName(System.IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        // Win32 class name shared by every popup menu window (WinForms
        // dropdowns included). Reliable even though modern MenuStrip menus
        // never send the legacy WM_ENTERMENULOOP messages.
        private static bool MenuPopupIsActive()
        {
            try
            {
                System.IntPtr h = GetForegroundWindow();
                if (h == System.IntPtr.Zero) return false;
                System.Text.StringBuilder sb = new System.Text.StringBuilder(16);
                GetClassName(h, sb, 16);
                return sb.ToString() == "#32768";
            }
            catch { return false; }
        }

        readonly System.Diagnostics.Stopwatch renderLoopClock = System.Diagnostics.Stopwatch.StartNew();
        double nextRenderLoopTick = 0;
        bool renderLoopStarted = false;
        bool uiMenuActive = false;
        int lastMenuRenderTick = 0;

        #region Utility panel (Controls / Console tabs)

        // Same layout as Re4QuadX: the bottom-right area is a tab control
        // whose "Controls" page hosts the control panels (objectMove /
        // cameraMove / ads) and whose "Console" page hosts the action log.
        Re4QuadExtremeEditor.src.Controls.DarkTabControl utilityPanel;
        System.Windows.Forms.TabPage controlsTab;
        System.Windows.Forms.TabPage consoleTab;

        private void BuildConsolePanel()
        {
            var back = System.Drawing.Color.FromArgb(17, 20, 24);

            utilityPanel = new Re4QuadExtremeEditor.src.Controls.DarkTabControl();
            utilityPanel.Name = "utilityPanel";
            utilityPanel.Dock = System.Windows.Forms.DockStyle.Fill;

            controlsTab = new System.Windows.Forms.TabPage("Controls");
            controlsTab.Name = "controlsTab";
            controlsTab.Padding = new System.Windows.Forms.Padding(0);

            consoleTab = new System.Windows.Forms.TabPage("Console");
            consoleTab.Name = "consoleTab";
            consoleTab.Padding = new System.Windows.Forms.Padding(0);
            consoleTab.BackColor = back;

            var consoleBox = new System.Windows.Forms.RichTextBox();
            consoleBox.Dock = System.Windows.Forms.DockStyle.Fill;
            consoleBox.Name = "consoleBox";
            consoleBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            consoleBox.BackColor = back;
            consoleTab.Controls.Add(consoleBox);

            utilityPanel.TabPages.Add(controlsTab);
            utilityPanel.TabPages.Add(consoleTab);

            splitContainerRight.Panel2.Controls.Add(utilityPanel);
            utilityPanel.BringToFront();

            EditorConsole.RegisterOutputControl(consoleBox);
        }

        /// <summary>
        /// Brings the console tab to the front (replaces the old
        /// show/hide console toggle from the View menu).
        /// </summary>
        public void ShowConsoleTab()
        {
            if (utilityPanel != null && consoleTab != null)
            {
                utilityPanel.SelectedTab = consoleTab;
            }
        }

        #endregion

        #region Quad Project (.quad)

        private string projectBaseTitle;

        /// <summary>
        /// Adds "Open Project / Save Project / Save Project As" to the File
        /// menu (Blender-style .quad sessions).
        /// </summary>
        private void WireProjectMenu()
        {
            projectBaseTitle = Text;

            var openProjectItem = new ToolStripMenuItem("Open Project...");
            openProjectItem.Click += OpenProjectMenuItem_Click;

            var saveProjectItem = new ToolStripMenuItem("Save Project");
            saveProjectItem.ShortcutKeys = Keys.Control | Keys.S;
            saveProjectItem.Click += SaveProjectMenuItem_Click;

            var saveProjectAsItem = new ToolStripMenuItem("Save Project As...");
            saveProjectAsItem.Click += SaveProjectAsMenuItem_Click;

            int insertAt = toolStripMenuItemFile.DropDownItems.IndexOf(toolStripMenuItemOpen) + 1;
            toolStripMenuItemFile.DropDownItems.Insert(insertAt++, new ToolStripSeparator());
            toolStripMenuItemFile.DropDownItems.Insert(insertAt++, openProjectItem);
            toolStripMenuItemFile.DropDownItems.Insert(insertAt++, saveProjectItem);
            toolStripMenuItemFile.DropDownItems.Insert(insertAt++, saveProjectAsItem);

            //double-clicked .quad file passed on the command line
            Shown += delegate
            {
                // first-run welcome/setup wizard (shows once, reopenable from
                // Misc > Setup Wizard)
                try
                {
                    if (Globals.BackupConfigs != null && !Globals.BackupConfigs.SetupDone)
                    {
                        ShowWelcomeSetup();
                    }
                }
                catch (Exception ex)
                {
                    EditorConsole.Log("Setup wizard failed: " + ex.Message);
                }

                string startup = Program.StartupProjectFile;
                if (!string.IsNullOrEmpty(startup) && System.IO.File.Exists(startup))
                {
                    Program.StartupProjectFile = null;
                    ProjectManager.OpenProject(this, startup);
                    glControl.Invalidate();
                }
            };
        }

        /// <summary>
        /// Opens the first-run welcome/setup wizard (Welcome / Directories /
        /// Tools pages). Called automatically on first launch and from Misc.
        /// </summary>
        private void ShowWelcomeSetup()
        {
            Re4QuadExtremeEditor.src.Forms.WelcomeSetupForm wizard = new Re4QuadExtremeEditor.src.Forms.WelcomeSetupForm();
            try
            {
                System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(wizard);
                helper.Owner = this.Handle;
                wizard.ShowDialog();
            }
            catch (Exception ex)
            {
                EditorConsole.Log("Setup wizard failed: " + ex.Message);
            }
            glControl.Invalidate();
        }

        /// <summary>
        /// Adds "Setup Wizard" to the top of the Misc menu.
        /// </summary>
        private void WireSetupWizardMenu()
        {
            var setupWizardItem = new ToolStripMenuItem("Setup Wizard");
            setupWizardItem.Click += delegate { ShowWelcomeSetup(); };
            toolStripMenuItemMisc.DropDownItems.Insert(0, setupWizardItem);
        }

        private void OpenProjectMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Open Quad Project";
                dialog.Filter = ProjectManager.FileFilter;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    ProjectManager.OpenProject(this, dialog.FileName);
                    glControl.Invalidate();
                }
            }
        }

        private void SaveProjectMenuItem_Click(object sender, EventArgs e)
        {
            TrySaveProject();
        }

        private void SaveProjectAsMenuItem_Click(object sender, EventArgs e)
        {
            TrySaveProjectAs();
        }

        /// <summary>Saves to the current .quad path (falls back to Save As when
        /// there is none). Returns true only when a project was actually saved.</summary>
        internal bool TrySaveProject()
        {
            if (string.IsNullOrEmpty(ProjectManager.CurrentProjectPath))
            {
                return TrySaveProjectAs();
            }
            ProjectManager.SaveProject(this, ProjectManager.CurrentProjectPath);
            glControl.Invalidate();
            return true;
        }

        /// <summary>Opens the file dialog and saves. Returns false if the
        /// user cancelled or the dialog never completed.</summary>
        internal bool TrySaveProjectAs()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Save Quad Project As";
                dialog.Filter = ProjectManager.FileFilter;
                dialog.DefaultExt = "quad";

                var room = DataBase.SelectedRoom;
                var roomModel = room?.GetRoomModel();
                if (roomModel?.JsonFileName != null)
                {
                    dialog.FileName = System.IO.Path.GetFileNameWithoutExtension(roomModel.JsonFileName);
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    ProjectManager.SaveProject(this, dialog.FileName);
                    glControl.Invalidate();
                    return true;
                }
            }
            return false;
        }

        internal void SetProjectTitle(string projectPath)
        {
            Text = projectBaseTitle + " - " + System.IO.Path.GetFileName(projectPath);
        }

        #endregion

        #region Room tools menu (extract / repack)

        private void SetupRoomToolsMenu()
        {
            toolStripMenuItemMisc.DropDownItems.Add(new System.Windows.Forms.ToolStripSeparator());

            var menuItemUnpackAllRooms = new System.Windows.Forms.ToolStripMenuItem("Unpack All Rooms");
            menuItemUnpackAllRooms.Click += unpackAllRoomsUHDToolStripMenuItem_Click;

            var menuItemUnpackRoom = new System.Windows.Forms.ToolStripMenuItem("Unpack Room...");
            menuItemUnpackRoom.Click += delegate { Re4QuadExtremeEditor.src.Class.ExternalToolManager.UnpackRoomUdas(); };

            var menuItemRepackCurrent = new System.Windows.Forms.ToolStripMenuItem("Repack Current Room");
            menuItemRepackCurrent.Click += repackCurrentRoomToolStripMenuItem_Click;

            var menuItemRepackRoom = new System.Windows.Forms.ToolStripMenuItem("Repack Room...");
            menuItemRepackRoom.Click += repackRoomToolStripMenuItem_Click;

            var menuItemUnpackAllTextures = new System.Windows.Forms.ToolStripMenuItem("Unpack All Textures");
            menuItemUnpackAllTextures.Click += unpackAllTexturesUHDToolStripMenuItem_Click;

            toolStripMenuItemMisc.DropDownItems.Add(menuItemUnpackAllRooms);
            toolStripMenuItemMisc.DropDownItems.Add(menuItemUnpackRoom);
            toolStripMenuItemMisc.DropDownItems.Add(menuItemRepackCurrent);
            toolStripMenuItemMisc.DropDownItems.Add(menuItemRepackRoom);
            toolStripMenuItemMisc.DropDownItems.Add(menuItemUnpackAllTextures);
        }

        private void repackCurrentRoomToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DataBase.SelectedRoom == null)
            {
                EditorConsole.Warning("No room is currently loaded.");
                return;
            }

            DialogResult result = MessageBox.Show(
                "This will pack all files in the current room folder into a .udas file.\n" +
                "Any unsaved modifications to the loaded objects will NOT be included.\n\n" +
                "Continue?",
                "Confirm Repack",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                ExportAllModifiedRoomFiles();
                Re4QuadExtremeEditor.src.Class.ExternalToolManager.RepackRoomUdas(true);
            }
        }

        /// <summary>
        /// Saves every loaded object file back over its original path
        /// (same behaviour as Re4QuadX before repacking the current room).
        /// </summary>
        private void ExportAllModifiedRoomFiles()
        {
            EditorConsole.Log("Exporting all modified room files...");

            var savers = new List<KeyValuePair<string, KeyValuePair<Action<FileStream>, string>>>();
            AddSaver(savers, "ETS", Globals.FilePathETS, FileManager.SaveFileETS);
            AddSaver(savers, "ITA", Globals.FilePathITA, FileManager.SaveFileITA);
            AddSaver(savers, "AEV", Globals.FilePathAEV, FileManager.SaveFileAEV);
            AddSaver(savers, "DSE", Globals.FilePathDSE, FileManager.SaveFileDSE);
            AddSaver(savers, "AVL", Globals.FilePathAVL, FileManager.SaveFileAVL);
            AddSaver(savers, "FSE", Globals.FilePathFSE, FileManager.SaveFileFSE);
            AddSaver(savers, "SAR", Globals.FilePathSAR, FileManager.SaveFileSAR);
            AddSaver(savers, "EAR", Globals.FilePathEAR, FileManager.SaveFileEAR);
            AddSaver(savers, "EMI", Globals.FilePathEMI, FileManager.SaveFileEMI);
            AddSaver(savers, "ESE", Globals.FilePathESE, FileManager.SaveFileESE);
            AddSaver(savers, "LIT", Globals.FilePathLIT, FileManager.SaveFileLIT);
            //ESL/quadcustom are not room specific

            int saved = 0;
            foreach (KeyValuePair<string, KeyValuePair<Action<FileStream>, string>> entry in savers)
            {
                string originalPath = entry.Value.Value;
                Action<FileStream> saver = entry.Value.Key;
                try
                {
                    using (FileStream fs = File.Create(originalPath))
                    {
                        saver(fs);
                    }
                    saved++;
                    EditorConsole.Log("Saved " + Path.GetFileName(originalPath));
                }
                catch (Exception ex)
                {
                    EditorConsole.Error("Failed to save " + Path.GetFileName(originalPath) + ": " + ex.Message);
                }
            }

            if (saved > 0) glControl.Invalidate();
        }

        private void repackRoomToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Re4QuadExtremeEditor.src.Class.ExternalToolManager.RepackRoomUdas(false);
        }

        private void unpackAllRoomsUHDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool deleteLFS = false;

            var result = MessageBox.Show(
                "Would you like to delete original '.lfs' compressed files and keep only new uncompressed '.udas'?\n\n(Keeping original will significantly fill disk space)",
                "Delete uncompressed .lfs files?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
                deleteLFS = true;

            Re4QuadExtremeEditor.src.Class.ExternalToolManager.UnpackAllRoomsUdas(deleteLFS);
        }

        private void unpackAllTexturesUHDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
            "Choose the unpack mode:\n\nYes = ImagePackHD\nNo = ImagePack (SD)",
            "Select Unpack Mode",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

            string imagepackType = null;

            if (result == DialogResult.Yes)
            {
                imagepackType = "ImagePackHD";
            }
            else if (result == DialogResult.No)
            {
                imagepackType = "ImagePack";
            }
            Re4QuadExtremeEditor.src.Class.ExternalToolManager.UnpackAllPacks(imagepackType);
        }

        #endregion

        private bool AppStillIdle
        {
            get
            {
                System.Windows.Forms.Message msg;
                return !PeekMessage(out msg, System.IntPtr.Zero, 0, 0, 0);
            }
        }

        private void StartRenderLoop()
        {
            if (!renderLoopStarted)
            {
                renderLoopStarted = true;
                Application.Idle += RenderLoop_Idle;
            }
        }

        private Object3D gridSnapNode;
        private UndoSystem.FullTransformState gridSnapState;
        private bool gridSnapValid;

        private void SnapshotGridUndoPosition()
        {
            gridSnapValid = false;
            try
            {
                Object3D n = DataBase.LastSelectNode as Object3D;
                if (n == null || n.Parent == null) return;
                gridSnapState = UndoSystem.CaptureFullTransform(n);
                gridSnapNode = n;
                gridSnapValid = true;
            }
            catch { }
        }

        private void PropertyGridObjs_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e)
        {
            try
            {
                string pname = "";
                if (e != null && e.ChangedItem != null && e.ChangedItem.PropertyDescriptor != null)
                    pname = e.ChangedItem.PropertyDescriptor.Name ?? "";

                bool transformAxis = System.Text.RegularExpressions.Regex.IsMatch(
                    pname, "(Position|Pos|Rotation|Rot|Scale)[XYZ]$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (transformAxis)
                {
                    Object3D n = DataBase.LastSelectNode as Object3D;
                    if (n != null && gridSnapValid && n == gridSnapNode)
                    {
                        Object3D captured = n;
                        UndoSystem.PushFullTransform(
                            new System.Collections.Generic.List<Object3D> { n },
                            new System.Collections.Generic.List<UndoSystem.FullTransformState> { gridSnapState },
                            delegate () { return new UndoSystem.FullTransformState[] { UndoSystem.CaptureFullTransform(captured) }; },
                            null);
                        if (glControl != null && !glControl.IsDisposed) glControl.Invalidate();
                    }
                }
            }
            catch { }
            finally
            {
                SnapshotGridUndoPosition();
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_ENTERMENULOOP = 0x0211;
            const int WM_EXITMENULOOP = 0x0212;

            if (m.Msg == WM_ENTERMENULOOP)
            {
                // Any menu (bar dropdown, submenu) entered its modal loop:
                // pause GL frames so the UI thread stays fully responsive.
                uiMenuActive = true;
            }
            else if (m.Msg == WM_EXITMENULOOP)
            {
                uiMenuActive = false;
                if (glControl != null && !glControl.IsDisposed) glControl.Invalidate();
            }

            base.WndProc(ref m);
        }

        private void RenderLoop_Idle(object sender, EventArgs e)
        {
            // View-menu limiter: 0 means "unlimited" with a 240 fps safety cap
            double targetFps = Globals.FpsLimit > 0 ? Globals.FpsLimit : 240.0;
            double frameTime = System.Diagnostics.Stopwatch.Frequency / targetFps;

            //when VSync is active the swap itself blocks at the monitor refresh
            //rate - that is perfect pacing. Adding the software limiter ON TOP
            //makes the two rates beat against each other (e.g. 240 fps cap vs
            //140 Hz screen), producing uneven frame cadence that looks like
            //motion blur / judder. So with vsync: no software waiting at all.
            bool vsyncPaced = false;
            try { vsyncPaced = glControl != null && !glControl.IsDisposed && glControl.VSync; } catch { }

            while (AppStillIdle
                && glControl != null && !glControl.IsDisposed && glControl.Visible
                && !IsDisposed && !Disposing)
            {
                // While a menu popup is active, throttle GL work to ~30 fps
                // instead of the full rate: the UI thread stays almost fully
                // free for hover/open messages (menus feel instant) yet the
                // 3D viewport keeps breathing instead of hard-freezing.
                if (uiMenuActive || MenuPopupIsActive())
                {
                    int nowMs = System.Environment.TickCount;
                    if (nowMs - lastMenuRenderTick >= 33)
                    {
                        lastMenuRenderTick = nowMs;
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(4);
                        continue;
                    }
                }

                if (!vsyncPaced)
                {
                    double now = renderLoopClock.ElapsedTicks;

                    if (nextRenderLoopTick < now - frameTime * 2)
                    {
                        nextRenderLoopTick = now; // fell far behind, resync
                    }

                    if (now < nextRenderLoopTick)
                    {
                        double remainingMs = (nextRenderLoopTick - now) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                        if (remainingMs > 2.0)
                        {
                            System.Threading.Thread.Sleep(1);
                            continue;
                        }
                        System.Threading.SpinWait.SpinUntil(() => renderLoopClock.ElapsedTicks >= nextRenderLoopTick, 2);
                        continue;
                    }

                    nextRenderLoopTick = Math.Max(now, nextRenderLoopTick) + frameTime;
                }

                //advance camera input exactly once per frame, dt-scaled
                long frameTick = renderLoopClock.ElapsedTicks;
                double camDt = lastCameraFrameTick < 0
                    ? 0.0071
                    : (frameTick - lastCameraFrameTick) / (double)System.Diagnostics.Stopwatch.Frequency;
                lastCameraFrameTick = frameTick;
                if (camDt > 0.05) camDt = 0.05;
                UpdateCameraPerFrame(camDt);

                glControl.Invalidate();
                glControl.Update(); // force synchronous paint -> renders + swaps
            }
        }

        #endregion

        bool RenderSelectViewer = false;
        private void toolStripMenuItemRenderSelectViewer_Click(object sender, EventArgs e)
        {
            RenderSelectViewer = !RenderSelectViewer;
            glControl.Invalidate();
        }

        private void toolStripMenuItemSnakeEnemyMode_Click(object sender, EventArgs e)
        {
            Globals.RenderSnakeAsEnemy = toolStripMenuItemSnakeEnemyMode.Checked;
            glControl.Invalidate();
        }

        #endregion


        #region botões do menu edit

        private void toolStripMenuItemAddNewObj_Click(object sender, EventArgs e)
        {
            AddNewObjForm form = new AddNewObjForm();
            form.OnButtonOk_Click += OnButtonOk_Click;
            form.TreeViewDisableDrawNode += TreeViewDisableDrawNode;
            form.TreeViewEnableDrawNode += TreeViewEnableDrawNode;
            form.ShowDialog();
        }

        private void OnButtonOk_Click()
        {
            UpdateTreeViewObjs();
            UpdatePropertyGrid();
            UpdateGL();
        }

        private void toolStripMenuItemDeleteSelectedObj_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(Lang.GetText(eLang.DeleteObjDialog), Lang.GetText(eLang.DeleteObjWarning), MessageBoxButtons.OKCancel) == DialogResult.OK)
            {
                Object3D[] itemsToDelete = new Object3D[treeViewObjs.SelectedNodes.Count];
                treeViewObjs.SelectedNodes.Values.CopyTo(itemsToDelete, 0);
                Array.Sort(itemsToDelete, (a, b) => b.Index.CompareTo(a.Index));

                var rtpIds = new List<ushort>();
                var camIds = new List<ushort>();
                var camZoneIds = new List<ushort>();
                foreach (Object3D item in itemsToDelete)
                {
                    if (item.Group == GroupType.RTP)
                        rtpIds.Add(item.ObjLineRef);
                    else if (item.Group == GroupType.CAM)
                        camIds.Add(item.ObjLineRef);
                    else if (item.Group == GroupType.CAM_ZONE)
                        camZoneIds.Add(item.ObjLineRef);
                }

                foreach (Object3D item in itemsToDelete)
                {
                    try
                    {
                        if (item.Group == GroupType.RTP || item.Group == GroupType.CAM || item.Group == GroupType.CAM_ZONE) continue;

                        if (item.Group == GroupType.ETS
                            || item.Group == GroupType.DSE
                            || item.Group == GroupType.SMX
                            || item.Group == GroupType.AVL
                            || item.Group == GroupType.FSE
                            || item.Group == GroupType.SAR
                            || item.Group == GroupType.EAR
                            || item.Group == GroupType.ESE
                            || item.Group == GroupType.EMI
                            || item.Group == GroupType.QUAD_CUSTOM
                            || item.Group == GroupType.LIT_ENTRYS
                            || item.Group == GroupType.LIT_GROUPS
                            || item.Group == GroupType.EFF_EffectEntry
                            || item.Group == GroupType.EFF_Table0
                            || item.Group == GroupType.EFF_Table1
                            || item.Group == GroupType.EFF_Table2
                            || item.Group == GroupType.EFF_Table3
                            || item.Group == GroupType.EFF_Table4
                            || item.Group == GroupType.EFF_Table6
                            || item.Group == GroupType.EFF_Table9
                            || item.Group == GroupType.EFF_Table7_Effect_0
                            || item.Group == GroupType.EFF_Table8_Effect_1
                            )
                        {
                            var parent = item.Parent;

                            if (parent is src.Class.Interfaces.INodeChangeAmount nodeGroup)
                            {
                                item.Remove();
                                nodeGroup.ChangeAmountMethods.RemoveLineID(item.ObjLineRef);
                            }

                            if (parent is src.Class.Interfaces.IChangeAmountIndexFix nodeIndexFix)
                            {
                                nodeIndexFix.OnDeleteNode();
                            }
                        }
                        else if (item.Group == GroupType.ITA || item.Group == GroupType.AEV)
                        {
                            DataBase.Extras.RemoveObj(item.ObjLineRef, Utils.GroupTypeToSpecialFileFormat(item.Group));
                            var ChangeAmountMethods = ((SpecialNodeGroup)item.Parent).ChangeAmountMethods;
                            item.Remove();
                            ChangeAmountMethods.RemoveLineID(item.ObjLineRef);
                        }
                    }
                    catch { }
                }

                if (rtpIds.Count > 0)
                {
                    // batch undo: one Ctrl+Z restores all deleted RTP nodes (snapshot before/after)
                    var rtpFile = DataBase.FileRTP;
                    Re4QuadExtremeEditor.src.Class.Files.RtpSnapshot rtpBefore = null;
                    if (rtpFile != null) try { rtpBefore = rtpFile.CaptureSnapshot(); } catch { }
                    bool oldSuppress = Re4QuadExtremeEditor.src.Class.Files.File_RTP_Group.SuppressUndo;
                    Re4QuadExtremeEditor.src.Class.Files.File_RTP_Group.SuppressUndo = true;
                    try
                    {
                        rtpIds.Sort((a, b) => b.CompareTo(a));
                        foreach (ushort id in rtpIds)
                        {
                            try { DataBase.NodeRTP.ChangeAmountMethods.RemoveLineID(id); } catch { }
                        }
                    }
                    finally { Re4QuadExtremeEditor.src.Class.Files.File_RTP_Group.SuppressUndo = oldSuppress; }
                    if (rtpFile != null && rtpBefore != null)
                    {
                        try
                        {
                            var rtpAfter = rtpFile.CaptureSnapshot();
                            Re4QuadExtremeEditor.src.Class.UndoSystem.PushRtp(rtpFile, rtpBefore, rtpAfter, "delete " + rtpIds.Count + " RTP node(s)");
                        }
                        catch { }
                    }
                }

                if (camIds.Count > 0)
                {
                    camIds.Sort((a, b) => b.CompareTo(a));
                    foreach (ushort id in camIds)
                    {
                        try { DataBase.NodeCAM.ChangeAmountMethods.RemoveLineID(id); } catch { }
                    }
                }

                if (camZoneIds.Count > 0)
                {
                    camZoneIds.Sort((a, b) => b.CompareTo(a));
                    foreach (ushort id in camZoneIds)
                    {
                        try { DataBase.NodeCAM_Zone.ChangeAmountMethods.RemoveLineID(id); } catch { }
                    }
                }

                TreeViewUpdateSelectedsClear();
                glControl.Invalidate();
            }
        }

        private void toolStripMenuItemMoveUp_Click(object sender, EventArgs e)
        {
            var ordernedSelectedNodes = treeViewObjs.SelectedNodes.Values.OrderBy(n => n.Index);
            foreach (Object3D item in ordernedSelectedNodes)
            {
                if (item.Group == GroupType.ETS
                    || item.Group == GroupType.ITA
                    || item.Group == GroupType.AEV
                    || item.Group == GroupType.DSE
                    || item.Group == GroupType.SMX
                        || item.Group == GroupType.AVL
                    || item.Group == GroupType.FSE
                    || item.Group == GroupType.SAR
                    || item.Group == GroupType.EAR
                    || item.Group == GroupType.ESE
                    || item.Group == GroupType.EMI
                    || item.Group == GroupType.QUAD_CUSTOM
                    || item.Group == GroupType.LIT_ENTRYS
                    || item.Group == GroupType.LIT_GROUPS
                    || item.Group == GroupType.EFF_EffectEntry
                    || item.Group == GroupType.EFF_Table0
                    || item.Group == GroupType.EFF_Table1
                    || item.Group == GroupType.EFF_Table2
                    || item.Group == GroupType.EFF_Table3
                    || item.Group == GroupType.EFF_Table4
                    || item.Group == GroupType.EFF_Table6
                    || item.Group == GroupType.EFF_Table9
                    || item.Group == GroupType.EFF_Table7_Effect_0
                    || item.Group == GroupType.EFF_Table8_Effect_1
                    )
                {
                    int index = item.Index;
                    if (index > 0)
                    {
                        var Parent = item.Parent;
                        item.Remove();
                        Parent.Nodes.Insert(index -1, item);

                        if (Parent is src.Class.Interfaces.IChangeAmountIndexFix nodeIndexFix)
                        {
                            nodeIndexFix.OnMoveNode();
                            UpdatePropertyGrid();
                        }
                    }
                }
            }
        }

        private void toolStripMenuItemMoveDown_Click(object sender, EventArgs e)
        {
            var ordernedSelectedNodes = treeViewObjs.SelectedNodes.Values.OrderByDescending(n => n.Index);
            foreach (Object3D item in ordernedSelectedNodes)
            {
                if (item.Group == GroupType.ETS
                    || item.Group == GroupType.ITA
                    || item.Group == GroupType.AEV
                    || item.Group == GroupType.DSE
                    || item.Group == GroupType.SMX
                        || item.Group == GroupType.AVL
                    || item.Group == GroupType.FSE
                    || item.Group == GroupType.SAR
                    || item.Group == GroupType.EAR
                    || item.Group == GroupType.ESE
                    || item.Group == GroupType.EMI
                    || item.Group == GroupType.QUAD_CUSTOM
                    || item.Group == GroupType.LIT_ENTRYS
                    || item.Group == GroupType.LIT_GROUPS
                    || item.Group == GroupType.EFF_EffectEntry
                    || item.Group == GroupType.EFF_Table0
                    || item.Group == GroupType.EFF_Table1
                    || item.Group == GroupType.EFF_Table2
                    || item.Group == GroupType.EFF_Table3
                    || item.Group == GroupType.EFF_Table4
                    || item.Group == GroupType.EFF_Table6
                    || item.Group == GroupType.EFF_Table9
                    || item.Group == GroupType.EFF_Table7_Effect_0
                    || item.Group == GroupType.EFF_Table8_Effect_1
                    )
                {
                    int index = item.Index;
                    var Parent = item.Parent;
                    if (index < Parent.GetNodeCount(false) -1)
                    {
                        item.Remove();
                        Parent.Nodes.Insert(index +1, item);

                        if (Parent is src.Class.Interfaces.IChangeAmountIndexFix nodeIndexFix)
                        {
                            nodeIndexFix.OnMoveNode();
                            UpdatePropertyGrid();
                        }
                    }
                }
            }
        }


        private bool TryOpenEnemyAssociatedITAItemSearch()
        {
            var enemy = propertyGridObjs == null ? null : propertyGridObjs.SelectedObject as EnemyProperty;
            if (enemy == null)
            {
                return false;
            }

            var gridItem = propertyGridObjs.SelectedGridItem;
            if (gridItem?.PropertyDescriptor == null ||
                gridItem.PropertyDescriptor.Name != nameof(EnemyProperty.AssociatedITAItemNumber_ListBox))
            {
                return false;
            }

            SearchForm search = new SearchForm(
                ListBoxProperty.ItemsList.Values.ToArray(),
                new UshortObjForListBox(enemy.AssociatedITAItemNumber, ""));

            search.Search += obj =>
            {
                if (obj is UshortObjForListBox item)
                {
                    enemy.AssociatedITAItemNumber_ListBox = item;
                    propertyGridObjs.Refresh();
                }
            };

            search.ShowDialog();
            return true;
        }

        /// <summary>
        /// F2 com a celula do Key ID selecionada abre a busca de itens/chaves;
        /// </summary>
        private bool TryOpenAvlKeyIdSearch()
        {
            var avl = propertyGridObjs == null ? null : propertyGridObjs.SelectedObject as NewAge_AVL_Property;
            if (avl == null)
            {
                return false;
            }

            var gridItem = propertyGridObjs.SelectedGridItem;
            if (gridItem?.PropertyDescriptor == null ||
                gridItem.PropertyDescriptor.Name != nameof(NewAge_AVL_Property.AVL_KeyId))
            {
                return false;
            }

            List<UshortObjForListBox> list = new List<UshortObjForListBox>();
            foreach (var pair in AVL_ItemNames.List.OrderBy(p => p.Key))
            {
                string desc = pair.Value.Length > 0
                    ? "0x" + pair.Key.ToString("X4") + ": " + pair.Value
                    : "0x" + pair.Key.ToString("X4");
                list.Add(new UshortObjForListBox(pair.Key, desc));
            }

            SearchForm search = new SearchForm(
                list.ToArray(),
                new UshortObjForListBox(avl.AVL_KeyId, ""));

            search.Search += obj =>
            {
                if (obj is UshortObjForListBox item)
                {
                    avl.AVL_KeyId = item.ID;
                    propertyGridObjs.Refresh();
                }
            };

            search.ShowDialog();
            return true;
        }

        /// <summary>
        /// True when keyboard focus currently sits in any text-input surface
        /// (search boxes, tool strip fields, combo boxes, the property grid),
        /// so letter shortcuts like F must type instead of jumping.
        /// </summary>
        private bool FocusIsInTextInput()
        {
            try
            {
                Control c = this.ActiveControl;
                int depth = 0;
                while (c != null && depth++ < 8)
                {
                    if (c is TextBoxBase || c is ComboBox || c is ToolStrip)
                    {
                        return true;
                    }
                    if (c is PropertyGrid)
                    {
                        // Browsing the grid must NOT block global shortcuts
                        // (undo/redo/delete/isolate...). Only an OPEN value
                        // editor counts as typing.
                        if (GridEditorHasFocus((PropertyGrid)c)) return true;
                    }
                    ContainerControl cc = c as ContainerControl;
                    c = cc != null ? cc.ActiveControl : null;
                }
            }
            catch { }
            return false;
        }

        private static bool GridEditorHasFocus(Control root)
        {
            int depth = 0;
            Control c = root;
            var stack = new List<Control>();
            stack.Add(root);
            while (stack.Count > 0 && depth++ < 64)
            {
                Control cur = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                if (cur is TextBoxBase && cur.ContainsFocus) return true;
                foreach (Control child in cur.Controls) stack.Add(child);
            }
            return false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            //ESC leaves the "Enter Camera View" first-person preview
            if (keyData == Keys.Escape && NewAgeTheRender.TheRender.CameraViewState.Enabled)
            {
                NewAgeTheRender.TheRender.CameraViewState.Enabled = false;
                glControl.Invalidate();
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Camera view OFF");
                return true;
            }

            //Alt+WASD precision flight: swallow the camera-key Alt combos so
            //menu mnemonics (e.g. &W items) never fire while holding Alt.
            //ProcessCmdKey runs BEFORE the focused control sees the key, so
            //consuming here must set the movement flags itself - otherwise
            //glControl.KeyDown would never receive them and flying would die.
            //Key-up still reaches glControl.KeyUp normally (SYSKEYUP).
            if (!FocusIsInTextInput()
                && (keyData & Keys.Alt) != 0 && (keyData & Keys.KeyCode) != Keys.Alt)
            {
                switch (keyData & Keys.KeyCode)
                {
                    case Keys.W: isWDown = true; return true;
                    case Keys.S: isSDown = true; return true;
                    case Keys.A: isADown = true; return true;
                    case Keys.D: isDDown = true; return true;
                    case Keys.Space: isSpaceDown = true; return true;
                    case Keys.C: isCDown = true; return true;
                }
            }

            if (keyData == Keys.F2 && TryOpenEnemyAssociatedITAItemSearch())
            {
                return true;
            }

            if (keyData == Keys.F2 && TryOpenAvlKeyIdSearch())
            {
                return true;
            }

            // F = jump instantly to the current selection, available from anywhere
            // in the main window (tree, viewport...). Skipped while typing.
            if (keyData == Keys.F && !FocusIsInTextInput())
            {
                FocusOnSelection();
                return true;
            }

            // G = Get: bring the selection to the camera, same as the panel button.
            // Skipped while typing so text boxes keep working.
            if (keyData == Keys.G && !FocusIsInTextInput())
            {
                cameraMove.DoGet();
                return true;
            }

            // E = Enter Camera View on the selected CAM keyframe node.
            if (keyData == Keys.E && !FocusIsInTextInput())
            {
                ToggleEnterCamView();
                return true;
            }

            // H = toggle: isolate ONLY the selected objects / restore everything.
            if (keyData == Keys.H && !FocusIsInTextInput())
            {
                EditorConsole.Log("Toggle isolate selection");
                ToggleIsolateFromSelection();
                return true;
            }

            // Ctrl+D = duplicate the current selection next to itself.
            if (keyData == (Keys.Control | Keys.D) && !FocusIsInTextInput())
            {
                EditorConsole.Log("Duplicate selection");
                DuplicateSelection();
                return true;
            }

            // X / Delete = remove the selected objects (same confirm dialog
            // as the Edit > delete menu entry).
            if ((keyData == Keys.Delete || keyData == Keys.X) && !FocusIsInTextInput())
            {
                EditorConsole.Log("Delete selected objects");
                toolStripMenuItemDeleteSelectedObj_Click(this, EventArgs.Empty);
                return true;
            }

            // Ctrl+Z / Ctrl+Y = undo / redo (gizmo moves and duplicates).
            if (keyData == (Keys.Control | Keys.Z) && !FocusIsInTextInput())
            {
                UndoSystem.Undo();
                glControl.Invalidate();
                treeViewObjs.Refresh();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Y) && !FocusIsInTextInput())
            {
                UndoSystem.Redo();
                glControl.Invalidate();
                treeViewObjs.Refresh();
                return true;
            }

            // Ctrl+S = quick save project (falls back to Save As if no path set).
            if (keyData == (Keys.Control | Keys.S) && !FocusIsInTextInput())
            {
                TrySaveProject();
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("Project saved");
                return true;
            }

            // 1 / 2 / 3 / 0 = snap grid presets 1.0 / 0.1 / 0.01 / off.
            if (!FocusIsInTextInput())
            {
                switch (keyData)
                {
                    case Keys.D1:
                    case Keys.NumPad1: ApplySnapStep(1f); return true;
                    case Keys.D2:
                    case Keys.NumPad2: ApplySnapStep(0.1f); return true;
                    case Keys.D3:
                    case Keys.NumPad3: ApplySnapStep(0.01f); return true;
                    case Keys.D0:
                    case Keys.NumPad0: ApplySnapStep(0f); return true;
                }
                // Gizmo — exact Re4QuadX parity: T toggles Move/Rotate, Y toggles World/Local, Q toggles gizmo on/off
                if (keyData == Keys.T)
                {
                    SetGizmoTool(Globals.CurrentTool == EditorTool.Move ? EditorTool.Rotate : EditorTool.Move);
                    return true;
                }
                if (keyData == Keys.Y)
                {
                    ToggleGizmoSpace();
                    return true;
                }
                if (keyData == Keys.Q)
                {
                    Re4QuadExtremeEditor.src.Class.Gizmo.Enabled = !Re4QuadExtremeEditor.src.Class.Gizmo.Enabled;
                    // sync toolbar toggle button
                    try
                    {
                        foreach (Control c in viewportGizmoBar.Controls)
                            if (c is Button b && b.Tag as string == "toggle")
                                UpdateGizmoToggleButton(b);
                    }
                    catch { }
                    EditorConsole.Log("Gizmo " + (Re4QuadExtremeEditor.src.Class.Gizmo.Enabled ? "ON" : "OFF"));
                    glControl.Invalidate();
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void toolStripMenuItemSearch_Click(object sender, EventArgs e)
        {
            // F2 on Enemy -> Associated ITA Item -> Item Number (List) must search Items,
            // not the normal Enemy list.
            if (TryOpenEnemyAssociatedITAItemSearch())
            {
                return;
            }

            // F2 on AVL -> Key ID must search the item/key list
            if (TryOpenAvlKeyIdSearch())
            {
                return;
            }

            var selectedObj = propertyGridObjs.SelectedObject;
            if (selectedObj is EnemyProperty enemy)
            {
                SearchForm search = new SearchForm(ListBoxProperty.EnemiesList.Values.ToArray(), new UshortObjForListBox(enemy.ReturnUshortFirstSearchSelect(), ""));
                search.Search += enemy.Searched;
                search.ShowDialog();
            }
            else if (selectedObj is EtcModelProperty etcModel)
            {
                SearchForm search = new SearchForm(ListBoxProperty.EtcmodelsList.Values.ToArray(), new UshortObjForListBox(etcModel.ReturnUshortFirstSearchSelect(), ""));
                search.Search += etcModel.Searched;
                search.ShowDialog();
            }
            else if (selectedObj is SpecialProperty special)
            {
                var specialType = special.GetSpecialType();

                // F2 on ITA "Enemy (ESL-Model)" property -> search Enemies
                if (DataBase.FileITA != null && DataBase.FileITA.Lines.ContainsKey(special.GetInternalID()))
                {
                    var gridItem = propertyGridObjs.SelectedGridItem;
                    if (gridItem != null && gridItem.Label == "Enemy (ESL-Model)")
                    {
                        SearchForm search = new SearchForm(ListBoxProperty.EnemiesList.Values.ToArray(),
                            new UshortObjForListBox(special.ReturnUshortFirstSearchSelect(), ""));
                        search.Search += (obj) =>
                        {
                            if (obj is UshortObjForListBox u && u.ID < 0xFFFF)
                            {
                                special.SetEnemyIDIntoPO(u.ID);
                                propertyGridObjs.Refresh();
                            }
                        };
                        search.ShowDialog();
                        return;
                    }
                }

                if (specialType == SpecialType.T03_Items || specialType == SpecialType.T11_ItemDependentEvents)
                {
                    SearchForm search = new SearchForm(ListBoxProperty.ItemsList.Values.ToArray(), new UshortObjForListBox(special.ReturnUshortFirstSearchSelect(), ""));
                    search.Search += special.Searched;
                    search.ShowDialog();
                }
            }
            else if (selectedObj is QuadCustomProperty quad)
            {
                SearchForm search = new SearchForm(ListBoxProperty.QuadCustomModelIDList.Values.ToArray(), new UintObjForListBox(quad.ReturnUshortFirstSearchSelect(), ""));
                search.Search += quad.Searched;
                search.ShowDialog();
            }

        }


        #endregion


        #region Botoes do menu

        private void SelectRoom_onLoadButtonClick(object sender, EventArgs e)
        {
            if (sender is string == false && sender != null)
            {
                string text = Lang.GetText(eLang.SelectedRoom) + ": " + sender.ToString();
                if (text.Length > 100)
                {
                    text = text.Substring(0,100);
                    text += "...";
                }
                toolStripMenuItemSelectRoom.Text = text;
                EditorConsole.Log("Room loaded: " + sender.ToString());
            }
            else
            {
                toolStripMenuItemSelectRoom.Text = Lang.GetText(eLang.SelectRoom);
            }

            if (Globals.AutoDefinedRoom)
            {
                if (DataBase.SelectedRoom != null)
                {
                    toolStripTextBoxDefinedRoom.Text = DataBase.SelectedRoom.GetRoomId().ToString("X4");
                }
                else
                {
                    toolStripTextBoxDefinedRoom.Text = "0000";
                }
            }
        }

        private void toolStripMenuItemSelectRoom_Click(object sender, EventArgs e)
        {
            SelectRoomForm selectRoom = new SelectRoomForm();
            selectRoom.onLoadButtonClick += SelectRoom_onLoadButtonClick;
            selectRoom.ShowDialog();
            glControl.Invalidate();
        }

        private void toolStripMenuItemClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void toolStripMenuItemCredits_Click(object sender, EventArgs e)
        {
            CreditsForm form = new CreditsForm();
            form.ShowDialog();
        }

        private void toolStripMenuItemOptions_Click(object sender, EventArgs e)
        {
            OptionsForm form = new OptionsForm();
            form.OnOKButtonClick += OptionsForm_ApplyLanguageLive;
            form.OnOKButtonClick += OptionsForm_OnOKButtonClick;
            form.OnOKButtonClick += UpdateTreeViewObjs;
            form.OnOKButtonClick += UpdatePropertyGrid;
            try
            {
                System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(form);
                helper.Owner = this.Handle;
                form.ShowDialog();
            }
            catch (Exception ex)
            {
                EditorConsole.Log("Options failed: " + ex.Message);
            }
            glControl.Invalidate();
        }

        private void toolStripMenuItemEnemyTemplates_Click(object sender, EventArgs e)
        {
            try
            {
                EnemyTemplateWindow form = new EnemyTemplateWindow();
                System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(form);
                helper.Owner = this.Handle;
                form.ShowDialog();
            }
            catch (Exception ex)
            {
                EditorConsole.Log("Enemy Templates error: " + ex.ToString());
                MessageBox.Show("Enemy Templates error:\n\n" + ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenElementLibrary()
        {
            try
            {
                ElementLibraryWindow form = new ElementLibraryWindow();
                System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(form);
                helper.Owner = this.Handle;
                form.ShowDialog();
            }
            catch (Exception ex)
            {
                EditorConsole.Log("Element Library error: " + ex.ToString());
                MessageBox.Show("Element Library error:\n\n" + ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void toolStripMenuItemOpenElementLibrary_Click(object sender, EventArgs e)
        {
            OpenElementLibrary();
        }

        private void OptionsForm_ApplyLanguageLive()
        {
            Re4QuadExtremeEditor.src.JSON.Configs cfg = Globals.BackupConfigs;
            if (cfg != null && cfg.LoadLangTranslation && !string.IsNullOrEmpty(cfg.LangJsonFile))
            {
                Utils.StartLoadLangFile();
            }
            else
            {
                Lang.RestoreEnglishDefaults();
                Lang.LoadedTranslation = false;
            }
            ApplyTranslationLive();
        }

        private void OptionsForm_OnOKButtonClick()
        {
            TreeViewUpdateSelectedsClear();
        }

        private void toolStripMenuItemCameraMenu_Click(object sender, EventArgs e)
        {
            CameraForm cameraForm = new CameraForm(ref camera, UpdateGL, UpdateCameraMatrix);
            cameraForm.ShowDialog();
        }

        #endregion


        #region botoes do menu view

        private void toolStripMenuItemHideFileEFF_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideFileEFF.Checked = !toolStripMenuItemHideFileEFF.Checked;
            Globals.RenderFileEFFBLOB = !toolStripMenuItemHideFileEFF.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideFileLIT_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideFileLIT.Checked = !toolStripMenuItemHideFileLIT.Checked;
            Globals.RenderFileLIT = !toolStripMenuItemHideFileLIT.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideRoomModel_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideRoomModel.Checked = !toolStripMenuItemHideRoomModel.Checked;
            Globals.RenderRoom = !toolStripMenuItemHideRoomModel.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideEnemyESL_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideEnemyESL.Checked = !toolStripMenuItemHideEnemyESL.Checked;
            Globals.RenderEnemyESL = !toolStripMenuItemHideEnemyESL.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideEtcmodelETS_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideEtcmodelETS.Checked = !toolStripMenuItemHideEtcmodelETS.Checked;
            Globals.RenderEtcmodelETS = !toolStripMenuItemHideEtcmodelETS.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideItemsITA_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideItemsITA.Checked = !toolStripMenuItemHideItemsITA.Checked;
            Globals.RenderItemsITA = !toolStripMenuItemHideItemsITA.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideEventsAEV_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideEventsAEV.Checked = !toolStripMenuItemHideEventsAEV.Checked;
            Globals.RenderEventsAEV = !toolStripMenuItemHideEventsAEV.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }


        private void toolStripMenuItemHideFileFSE_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideFileFSE.Checked = !toolStripMenuItemHideFileFSE.Checked;
            Globals.RenderFileFSE = !toolStripMenuItemHideFileFSE.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideFileCAM_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideFileCAM.Checked = !toolStripMenuItemHideFileCAM.Checked;
            Globals.RenderFileCAM = !toolStripMenuItemHideFileCAM.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideFileCAM_ZONE_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideFileCAM_ZONE.Checked = !toolStripMenuItemHideFileCAM_ZONE.Checked;
            Globals.RenderFileCAM_Zone = !toolStripMenuItemHideFileCAM_ZONE.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideFileRTP_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideFileRTP.Checked = !toolStripMenuItemHideFileRTP.Checked;
            Globals.RenderFileRTP = !toolStripMenuItemHideFileRTP.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideFileSAR_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideFileSAR.Checked = !toolStripMenuItemHideFileSAR.Checked;
            Globals.RenderFileSAR = !toolStripMenuItemHideFileSAR.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideFileEAR_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideFileEAR.Checked = !toolStripMenuItemHideFileEAR.Checked;
            Globals.RenderFileEAR = !toolStripMenuItemHideFileEAR.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideFileESE_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideFileESE.Checked = !toolStripMenuItemHideFileESE.Checked;
            Globals.RenderFileESE = !toolStripMenuItemHideFileESE.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideFileEMI_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideFileEMI.Checked = !toolStripMenuItemHideFileEMI.Checked;
            Globals.RenderFileEMI = !toolStripMenuItemHideFileEMI.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideQuadCustom_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideQuadCustom.Checked = !toolStripMenuItemHideQuadCustom.Checked;
            Globals.RenderFileQuadCustom = !toolStripMenuItemHideQuadCustom.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }


        private void toolStripMenuItemHideDesabledEnemy_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideDesabledEnemy.Checked = !toolStripMenuItemHideDesabledEnemy.Checked;
            Globals.RenderDisabledEnemy = !toolStripMenuItemHideDesabledEnemy.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripTextBoxDefinedRoom_TextChanged(object sender, EventArgs e)
        {
            Globals.RenderEnemyFromDefinedRoom = ushort.Parse(toolStripTextBoxDefinedRoom.Text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripTextBoxDefinedRoom_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            if (char.IsDigit(e.KeyChar)
                || e.KeyChar == 'A'
                || e.KeyChar == 'B'
                || e.KeyChar == 'C'
                || e.KeyChar == 'D'
                || e.KeyChar == 'E'
                || e.KeyChar == 'F'
                || e.KeyChar == 'a'
                || e.KeyChar == 'b'
                || e.KeyChar == 'c'
                || e.KeyChar == 'd'
                || e.KeyChar == 'e'
                || e.KeyChar == 'f'
                )
            {
                if (toolStripTextBoxDefinedRoom.SelectionStart < toolStripTextBoxDefinedRoom.TextLength)
                {
                    int CacheSelectionStart = toolStripTextBoxDefinedRoom.SelectionStart;
                    StringBuilder sb = new StringBuilder(toolStripTextBoxDefinedRoom.Text);
                    sb[toolStripTextBoxDefinedRoom.SelectionStart] = e.KeyChar;
                    toolStripTextBoxDefinedRoom.Text = sb.ToString();
                    toolStripTextBoxDefinedRoom.SelectionStart = CacheSelectionStart + 1;
                }
            }
            e.Handled = true;
        }


        private void toolStripMenuItemShowOnlyDefinedRoom_Click(object sender, EventArgs e)
        {
            toolStripMenuItemShowOnlyDefinedRoom.Checked = !toolStripMenuItemShowOnlyDefinedRoom.Checked;
            Globals.RenderDontShowOnlyDefinedRoom = !toolStripMenuItemShowOnlyDefinedRoom.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemAutoDefineRoom_Click(object sender, EventArgs e)
        {
            toolStripMenuItemAutoDefineRoom.Checked = !toolStripMenuItemAutoDefineRoom.Checked;
            Globals.AutoDefinedRoom = toolStripMenuItemAutoDefineRoom.Checked;
        }

        private void toolStripMenuItemHideItemTriggerZone_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideItemTriggerZone.Checked = !toolStripMenuItemHideItemTriggerZone.Checked;
            Globals.RenderItemTriggerZone = !toolStripMenuItemHideItemTriggerZone.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideItemTriggerRadius_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideItemTriggerRadius.Checked = !toolStripMenuItemHideItemTriggerRadius.Checked;
            Globals.RenderItemTriggerRadius = !toolStripMenuItemHideItemTriggerRadius.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideItemAura_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideItemAura.Checked = !toolStripMenuItemHideItemAura.Checked;
            Globals.RenderItemAura = !toolStripMenuItemHideItemAura.Checked;
            glControl.Invalidate();
        }

        private void toolStripMenuItemItemPositionAtAssociatedObjectLocation_Click(object sender, EventArgs e)
        {
            toolStripMenuItemItemPositionAtAssociatedObjectLocation.Checked = !toolStripMenuItemItemPositionAtAssociatedObjectLocation.Checked;
            Globals.RenderItemPositionAtAssociatedObjectLocation = toolStripMenuItemItemPositionAtAssociatedObjectLocation.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideExtraObjs_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideExtraObjs.Checked = !toolStripMenuItemHideExtraObjs.Checked;
            Globals.RenderExtraObjs = !toolStripMenuItemHideExtraObjs.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideSpecialTriggerZone_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideSpecialTriggerZone.Checked = !toolStripMenuItemHideSpecialTriggerZone.Checked;
            Globals.RenderSpecialTriggerZone = !toolStripMenuItemHideSpecialTriggerZone.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemUseMoreSpecialColors_Click(object sender, EventArgs e)
        {
            toolStripMenuItemUseMoreSpecialColors.Checked = !toolStripMenuItemUseMoreSpecialColors.Checked;
            Globals.UseMoreSpecialColors = toolStripMenuItemUseMoreSpecialColors.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemUseCustomColors_Click(object sender, EventArgs e)
        {
            toolStripMenuItemUseCustomColors.Checked = !toolStripMenuItemUseCustomColors.Checked;
            Globals.UseMoreQuadCustomColors = toolStripMenuItemUseCustomColors.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }


        private void toolStripMenuItemEtcModelUseScale_Click(object sender, EventArgs e)
        {
            toolStripMenuItemEtcModelUseScale.Checked = !toolStripMenuItemEtcModelUseScale.Checked;
            Globals.RenderEtcmodelUsingScale = toolStripMenuItemEtcModelUseScale.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideExtraExceptWarpDoor_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideExtraExceptWarpDoor.Checked = !toolStripMenuItemHideExtraExceptWarpDoor.Checked;
            Globals.HideExtraExceptWarpDoor = toolStripMenuItemHideExtraExceptWarpDoor.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideOnlyWarpDoor_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideOnlyWarpDoor.Checked = !toolStripMenuItemHideOnlyWarpDoor.Checked;
            Globals.RenderExtraWarpDoor = !toolStripMenuItemHideOnlyWarpDoor.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemNodeDisplayNameInHex_Click(object sender, EventArgs e)
        {
            toolStripMenuItemNodeDisplayNameInHex.Checked = !toolStripMenuItemNodeDisplayNameInHex.Checked;
            Globals.TreeNodeRenderHexValues = toolStripMenuItemNodeDisplayNameInHex.Checked;
            TreeViewDisableDrawNode();
            if (Globals.TreeNodeRenderHexValues)
            {
                treeViewObjs.Font = Globals.TreeNodeFontHex;
            }
            else 
            {
                treeViewObjs.Font = Globals.TreeNodeFontText;
            }
            TreeViewEnableDrawNode();
            treeViewObjs.Refresh();
        }

        private void toolStripMenuItemAvlNumbersInDec_Click(object sender, EventArgs e)
        {
            toolStripMenuItemAvlNumbersInDec.Checked = !toolStripMenuItemAvlNumbersInDec.Checked;
            Globals.AvlRenderDecimal = toolStripMenuItemAvlNumbersInDec.Checked;
            treeViewObjs.Refresh();
            propertyGridObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemRefresh_Click(object sender, EventArgs e)
        {
            glControl.Invalidate();
            treeViewObjs.Refresh();
            propertyGridObjs.Refresh();
            glControl.Update(); // Needed after calling propertyGridObjs.Refresh();
        }

        private void toolStripMenuItemResetCamera_Click(object sender, EventArgs e)
        {
            cameraMove.ResetCamera();
        }


        private void toolStripMenuItemHideSideMenu_Click(object sender, EventArgs e)
        {
            if (toolStripMenuItemHideLateralMenu.Checked) // fazer reaparecer
            {
                splitContainerMain.Panel1.Enabled = true;
                splitContainerMain.Panel1Collapsed = false;

                toolStripMenuItemHideLateralMenu.Checked = false;
            }
            else //fazer esconder
            {
                splitContainerMain.Panel1Collapsed = true;
                splitContainerMain.Panel1.Enabled = false;

                toolStripMenuItemHideLateralMenu.Checked = true;
            }
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideBottomMenu_Click(object sender, EventArgs e)
        {
            if (toolStripMenuItemHideBottomMenu.Checked) // fazer reaparecer
            {
                splitContainerRight.Panel2.Enabled = true;
                splitContainerRight.Panel2Collapsed = false;

                toolStripMenuItemHideBottomMenu.Checked = false;
            }
            else //fazer esconder
            {
                splitContainerRight.Panel2Collapsed = true;
                splitContainerRight.Panel2.Enabled = false;

                toolStripMenuItemHideBottomMenu.Checked = true;
            }

            glControl.Invalidate();
        }

        //------------
        private void toolStripMenuItemRoomHideTextures_Click(object sender, EventArgs e)
        {
            toolStripMenuItemRoomHideTextures.Checked = !toolStripMenuItemRoomHideTextures.Checked;
            NewAgeTheRender.RoomSelectedObj.RenderTextures = !NewAgeTheRender.RoomSelectedObj.RenderTextures;
            glControl.Invalidate();
        }

        private void toolStripMenuItemRoomWireframe_Click(object sender, EventArgs e)
        {
            toolStripMenuItemRoomWireframe.Checked = !toolStripMenuItemRoomWireframe.Checked;
            NewAgeTheRender.RoomSelectedObj.RenderWireframe = !NewAgeTheRender.RoomSelectedObj.RenderWireframe;
            glControl.Invalidate();
        }

        private void toolStripMenuItemRoomRenderNormals_Click(object sender, EventArgs e)
        {
            toolStripMenuItemRoomRenderNormals.Checked = !toolStripMenuItemRoomRenderNormals.Checked;
            NewAgeTheRender.RoomSelectedObj.RenderNormals = !NewAgeTheRender.RoomSelectedObj.RenderNormals;
            glControl.Invalidate();
        }

        private void toolStripMenuItemRoomOnlyFrontFace_Click(object sender, EventArgs e)
        {
            toolStripMenuItemRoomOnlyFrontFace.Checked = !toolStripMenuItemRoomOnlyFrontFace.Checked;
            NewAgeTheRender.RoomSelectedObj.RenderOnlyFrontFace = !NewAgeTheRender.RoomSelectedObj.RenderOnlyFrontFace;
            glControl.Invalidate();
        }

        private void toolStripMenuItemRoomVertexColor_Click(object sender, EventArgs e)
        {
            toolStripMenuItemRoomVertexColor.Checked = !toolStripMenuItemRoomVertexColor.Checked;
            NewAgeTheRender.RoomSelectedObj.RenderVertexColor = !NewAgeTheRender.RoomSelectedObj.RenderVertexColor;
            glControl.Invalidate();
        }

        private void toolStripMenuItemRoomAlphaChannel_Click(object sender, EventArgs e)
        {
            toolStripMenuItemRoomAlphaChannel.Checked = !toolStripMenuItemRoomAlphaChannel.Checked;
            NewAgeTheRender.RoomSelectedObj.RenderAlphaChannel = !NewAgeTheRender.RoomSelectedObj.RenderAlphaChannel;
            glControl.Invalidate();
        }

        private void toolStripMenuItemModelsHideTextures_Click(object sender, EventArgs e)
        {
            toolStripMenuItemModelsHideTextures.Checked = !toolStripMenuItemModelsHideTextures.Checked;
            NewAgeTheRender.ObjModel3D.RenderTextures = !NewAgeTheRender.ObjModel3D.RenderTextures;
            glControl.Invalidate();
        }

        private void toolStripMenuItemModelsWireframe_Click(object sender, EventArgs e)
        {
            toolStripMenuItemModelsWireframe.Checked = !toolStripMenuItemModelsWireframe.Checked;
            NewAgeTheRender.ObjModel3D.RenderWireframe = !NewAgeTheRender.ObjModel3D.RenderWireframe;
            glControl.Invalidate();
        }

        private void toolStripMenuItemModelsRenderNormals_Click(object sender, EventArgs e)
        {
            toolStripMenuItemModelsRenderNormals.Checked = !toolStripMenuItemModelsRenderNormals.Checked;
            NewAgeTheRender.ObjModel3D.RenderNormals = !NewAgeTheRender.ObjModel3D.RenderNormals;
            glControl.Invalidate();
        }

        private void toolStripMenuItemModelsOnlyFrontFace_Click(object sender, EventArgs e)
        {
            toolStripMenuItemModelsOnlyFrontFace.Checked = !toolStripMenuItemModelsOnlyFrontFace.Checked;
            NewAgeTheRender.ObjModel3D.RenderOnlyFrontFace = !NewAgeTheRender.ObjModel3D.RenderOnlyFrontFace;
            glControl.Invalidate();
        }

        private void toolStripMenuItemModelsVertexColor_Click(object sender, EventArgs e)
        {
            toolStripMenuItemModelsVertexColor.Checked = !toolStripMenuItemModelsVertexColor.Checked;
            NewAgeTheRender.ObjModel3D.RenderVertexColor = !NewAgeTheRender.ObjModel3D.RenderVertexColor;
            glControl.Invalidate();
        }

        private void toolStripMenuItemModelsAlphaChannel_Click(object sender, EventArgs e)
        {
            toolStripMenuItemModelsAlphaChannel.Checked = !toolStripMenuItemModelsAlphaChannel.Checked;
            NewAgeTheRender.ObjModel3D.RenderAlphaChannel = !NewAgeTheRender.ObjModel3D.RenderAlphaChannel;
            glControl.Invalidate();
        }

        private void toolStripMenuItemRoomTextureNearestLinear_Click(object sender, EventArgs e)
        {
            NewAgeTheRender.RoomSelectedObj.LoadTextureLinear = !NewAgeTheRender.RoomSelectedObj.LoadTextureLinear;
            
            toolStripMenuItemRoomTextureNearestLinear.Text =
                NewAgeTheRender.RoomSelectedObj.LoadTextureLinear ?
                Lang.GetText(eLang.toolStripMenuItemRoomTextureIsLinear) :
                Lang.GetText(eLang.toolStripMenuItemRoomTextureIsNearest) ;

            DataBase.SelectedRoom?.ChangeTextureType();

            glControl.Invalidate();
        }

        private void toolStripMenuItemModelsTextureNearestLinear_Click(object sender, EventArgs e)
        {
            NewAgeTheRender.ObjModel3D.LoadTextureLinear = !NewAgeTheRender.ObjModel3D.LoadTextureLinear;

            toolStripMenuItemModelsTextureNearestLinear.Text =
              NewAgeTheRender.ObjModel3D.LoadTextureLinear ?
              Lang.GetText(eLang.toolStripMenuItemModelsTextureIsLinear) :
              Lang.GetText(eLang.toolStripMenuItemModelsTextureIsNearest);

            Utils.ChangeTextureTypeFromModels();

            glControl.Invalidate();
        }

        private void toolStripMenuItemShowOnlySelectedGroup_Click(object sender, EventArgs e)
        {
            toolStripMenuItemShowOnlySelectedGroup.Checked = !toolStripMenuItemShowOnlySelectedGroup.Checked;
            Globals.LIT_ShowOnlySelectedGroup = toolStripMenuItemShowOnlySelectedGroup.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemSelectedGroupUp_Click(object sender, EventArgs e)
        {
            var value = int.Parse(toolStripTextBoxSelectedGroupValue.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
            value++;
            if (value > 999)
            {
                value = 999;
            }
            toolStripTextBoxSelectedGroupValue.Text = value.ToString("D3");
        }

        private void toolStripMenuItemSelectedGroupDown_Click(object sender, EventArgs e)
        {
            var value = int.Parse(toolStripTextBoxSelectedGroupValue.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
            value--;
            if (value < 0)
            {
                value = 0;
            }
            toolStripTextBoxSelectedGroupValue.Text = value.ToString("D3");
        }

        private void toolStripMenuItemEnableLightColor_Click(object sender, EventArgs e)
        {
            toolStripMenuItemEnableLightColor.Checked = !toolStripMenuItemEnableLightColor.Checked;
            Globals.LIT_EnableLightColor = toolStripMenuItemEnableLightColor.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripTextBoxSelectedGroupValue_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            if (char.IsDigit(e.KeyChar))
            {
                if (toolStripTextBoxSelectedGroupValue.SelectionStart < toolStripTextBoxSelectedGroupValue.TextLength)
                {
                    int CacheSelectionStart = toolStripTextBoxSelectedGroupValue.SelectionStart;
                    StringBuilder sb = new StringBuilder(toolStripTextBoxSelectedGroupValue.Text);
                    sb[toolStripTextBoxSelectedGroupValue.SelectionStart] = e.KeyChar;
                    toolStripTextBoxSelectedGroupValue.Text = sb.ToString();
                    toolStripTextBoxSelectedGroupValue.SelectionStart = CacheSelectionStart + 1;
                }
            }
            e.Handled = true;
        }

        private void toolStripTextBoxSelectedGroupValue_TextChanged(object sender, EventArgs e)
        {
            Globals.LIT_SelectedGroup = ushort.Parse(toolStripTextBoxSelectedGroupValue.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripTextBoxSelectedGroupValue_EFF_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            if (char.IsDigit(e.KeyChar))
            {
                if (toolStripTextBoxSelectedGroupValue_EFF.SelectionStart < toolStripTextBoxSelectedGroupValue_EFF.TextLength)
                {
                    int CacheSelectionStart = toolStripTextBoxSelectedGroupValue_EFF.SelectionStart;
                    StringBuilder sb = new StringBuilder(toolStripTextBoxSelectedGroupValue_EFF.Text);
                    sb[toolStripTextBoxSelectedGroupValue_EFF.SelectionStart] = e.KeyChar;
                    toolStripTextBoxSelectedGroupValue_EFF.Text = sb.ToString();
                    toolStripTextBoxSelectedGroupValue_EFF.SelectionStart = CacheSelectionStart + 1;
                }
            }
            e.Handled = true;
        }

        private void toolStripTextBoxSelectedGroupValue_EFF_TextChanged(object sender, EventArgs e)
        {
            Globals.EFF_SelectedGroup = ushort.Parse(toolStripTextBoxSelectedGroupValue_EFF.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemShowOnlySelectedGroup_EFF_Click(object sender, EventArgs e)
        {
            toolStripMenuItemShowOnlySelectedGroup_EFF.Checked = !toolStripMenuItemShowOnlySelectedGroup_EFF.Checked;
            Globals.EFF_ShowOnlySelectedGroup = toolStripMenuItemShowOnlySelectedGroup_EFF.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemSelectedGroupUp_EFF_Click(object sender, EventArgs e)
        {
            var value = int.Parse(toolStripTextBoxSelectedGroupValue_EFF.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
            value++;
            if (value > 999)
            {
                value = 999;
            }
            toolStripTextBoxSelectedGroupValue_EFF.Text = value.ToString("D3");
        }

        private void toolStripMenuItemSelectedGroupDown_EFF_Click(object sender, EventArgs e)
        {
            var value = int.Parse(toolStripTextBoxSelectedGroupValue_EFF.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
            value--;
            if (value < 0)
            {
                value = 0;
            }
            toolStripTextBoxSelectedGroupValue_EFF.Text = value.ToString("D3");
        }

        private void toolStripMenuItemHideTable7_EFF_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideTable7_EFF.Checked = !toolStripMenuItemHideTable7_EFF.Checked;
            Globals.EFF_RenderTable7 = !toolStripMenuItemHideTable7_EFF.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideTable8_EFF_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideTable8_EFF.Checked = !toolStripMenuItemHideTable8_EFF.Checked;
            Globals.EFF_RenderTable8 = !toolStripMenuItemHideTable8_EFF.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemHideTable9_EFF_Click(object sender, EventArgs e)
        {
            toolStripMenuItemHideTable9_EFF.Checked = !toolStripMenuItemHideTable9_EFF.Checked;
            Globals.EFF_RenderTable9 = !toolStripMenuItemHideTable9_EFF.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        private void toolStripMenuItemDisableGroupPositionEFF_Click(object sender, EventArgs e)
        {
            toolStripMenuItemDisableGroupPositionEFF.Checked = !toolStripMenuItemDisableGroupPositionEFF.Checked;
            Globals.EFF_Use_Group_Position = !toolStripMenuItemDisableGroupPositionEFF.Checked;
            treeViewObjs.Refresh();
            glControl.Invalidate();
        }

        #endregion


        #region propertyGridObjs and TreeViewObjs

        private IObject3D getSelectedObject()
        {
            if (DataBase.LastSelectNode is IObject3D node)
            {
                return node;
            }
            return null;
        }

        public void UpdateGL()
        {
            glControl.Invalidate();
        }

        private void UpdateCameraMatrix() 
        {
            camMtx = camera.GetViewMatrix();
        }

        public void UpdatePropertyGrid()
        {
            if (propertyGridObjs.SelectedObject is EnemyProperty enemyProperty)
            {
                enemyProperty.RefreshAssociatedITAPropertiesVisibility();
            }

            propertyGridObjs.Refresh();
            glControl.Update(); // Needed after calling propertyGridObjs.Refresh();
        }

        public void UpdateTreeViewObjs()
        {
            treeViewObjs.Refresh();
        }

        /// <summary>Returns the ObjLineRef (0-255) of the currently selected ESL enemy node, or null.</summary>
        public ushort? GetSelectedEnemyNode()
        {
            if (DataBase.LastSelectNode is Object3D node && node.Group == GroupType.ESL)
                return node.ObjLineRef;
            return null;
        }

        private void UpdateOrbitCamera() 
        {
            if (camera.isOrbitCamera())
            {
                camera.UpdateCameraOrbitOnChangeValue();
                camMtx = camera.GetViewMatrix();
            }
        }

        private void propertyGridObjs_Enter(object sender, EventArgs e)
        {
            InPropertyGrid = true;
        }

        private void propertyGridObjs_Leave(object sender, EventArgs e)
        {
            InPropertyGrid = false;
        }

        private void propertyGridObjs_PropertySortChanged(object sender, EventArgs e)
        {
            if (propertyGridObjs.PropertySort == PropertySort.CategorizedAlphabetical)
               {propertyGridObjs.PropertySort = PropertySort.Categorized;}
        }


        private void propertyGridObjs_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            propertyGridObjs.Refresh();
            treeViewObjs.Refresh();
        }

        private void propertyGridObjs_SelectedGridItemChanged(object sender, SelectedGridItemChangedEventArgs e)
        {
        }

        public void TreeViewUpdateSelectedsClear()
        {
            treeViewObjs.SelectedNodesClearNoRedraw();
            propertyGridObjs.SelectedObject = none;
            objectMove.UpdateSelection();
            treeViewObjs.Refresh();
            propertyGridObjs.Refresh();
        }

        private void TreeViewDisableDrawNode()
        {
            treeViewObjs.Enabled = false;
            //treeViewObjs.Visible = false;
            treeViewObjs.DisableDrawNode();
            //propertyGridObjs.Visible = false;
        }

        private void TreeViewEnableDrawNode()
        {
            treeViewObjs.EnableDrawNode();
            //treeViewObjs.Visible = true;
            treeViewObjs.Enabled = true;
            //propertyGridObjs.Visible = true;
        }

        private void treeViewObjs_AfterSelect(object sender, TreeViewEventArgs e)
        {
            bool OldLastNodeIsNull = !(DataBase.LastSelectNode is Object3D);
            //Console.WriteLine(e.Node);
            //Console.WriteLine(treeViewObjs.SelectedNodes.Count);
            if (e.Node == null || e.Node.Parent == null || treeViewObjs.SelectedNodes.Count == 0)
            {
                propertyGridObjs.SelectedObject = none;
                DataBase.LastSelectNode = null;
            }
            else if (treeViewObjs.SelectedNodes.Count == 1 && e.Node is Object3D node)
            {
                DataBase.LastSelectNode = node;

                // CAM: keep the record's SelectedKeyframe in sync with the
                // clicked Ky node (so the property grid edits THAT keyframe),
                // and while inside "Enter Camera View" jump straight to any
                // camera clicked in the tree
                if (node.Group == GroupType.CAM)
                {
                    DataBase.FileCAM?.SyncSelectedKeyframeFromNode(node.ObjLineRef);
                    if (NewAgeTheRender.TheRender.CameraViewState.Enabled
                      && NewAgeTheRender.TheRender.CameraViewState.NodeId != node.ObjLineRef)
                    {
                        NewAgeTheRender.TheRender.CameraViewState.NodeId = node.ObjLineRef;
                        glControl.Invalidate();
                    }
                }

                if (node.Group == GroupType.ESL)
                {
                    EnemyProperty p = new EnemyProperty(node.ObjLineRef, updateMethods, ((EnemyNodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.ETS)
                {
                    EtcModelProperty p = new EtcModelProperty(node.ObjLineRef, updateMethods, ((EtcModelNodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.ITA)
                {
                    SpecialProperty p = new SpecialProperty(node.ObjLineRef, updateMethods, ((SpecialNodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.AEV)
                {
                    SpecialProperty p = new SpecialProperty(node.ObjLineRef, updateMethods, ((SpecialNodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.EXTRAS)
                {
                    var r = DataBase.Extras.AssociationList[node.ObjLineRef];
                    if (r.FileFormat == SpecialFileFormat.AEV)
                    {
                        SpecialProperty p = new SpecialProperty(r.LineID, updateMethods, DataBase.NodeAEV.PropertyMethods, true);
                        propertyGridObjs.SelectedObject = p;
                    }
                    else if (r.FileFormat == SpecialFileFormat.ITA)
                    {
                        SpecialProperty p = new SpecialProperty(r.LineID, updateMethods, DataBase.NodeITA.PropertyMethods, true);
                        propertyGridObjs.SelectedObject = p;
                    }
                    else
                    {
                        propertyGridObjs.SelectedObject = none;
                    }
                }
                else if (node.Group == GroupType.DSE)
                {
                    NewAge_DSE_Property p = new NewAge_DSE_Property(node.ObjLineRef, updateMethods, ((NewAge_DSE_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.SMX)
                {
                    NewAge_SMX_Property p = new NewAge_SMX_Property(node.ObjLineRef, updateMethods, ((NewAge_SMX_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.AVL)
                {
                    NewAge_AVL_Property p = new NewAge_AVL_Property(node.ObjLineRef, updateMethods, ((NewAge_AVL_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.CAM)
                {
                    NewAge_CAM_Property p = new NewAge_CAM_Property(node.ObjLineRef, updateMethods, ((NewAge_CAM_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.CAM_ZONE)
                {
                    NewAge_CAM_Zone_Property p = new NewAge_CAM_Zone_Property(node.ObjLineRef, updateMethods, ((NewAge_CAM_Zone_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.RTP)
                {
                    NewAge_RTP_Property p = new NewAge_RTP_Property(node.ObjLineRef, updateMethods, ((NewAge_RTP_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.FSE)
                {
                    NewAge_FSE_Property p = new NewAge_FSE_Property(node.ObjLineRef, updateMethods, ((NewAge_FSE_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.SAR)
                {
                    NewAge_ESAR_Property p = new NewAge_ESAR_Property(node.ObjLineRef, updateMethods, ((NewAge_ESAR_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.EAR)
                {
                    NewAge_ESAR_Property p = new NewAge_ESAR_Property(node.ObjLineRef, updateMethods, ((NewAge_ESAR_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.ESE)
                {
                    NewAge_ESE_Property p = new NewAge_ESE_Property(node.ObjLineRef, updateMethods, ((NewAge_ESE_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.EMI)
                {
                    NewAge_EMI_Property p = new NewAge_EMI_Property(node.ObjLineRef, updateMethods, ((NewAge_EMI_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.LIT_ENTRYS)
                {
                    NewAge_LIT_Entry_Property p = new NewAge_LIT_Entry_Property(node.ObjLineRef, updateMethods, ((NewAge_LIT_Entrys_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.LIT_GROUPS)
                {
                    NewAge_LIT_Group_Property p = new NewAge_LIT_Group_Property(node.ObjLineRef, updateMethods, ((NewAge_LIT_Groups_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.QUAD_CUSTOM)
                {
                    QuadCustomProperty p = new QuadCustomProperty(node.ObjLineRef, updateMethods, ((QuadCustomNodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.EFF_EffectEntry) 
                {
                    EFF_TableEffectEntry_Property p = new EFF_TableEffectEntry_Property(node.ObjLineRef, updateMethods, ((NewAge_EFF_EffectEntry_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.EFF_Table0)
                {
                    EFF_Table0_Property p = new EFF_Table0_Property(node.ObjLineRef, updateMethods, ((NewAge_EFF_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.EFF_Table1)
                {
                    EFF_Table1_Property p = new EFF_Table1_Property(node.ObjLineRef, updateMethods, ((NewAge_EFF_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.EFF_Table2)
                {
                    EFF_Table2_Property p = new EFF_Table2_Property(node.ObjLineRef, updateMethods, ((NewAge_EFF_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.EFF_Table3)
                {
                    EFF_Table3_Property p = new EFF_Table3_Property(node.ObjLineRef, updateMethods, ((NewAge_EFF_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.EFF_Table4)
                {
                    EFF_Table4_Property p = new EFF_Table4_Property(node.ObjLineRef, updateMethods, ((NewAge_EFF_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.EFF_Table6)
                {
                    EFF_Table6_Property p = new EFF_Table6_Property(node.ObjLineRef, updateMethods, ((NewAge_EFF_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.EFF_Table7_Effect_0)
                {
                    EFF_TableEffectGroup_Property p = new EFF_TableEffectGroup_Property(node.ObjLineRef, updateMethods, ((NewAge_EFF_EffectGroup_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.EFF_Table8_Effect_1)
                {
                    EFF_TableEffectGroup_Property p = new EFF_TableEffectGroup_Property(node.ObjLineRef, updateMethods, ((NewAge_EFF_EffectGroup_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else if (node.Group == GroupType.EFF_Table9)
                {
                    EFF_Table9_Property p = new EFF_Table9_Property(node.ObjLineRef, updateMethods, ((NewAge_EFF_Table9Entry_NodeGroup)node.Parent).PropertyMethods);
                    propertyGridObjs.SelectedObject = p;
                }
                else
                {
                    propertyGridObjs.SelectedObject = none;
                    DataBase.LastSelectNode = null;
                }
            }
            else if (treeViewObjs.SelectedNodes.Count > 1)
            {
                DataBase.LastSelectNode = treeViewObjs.SelectedNodes.Last().Value;

                MultiSelectProperty p = new MultiSelectProperty(updateMethods);
                int count = p.LoadContent(treeViewObjs.SelectedNodes.Values.ToList());
                if (count != 0)
                {
                    propertyGridObjs.SelectedObject = p;
                }
                else 
                {
                    propertyGridObjs.SelectedObject = none;
                }  
            }
            else 
            {
                propertyGridObjs.SelectedObject = none;
                DataBase.LastSelectNode = null;
            }
            if (camera.isOrbitCamera())
            {
                if (OldLastNodeIsNull)
                {
                    camera.ResetOrbitToSelectedObject();
                }
                // Update only the look-at target so the camera position doesn't jump when selecting an object
                camera.UpdateLookAtToSelectedObjectKeepPos();
                camMtx = camera.GetViewMatrix();
            }
            objectMove.UpdateSelection();
            glControl.Invalidate();
        }

        #endregion


        #region Gerenciamento de arquivos //new

        private void toolStripMenuItemNewESL_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.NewFileESL();
            Globals.FilePathESL = null;
            TreeViewEnableDrawNode();
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewETS_2007_PS2_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileETS(Re4Version.V2007PS2);
            Globals.FilePathETS = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewETS_UHD_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileETS(Re4Version.UHD);
            Globals.FilePathETS = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewITA_2007_PS2_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileITA(Re4Version.V2007PS2);
            Globals.FilePathITA = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewITA_UHD_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileITA(Re4Version.UHD);
            Globals.FilePathITA = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewAEV_2007_PS2_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileAEV(Re4Version.V2007PS2);
            Globals.FilePathAEV = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewAEV_UHD_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileAEV(Re4Version.UHD);
            Globals.FilePathAEV = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewDSE_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileDSE();
            Globals.FilePathDSE = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewSMX_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileSMX();
            Globals.FilePathSMX = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewAVL_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileAVL();
            Globals.FilePathAVL = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewFSE_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileFSE();
            Globals.FilePathFSE = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewSAR_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileSAR();
            Globals.FilePathSAR = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewEAR_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileEAR();
            Globals.FilePathEAR = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewEMI_2007_PS2_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileEMI(Re4Version.V2007PS2);
            Globals.FilePathEMI = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewESE_2007_PS2_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileESE(Re4Version.V2007PS2);
            Globals.FilePathESE = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewEMI_UHD_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileEMI(Re4Version.UHD);
            Globals.FilePathEMI = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewESE_UHD_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileESE(Re4Version.UHD);
            Globals.FilePathESE = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewQuadCustom_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileQuadCustom();
            Globals.FilePathQuadCustom = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewLIT_2007_PS2_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileLIT(Re4Version.V2007PS2);
            Globals.FilePathLIT = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewLIT_UHD_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileLIT(Re4Version.UHD);
            Globals.FilePathLIT = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewITA_PS4_NS_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileITA(Re4Version.UHD, true);
            Globals.FilePathITA = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewAEV_PS4_NS_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileAEV(Re4Version.UHD, true);
            Globals.FilePathAEV = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewEFFBLOB_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileEFFBLOB(Endianness.LittleEndian);
            Globals.FilePathEFFBLOB = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewEFFBLOBBIG_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileEFFBLOB(Endianness.BigEndian);
            Globals.FilePathEFFBLOB = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewCAM_BIG_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileCAM(IsRe4Version.BIG_ENDIAN);
            Globals.FilePathCAM = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewCAM_2007_PS2_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileCAM(IsRe4Version.V2007PS2);
            Globals.FilePathCAM = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewCAM_UHD_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileCAM(IsRe4Version.UHD);
            Globals.FilePathCAM = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewCAM_PS4NS_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileCAM(IsRe4Version.PS4NS);
            Globals.FilePathCAM = null;
            glControl.Invalidate();
        }

        private void toolStripMenuItemNewRTP_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            FileManager.NewFileRTP();
            Globals.FilePathRTP = null;
            glControl.Invalidate();
        }

        #endregion

        #region Gerenciamento de arquivos //open

        private bool OpenIsUHD = false;
        private bool OpenIsPs4Ns_Adapted = false;
        private IsRe4Version OpenIsRe4Version = IsRe4Version.NULL;
        private void toolStripMenuItemOpenESL_Click(object sender, EventArgs e)
        {
            openFileDialogESL.ShowDialog();
        }
        private void toolStripMenuItemOpenETS_2007_PS2_Click(object sender, EventArgs e)
        {
            OpenIsUHD = false;
            openFileDialogETS.ShowDialog();
        }
        private void toolStripMenuItemOpenETS_UHD_Click(object sender, EventArgs e)
        {
            OpenIsUHD = true;
            openFileDialogETS.ShowDialog();
        }
        private void toolStripMenuItemOpenITA_2007_PS2_Click(object sender, EventArgs e)
        {
            OpenIsUHD = false;
            OpenIsPs4Ns_Adapted = false;
            openFileDialogITA.ShowDialog();
        }
        private void toolStripMenuItemOpenITA_UHD_Click(object sender, EventArgs e)
        {
            OpenIsUHD = true;
            OpenIsPs4Ns_Adapted = false;
            openFileDialogITA.ShowDialog();
        }
        private void toolStripMenuItemOpenAEV_2007_PS2_Click(object sender, EventArgs e)
        {
            OpenIsUHD = false;
            OpenIsPs4Ns_Adapted = false;
            openFileDialogAEV.ShowDialog();
        }
        private void toolStripMenuItemOpenAEV_UHD_Click(object sender, EventArgs e)
        {
            OpenIsUHD = true;
            OpenIsPs4Ns_Adapted = false;
            openFileDialogAEV.ShowDialog();
        }
        private void toolStripMenuItemOpenITA_PS4_NS_Click(object sender, EventArgs e)
        {
            OpenIsUHD = true;
            OpenIsPs4Ns_Adapted = true;
            openFileDialogITA.ShowDialog();
        }
        private void toolStripMenuItemOpenAEV_PS4_NS_Click(object sender, EventArgs e)
        {
            OpenIsUHD = true;
            OpenIsPs4Ns_Adapted = true;
            openFileDialogAEV.ShowDialog();
        }
        private void toolStripMenuItemOpenDSE_Click(object sender, EventArgs e)
        {
            openFileDialogDSE.ShowDialog();
        }
        private void toolStripMenuItemOpenSMX_Click(object sender, EventArgs e)
        {
            openFileDialogSMX.ShowDialog();
        }
        private void toolStripMenuItemOpenAVL_Click(object sender, EventArgs e)
        {
            openFileDialogAVL.ShowDialog();
        }
        private void toolStripMenuItemOpenFSE_Click(object sender, EventArgs e)
        {
            openFileDialogFSE.ShowDialog();
        }
        private void toolStripMenuItemOpenSAR_Click(object sender, EventArgs e)
        {
            openFileDialogSAR.ShowDialog();
        }
        private void toolStripMenuItemOpenEAR_Click(object sender, EventArgs e)
        {
            openFileDialogEAR.ShowDialog();
        }
        private void toolStripMenuItemOpenEMI_2007_PS2_Click(object sender, EventArgs e)
        {
            OpenIsUHD = false;
            openFileDialogEMI.ShowDialog();
        }
        private void toolStripMenuItemOpenESE_2007_PS2_Click(object sender, EventArgs e)
        {
            OpenIsUHD = false;
            openFileDialogESE.ShowDialog();
        }
        private void toolStripMenuItemOpenEMI_UHD_Click(object sender, EventArgs e)
        {
            OpenIsUHD = true;
            openFileDialogEMI.ShowDialog();
        }
        private void toolStripMenuItemOpenESE_UHD_Click(object sender, EventArgs e)
        {
            OpenIsUHD = true;
            openFileDialogESE.ShowDialog();
        }
        private void toolStripMenuItemOpenQuadCustom_Click(object sender, EventArgs e)
        {
            openFileDialogQuadCustom.ShowDialog();
        }

        private void toolStripMenuItemOpenLIT_2007_PS2_Click(object sender, EventArgs e)
        {
            OpenIsUHD = false;
            openFileDialogLIT.ShowDialog();
        }

        private void toolStripMenuItemOpenLIT_UHD_Click(object sender, EventArgs e)
        {
            OpenIsUHD = true;
            openFileDialogLIT.ShowDialog();
        }

        private void toolStripMenuItemOpenEFFBLOB_Click(object sender, EventArgs e)
        {
            openFileDialogEFFBLOB.ShowDialog();
        }

        private void toolStripMenuItemOpenEFFBLOBBIG_Click(object sender, EventArgs e)
        {
            openFileDialogEFFBLOBBIG.ShowDialog();
        }

        private void toolStripMenuItemOpenCAM_BIG_Click(object sender, EventArgs e)
        {
            OpenIsRe4Version = IsRe4Version.BIG_ENDIAN;
            openFileDialogCAM.ShowDialog();
        }

        private void toolStripMenuItemOpenCAM_2007_PS2_Click(object sender, EventArgs e)
        {
            OpenIsRe4Version = IsRe4Version.V2007PS2;
            openFileDialogCAM.ShowDialog();
        }

        private void toolStripMenuItemOpenCAM_UHD_Click(object sender, EventArgs e)
        {
            OpenIsRe4Version = IsRe4Version.UHD;
            openFileDialogCAM.ShowDialog();
        }

        private void toolStripMenuItemOpenCAM_PS4NS_Click(object sender, EventArgs e)
        {
            OpenIsRe4Version = IsRe4Version.PS4NS;
            openFileDialogCAM.ShowDialog();
        }

        private void toolStripMenuItemOpenRTP_Click(object sender, EventArgs e)
        {
            openFileDialogRTP.ShowDialog();
        }

        private void openFileDialogESL_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogESL.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();
                        try
                        {
                            FileManager.LoadFileESL(file, fileInfo);
                            Globals.FilePathESL = openFileDialogESL.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ESL opened");
                            openFileDialogESL.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearESL();
                            Globals.FilePathESL = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                    }
 
                }
            }

        }
        private void openFileDialogETS_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogETS.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 16)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16Bytes), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();
                        try
                        {
                            if (OpenIsUHD)
                            {
                                FileManager.LoadFileETS_UHD(file, fileInfo);
                            }
                            else
                            {
                                FileManager.LoadFileETS_2007_PS2(file, fileInfo);
                            }
                            Globals.FilePathETS = openFileDialogETS.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ETS opened");
                            openFileDialogETS.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearETS();
                            Globals.FilePathETS = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                    }
                }
            }
        }
        private void openFileDialogITA_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogITA.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 16)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16Bytes), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();

                        try
                        {
                            if (OpenIsPs4Ns_Adapted)
                            {
                                FileManager.LoadFileITA_PS4_NS(file, fileInfo);
                            }
                            else if (OpenIsUHD)
                            {
                                FileManager.LoadFileITA_UHD(file, fileInfo);
                            }
                            else
                            {
                                FileManager.LoadFileITA_2007_PS2(file, fileInfo);
                            }
                            Globals.FilePathITA = openFileDialogITA.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ITA opened");
                            openFileDialogITA.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearITA();
                            Globals.FilePathITA = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                       
                    }
                }
            }
        }
        private void openFileDialogAEV_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogAEV.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 16)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16Bytes), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();
                        try
                        {
                            if (OpenIsPs4Ns_Adapted)
                            {
                                FileManager.LoadFileAEV_PS4_NS(file, fileInfo);
                            }
                            else if (OpenIsUHD)
                            {
                                FileManager.LoadFileAEV_UHD(file, fileInfo);
                            }
                            else
                            {
                                FileManager.LoadFileAEV_2007_PS2(file, fileInfo);
                            }
                            Globals.FilePathAEV = openFileDialogAEV.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("AEV opened");
                            openFileDialogAEV.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearAEV();
                            Globals.FilePathAEV = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }

                    }
                }
            }
        }
        private void openFileDialogDSE_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogDSE.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 4)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile4Bytes), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();
                        try
                        {
                            FileManager.LoadFileDSE(file, fileInfo);
                            Globals.FilePathDSE = openFileDialogDSE.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("DSE opened");
                            openFileDialogDSE.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearDSE();
                            Globals.FilePathDSE = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                    }
                }
            }
        }
        private void openFileDialogSMX_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogSMX.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 16)
                {
                    MessageBox.Show("File too small for SMX header (min 16 bytes)", Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();
                        try
                        {
                            FileManager.LoadFileSMX(file, fileInfo);
                            Globals.FilePathSMX = openFileDialogSMX.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("SMX opened");
                            openFileDialogSMX.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearSMX();
                            Globals.FilePathSMX = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                    }
                }
            }
        }
        private void openFileDialogAVL_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogAVL.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();
                        try
                        {
                            FileManager.LoadFileAVL(file, fileInfo);
                            Globals.FilePathAVL = openFileDialogAVL.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("AVL opened");
                            openFileDialogAVL.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearAVL();
                            Globals.FilePathAVL = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                    }
                }
            }
        }

        private void openFileDialogFSE_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogFSE.FileName);
            }
            catch (Exception ex)
            {                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 16)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16Bytes), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();

                        try
                        {
                            FileManager.LoadFileFSE(file, fileInfo);
                            Globals.FilePathFSE = openFileDialogFSE.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("FSE opened");
                            openFileDialogFSE.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearFSE();
                            Globals.FilePathFSE = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }

                    }
                }
            }
        }
        private void openFileDialogSAR_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogSAR.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 16)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16Bytes), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();

                        try
                        {
                            FileManager.LoadFileSAR(file, fileInfo);
                            Globals.FilePathSAR = openFileDialogSAR.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("SAR opened");
                            openFileDialogSAR.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearSAR();
                            Globals.FilePathSAR = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally 
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                       
                    }
                }
            }
        }
        private void openFileDialogEAR_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogEAR.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 16)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16Bytes), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();

                        try
                        {
                            FileManager.LoadFileEAR(file, fileInfo);
                            Globals.FilePathEAR = openFileDialogEAR.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("EAR opened");
                            openFileDialogEAR.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearEAR();
                            Globals.FilePathEAR = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                    }
                }
            }
        }
        private void openFileDialogEMI_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogEMI.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 4)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile4Bytes), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();
                        try
                        {
                            if (OpenIsUHD)
                            {
                                FileManager.LoadFileEMI_UHD(file, fileInfo);
                            }
                            else
                            {
                                FileManager.LoadFileEMI_2007_PS2(file, fileInfo);
                            }
                            Globals.FilePathEMI = openFileDialogEMI.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("EMI opened");
                            openFileDialogEMI.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearEMI();
                            Globals.FilePathEMI = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally 
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                    }
                }
            }
        }
        private void openFileDialogESE_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogESE.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 16)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16Bytes), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();

                        try
                        {
                            if (OpenIsUHD)
                            {
                                FileManager.LoadFileESE_UHD(file, fileInfo);
                            }
                            else
                            {
                                FileManager.LoadFileESE_2007_PS2(file, fileInfo);
                            }
                            Globals.FilePathESE = openFileDialogESE.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ESE opened");
                            openFileDialogESE.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearESE();
                            Globals.FilePathESE = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally 
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                      
                   
                    }
                }
            }
        }
        private void openFileDialogQuadCustom_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogQuadCustom.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                   
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();

                        try
                        {
                            FileManager.LoadFileQuadCustom(file, fileInfo);
                            Globals.FilePathQuadCustom = openFileDialogQuadCustom.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("QuadCustom opened");
                            openFileDialogQuadCustom.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearQuadCustom();
                            Globals.FilePathQuadCustom = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally 
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                    }

                }
            }
        }
        private void openFileDialogLIT_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogLIT.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 4)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile4Bytes), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();
                        try
                        {
                            if (OpenIsUHD)
                            {
                                FileManager.LoadFileLIT_UHD(file, fileInfo);
                            }
                            else
                            {
                                FileManager.LoadFileLIT_2007_PS2(file, fileInfo);
                            }
                            Globals.FilePathLIT = openFileDialogLIT.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("LIT opened");
                            openFileDialogLIT.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearLIT();
                            Globals.FilePathLIT = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally 
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
               
                    }
                }
            }
        }
        private void openFileDialogEFFBLOB_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogEFFBLOB.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 16)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16Bytes), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();
                        try
                        {
                            FileManager.LoadFileEFFBLOB(file, Endianness.LittleEndian);
                            Globals.FilePathEFFBLOB = openFileDialogEFFBLOB.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("EFFBLOB opened");
                            openFileDialogEFFBLOB.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearEFFBLOB();
                            Globals.FilePathEFFBLOB = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                    }
                }
            }
        }
        private void openFileDialogEFFBLOBBIG_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogEFFBLOBBIG.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 16)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16Bytes), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();
                        try
                        {
                            FileManager.LoadFileEFFBLOB(file, Endianness.BigEndian);
                            Globals.FilePathEFFBLOB = openFileDialogEFFBLOBBIG.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("EFFBLOB (BigEndian) opened");
                            openFileDialogEFFBLOBBIG.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearEFFBLOB();
                            Globals.FilePathEFFBLOB = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                    }
                }
            }
        }
        private void openFileDialogCAM_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(openFileDialogCAM.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if (fileInfo != null)
            {
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 16)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16Bytes), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    FileStream file;
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();
                        try
                        {
                            FileManager.LoadFileCAM(file, OpenIsRe4Version);
                            Globals.FilePathCAM = openFileDialogCAM.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("CAM opened: "
                                + DataBase.FileCAM.Cameras.Count + " cameras, "
                                + DataBase.FileCAM.Zones.Count + " zones");
                            openFileDialogCAM.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearCAM();
                            Globals.FilePathCAM = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                    }
                }
            }
        }

        private void openFileDialogRTP_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo fileInfo;
            FileStream file;
            try
            {
                fileInfo = new FileInfo(openFileDialogRTP.FileName);
                if (fileInfo.Length > 0x1000000)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length == 0)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile0MB), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else if (fileInfo.Length < 16)
                {
                    MessageBox.Show(Lang.GetText(eLang.MessageBoxFile16Bytes), Lang.GetText(eLang.MessageBoxWarningTitle), MessageBoxButtons.OK);
                    e.Cancel = true;
                    return;
                }
                else
                {
                    try
                    {
                        file = fileInfo.OpenRead();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                        e.Cancel = true;
                        return;
                    }
                    if (file != null && fileInfo != null)
                    {
                        TreeViewUpdateSelectedsClear();
                        TreeViewDisableDrawNode();
                        try
                        {
                            FileManager.LoadFileRTP(file);
                            Globals.FilePathRTP = openFileDialogRTP.FileName;
                            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("RTP opened: "
                                + DataBase.FileRTP.Nodes.Count + " nodes, "
                                + DataBase.FileRTP.Distances.Count + " distances");
                            openFileDialogRTP.FileName = null;
                        }
                        catch (Exception ex)
                        {
                            FileManager.ClearRTP();
                            Globals.FilePathRTP = null;
                            MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                            e.Cancel = true;
                            return;
                        }
                        finally
                        {
                            file.Close();
                            glControl.Invalidate();
                            TreeViewEnableDrawNode();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
            }
        }

        #endregion

        #region Gerenciamento de arquivos //Clear

        private void toolStripMenuItemClear_DropDownOpening(object sender, EventArgs e)
        {
            toolStripMenuItemClearESL.Enabled = DataBase.FileESL != null;
            toolStripMenuItemClearETS.Enabled = DataBase.FileETS != null;
            toolStripMenuItemClearITA.Enabled = DataBase.FileITA != null;
            toolStripMenuItemClearAEV.Enabled = DataBase.FileAEV != null;
            toolStripMenuItemClearDSE.Enabled = DataBase.FileDSE != null;
            toolStripMenuItemClearSMX.Enabled = DataBase.FileSMX != null;
            toolStripMenuItemClearAVL.Enabled = DataBase.FileAVL != null;
            toolStripMenuItemClearCAM.Enabled = DataBase.FileCAM != null;
            toolStripMenuItemClearRTP.Enabled = DataBase.FileRTP != null;
            toolStripMenuItemClearFSE.Enabled = DataBase.FileFSE != null;
            toolStripMenuItemClearSAR.Enabled = DataBase.FileSAR != null;
            toolStripMenuItemClearEAR.Enabled = DataBase.FileEAR != null;
            toolStripMenuItemClearEMI.Enabled = DataBase.FileEMI != null;
            toolStripMenuItemClearESE.Enabled = DataBase.FileESE != null;
            toolStripMenuItemClearLIT.Enabled = DataBase.FileLIT != null;
            toolStripMenuItemClearEFFBLOB.Enabled = DataBase.FileEFF != null;
            toolStripMenuItemClearQuadCustom.Enabled = DataBase.FileQuadCustom != null;
        }

        private void toolStripMenuItemClearESL_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearESL();
            Globals.FilePathESL = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ESL cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearETS_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearETS();
            Globals.FilePathETS = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ETS cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearITA_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearITA();
            Globals.FilePathITA = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ITA cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearAEV_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearAEV();
            Globals.FilePathAEV = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("AEV cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearDSE_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearDSE();
            Globals.FilePathDSE = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("DSE cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearSMX_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearSMX();
            Globals.FilePathSMX = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("SMX cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearAVL_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearAVL();
            Globals.FilePathAVL = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("AVL cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearCAM_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearCAM();
            Globals.FilePathCAM = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("CAM cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearRTP_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearRTP();
            Globals.FilePathRTP = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("RTP cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearFSE_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearFSE();
            Globals.FilePathFSE = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("FSE cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearSAR_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearSAR();
            Globals.FilePathSAR = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("SAR cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearEAR_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearEAR();
            Globals.FilePathEAR = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("EAR cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearEMI_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearEMI();
            Globals.FilePathEMI = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("EMI cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearESE_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearESE();
            Globals.FilePathESE = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ESE cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearQuadCustom_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearQuadCustom();
            Globals.FilePathQuadCustom = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("QuadCustom cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearLIT_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearLIT();
            Globals.FilePathLIT = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("LIT cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        private void toolStripMenuItemClearEFFBLOB_Click(object sender, EventArgs e)
        {
            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();
            FileManager.ClearEFFBLOB();
            Globals.FilePathEFFBLOB = null;
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("EFFBLOB cleared");
            glControl.Invalidate();
            TreeViewEnableDrawNode();
        }

        #endregion

        #region Gerenciamento de arquivos //Save As..

        private void toolStripMenuItemSaveAs_DropDownOpening(object sender, EventArgs e)
        {
            Re4QuadExtremeEditor.src.DebugLog.Write("SaveAsSubmenu OPEN FileLIT=" + (DataBase.FileLIT != null) + " FilePathLIT=" + Globals.FilePathLIT);
            toolStripMenuItemSaveAsESL.Enabled = DataBase.FileESL != null;
            toolStripMenuItemSaveAsETS.Enabled = DataBase.FileETS != null;
            toolStripMenuItemSaveAsITA.Enabled = DataBase.FileITA != null;
            toolStripMenuItemSaveAsAEV.Enabled = DataBase.FileAEV != null;
            toolStripMenuItemSaveAsDSE.Enabled = DataBase.FileDSE != null;
            toolStripMenuItemSaveAsSMX.Enabled = DataBase.FileSMX != null;
            toolStripMenuItemSaveAsAVL.Enabled = DataBase.FileAVL != null;
            toolStripMenuItemSaveAsCAM.Enabled = DataBase.FileCAM != null;
            toolStripMenuItemSaveAsRTP.Enabled = DataBase.FileRTP != null;
            toolStripMenuItemSaveAsFSE.Enabled = DataBase.FileFSE != null;
            toolStripMenuItemSaveAsSAR.Enabled = DataBase.FileSAR != null;
            toolStripMenuItemSaveAsEAR.Enabled = DataBase.FileEAR != null;
            toolStripMenuItemSaveAsEMI.Enabled = DataBase.FileEMI != null;
            toolStripMenuItemSaveAsESE.Enabled = DataBase.FileESE != null;
            toolStripMenuItemSaveAsLIT.Enabled = DataBase.FileLIT != null;
            toolStripMenuItemSaveAsEFFBLOB.Enabled = DataBase.FileEFF != null;
            toolStripMenuItemSaveAsQuadCustom.Enabled = DataBase.FileQuadCustom != null;

            if (DataBase.FileETS != null && DataBase.FileETS.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveAsETS.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsETS_2007_PS2);
            }
            else if (DataBase.FileETS != null && DataBase.FileETS.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveAsETS.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsETS_UHD);
            }
            else 
            {
                toolStripMenuItemSaveAsETS.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsETS);
            }

            if (DataBase.FileITA != null && DataBase.FileITA.IsPs4Ns_Adapted)
            {
                toolStripMenuItemSaveAsITA.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsITA_PS4_NS);
            }
            else if (DataBase.FileITA != null && DataBase.FileITA.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveAsITA.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsITA_2007_PS2);
            }
            else if (DataBase.FileITA != null && DataBase.FileITA.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveAsITA.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsITA_UHD);
            }
            else
            {
                toolStripMenuItemSaveAsITA.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsITA);
            }

            if (DataBase.FileAEV != null && DataBase.FileAEV.IsPs4Ns_Adapted)
            {
                toolStripMenuItemSaveAsAEV.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsAEV_PS4_NS);
            }
            else if (DataBase.FileAEV != null && DataBase.FileAEV.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveAsAEV.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsAEV_2007_PS2);
            }
            else if (DataBase.FileAEV != null && DataBase.FileAEV.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveAsAEV.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsAEV_UHD);
            }
            else
            {
                toolStripMenuItemSaveAsAEV.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsAEV);
            }

            if (DataBase.FileEMI != null && DataBase.FileEMI.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveAsEMI.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsEMI_2007_PS2);
            }
            else if (DataBase.FileEMI != null && DataBase.FileEMI.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveAsEMI.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsEMI_UHD);
            }
            else
            {
                toolStripMenuItemSaveAsEMI.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsEMI);
            }

            if (DataBase.FileESE != null && DataBase.FileESE.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveAsESE.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsESE_2007_PS2);
            }
            else if (DataBase.FileESE != null && DataBase.FileESE.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveAsESE.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsESE_UHD);
            }
            else
            {
                toolStripMenuItemSaveAsESE.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsESE);
            }

            if (DataBase.FileLIT != null && DataBase.FileLIT.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveAsLIT.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsLIT_2007_PS2);
            }
            else if (DataBase.FileLIT != null && DataBase.FileLIT.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveAsLIT.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsLIT_UHD);
            }
            else
            {
                toolStripMenuItemSaveAsLIT.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsLIT);
            }

            if (DataBase.FileEFF != null && DataBase.FileEFF.Endian == Endianness.LittleEndian)
            {
                toolStripMenuItemSaveAsEFFBLOB.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsEFFBLOB_LittleEndian);
            }
            else if (DataBase.FileEFF != null && DataBase.FileEFF.Endian == Endianness.BigEndian)
            {
                toolStripMenuItemSaveAsEFFBLOB.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsEFFBLOB_BigEndian);
            }
            else
            {
                toolStripMenuItemSaveAsEFFBLOB.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsEFFBLOB);
            }

        }

        private void toolStripMenuItemSaveAsESL_Click(object sender, EventArgs e)
        {
            saveFileDialogESL.FileName = Globals.FilePathESL;
            saveFileDialogESL.ShowDialog();
        }

        private void toolStripMenuItemSaveAsETS_Click(object sender, EventArgs e)
        {
            saveFileDialogETS.FileName = Globals.FilePathETS;
            saveFileDialogETS.ShowDialog();
        }

        private void toolStripMenuItemSaveAsITA_Click(object sender, EventArgs e)
        {
            saveFileDialogITA.FileName = Globals.FilePathITA;
            saveFileDialogITA.ShowDialog();
        }

        private void toolStripMenuItemSaveAsAEV_Click(object sender, EventArgs e)
        {
            saveFileDialogAEV.FileName = Globals.FilePathAEV;
            saveFileDialogAEV.ShowDialog();
        }

        private void toolStripMenuItemSaveAsDSE_Click(object sender, EventArgs e)
        {
            saveFileDialogDSE.FileName = Globals.FilePathDSE;
            saveFileDialogDSE.ShowDialog();
        }

        private void toolStripMenuItemSaveAsSMX_Click(object sender, EventArgs e)
        {
            saveFileDialogSMX.FileName = Globals.FilePathSMX;
            saveFileDialogSMX.ShowDialog();
        }

        private void toolStripMenuItemSaveAsAVL_Click(object sender, EventArgs e)
        {
            saveFileDialogAVL.FileName = Globals.FilePathAVL;
            saveFileDialogAVL.ShowDialog();
        }

        private void toolStripMenuItemSaveAsCAM_Click(object sender, EventArgs e)
        {
            saveFileDialogCAM.FileName = Globals.FilePathCAM;
            saveFileDialogCAM.ShowDialog();
        }

        private void toolStripMenuItemSaveAsRTP_Click(object sender, EventArgs e)
        {
            saveFileDialogRTP.FileName = Globals.FilePathRTP;
            saveFileDialogRTP.ShowDialog();
        }

        private void toolStripMenuItemSaveAsFSE_Click(object sender, EventArgs e)
        {
            saveFileDialogFSE.FileName = Globals.FilePathFSE;
            saveFileDialogFSE.ShowDialog();
        }

        private void toolStripMenuItemSaveAsSAR_Click(object sender, EventArgs e)
        {
            saveFileDialogSAR.FileName = Globals.FilePathSAR;
            saveFileDialogSAR.ShowDialog();
        }

        private void toolStripMenuItemSaveAsEAR_Click(object sender, EventArgs e)
        {
            saveFileDialogEAR.FileName = Globals.FilePathEAR;
            saveFileDialogEAR.ShowDialog();
        }

        private void toolStripMenuItemSaveAsEMI_Click(object sender, EventArgs e)
        {
            saveFileDialogEMI.FileName = Globals.FilePathEMI;
            saveFileDialogEMI.ShowDialog();
        }

        private void toolStripMenuItemSaveAsESE_Click(object sender, EventArgs e)
        {
            saveFileDialogESE.FileName = Globals.FilePathESE;
            saveFileDialogESE.ShowDialog();
        }

        private void toolStripMenuItemSaveAsQuadCustom_Click(object sender, EventArgs e)
        {
            saveFileDialogQuadCustom.FileName = Globals.FilePathQuadCustom;
            saveFileDialogQuadCustom.ShowDialog();
        }

        private void toolStripMenuItemSaveAsLIT_Click(object sender, EventArgs e)
        {
            saveFileDialogLIT.FileName = Globals.FilePathLIT;
            saveFileDialogLIT.ShowDialog();
        }

        private void toolStripMenuItemSaveAsEFFBLOB_Click(object sender, EventArgs e)
        {
            if (DataBase.FileEFF.Endian == Endianness.LittleEndian)
            {
                saveFileDialogEFFBLOB.FileName = Globals.FilePathEFFBLOB;
                saveFileDialogEFFBLOB.ShowDialog();
            }
            else 
            {
                saveFileDialogEFFBLOBBIG.FileName = Globals.FilePathEFFBLOB;
                saveFileDialogEFFBLOBBIG.ShowDialog();
            }
        }

        private void saveFileDialogESL_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogESL.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileESL(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ESL saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally 
                {
                    stream.Close();
                    Globals.FilePathESL = saveFileDialogESL.FileName;
                    saveFileDialogESL.FileName = null;
                }
            }
        }

        private void saveFileDialogETS_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogETS.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileETS(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ETS saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally 
                {
                    stream.Close();
                    Globals.FilePathETS = saveFileDialogETS.FileName;
                    saveFileDialogETS.FileName = null;
                }
            }
        }

        private void saveFileDialogITA_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogITA.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileITA(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ITA saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally 
                {
                    stream.Close();
                    Globals.FilePathITA = saveFileDialogITA.FileName;
                    saveFileDialogITA.FileName = null;
                }
            }
        }

        private void saveFileDialogAEV_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogAEV.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileAEV(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("AEV saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally 
                {
                    stream.Close();
                    Globals.FilePathAEV = saveFileDialogAEV.FileName;
                    saveFileDialogAEV.FileName = null;
                }
            }
        }

        private void saveFileDialogDSE_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogDSE.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileDSE(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("DSE saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally 
                {
                    stream.Close();
                    Globals.FilePathDSE = saveFileDialogDSE.FileName;
                    saveFileDialogDSE.FileName = null;
                }
            }
        }

        private void saveFileDialogSMX_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogSMX.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileSMX(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("SMX saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                    Globals.FilePathSMX = saveFileDialogSMX.FileName;
                    saveFileDialogSMX.FileName = null;
                }
            }
        }

        private void saveFileDialogAVL_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogAVL.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileAVL(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("AVL saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                    Globals.FilePathAVL = saveFileDialogAVL.FileName;
                    saveFileDialogAVL.FileName = null;
                }
            }
        }

        private void saveFileDialogFSE_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogFSE.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileFSE(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("FSE saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally 
                {
                    stream.Close();
                    Globals.FilePathFSE = saveFileDialogFSE.FileName;
                    saveFileDialogFSE.FileName = null;
                }
            }
        }

        private void saveFileDialogSAR_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogSAR.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {

                    FileManager.SaveFileSAR(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("SAR saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally 
                {
                    stream.Close();
                    Globals.FilePathSAR = saveFileDialogSAR.FileName;
                    saveFileDialogSAR.FileName = null;
                }

            }
        }

        private void saveFileDialogEAR_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogEAR.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileEAR(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("EAR saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally 
                {
                    stream.Close();
                    Globals.FilePathEAR = saveFileDialogEAR.FileName;
                    saveFileDialogEAR.FileName = null;
                }
            }
        }

        private void saveFileDialogEMI_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogEMI.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileEMI(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("EMI saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally 
                {
                    stream.Close();
                    Globals.FilePathEMI = saveFileDialogEMI.FileName;
                    saveFileDialogEMI.FileName = null;
                }
            }
        }

        private void saveFileDialogESE_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogESE.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileESE(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ESE saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally 
                {
                    stream.Close();
                    Globals.FilePathESE = saveFileDialogESE.FileName;
                    saveFileDialogESE.FileName = null;
                }
            }
        }

        private void saveFileDialogQuadCustom_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogQuadCustom.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileQuadCustom(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("QuadCustom saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally 
                {
                    stream.Close();
                    Globals.FilePathQuadCustom = saveFileDialogQuadCustom.FileName;
                    saveFileDialogQuadCustom.FileName = null;
                }
            }
        }

        private void saveFileDialogLIT_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogLIT.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileLIT(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("LIT saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally 
                {
                    stream.Close();
                    Globals.FilePathLIT = saveFileDialogLIT.FileName;
                    saveFileDialogLIT.FileName = null;
                }
            }
        }

    

        private void saveFileDialogEFFBLOB_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogEFFBLOB.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileEFFBLOB(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("EFFBLOB saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                    Globals.FilePathEFFBLOB = saveFileDialogEFFBLOB.FileName;
                    saveFileDialogEFFBLOB.FileName = null;
                }
            }
        }

        private void saveFileDialogEFFBLOBBIG_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogEFFBLOBBIG.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileEFFBLOB(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("EFFBLOB saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                    Globals.FilePathEFFBLOB = saveFileDialogEFFBLOBBIG.FileName;
                    saveFileDialogEFFBLOBBIG.FileName = null;
                }
            }
        }

        private void saveFileDialogCAM_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogCAM.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileCAM(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("CAM saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                    Globals.FilePathCAM = saveFileDialogCAM.FileName;
                    saveFileDialogCAM.FileName = null;
                }
            }
        }

        private void saveFileDialogRTP_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(saveFileDialogRTP.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileRTP(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("RTP saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                    Globals.FilePathRTP = saveFileDialogRTP.FileName;
                    saveFileDialogRTP.FileName = null;
                }
            }
        }

        #endregion

        #region Gerenciamento de arquivos //Save

        #region Save ALL / Clear ALL open files

        /// <summary>
        /// Adds "Save ALL open files" and "Clear ALL open files" bulk actions
        /// to the top of the Save and Clear sub-menus.
        /// </summary>
        private void SetupBulkFileMenuItems()
        {
            ToolStripMenuItem saveAll = new ToolStripMenuItem("Save ALL open files");
            saveAll.Font = new Font(saveAll.Font, FontStyle.Bold);
            saveAll.Click += toolStripMenuItemSaveAll_Click;
            toolStripMenuItemSave.DropDownItems.Insert(0, new ToolStripSeparator());
            toolStripMenuItemSave.DropDownItems.Insert(0, saveAll);

            ToolStripMenuItem clearAll = new ToolStripMenuItem("Clear ALL open files");
            clearAll.Click += toolStripMenuItemClearAll_Click;
            toolStripMenuItemClear.DropDownItems.Insert(0, new ToolStripSeparator());
            toolStripMenuItemClear.DropDownItems.Insert(0, clearAll);
        }

        private void SaveAllOne(string label, bool present, string path,
            System.Action<FileStream> saver, List<string> errors, ref int saved)
        {
            if (!present || string.IsNullOrEmpty(path)) return;
            try
            {
                FileInfo file = new FileInfo(path);
                using (FileStream stream = file.Create())
                {
                    saver(stream);
                }
                saved++;
            }
            catch (Exception ex)
            {
                errors.Add(label + ": " + ex.Message);
            }
        }

        private void toolStripMenuItemSaveAll_Click(object sender, EventArgs e)
        {
            List<string> errors = new List<string>();
            int saved = 0;

            SaveAllOne("ESL", DataBase.FileESL != null, Globals.FilePathESL, s => FileManager.SaveFileESL(s), errors, ref saved);
            SaveAllOne("ETS", DataBase.FileETS != null, Globals.FilePathETS, s => FileManager.SaveFileETS(s), errors, ref saved);
            SaveAllOne("ITA", DataBase.FileITA != null, Globals.FilePathITA, s => FileManager.SaveFileITA(s), errors, ref saved);
            SaveAllOne("AEV", DataBase.FileAEV != null, Globals.FilePathAEV, s => FileManager.SaveFileAEV(s), errors, ref saved);
            SaveAllOne("DSE", DataBase.FileDSE != null, Globals.FilePathDSE, s => FileManager.SaveFileDSE(s), errors, ref saved);
            SaveAllOne("SMX", DataBase.FileSMX != null, Globals.FilePathSMX, s => FileManager.SaveFileSMX(s), errors, ref saved);
            SaveAllOne("AVL", DataBase.FileAVL != null, Globals.FilePathAVL, s => FileManager.SaveFileAVL(s), errors, ref saved);
            SaveAllOne("CAM", DataBase.FileCAM != null, Globals.FilePathCAM, s => FileManager.SaveFileCAM(s), errors, ref saved);
            SaveAllOne("RTP", DataBase.FileRTP != null, Globals.FilePathRTP, s => FileManager.SaveFileRTP(s), errors, ref saved);
            SaveAllOne("FSE", DataBase.FileFSE != null, Globals.FilePathFSE, s => FileManager.SaveFileFSE(s), errors, ref saved);
            SaveAllOne("SAR", DataBase.FileSAR != null, Globals.FilePathSAR, s => FileManager.SaveFileSAR(s), errors, ref saved);
            SaveAllOne("EAR", DataBase.FileEAR != null, Globals.FilePathEAR, s => FileManager.SaveFileEAR(s), errors, ref saved);
            SaveAllOne("EMI", DataBase.FileEMI != null, Globals.FilePathEMI, s => FileManager.SaveFileEMI(s), errors, ref saved);
            SaveAllOne("ESE", DataBase.FileESE != null, Globals.FilePathESE, s => FileManager.SaveFileESE(s), errors, ref saved);
            SaveAllOne("QuadCustom", DataBase.FileQuadCustom != null, Globals.FilePathQuadCustom, s => FileManager.SaveFileQuadCustom(s), errors, ref saved);
            SaveAllOne("LIT", DataBase.FileLIT != null, Globals.FilePathLIT, s => FileManager.SaveFileLIT(s), errors, ref saved);
            SaveAllOne("EFFBLOB", DataBase.FileEFF != null, Globals.FilePathEFFBLOB, s => FileManager.SaveFileEFFBLOB(s), errors, ref saved);

            if (saved == 0 && errors.Count == 0)
            {
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("No open files to save");
                return;
            }
            if (errors.Count == 0)
            {
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast(
                    "Saved " + saved + " file" + (saved == 1 ? "" : "s"));
            }
            else
            {
                MessageBox.Show(
                    string.Join(Environment.NewLine, errors.ToArray()),
                    "Save ALL finished (" + saved + " saved, " + errors.Count + " failed)",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void toolStripMenuItemClearAll_Click(object sender, EventArgs e)
        {
            bool any = DataBase.FileESL != null || DataBase.FileETS != null ||
                       DataBase.FileITA != null || DataBase.FileAEV != null ||
                       DataBase.FileDSE != null || DataBase.FileFSE != null ||
                       DataBase.FileSMX != null ||
                       DataBase.FileAVL != null ||
                       DataBase.FileSAR != null || DataBase.FileEAR != null ||
                       DataBase.FileEMI != null || DataBase.FileESE != null ||
                       DataBase.FileLIT != null || DataBase.FileEFF != null ||
                       DataBase.FileCAM != null || DataBase.FileRTP != null ||
                       DataBase.FileQuadCustom != null;
            if (!any)
            {
                Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("No open files");
                return;
            }

            DialogResult ok = MessageBox.Show(
                "Unload ALL open files?\n\nUnsaved changes will be lost.",
                "Clear ALL open files",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (ok != DialogResult.Yes) return;

            TreeViewUpdateSelectedsClear();
            TreeViewDisableDrawNode();

            if (DataBase.FileESL != null) { FileManager.ClearESL(); Globals.FilePathESL = null; }
            if (DataBase.FileETS != null) { FileManager.ClearETS(); Globals.FilePathETS = null; }
            if (DataBase.FileITA != null) { FileManager.ClearITA(); Globals.FilePathITA = null; }
            if (DataBase.FileAEV != null) { FileManager.ClearAEV(); Globals.FilePathAEV = null; }
            if (DataBase.FileDSE != null) { FileManager.ClearDSE(); Globals.FilePathDSE = null; }
            if (DataBase.FileSMX != null) { FileManager.ClearSMX(); Globals.FilePathSMX = null; }
            if (DataBase.FileAVL != null) { FileManager.ClearAVL(); Globals.FilePathAVL = null; }
            if (DataBase.FileFSE != null) { FileManager.ClearFSE(); Globals.FilePathFSE = null; }
            if (DataBase.FileSAR != null) { FileManager.ClearSAR(); Globals.FilePathSAR = null; }
            if (DataBase.FileEAR != null) { FileManager.ClearEAR(); Globals.FilePathEAR = null; }
            if (DataBase.FileEMI != null) { FileManager.ClearEMI(); Globals.FilePathEMI = null; }
            if (DataBase.FileESE != null) { FileManager.ClearESE(); Globals.FilePathESE = null; }
            if (DataBase.FileQuadCustom != null) { FileManager.ClearQuadCustom(); Globals.FilePathQuadCustom = null; }
            if (DataBase.FileLIT != null) { FileManager.ClearLIT(); Globals.FilePathLIT = null; }
            if (DataBase.FileEFF != null) { FileManager.ClearEFFBLOB(); Globals.FilePathEFFBLOB = null; }
            if (DataBase.FileCAM != null) { FileManager.ClearCAM(); Globals.FilePathCAM = null; }
            if (DataBase.FileRTP != null) { FileManager.ClearRTP(); Globals.FilePathRTP = null; }

            glControl.Invalidate();
            TreeViewEnableDrawNode();
            Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("All open files cleared");
        }

        #endregion

        private void toolStripMenuItemSave_DropDownOpening(object sender, EventArgs e)
        {
            Re4QuadExtremeEditor.src.DebugLog.Write("SaveSubmenu OPEN FileLIT=" + (DataBase.FileLIT != null) + " FilePathLIT=" + Globals.FilePathLIT);
            toolStripMenuItemSaveESL.Enabled = DataBase.FileESL != null;
            toolStripMenuItemSaveETS.Enabled = DataBase.FileETS != null;
            toolStripMenuItemSaveITA.Enabled = DataBase.FileITA != null;
            toolStripMenuItemSaveAEV.Enabled = DataBase.FileAEV != null;
            toolStripMenuItemSaveDSE.Enabled = DataBase.FileDSE != null;
            toolStripMenuItemSaveSMX.Enabled = DataBase.FileSMX != null;
            toolStripMenuItemSaveAVL.Enabled = DataBase.FileAVL != null;
            toolStripMenuItemSaveCAM.Enabled = DataBase.FileCAM != null;
            toolStripMenuItemSaveRTP.Enabled = DataBase.FileRTP != null;
            toolStripMenuItemSaveFSE.Enabled = DataBase.FileFSE != null;
            toolStripMenuItemSaveSAR.Enabled = DataBase.FileSAR != null;
            toolStripMenuItemSaveEAR.Enabled = DataBase.FileEAR != null;
            toolStripMenuItemSaveEMI.Enabled = DataBase.FileEMI != null;
            toolStripMenuItemSaveESE.Enabled = DataBase.FileESE != null;
            toolStripMenuItemSaveLIT.Enabled = DataBase.FileLIT != null;
            toolStripMenuItemSaveQuadCustom.Enabled = DataBase.FileQuadCustom != null;
            toolStripMenuItemSaveEFFBLOB.Enabled = DataBase.FileEFF != null;

            if (DataBase.FileETS != null && DataBase.FileETS.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveETS.Text = Lang.GetText(eLang.toolStripMenuItemSaveETS_2007_PS2);
            }
            else if (DataBase.FileETS != null && DataBase.FileETS.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveETS.Text = Lang.GetText(eLang.toolStripMenuItemSaveETS_UHD);
            }
            else
            {
                toolStripMenuItemSaveETS.Text = Lang.GetText(eLang.toolStripMenuItemSaveETS);
            }

            if (DataBase.FileITA != null && DataBase.FileITA.IsPs4Ns_Adapted)
            {
                toolStripMenuItemSaveITA.Text = Lang.GetText(eLang.toolStripMenuItemSaveITA_PS4_NS);
            }
            else if (DataBase.FileITA != null && DataBase.FileITA.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveITA.Text = Lang.GetText(eLang.toolStripMenuItemSaveITA_2007_PS2);
            }
            else if (DataBase.FileITA != null && DataBase.FileITA.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveITA.Text = Lang.GetText(eLang.toolStripMenuItemSaveITA_UHD);
            }
            else
            {
                toolStripMenuItemSaveITA.Text = Lang.GetText(eLang.toolStripMenuItemSaveITA);
            }

            if (DataBase.FileAEV != null && DataBase.FileAEV.IsPs4Ns_Adapted)
            {
                toolStripMenuItemSaveAEV.Text = Lang.GetText(eLang.toolStripMenuItemSaveAEV_PS4_NS);
            }
            else if (DataBase.FileAEV != null && DataBase.FileAEV.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveAEV.Text = Lang.GetText(eLang.toolStripMenuItemSaveAEV_2007_PS2);
            }
            else if (DataBase.FileAEV != null && DataBase.FileAEV.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveAEV.Text = Lang.GetText(eLang.toolStripMenuItemSaveAEV_UHD);
            }
            else
            {
                toolStripMenuItemSaveAEV.Text = Lang.GetText(eLang.toolStripMenuItemSaveAEV);
            }


            if (DataBase.FileEMI != null && DataBase.FileEMI.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveEMI.Text = Lang.GetText(eLang.toolStripMenuItemSaveEMI_2007_PS2);
            }
            else if (DataBase.FileEMI != null && DataBase.FileEMI.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveEMI.Text = Lang.GetText(eLang.toolStripMenuItemSaveEMI_UHD);
            }
            else
            {
                toolStripMenuItemSaveEMI.Text = Lang.GetText(eLang.toolStripMenuItemSaveEMI);
            }

            if (DataBase.FileESE != null && DataBase.FileESE.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveESE.Text = Lang.GetText(eLang.toolStripMenuItemSaveESE_2007_PS2);
            }
            else if (DataBase.FileESE != null && DataBase.FileESE.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveESE.Text = Lang.GetText(eLang.toolStripMenuItemSaveESE_UHD);
            }
            else
            {
                toolStripMenuItemSaveESE.Text = Lang.GetText(eLang.toolStripMenuItemSaveESE);
            }

            if (DataBase.FileLIT != null && DataBase.FileLIT.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveLIT.Text = Lang.GetText(eLang.toolStripMenuItemSaveLIT_2007_PS2);
            }
            else if (DataBase.FileLIT != null && DataBase.FileLIT.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveLIT.Text = Lang.GetText(eLang.toolStripMenuItemSaveLIT_UHD);
            }
            else
            {
                toolStripMenuItemSaveLIT.Text = Lang.GetText(eLang.toolStripMenuItemSaveLIT);
            }

            if (DataBase.FileEFF != null && DataBase.FileEFF.Endian == Endianness.LittleEndian)
            {
                toolStripMenuItemSaveEFFBLOB.Text = Lang.GetText(eLang.toolStripMenuItemSaveEFFBLOB_LittleEndian);
            }
            else if (DataBase.FileEFF != null && DataBase.FileEFF.Endian == Endianness.BigEndian)
            {
                toolStripMenuItemSaveEFFBLOB.Text = Lang.GetText(eLang.toolStripMenuItemSaveEFFBLOB_BigEndian);
            }
            else
            {
                toolStripMenuItemSaveEFFBLOB.Text = Lang.GetText(eLang.toolStripMenuItemSaveEFFBLOB);
            }

        }

        private void toolStripMenuItemSaveESL_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathESL);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogESL.FileName = Globals.FilePathESL;
                saveFileDialogESL.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileESL(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ESL saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveETS_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathETS);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogETS.FileName = Globals.FilePathETS;
                saveFileDialogETS.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileETS(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ETS saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveITA_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathITA);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogITA.FileName = Globals.FilePathITA;
                saveFileDialogITA.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileITA(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ITA saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveAEV_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathAEV);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogAEV.FileName = Globals.FilePathAEV;
                saveFileDialogAEV.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileAEV(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("AEV saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveDSE_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathDSE);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogDSE.FileName = Globals.FilePathDSE;
                saveFileDialogDSE.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileDSE(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("DSE saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveSMX_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathSMX);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogSMX.FileName = Globals.FilePathSMX;
                saveFileDialogSMX.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileSMX(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("SMX saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveAVL_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathAVL);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogAVL.FileName = Globals.FilePathAVL;
                saveFileDialogAVL.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileAVL(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("AVL saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveCAM_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathCAM);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogCAM.FileName = Globals.FilePathCAM;
                saveFileDialogCAM.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileCAM(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("CAM saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveRTP_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathRTP);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogRTP.FileName = Globals.FilePathRTP;
                saveFileDialogRTP.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileRTP(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("RTP saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveFSE_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathFSE);
                stream = file.Create();            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogFSE.FileName = Globals.FilePathFSE;
                saveFileDialogFSE.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileFSE(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("FSE saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveSAR_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathSAR);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogSAR.FileName = Globals.FilePathSAR;
                saveFileDialogSAR.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileSAR(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("SAR saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveEAR_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathEAR);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogEAR.FileName = Globals.FilePathEAR;
                saveFileDialogEAR.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileEAR(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("EAR saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveEMI_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathEMI);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogEMI.FileName = Globals.FilePathEMI;
                saveFileDialogEMI.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileEMI(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("EMI saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveESE_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathESE);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogESE.FileName = Globals.FilePathESE;
                saveFileDialogESE.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileESE(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("ESE saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveQuadCustom_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathQuadCustom);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogQuadCustom.FileName = Globals.FilePathQuadCustom;
                saveFileDialogQuadCustom.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileQuadCustom(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("QuadCustom saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveLIT_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathLIT);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                saveFileDialogLIT.FileName = Globals.FilePathLIT;
                saveFileDialogLIT.ShowDialog();
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileLIT(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("LIT saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally 
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveEFFBLOB_Click(object sender, EventArgs e)
        {
            FileInfo file;
            FileStream stream;
            try
            {
                file = new FileInfo(Globals.FilePathEFFBLOB);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                if (DataBase.FileEFF.Endian == Endianness.LittleEndian)
                {
                    saveFileDialogEFFBLOB.FileName = Globals.FilePathEFFBLOB;
                    saveFileDialogEFFBLOB.ShowDialog();
                }
                else
                {
                    saveFileDialogEFFBLOBBIG.FileName = Globals.FilePathEFFBLOB;
                    saveFileDialogEFFBLOBBIG.ShowDialog();
                }
                return;
            }

            if (file != null && stream != null)
            {
                try
                {
                    FileManager.SaveFileEFFBLOB(stream);
                    Re4QuadExtremeEditor.src.Class.ViewAnim.ShowToast("EFFBLOB saved");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                    return;
                }
                finally
                {
                    stream.Close();
                }
            }
        }

        private void toolStripMenuItemSaveDirectories_DropDownOpening(object sender, EventArgs e)
        {
            if (Path.GetExtension(Globals.FilePathEFFBLOB ?? "").ToUpperInvariant() == ".EFFBLOBBIG")
            {
                toolStripMenuItemDirectory_EFFBLOB.Text = Lang.GetText(eLang.DirectoryEFFBLOBBIG) + " " + (Globals.FilePathEFFBLOB ?? "");
            }
            else
            {
                toolStripMenuItemDirectory_EFFBLOB.Text = Lang.GetText(eLang.DirectoryEFFBLOB) + " " + (Globals.FilePathEFFBLOB ?? "");
            }
            toolStripMenuItemDirectory_ESL.Text = Lang.GetText(eLang.DirectoryESL) + " " + (Globals.FilePathESL ?? "");
            toolStripMenuItemDirectory_ETS.Text = Lang.GetText(eLang.DirectoryETS) + " " + (Globals.FilePathETS ?? "");
            toolStripMenuItemDirectory_ITA.Text = Lang.GetText(eLang.DirectoryITA) + " " + (Globals.FilePathITA ?? "");
            toolStripMenuItemDirectory_AEV.Text = Lang.GetText(eLang.DirectoryAEV) + " " + (Globals.FilePathAEV ?? "");
            toolStripMenuItemDirectory_DSE.Text = Lang.GetText(eLang.DirectoryDSE) + " " + (Globals.FilePathDSE ?? "");
            toolStripMenuItemDirectory_SMX.Text = Lang.GetText(eLang.DirectorySMX) + " " + (Globals.FilePathSMX ?? "");
            toolStripMenuItemDirectory_AVL.Text = Lang.GetText(eLang.DirectoryAVL) + " " + (Globals.FilePathAVL ?? "");
            toolStripMenuItemDirectory_FSE.Text = Lang.GetText(eLang.DirectoryFSE) + " " + (Globals.FilePathFSE ?? "");
            toolStripMenuItemDirectory_SAR.Text = Lang.GetText(eLang.DirectorySAR) + " " + (Globals.FilePathSAR ?? "");
            toolStripMenuItemDirectory_EAR.Text = Lang.GetText(eLang.DirectoryEAR) + " " + (Globals.FilePathEAR ?? "");
            toolStripMenuItemDirectory_EMI.Text = Lang.GetText(eLang.DirectoryEMI) + " " + (Globals.FilePathEMI ?? "");
            toolStripMenuItemDirectory_ESE.Text = Lang.GetText(eLang.DirectoryESE) + " " + (Globals.FilePathESE ?? "");
            toolStripMenuItemDirectory_LIT.Text = Lang.GetText(eLang.DirectoryLIT) + " " + (Globals.FilePathLIT ?? "");
            toolStripMenuItemDirectory_QuadCustom.Text = Lang.GetText(eLang.DirectoryQuadCustom) + " " + (Globals.FilePathQuadCustom ?? "");
        }

        #endregion

        #region Gerenciamento de arquivos //Save Convert

        private void toolStripMenuItemSaveConverter_DropDownOpening(object sender, EventArgs e)
        {
            toolStripMenuItemSaveConverterETS.Enabled = DataBase.FileETS != null;
            toolStripMenuItemSaveConverterITA.Enabled = DataBase.FileITA != null && DataBase.FileITA.IsPs4Ns_Adapted == false;
            toolStripMenuItemSaveConverterAEV.Enabled = DataBase.FileAEV != null && DataBase.FileAEV.IsPs4Ns_Adapted == false;
            toolStripMenuItemSaveConverterEMI.Enabled = DataBase.FileEMI != null;
            toolStripMenuItemSaveConverterESE.Enabled = DataBase.FileESE != null;

            if (DataBase.FileETS != null && DataBase.FileETS.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveConverterETS.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterETS_UHD);
            }
            else if (DataBase.FileETS != null && DataBase.FileETS.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveConverterETS.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterETS_2007_PS2);
            }
            else
            {
                toolStripMenuItemSaveConverterETS.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterETS);
            }

            if (DataBase.FileITA != null && DataBase.FileITA.GetRe4Version == Re4Version.V2007PS2 && DataBase.FileITA.IsPs4Ns_Adapted == false)
            {
                toolStripMenuItemSaveConverterITA.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterITA_UHD);
            }
            else if (DataBase.FileITA != null && DataBase.FileITA.GetRe4Version == Re4Version.UHD && DataBase.FileITA.IsPs4Ns_Adapted == false)
            {
                toolStripMenuItemSaveConverterITA.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterITA_2007_PS2);
            }
            else
            {
                toolStripMenuItemSaveConverterITA.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterITA);
            }

            if (DataBase.FileAEV != null && DataBase.FileAEV.GetRe4Version == Re4Version.V2007PS2 && DataBase.FileAEV.IsPs4Ns_Adapted == false)
            {
                toolStripMenuItemSaveConverterAEV.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterAEV_UHD);
            }
            else if (DataBase.FileAEV != null && DataBase.FileAEV.GetRe4Version == Re4Version.UHD && DataBase.FileAEV.IsPs4Ns_Adapted == false)
            {
                toolStripMenuItemSaveConverterAEV.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterAEV_2007_PS2);
            }
            else
            {
                toolStripMenuItemSaveConverterAEV.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterAEV);
            }

            if (DataBase.FileEMI != null && DataBase.FileEMI.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveConverterEMI.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterEMI_UHD);
            }
            else if (DataBase.FileEMI != null && DataBase.FileEMI.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveConverterEMI.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterEMI_2007_PS2);
            }
            else
            {
                toolStripMenuItemSaveConverterEMI.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterEMI);
            }

            if (DataBase.FileESE != null && DataBase.FileESE.GetRe4Version == Re4Version.V2007PS2)
            {
                toolStripMenuItemSaveConverterESE.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterESE_UHD);
            }
            else if (DataBase.FileESE != null && DataBase.FileESE.GetRe4Version == Re4Version.UHD)
            {
                toolStripMenuItemSaveConverterESE.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterESE_2007_PS2);
            }
            else
            {
                toolStripMenuItemSaveConverterESE.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterESE);
            }
        }

        private void toolStripMenuItemSaveConverterETS_Click(object sender, EventArgs e)
        {
            saveFileDialogConvertETS.FileName = null;
            saveFileDialogConvertETS.ShowDialog();
        }

        private void toolStripMenuItemSaveConverterITA_Click(object sender, EventArgs e)
        {
            saveFileDialogConvertITA.FileName = null;
            saveFileDialogConvertITA.ShowDialog();
        }

        private void toolStripMenuItemSaveConverterAEV_Click(object sender, EventArgs e)
        {
            saveFileDialogConvertAEV.FileName = null;
            saveFileDialogConvertAEV.ShowDialog();
        }

        private void toolStripMenuItemSaveConverterEMI_Click(object sender, EventArgs e)
        {
            saveFileDialogConvertEMI.FileName = null;
            saveFileDialogConvertEMI.ShowDialog();
        }

        private void toolStripMenuItemSaveConverterESE_Click(object sender, EventArgs e)
        {
            saveFileDialogConvertESE.FileName = null;
            saveFileDialogConvertESE.ShowDialog();
        }

        private void saveFileDialogConvertETS_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file = null;
            FileStream stream = null;
            try
            {
                file = new FileInfo(saveFileDialogConvertETS.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                FileManager.SaveConvertFileETS(stream);
                stream.Close();
            }
        }

        private void saveFileDialogConvertITA_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file = null;
            FileStream stream = null;
            try
            {
                file = new FileInfo(saveFileDialogConvertITA.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                FileManager.SaveConvertFileITA(stream);
                stream.Close();
            }
        }

        private void saveFileDialogConvertAEV_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file = null;
            FileStream stream = null;
            try
            {
                file = new FileInfo(saveFileDialogConvertAEV.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                FileManager.SaveConvertFileAEV(stream);
                stream.Close();
            }
        }

        private void saveFileDialogConvertEMI_FileOk(object sender, CancelEventArgs e)
        {
            FileInfo file = null;
            FileStream stream = null;
            try
            {
                file = new FileInfo(saveFileDialogConvertEMI.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                FileManager.SaveConvertFileEMI(stream);
                stream.Close();
            }
        }

        private void saveFileDialogConvertESE_FileOk(object sender, CancelEventArgs e)
        {

            FileInfo file = null;
            FileStream stream = null;
            try
            {
                file = new FileInfo(saveFileDialogConvertESE.FileName);
                stream = file.Create();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Lang.GetText(eLang.MessageBoxErrorTitle), MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }

            if (file != null && stream != null)
            {
                FileManager.SaveConvertFileESE(stream);
                stream.Close();
            }
        }

        #endregion


        #region MainForm events/ metodos

        bool enable_splitContainerRight_Panel2_Resize = false;

        private void splitContainerRight_Panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void splitContainerRight_Panel2_Resize(object sender, EventArgs e)
        {
            if (enable_splitContainerRight_Panel2_Resize)
            {
                int painel2Width = splitContainerRight.Panel2.Width;
                int quite = painel2Width / 2;

                int adWidth = advertising1Control.Width;
                int adquite = adWidth / 2;

                int ad2Width = advertising2Control.Width;
                int ad2quite = ad2Width / 2;

                if (painel2Width > 670 + advertising2Control.Width)
                {
                    int posX = quite - ad2quite;
                    if (posX < 426)
                    {
                        posX = 426;
                    }
                    advertising1Control.Hide();
                    advertising1Control.Location = new Point(painel2Width, advertising1Control.Location.Y);
                    advertising2Control.Location = new Point(posX, advertising2Control.Location.Y);
                    advertising2Control.Show();
                }
                else if (painel2Width > 670 + advertising1Control.Width)
                {
                    int posX = painel2Width - cameraMove.Width - advertising1Control.Width;

                    advertising2Control.Hide();
                    advertising2Control.Location = new Point(painel2Width, advertising2Control.Location.Y);
                    advertising1Control.Location = new Point(posX, advertising1Control.Location.Y);
                    advertising1Control.Show();
                }
                else
                {
                    advertising1Control.Hide();
                    advertising2Control.Hide();
                    advertising1Control.Location = new Point(painel2Width, advertising1Control.Location.Y);
                    advertising2Control.Location = new Point(painel2Width, advertising2Control.Location.Y);
                }
            }
        }

        private void DarkerGrayTheme()
        {
            DarkTheme.Apply(this);
        }

        private void StartUpdateTranslation()
        {
            // menu principal
            toolStripMenuItemFile.Text = Lang.GetText(eLang.toolStripMenuItemFile);
            toolStripMenuItemEdit.Text = Lang.GetText(eLang.toolStripMenuItemEdit);
            toolStripMenuItemView.Text = Lang.GetText(eLang.toolStripMenuItemView);
            toolStripMenuItemMisc.Text = Lang.GetText(eLang.toolStripMenuItemMisc);
            toolStripMenuItemSelectRoom.Text = Lang.GetText(eLang.SelectRoom);
            //submenu File
            toolStripMenuItemNewFile.Text = Lang.GetText(eLang.toolStripMenuItemNewFile);
            toolStripMenuItemOpen.Text = Lang.GetText(eLang.toolStripMenuItemOpen);
            toolStripMenuItemSave.Text = Lang.GetText(eLang.toolStripMenuItemSave);
            toolStripMenuItemSaveAs.Text = Lang.GetText(eLang.toolStripMenuItemSaveAs);
            toolStripMenuItemSaveConverter.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverter);
            toolStripMenuItemClear.Text = Lang.GetText(eLang.toolStripMenuItemClear);
            toolStripMenuItemClose.Text = Lang.GetText(eLang.toolStripMenuItemClose);
            
            // subsubmenu New
            toolStripMenuItemNewESL.Text = Lang.GetText(eLang.toolStripMenuItemNewESL);
            toolStripMenuItemNewETS_2007_PS2.Text = Lang.GetText(eLang.toolStripMenuItemNewETS_2007_PS2);
            toolStripMenuItemNewITA_2007_PS2.Text = Lang.GetText(eLang.toolStripMenuItemNewITA_2007_PS2);
            toolStripMenuItemNewAEV_2007_PS2.Text = Lang.GetText(eLang.toolStripMenuItemNewAEV_2007_PS2);
            toolStripMenuItemNewETS_UHD_PS4NS.Text = Lang.GetText(eLang.toolStripMenuItemNewETS_UHD_PS4NS);
            toolStripMenuItemNewITA_UHD.Text = Lang.GetText(eLang.toolStripMenuItemNewITA_UHD);
            toolStripMenuItemNewAEV_UHD.Text = Lang.GetText(eLang.toolStripMenuItemNewAEV_UHD);
            toolStripMenuItemNewDSE.Text = Lang.GetText(eLang.toolStripMenuItemNewDSE);
            toolStripMenuItemNewSMX.Text = Lang.GetText(eLang.toolStripMenuItemNewSMX);
            toolStripMenuItemNewFSE.Text = Lang.GetText(eLang.toolStripMenuItemNewFSE);
            toolStripMenuItemNewSAR.Text = Lang.GetText(eLang.toolStripMenuItemNewSAR);
            toolStripMenuItemNewEAR.Text = Lang.GetText(eLang.toolStripMenuItemNewEAR);
            toolStripMenuItemNewEMI_2007_PS2.Text = Lang.GetText(eLang.toolStripMenuItemNewEMI_2007_PS2);
            toolStripMenuItemNewESE_2007_PS2.Text = Lang.GetText(eLang.toolStripMenuItemNewESE_2007_PS2);
            toolStripMenuItemNewEMI_UHD_PS4NS.Text = Lang.GetText(eLang.toolStripMenuItemNewEMI_UHD_PS4NS);
            toolStripMenuItemNewESE_UHD_PS4NS.Text = Lang.GetText(eLang.toolStripMenuItemNewESE_UHD_PS4NS);
            toolStripMenuItemNewQuadCustom.Text = Lang.GetText(eLang.toolStripMenuItemNewQuadCustom);
            toolStripMenuItemNewITA_PS4_NS.Text = Lang.GetText(eLang.toolStripMenuItemNewITA_PS4_NS);
            toolStripMenuItemNewAEV_PS4_NS.Text = Lang.GetText(eLang.toolStripMenuItemNewAEV_PS4_NS);
            toolStripMenuItemNewLIT_2007_PS2.Text = Lang.GetText(eLang.toolStripMenuItemNewLIT_2007_PS2);
            toolStripMenuItemNewLIT_UHD_PS4NS.Text = Lang.GetText(eLang.toolStripMenuItemNewLIT_UHD_PS4NS);
            toolStripMenuItemNewEFFBLOB.Text = Lang.GetText(eLang.toolStripMenuItemNewEFFBLOB);
            toolStripMenuItemNewBigEndianFiles.Text = Lang.GetText(eLang.toolStripMenuItemNewBigEndianFiles);
            toolStripMenuItemNewEFFBLOBBIG.Text = Lang.GetText(eLang.toolStripMenuItemNewEFFBLOBBIG);
            // subsubmenu Open
            toolStripMenuItemOpenESL.Text = Lang.GetText(eLang.toolStripMenuItemOpenESL);
            toolStripMenuItemOpenETS_2007_PS2.Text = Lang.GetText(eLang.toolStripMenuItemOpenETS_2007_PS2);
            toolStripMenuItemOpenITA_2007_PS2.Text = Lang.GetText(eLang.toolStripMenuItemOpenITA_2007_PS2);
            toolStripMenuItemOpenAEV_2007_PS2.Text = Lang.GetText(eLang.toolStripMenuItemOpenAEV_2007_PS2);
            toolStripMenuItemOpenETS_UHD_PS4NS.Text = Lang.GetText(eLang.toolStripMenuItemOpenETS_UHD_PS4NS);
            toolStripMenuItemOpenITA_UHD.Text = Lang.GetText(eLang.toolStripMenuItemOpenITA_UHD);
            toolStripMenuItemOpenAEV_UHD.Text = Lang.GetText(eLang.toolStripMenuItemOpenAEV_UHD);
            toolStripMenuItemOpenDSE.Text = Lang.GetText(eLang.toolStripMenuItemOpenDSE);
            toolStripMenuItemOpenSMX.Text = Lang.GetText(eLang.toolStripMenuItemOpenSMX);
            toolStripMenuItemOpenFSE.Text = Lang.GetText(eLang.toolStripMenuItemOpenFSE);
            toolStripMenuItemOpenSAR.Text = Lang.GetText(eLang.toolStripMenuItemOpenSAR);
            toolStripMenuItemOpenEAR.Text = Lang.GetText(eLang.toolStripMenuItemOpenEAR);
            toolStripMenuItemOpenEMI_2007_PS2.Text = Lang.GetText(eLang.toolStripMenuItemOpenEMI_2007_PS2);
            toolStripMenuItemOpenESE_2007_PS2.Text = Lang.GetText(eLang.toolStripMenuItemOpenESE_2007_PS2);
            toolStripMenuItemOpenEMI_UHD_PS4NS.Text = Lang.GetText(eLang.toolStripMenuItemOpenEMI_UHD_PS4NS);
            toolStripMenuItemOpenESE_UHD_PS4NS.Text = Lang.GetText(eLang.toolStripMenuItemOpenESE_UHD_PS4NS);
            toolStripMenuItemOpenQuadCustom.Text = Lang.GetText(eLang.toolStripMenuItemOpenQuadCustom);
            toolStripMenuItemOpenITA_PS4_NS.Text = Lang.GetText(eLang.toolStripMenuItemOpenITA_PS4_NS);
            toolStripMenuItemOpenAEV_PS4_NS.Text = Lang.GetText(eLang.toolStripMenuItemOpenAEV_PS4_NS);
            toolStripMenuItemOpenLIT_2007_PS2.Text = Lang.GetText(eLang.toolStripMenuItemOpenLIT_2007_PS2);
            toolStripMenuItemOpenLIT_UHD_PS4NS.Text = Lang.GetText(eLang.toolStripMenuItemOpenLIT_UHD_PS4NS);
            toolStripMenuItemOpenEFFBLOB.Text = Lang.GetText(eLang.toolStripMenuItemOpenEFFBLOB);
            toolStripMenuItemOpenBigEndianFiles.Text = Lang.GetText(eLang.toolStripMenuItemOpenBigEndianFiles);
            toolStripMenuItemOpenEFFBLOBBIG.Text = Lang.GetText(eLang.toolStripMenuItemOpenEFFBLOBBIG);           
            // subsubmenu Save
            toolStripMenuItemSaveESL.Text = Lang.GetText(eLang.toolStripMenuItemSaveESL);
            toolStripMenuItemSaveETS.Text = Lang.GetText(eLang.toolStripMenuItemSaveETS);
            toolStripMenuItemSaveITA.Text = Lang.GetText(eLang.toolStripMenuItemSaveITA);
            toolStripMenuItemSaveAEV.Text = Lang.GetText(eLang.toolStripMenuItemSaveAEV);
            toolStripMenuItemSaveEMI.Text = Lang.GetText(eLang.toolStripMenuItemSaveEMI);
            toolStripMenuItemSaveESE.Text = Lang.GetText(eLang.toolStripMenuItemSaveESE);
            toolStripMenuItemSaveDSE.Text = Lang.GetText(eLang.toolStripMenuItemSaveDSE);
            toolStripMenuItemSaveSMX.Text = Lang.GetText(eLang.toolStripMenuItemSaveSMX);
            toolStripMenuItemSaveFSE.Text = Lang.GetText(eLang.toolStripMenuItemSaveFSE);
            toolStripMenuItemSaveSAR.Text = Lang.GetText(eLang.toolStripMenuItemSaveSAR);
            toolStripMenuItemSaveEAR.Text = Lang.GetText(eLang.toolStripMenuItemSaveEAR);
            toolStripMenuItemSaveLIT.Text = Lang.GetText(eLang.toolStripMenuItemSaveLIT);
            toolStripMenuItemSaveEFFBLOB.Text = Lang.GetText(eLang.toolStripMenuItemSaveEFFBLOB);
            toolStripMenuItemSaveQuadCustom.Text = Lang.GetText(eLang.toolStripMenuItemSaveQuadCustom);
            toolStripMenuItemSaveDirectories.Text = Lang.GetText(eLang.toolStripMenuItemSaveDirectories);
            // subsubmenu Save As...
            toolStripMenuItemSaveAsESL.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsESL);
            toolStripMenuItemSaveAsETS.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsETS);
            toolStripMenuItemSaveAsITA.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsITA);
            toolStripMenuItemSaveAsAEV.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsAEV);
            toolStripMenuItemSaveAsEMI.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsEMI);
            toolStripMenuItemSaveAsESE.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsESE);
            toolStripMenuItemSaveAsDSE.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsDSE);
            toolStripMenuItemSaveAsSMX.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsSMX);
            toolStripMenuItemSaveAsFSE.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsFSE);
            toolStripMenuItemSaveAsSAR.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsSAR);
            toolStripMenuItemSaveAsEAR.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsEAR);
            toolStripMenuItemSaveAsLIT.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsLIT);
            toolStripMenuItemSaveAsEFFBLOB.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsEFFBLOB);
            toolStripMenuItemSaveAsQuadCustom.Text = Lang.GetText(eLang.toolStripMenuItemSaveAsQuadCustom);
            // subsubmenu Save As (Convert)
            toolStripMenuItemSaveConverterETS.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterETS);
            toolStripMenuItemSaveConverterITA.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterITA);
            toolStripMenuItemSaveConverterAEV.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterAEV);
            toolStripMenuItemSaveConverterEMI.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterEMI);
            toolStripMenuItemSaveConverterESE.Text = Lang.GetText(eLang.toolStripMenuItemSaveConverterESE);
            // subsubmenu Clear
            toolStripMenuItemClearESL.Text = Lang.GetText(eLang.toolStripMenuItemClearESL);
            toolStripMenuItemClearETS.Text = Lang.GetText(eLang.toolStripMenuItemClearETS);
            toolStripMenuItemClearITA.Text = Lang.GetText(eLang.toolStripMenuItemClearITA);
            toolStripMenuItemClearAEV.Text = Lang.GetText(eLang.toolStripMenuItemClearAEV);
            toolStripMenuItemClearDSE.Text = Lang.GetText(eLang.toolStripMenuItemClearDSE);
            toolStripMenuItemClearSMX.Text = Lang.GetText(eLang.toolStripMenuItemClearSMX);
            toolStripMenuItemClearFSE.Text = Lang.GetText(eLang.toolStripMenuItemClearFSE);
            toolStripMenuItemClearSAR.Text = Lang.GetText(eLang.toolStripMenuItemClearSAR);
            toolStripMenuItemClearEAR.Text = Lang.GetText(eLang.toolStripMenuItemClearEAR);
            toolStripMenuItemClearEMI.Text = Lang.GetText(eLang.toolStripMenuItemClearEMI);
            toolStripMenuItemClearESE.Text = Lang.GetText(eLang.toolStripMenuItemClearESE);
            toolStripMenuItemClearLIT.Text = Lang.GetText(eLang.toolStripMenuItemClearLIT);
            toolStripMenuItemClearEFFBLOB.Text = Lang.GetText(eLang.toolStripMenuItemClearEFFBLOB);
            toolStripMenuItemClearQuadCustom.Text = Lang.GetText(eLang.toolStripMenuItemClearQuadCustom);

            // sub menu edit
            toolStripMenuItemAddNewObj.Text = Lang.GetText(eLang.toolStripMenuItemAddNewObj);
            toolStripMenuItemDeleteSelectedObj.Text = Lang.GetText(eLang.toolStripMenuItemDeleteSelectedObj);
            toolStripMenuItemMoveUp.Text = Lang.GetText(eLang.toolStripMenuItemMoveUp);
            toolStripMenuItemMoveDown.Text = Lang.GetText(eLang.toolStripMenuItemMoveDown);
            toolStripMenuItemSearch.Text = Lang.GetText(eLang.toolStripMenuItemSearch);

            // sub menu Misc
            toolStripMenuItemOptions.Text = Lang.GetText(eLang.toolStripMenuItemOptions);
            toolStripMenuItemCredits.Text = Lang.GetText(eLang.toolStripMenuItemCredits);

            // sub menu View
            toolStripMenuItemSubMenuHide.Text = Lang.GetText(eLang.toolStripMenuItemSubMenuHide);
            toolStripMenuItemSubMenuRoom.Text = Lang.GetText(eLang.toolStripMenuItemSubMenuRoom);
            toolStripMenuItemSubMenuModels.Text = Lang.GetText(eLang.toolStripMenuItemSubMenuModels);
            toolStripMenuItemSubMenuEnemy.Text = Lang.GetText(eLang.toolStripMenuItemSubMenuEnemy);
            toolStripMenuItemSubMenuItem.Text = Lang.GetText(eLang.toolStripMenuItemSubMenuItem);
            toolStripMenuItemSubMenuSpecial.Text = Lang.GetText(eLang.toolStripMenuItemSubMenuSpecial);
            toolStripMenuItemSubMenuEtcModel.Text = Lang.GetText(eLang.toolStripMenuItemSubMenuEtcModel);
            toolStripMenuItemSubMenuLight.Text = Lang.GetText(eLang.toolStripMenuItemSubMenuLight);
            toolStripMenuItemSubMenuEffect.Text = Lang.GetText(eLang.toolStripMenuItemSubMenuEffect);
            toolStripMenuItemNodeDisplayNameInHex.Text = Lang.GetText(eLang.toolStripMenuItemNodeDisplayNameInHex);
            toolStripMenuItemCameraMenu.Text = Lang.GetText(eLang.toolStripMenuItemCameraMenu);
            toolStripMenuItemResetCamera.Text = Lang.GetText(eLang.toolStripMenuItemResetCamera);
            toolStripMenuItemRefresh.Text = Lang.GetText(eLang.toolStripMenuItemRefresh);

            //sub menu hide
            toolStripMenuItemHideRoomModel.Text = Lang.GetText(eLang.toolStripMenuItemHideRoomModel);
            toolStripMenuItemHideEnemyESL.Text = Lang.GetText(eLang.toolStripMenuItemHideEnemyESL);
            toolStripMenuItemHideEtcmodelETS.Text = Lang.GetText(eLang.toolStripMenuItemHideEtcmodelETS);
            toolStripMenuItemHideItemsITA.Text = Lang.GetText(eLang.toolStripMenuItemHideItemsITA);
            toolStripMenuItemHideEventsAEV.Text = Lang.GetText(eLang.toolStripMenuItemHideEventsAEV);
            toolStripMenuItemHideLateralMenu.Text = Lang.GetText(eLang.toolStripMenuItemHideLateralMenu);
            toolStripMenuItemHideBottomMenu.Text = Lang.GetText(eLang.toolStripMenuItemHideBottomMenu);
            toolStripMenuItemHideFileFSE.Text = Lang.GetText(eLang.toolStripMenuItemHideFileFSE);
            toolStripMenuItemHideFileSAR.Text = Lang.GetText(eLang.toolStripMenuItemHideFileSAR);
            toolStripMenuItemHideFileEAR.Text = Lang.GetText(eLang.toolStripMenuItemHideFileEAR);
            toolStripMenuItemHideFileESE.Text = Lang.GetText(eLang.toolStripMenuItemHideFileESE);
            toolStripMenuItemHideFileEMI.Text = Lang.GetText(eLang.toolStripMenuItemHideFileEMI);
            toolStripMenuItemHideFileLIT.Text = Lang.GetText(eLang.toolStripMenuItemHideFileLIT);
            toolStripMenuItemHideFileEFF.Text = Lang.GetText(eLang.toolStripMenuItemHideFileEFF);
            toolStripMenuItemHideQuadCustom.Text = Lang.GetText(eLang.toolStripMenuItemHideQuadCustom);
            toolStripMenuItemHideFileCAM.Text = Lang.GetText(eLang.toolStripMenuItemHideFileCAM);
            toolStripMenuItemHideFileCAM_ZONE.Text = Lang.GetText(eLang.toolStripMenuItemHideFileCAM_ZONE);
            toolStripMenuItemHideFileRTP.Text = Lang.GetText(eLang.toolStripMenuItemHideFileRTP);

            // sub menus de view
            toolStripMenuItemHideDesabledEnemy.Text = Lang.GetText(eLang.toolStripMenuItemHideDesabledEnemy);
            toolStripMenuItemShowOnlyDefinedRoom.Text = Lang.GetText(eLang.toolStripMenuItemShowOnlyDefinedRoom);
            toolStripMenuItemAutoDefineRoom.Text = Lang.GetText(eLang.toolStripMenuItemAutoDefineRoom);
            toolStripMenuItemItemPositionAtAssociatedObjectLocation.Text = Lang.GetText(eLang.toolStripMenuItemItemPositionAtAssociatedObjectLocation);
            toolStripMenuItemHideItemTriggerZone.Text = Lang.GetText(eLang.toolStripMenuItemHideItemTriggerZone);
            toolStripMenuItemHideItemTriggerRadius.Text = Lang.GetText(eLang.toolStripMenuItemHideItemTriggerRadius);
            toolStripMenuItemHideItemAura.Text = Lang.GetText(eLang.toolStripMenuItemHideItemAura);
            toolStripMenuItemHideSpecialTriggerZone.Text = Lang.GetText(eLang.toolStripMenuItemHideSpecialTriggerZone);
            toolStripMenuItemHideExtraObjs.Text = Lang.GetText(eLang.toolStripMenuItemHideExtraObjs);
            toolStripMenuItemHideOnlyWarpDoor.Text = Lang.GetText(eLang.toolStripMenuItemHideOnlyWarpDoor);
            toolStripMenuItemHideExtraExceptWarpDoor.Text = Lang.GetText(eLang.toolStripMenuItemHideExtraExceptWarpDoor);
            toolStripMenuItemUseMoreSpecialColors.Text = Lang.GetText(eLang.toolStripMenuItemUseMoreSpecialColors);
            toolStripMenuItemEtcModelUseScale.Text = Lang.GetText(eLang.toolStripMenuItemEtcModelUseScale);
            toolStripMenuItemSubMenuQuadCustom.Text = Lang.GetText(eLang.toolStripMenuItemSubMenuQuadCustom);
            toolStripMenuItemUseCustomColors.Text = Lang.GetText(eLang.toolStripMenuItemUseCustomColors);
            toolStripMenuItemShowOnlySelectedGroup.Text = Lang.GetText(eLang.toolStripMenuItemShowOnlySelectedGroup);
            toolStripMenuItemSelectedGroupUp.Text = Lang.GetText(eLang.toolStripMenuItemSelectedGroupUp);
            toolStripMenuItemSelectedGroupDown.Text = Lang.GetText(eLang.toolStripMenuItemSelectedGroupDown);
            toolStripMenuItemEnableLightColor.Text = Lang.GetText(eLang.toolStripMenuItemEnableLightColor);
            toolStripMenuItemShowOnlySelectedGroup_EFF.Text = Lang.GetText(eLang.toolStripMenuItemShowOnlySelectedGroup_EFF);
            toolStripMenuItemSelectedGroupUp_EFF.Text = Lang.GetText(eLang.toolStripMenuItemSelectedGroupUp_EFF);
            toolStripMenuItemSelectedGroupDown_EFF.Text = Lang.GetText(eLang.toolStripMenuItemSelectedGroupDown_EFF);
            toolStripMenuItemHideTable7_EFF.Text = Lang.GetText(eLang.toolStripMenuItemHideTable7_EFF);
            toolStripMenuItemHideTable8_EFF.Text = Lang.GetText(eLang.toolStripMenuItemHideTable8_EFF);
            toolStripMenuItemHideTable9_EFF.Text = Lang.GetText(eLang.toolStripMenuItemHideTable9_EFF);
            toolStripMenuItemDisableGroupPositionEFF.Text = Lang.GetText(eLang.toolStripMenuItemDisableGroupPositionEFF);

            //sub menu de view room and model
            toolStripMenuItemModelsHideTextures.Text = Lang.GetText(eLang.toolStripMenuItemModelsHideTextures);
            toolStripMenuItemModelsWireframe.Text = Lang.GetText(eLang.toolStripMenuItemModelsWireframe);
            toolStripMenuItemModelsRenderNormals.Text = Lang.GetText(eLang.toolStripMenuItemModelsRenderNormals);
            toolStripMenuItemModelsOnlyFrontFace.Text = Lang.GetText(eLang.toolStripMenuItemModelsOnlyFrontFace);
            toolStripMenuItemModelsVertexColor.Text = Lang.GetText(eLang.toolStripMenuItemModelsVertexColor);
            toolStripMenuItemModelsAlphaChannel.Text = Lang.GetText(eLang.toolStripMenuItemModelsAlphaChannel);
            toolStripMenuItemRoomHideTextures.Text = Lang.GetText(eLang.toolStripMenuItemRoomHideTextures);
            toolStripMenuItemRoomWireframe.Text = Lang.GetText(eLang.toolStripMenuItemRoomWireframe);
            toolStripMenuItemRoomRenderNormals.Text = Lang.GetText(eLang.toolStripMenuItemRoomRenderNormals);
            toolStripMenuItemRoomOnlyFrontFace.Text = Lang.GetText(eLang.toolStripMenuItemRoomOnlyFrontFace);
            toolStripMenuItemRoomVertexColor.Text = Lang.GetText(eLang.toolStripMenuItemRoomVertexColor);
            toolStripMenuItemRoomAlphaChannel.Text = Lang.GetText(eLang.toolStripMenuItemRoomAlphaChannel);
            toolStripMenuItemRoomTextureNearestLinear.Text = Lang.GetText(eLang.toolStripMenuItemRoomTextureIsLinear);
            toolStripMenuItemModelsTextureNearestLinear.Text = Lang.GetText(eLang.toolStripMenuItemModelsTextureIsLinear);


            //save and open windows
            openFileDialogAEV.Title = Lang.GetText(eLang.openFileDialogAEV);
            openFileDialogESL.Title = Lang.GetText(eLang.openFileDialogESL);
            openFileDialogETS.Title = Lang.GetText(eLang.openFileDialogETS);
            openFileDialogITA.Title = Lang.GetText(eLang.openFileDialogITA);
            openFileDialogDSE.Title = Lang.GetText(eLang.openFileDialogDSE);
            openFileDialogSMX.Title = Lang.GetText(eLang.openFileDialogSMX);
            openFileDialogFSE.Title = Lang.GetText(eLang.openFileDialogFSE);
            openFileDialogSAR.Title = Lang.GetText(eLang.openFileDialogSAR);
            openFileDialogEAR.Title = Lang.GetText(eLang.openFileDialogEAR);
            openFileDialogEMI.Title = Lang.GetText(eLang.openFileDialogEMI);
            openFileDialogESE.Title = Lang.GetText(eLang.openFileDialogESE);
            openFileDialogLIT.Title = Lang.GetText(eLang.openFileDialogLIT);
            openFileDialogEFFBLOB.Title = Lang.GetText(eLang.openFileDialogEFFBLOB);
            openFileDialogEFFBLOBBIG.Title = Lang.GetText(eLang.openFileDialogEFFBLOBBIG);
            openFileDialogQuadCustom.Title = Lang.GetText(eLang.openFileDialogQuadCustom);

            saveFileDialogConvertAEV.Title = Lang.GetText(eLang.saveFileDialogConvertAEV);
            saveFileDialogConvertETS.Title = Lang.GetText(eLang.saveFileDialogConvertETS);
            saveFileDialogConvertITA.Title = Lang.GetText(eLang.saveFileDialogConvertITA);
            saveFileDialogConvertEMI.Title = Lang.GetText(eLang.saveFileDialogConvertEMI);
            saveFileDialogConvertESE.Title = Lang.GetText(eLang.saveFileDialogConvertESE);

            saveFileDialogAEV.Title = Lang.GetText(eLang.saveFileDialogAEV);
            saveFileDialogESL.Title = Lang.GetText(eLang.saveFileDialogESL);
            saveFileDialogETS.Title = Lang.GetText(eLang.saveFileDialogETS);
            saveFileDialogITA.Title = Lang.GetText(eLang.saveFileDialogITA);
            saveFileDialogDSE.Title = Lang.GetText(eLang.saveFileDialogDSE);
            saveFileDialogSMX.Title = Lang.GetText(eLang.saveFileDialogSMX);
            saveFileDialogFSE.Title = Lang.GetText(eLang.saveFileDialogFSE);
            saveFileDialogSAR.Title = Lang.GetText(eLang.saveFileDialogSAR);
            saveFileDialogEAR.Title = Lang.GetText(eLang.saveFileDialogEAR);
            saveFileDialogEMI.Title = Lang.GetText(eLang.saveFileDialogEMI);
            saveFileDialogESE.Title = Lang.GetText(eLang.saveFileDialogESE);
            saveFileDialogLIT.Title = Lang.GetText(eLang.saveFileDialogLIT);
            saveFileDialogEFFBLOB.Title = Lang.GetText(eLang.saveFileDialogEFFBLOB);
            saveFileDialogEFFBLOBBIG.Title = Lang.GetText(eLang.saveFileDialogEFFBLOBBIG);
            saveFileDialogQuadCustom.Title = Lang.GetText(eLang.saveFileDialogQuadCustom);

        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (theAppLoadedWell)
            {
                e.Cancel = true;

                ExitConfirmForm.ExitChoice choice;
                using (var dialog = new ExitConfirmForm(ProjectManager.CurrentProjectPath))
                {
                    dialog.ShowDialog(this);
                    choice = dialog.Choice;
                }

                //Save / Save As must complete before the app may close; if the
                //user cancels the save dialog we stay open.
                if (choice == ExitConfirmForm.ExitChoice.Save && !TrySaveProject()) return;
                if (choice == ExitConfirmForm.ExitChoice.SaveAs && !TrySaveProjectAs()) return;
                if (choice == ExitConfirmForm.ExitChoice.Cancel) return;

                e.Cancel = false;

                    DataBase.ItemsModels?.ClearGL();
                    DataBase.EtcModels?.ClearGL();
                    DataBase.EnemiesModels?.ClearGL();
                    DataBase.InternalModels?.ClearGL();
                    DataBase.QuadCustomModels?.ClearGL();
                    DataBase.SelectedRoom?.ClearGL();
                    DataShader.EndUnload();
            }
        }


        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            // entrada de teclas para açoes especiais
            cameraMove.isControlDown = e.Control;

            if (e.KeyCode == Keys.F8)
            {
                e.Handled = true;
                OpenElementLibrary();
                return;
            }

            #region usado em propery
            // proibe a estrada de caracteres que não vão nos campos de numeros
            if (InPropertyGrid && propertyGridObjs.SelectedGridItem != null && propertyGridObjs.SelectedGridItem.PropertyDescriptor != null)
            {

                if (propertyGridObjs.SelectedGridItem.PropertyDescriptor.Attributes.Contains(new DecNumberAttribute()))
                {

                    e.SuppressKeyPress = true;
                    if (KeysCheck.KeyIsNum(e.KeyCode))
                    {
                        e.SuppressKeyPress = false;
                    }
                    if (e.Control)
                    {
                        e.SuppressKeyPress = false;
                    }
                    if (e.Alt || e.Shift || e.KeyCode == Keys.Alt)
                    {
                        e.SuppressKeyPress = true;
                    }
                    if (KeysCheck.KeyIsEssential(e.KeyCode))
                    {
                        e.SuppressKeyPress = false;
                    }

                }

                if (propertyGridObjs.SelectedGridItem.PropertyDescriptor.Attributes.Contains(new DecNegativeNumberAttribute()))
                {

                    e.SuppressKeyPress = true;
                    if (KeysCheck.KeyIsNum(e.KeyCode))
                    {
                        e.SuppressKeyPress = false;
                    }
                    if (KeysCheck.KeyIsMinus(e.KeyCode))
                    {
                        e.SuppressKeyPress = false;
                    }
                    if (e.Control)
                    {
                        e.SuppressKeyPress = false;
                    }
                    if (e.Alt || e.Shift || e.KeyCode == Keys.Alt)
                    {
                        e.SuppressKeyPress = true;
                    }
                    if (KeysCheck.KeyIsEssential(e.KeyCode))
                    {
                        e.SuppressKeyPress = false;
                    }

                }

                if (propertyGridObjs.SelectedGridItem.PropertyDescriptor.Attributes.Contains(new HexNumberAttribute()))
                {

                    e.SuppressKeyPress = true;
                    if (KeysCheck.KeyIsNum(e.KeyCode))
                    {
                        e.SuppressKeyPress = false;
                    }
                    if (e.Shift)
                    {
                        e.SuppressKeyPress = true;
                    }
                    if (KeysCheck.KeyIsHex(e.KeyCode))
                    {
                        e.SuppressKeyPress = false;
                    }
                    if (e.Control)
                    {
                        e.SuppressKeyPress = false;
                    }
                    if (e.Alt || e.KeyCode == Keys.Alt)
                    {
                        e.SuppressKeyPress = true;
                    }
                    if (KeysCheck.KeyIsEssential(e.KeyCode))
                    {
                        e.SuppressKeyPress = false;
                    }

                }

                if (propertyGridObjs.SelectedGridItem.PropertyDescriptor.Attributes.Contains(new FloatNumberAttribute()))
                {

                    e.SuppressKeyPress = true;
                    if (KeysCheck.KeyIsNum(e.KeyCode))
                    {
                        e.SuppressKeyPress = false;
                    }
                    if (KeysCheck.KeyIsMinus(e.KeyCode))
                    {
                        e.SuppressKeyPress = false;
                    }
                    if (KeysCheck.KeyIsCommaDot(e.KeyCode))
                    {
                        e.SuppressKeyPress = false;
                    }
                    if (KeysCheck.KeyIsOnlyDot(e.KeyValue))
                    {
                        e.SuppressKeyPress = false;
                    }
                    if (e.Control)
                    {
                        e.SuppressKeyPress = false;
                    }
                    if (e.Alt || e.Shift || e.KeyCode == Keys.Alt)
                    {
                        e.SuppressKeyPress = true;
                    }
                    if (KeysCheck.KeyIsEssential(e.KeyCode))
                    {
                        e.SuppressKeyPress = false;
                    }
                }

                if (propertyGridObjs.SelectedGridItem.PropertyDescriptor.Attributes.Contains(new NoKeyAttribute()))
                {
                    e.SuppressKeyPress = true;
                    if (KeysCheck.KeyIsEssentialNoKey(e.KeyCode))
                    {
                        e.SuppressKeyPress = false;
                    }
                }
            }

            #endregion
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            cameraMove.isControlDown = e.Control;
        }

        #endregion

    }
}
