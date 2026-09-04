using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Diagnostics;
using OpenTK;
using Re4QuadExtremeEditor.src.Class.Enums;

namespace Re4QuadExtremeEditor.src
{
    /// <summary>
    /// Representa todos os status (configurações/opções) do programa;
    /// </summary>
    public static class Globals
    {

        #region Configs

        // diretorios
        public static string DirectoryXFILE = @"";
        public static string Directory2007RE4 = @"";
        public static string DirectoryPS2RE4 = @"";
        public static string DirectoryUHDRE4 = @"";
        public static string DirectoryPS4NSRE4 = @"";
        public static string DirectoryCustom1 = @"";
        public static string DirectoryCustom2 = @"";
        public static string DirectoryCustom3 = @"";

        // caminhos manuais das ferramentas externas (fallback quando a cópia
        // empacotada em ExternalTools não estiver presente)
        public static string ToolPathUDAS = @"";
        public static string ToolPathLFS = @"";
        public static string ToolPathPACK = @"";
        public static string ToolPathGCA = @"";

        // arquivos incluídos no carregamento completo da room (Load Complete)
        public static bool includeAEV = true;
        public static bool includeAVL = true;
        public static bool includeCAM = true;
        public static bool includeDSE = true;
        public static bool includeSMX = true;
        public static bool includeEAR = true;
        public static bool includeEMI = true;
        public static bool includeESE = true;
        public static bool includeETS = true;
        public static bool includeFSE = true;
        public static bool includeITA = true;
        public static bool includeLIT = true;
        public static bool includeRTP = true;
        public static bool includeSAR = true;

        //when true, "Load With Objects" reads room object files from
        //<game>\BIO4\data\<room>\0000.<ext> instead of St?<room>\<room>.<ext>
        public static bool useDataQingshengSource = false;

        //listagens json
        public static string FileDiretoryItemsList = Consts.DefaultItemsListFileDirectory;
        public static string FileDiretoryEtcModelsList = Consts.DefaultEtcModelsListFileDirectory;
        public static string FileDiretoryEnemiesList = Consts.DefaultEnemiesListFileDirectory;
        public static string FileDiretoryQuadCustomList = Consts.DefaultQuadCustomModelsListFileDirectory;

        // a cor do ceu
        public static Color SkyColor = Color.FromArgb(0xFF, 0x94, 0xD2, 0xFF);

        // float
        public static ConfigFrationalSymbol FrationalSymbol = ConfigFrationalSymbol.AcceptsCommaAndPeriod_OutputPeriod;
        public static int FrationalAmount = 9;

        // itens rotations options

        public static bool ItemDisableRotationAll = false;
        public static bool ItemDisableRotationIfXorYorZequalZero = false;
        public static bool ItemDisableRotationIfZisNotGreaterThanZero = true;
        public static ObjRotationOrder ItemRotationOrder = ObjRotationOrder.RotationXY;
        public static float ItemRotationCalculationMultiplier = 1;
        public static float ItemRotationCalculationDivider = 1;

        #endregion

        #region Colors

        // cores
        public static Color NodeColorEntry = Color.Black;
        public static Color NodeColorHided = Color.SlateGray;
        public static Color NodeColorESL = Color.FromArgb(192, 0, 0);
        public static Color NodeColorETS = Color.Maroon;
        public static Color NodeColorITA = Color.FromArgb(0, 0, 192);
        public static Color NodeColorAEV = Color.FromArgb(0, 192, 0);
        public static Color NodeColorEXTRAS = Color.FromArgb(0x0062707E);
        public static Color NodeColorDSE = Color.FromArgb(192, 192, 0);
        public static Color NodeColorSMX = Color.FromArgb(220, 120, 40);
        public static Color NodeColorAVL = Color.FromArgb(192, 96, 0);
        public static Color NodeColorCAM = Color.FromArgb(138, 43, 226);
        public static Color NodeColorCAM_ZONE = Color.FromArgb(186, 85, 211);
        public static Color NodeColorRTP = Color.FromArgb(0, 162, 162);
        public static Color NodeColorEMI = Color.Goldenrod;
        public static Color NodeColorSAR = Color.FromArgb(0, 192, 192); 
        public static Color NodeColorEAR = Color.DodgerBlue;
        public static Color NodeColorESE = Color.Violet;
        public static Color NodeColorFSE = Color.FromArgb(161, 192, 192);
        public static Color NodeColorLIT_GROUPS = Color.DarkSlateGray;
        public static Color NodeColorLIT_ENTRYS = Color.DarkSlateGray;
        public static Color NodeColorQuadCustom = Color.DimGray;

        public static Color NodeColorEFF_Table0 = Color.DarkSlateGray;
        public static Color NodeColorEFF_Table1 = Color.DarkSlateGray;
        public static Color NodeColorEFF_Table2 = Color.DarkSlateGray;
        public static Color NodeColorEFF_Table3 = Color.DarkSlateGray;
        public static Color NodeColorEFF_Table4 = Color.DarkSlateGray;
        public static Color NodeColorEFF_Table6 = Color.DarkSlateGray;
        public static Color NodeColorEFF_Table7_Effect_0 = Color.Teal;
        public static Color NodeColorEFF_Table8_Effect_1 = Color.SeaGreen;
        public static Color NodeColorEFF_EffectEntry = Color.DarkSlateGray;
        public static Color NodeColorEFF_Table9 = Color.DarkViolet;

        // color GL
        // cores
        public static Vector4 GL_ColorESL = Utils.ColorToVector4(Color.Red);
        public static Vector4 GL_ColorETS = Utils.ColorToVector4(Color.Maroon);
        public static Vector4 GL_ColorITA = Utils.ColorToVector4(Color.Blue);
        public static Vector4 GL_ColorAEV = Utils.ColorToVector4(Color.Lime);
        public static Vector4 GL_ColorEXTRAS = Utils.ColorToVector4(Color.SlateGray);
        public static Vector4 GL_ColorSelected = Utils.ColorToVector4(Color.Yellow);
        public static Vector4 GL_ColorItemTriggerZone = Utils.ColorToVector4(Color.Fuchsia);
        public static Vector4 GL_ColorItemTriggerZoneSelected = Utils.ColorToVector4(Color.Pink);
        public static Vector4 GL_ColorItemTrigggerRadius = Utils.ColorToVector4(Color.DeepPink);
        public static Vector4 GL_ColorItemTrigggerRadiusSelected = Utils.ColorToVector4(Color.Plum);
        public static Vector4 GL_ColorGrid = Utils.ColorToVector4(Color.DarkGray);

        public static Vector4 GL_ColorFSE = Utils.ColorToVector4(Color.LightCyan);
        public static Vector4 GL_ColorEAR = Utils.ColorToVector4(Color.DodgerBlue);
        public static Vector4 GL_ColorSAR = Utils.ColorToVector4(Color.Cyan);
        public static Vector4 GL_ColorEMI = Utils.ColorToVector4(Color.Goldenrod);
        public static Vector4 GL_ColorESE = Utils.ColorToVector4(Color.Violet);
        public static Vector4 GL_ColorLIT = Utils.ColorToVector4(Color.DarkSlateGray);
        public static Vector4 GL_ColorQuadCustom = Utils.ColorToVector4(Color.DimGray);
        public static Vector4 GL_ColorEFF_EffectEntry = Utils.ColorToVector4(Color.DarkSlateGray);
        public static Vector4 GL_ColorEFF_Table7 = Utils.ColorToVector4(Color.Teal);
        public static Vector4 GL_ColorEFF_Table8 = Utils.ColorToVector4(Color.SeaGreen);
        public static Vector4 GL_ColorEFF_Table9 = Utils.ColorToVector4(Color.DarkViolet);
        public static Vector4 GL_ColorCAM = Utils.ColorToVector4(Color.HotPink);
        public static Vector4 GL_ColorCAM_ZONE = Utils.ColorToVector4(Color.Orange);
        public static Vector4 GL_ColorRTP = Utils.ColorToVector4(Color.SpringGreen);
        public static Vector4 GL_ColorRTP_Link = Utils.ColorToVector4(Color.FromArgb(0, 190, 190));


        // more Colors
        public static Vector4 GL_MoreColor_T00_GeneralPurpose = Utils.ColorToVector4(Color.Green);
        public static Vector4 GL_MoreColor_T01_DoorWarp = Utils.ColorToVector4(Color.DarkOrange); //DarkOrange
        public static Vector4 GL_MoreColor_T02_CutSceneEvents = Utils.ColorToVector4(Color.Olive);
        public static Vector4 GL_MoreColor_T04_GroupedEnemyTrigger = Utils.ColorToVector4(Color.Sienna); //Thistle //DarkMagenta
        public static Vector4 GL_MoreColor_T05_Message = Utils.ColorToVector4(Color.MediumPurple);
        public static Vector4 GL_MoreColor_T08_TypeWriter = Utils.ColorToVector4(Color.Indigo);
        public static Vector4 GL_MoreColor_T0A_DamagesThePlayer = Utils.ColorToVector4(Color.LightSteelBlue); //Tomato
        public static Vector4 GL_MoreColor_T0B_FalseCollision = Utils.ColorToVector4(Color.Crimson); //Crimson
        public static Vector4 GL_MoreColor_T0D_FieldInfo = Utils.ColorToVector4(Color.DarkSeaGreen);
        public static Vector4 GL_MoreColor_T0E_Crouch = Utils.ColorToVector4(Color.BlanchedAlmond); //DarkSlateGray //DarkSalmon
        public static Vector4 GL_MoreColor_T10_FixedLadderClimbUp = Utils.ColorToVector4(Color.SteelBlue); //Chocolate
        public static Vector4 GL_MoreColor_T11_ItemDependentEvents = Utils.ColorToVector4(Color.DarkViolet);//DarkViolet //BlueViolet //DarkSlateBlue //Goldenrod //BlanchedAlmond
        public static Vector4 GL_MoreColor_T12_AshleyHideCommand = Utils.ColorToVector4(Color.Lavender);
        public static Vector4 GL_MoreColor_T13_LocalTeleportation = Utils.ColorToVector4(Color.DarkSalmon); //Wheat //DarkViolet
        public static Vector4 GL_MoreColor_T14_UsedForElevators = Utils.ColorToVector4(Color.YellowGreen);
        public static Vector4 GL_MoreColor_T15_AdaGrappleGun = Utils.ColorToVector4(Color.Navy);

        #endregion

        // backup da class config
        public static Re4QuadExtremeEditor.src.JSON.Configs BackupConfigs = null;


        #region Menu options
        // se pode renderizar o modelo 3d da room
        public static bool RenderRoom = true;

        public static bool RenderEnemyESL = true;
        public static bool RenderEtcmodelETS = true;
        public static bool RenderItemsITA = true;
        public static bool RenderEventsAEV = true;
        // Snake ITA (ItemNumber 0x1000) renders as its ESL enemy model instead of the snake item
        public static bool RenderSnakeAsEnemy = false;
        public static bool RenderFileFSE = true;
        public static bool RenderFileSAR = true;
        public static bool RenderFileEAR = true;
        public static bool RenderFileEMI = true;
        public static bool RenderFileESE = true;
        public static bool RenderFileQuadCustom = true;
        public static bool RenderFileLIT = true;
        public static bool RenderFileEFFBLOB = true;
        public static bool RenderFileCAM = true;
        public static bool RenderFileCAM_Zone = true;
        public static bool RenderFileRTP = true;

        //enemy renders
        public static bool RenderDisabledEnemy = true;
        public static bool RenderDontShowOnlyDefinedRoom = true;
        public static ushort RenderEnemyFromDefinedRoom = 0x0000;
        public static bool AutoDefinedRoom = false;

        // items render
        public static bool RenderItemTriggerZone = true;
        public static bool RenderItemPositionAtAssociatedObjectLocation = false;
        public static bool RenderItemTriggerRadius = true;
        public static bool RenderItemAura = false;

        //special render
        public static bool RenderSpecialTriggerZone = true;
        public static bool RenderExtraObjs = true;
        public static bool UseMoreSpecialColors = false;
        public static bool RenderExtraWarpDoor = true;
        public static bool HideExtraExceptWarpDoor = false;

        //QuadCustom
        public static bool UseMoreQuadCustomColors = false;

        //Etcmodel
        public static bool RenderEtcmodelUsingScale = false;


        public static bool TreeNodeRenderHexValues = false;

        // AVL: exibe os valores em decimal (false = hexadecimal)
        public static bool AvlRenderDecimal = false;

        // opção que muda no propetyGrid
        public static bool PropertyGridUseHexFloat = false;

        //search
        public static bool SearchFilterMode = false;

        //light
        public static bool LIT_ShowOnlySelectedGroup = false;
        public static ushort LIT_SelectedGroup = 0;
        public static bool LIT_EnableLightColor = false;

        //Effect
        public static bool EFF_ShowOnlySelectedGroup = false;
        public static ushort EFF_SelectedGroup = 0;
        public static bool EFF_RenderTable7 = true;
        public static bool EFF_RenderTable8 = true;
        public static bool EFF_RenderTable9 = true;
        public static bool EFF_Use_Group_Position = true;

        #endregion


        #region patch Files, diretorios dos arquivos

        public static string FilePathESL = null;
        public static string FilePathETS = null;
        public static string FilePathITA = null;
        public static string FilePathAEV = null;
        public static string FilePathDSE = null;
        public static string FilePathSMX = null;
 public static string FilePathAVL = null;
        public static string FilePathFSE = null;
        public static string FilePathSAR = null;
        public static string FilePathEAR = null;
        public static string FilePathEMI = null;
        public static string FilePathESE = null;
        public static string FilePathQuadCustom = null;
        public static string FilePathEFFBLOB = null;
        public static string FilePathLIT = null;
        public static string FilePathCAM = null;
        public static string FilePathRTP = null;
        #endregion

        // Render Options
        public static float FOV = 60f; // field of view (in degrees), float so transitions can be smoothly interpolated

        //opção de lista de inimigos extra sets.
        public static bool CreateEnemyExtraSegmentList = true;

        // gizmo — ported from Re4QuadX for exact parity
        public static EditorTool CurrentTool = EditorTool.Move;
        public static GizmoSpace CurrentGizmoSpace = GizmoSpace.World;
        // Mirrors ObjectMoveControl.MoveObjTypeSelected so gizmo can emulate the same yellow-arrow separation (item vs trigger zone)
        public static MoveObjType CurrentMoveType = MoveObjType.Null;

        //cam grid
        public static bool CamGridEnable = false;
        public static int CamGridvalue = 100;


        // treenode fonts
        public static Font TreeNodeFontText = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold);
        public static Font TreeNodeFontHex = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Bold);

        //OpenGLVersion
        public static string OpenGLVersion = "";

        // FPS tracking for the runtime system panel
        public static int CurrentFps = 0;

        /// <summary>
        /// Viewport frame-rate limiter (View menu). 0 = unlimited
        /// (internally capped at 240 to keep the idle loop sane).
        /// </summary>
        public static int FpsLimit = 120;

        private static readonly Stopwatch RenderFpsStopwatch = Stopwatch.StartNew();
        private static int RenderFrameCounter = 0;
        private static long RenderFpsLastSampleMs = 0;

        public static void UpdateRenderFps()
        {
            RenderFrameCounter++;
            long elapsedMs = RenderFpsStopwatch.ElapsedMilliseconds;

            if (elapsedMs - RenderFpsLastSampleMs >= 1000)
            {
                double deltaMs = Math.Max(1, elapsedMs - RenderFpsLastSampleMs);
                CurrentFps = (int)Math.Round(RenderFrameCounter * 1000.0 / deltaMs);
                RenderFrameCounter = 0;
                RenderFpsLastSampleMs = elapsedMs;
            }
        }
    }
}
