using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Re4QuadExtremeEditor.src.JSON;
using Re4QuadExtremeEditor.src.Class;
using Re4QuadExtremeEditor.src.Class.Enums;
using System.IO;
using NewAgeTheRender;
using Re4QuadExtremeEditor.src;
using OpenTK.Graphics.OpenGL;
using Microsoft.Win32;


namespace Re4QuadExtremeEditor.src.Forms
{
    public partial class SelectRoomForm : Form
    {
        // ------------------------------------------------------------------
        //  LoadJsonFiles stays as a FALLBACK (called only if the startup
        //  cache is not ready yet).  Normally the cache is always ready.
        // ------------------------------------------------------------------
        private List<RoomInfo> LoadJsonFiles() 
        {
            return Re4QuadExtremeEditor.src.Utils.LoadRoomInfoList();
        }

        /// <summary>
        /// evendo que acontece depois de clicar em load;
        /// </summary>
        public event EventHandler onLoadButtonClick;

        // -----------------------------------------------------------------
        //  "LOAD COMPLETE" split button (ported from Re4QuadX): loads the room
        //  model AND every object file type enabled in its dropdown checklist
        //  (ETS/ITA/AEV/LIT/ESE/EMI/DSE/FSE/SAR/EAR), auto-extracting the
        //  room archive (.udas.lfs/.udas/.dat) through the external tools
        //  when needed.
        // -----------------------------------------------------------------
        private System.Windows.Forms.Button buttonLoadComplete;
        private System.Windows.Forms.ContextMenuStrip advancedLoadContextMenu;
        private System.Windows.Forms.ToolStripMenuItem includeAEV;
        private System.Windows.Forms.ToolStripMenuItem includeAVL;
        private System.Windows.Forms.ToolStripMenuItem includeCAM;
        private System.Windows.Forms.ToolStripMenuItem includeDSE;
        private System.Windows.Forms.ToolStripMenuItem includeSMX;
        private System.Windows.Forms.ToolStripMenuItem includeEAR;
        private System.Windows.Forms.ToolStripMenuItem includeEMI;
        private System.Windows.Forms.ToolStripMenuItem includeESE;
        private System.Windows.Forms.ToolStripMenuItem includeETS;
        private System.Windows.Forms.ToolStripMenuItem includeFSE;
        private System.Windows.Forms.ToolStripMenuItem includeITA;
        private System.Windows.Forms.ToolStripMenuItem includeLIT;
        private System.Windows.Forms.ToolStripMenuItem includeRTP;
        private System.Windows.Forms.ToolStripMenuItem includeSAR;

        //file source selector: Default RE4 layout vs "data Qingsheng DLL"
        //(all object files live as <base>\data\<room>\0000.<extension>)
        private System.Windows.Forms.Label labelFileSource;
        internal System.Windows.Forms.ComboBox comboFileSource;

        public SelectRoomForm()
        {
            InitializeComponent();
            BuildCompleteLoadControls();

            bool useModernStyle = !UiTheme.IsLight;
            if (useModernStyle)
            {
                DarkTheme.Apply(this);
            }
            StyleModernDialog(useModernStyle);
            KeyPreview = true;

            comboBoxMainList.Items.Add(Lang.GetText(eLang.NoneRoom));
            comboBoxMainList.SelectedIndex = 0;

            // ---------------------------------------------------------------
            //  Use the preloaded cache whenever possible so the form opens
            //  instantly.  Falls back to disk I/O only if cache is not ready.
            // ---------------------------------------------------------------
            var list1 = DataBase.CachedRoomInfoList ?? LoadJsonFiles();
            comboBoxMainList.Items.AddRange(list1.ToArray());

            if (DataBase.SelectedRoom != null)
            {
                //aqui deve selecionar o que ja esta carregado

                var list = comboBoxMainList.Items.OfType<RoomInfo>();
                var obj = list.Where(x => x.RoomListObj != null && DataBase.SelectedRoom.GetRoomListObj() != null 
                && x.RoomListObj.JsonFileName == DataBase.SelectedRoom.GetRoomListObj().JsonFileName).FirstOrDefault();
                if (obj != null)
                {
                    int index = comboBoxMainList.Items.IndexOf(obj);
                    if (index > -1)
                    {
                        comboBoxMainList.SelectedIndex = index;

                        if (comboBoxRoomList.Items.Contains(DataBase.SelectedRoom.GetRoomModel()))
                        {
                            comboBoxRoomList.SelectedIndex = comboBoxRoomList.Items.IndexOf(DataBase.SelectedRoom.GetRoomModel());
                        }
                    }
                }
            }

            if (Lang.LoadedTranslation)
            {
                StartUpdateTranslation();
            }

            //disable load buttons to prevent null room load
            if (lastSelected == null)
            {
                buttonLoad.Enabled = false;
                buttonLoadComplete.Enabled = false;
                buttonLoadCompleteArrow.Enabled = false;
            }
        }

        object lastSelected = null;

        private void comboBoxMainList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxMainList.SelectedItem is RoomInfo r)
            {
                comboBoxRoomList.Items.Clear();
                comboBoxRoomList.Items.Add(Lang.GetText(eLang.NoneRoom));
                comboBoxRoomList.Items.AddRange(r.RoomModelDict.Values.ToArray());
            }
            else 
            {
                comboBoxRoomList.Items.Clear();
                comboBoxRoomList.Items.Add(Lang.GetText(eLang.NoneRoom));
            }

            bool foundIt = false;

            if (lastSelected is RoomModel rm)
            {
                 var list = comboBoxRoomList.Items.OfType<RoomModel>();
                 var obj = list.Where(x => x.JsonFileName == rm.JsonFileName || x.HexID == rm.HexID).FirstOrDefault();
                if (obj != null)
                {
                    int index = comboBoxRoomList.Items.IndexOf(obj);
                    if (index > -1)
                    {
                        comboBoxRoomList.SelectedIndex = index;
                        foundIt = true;
                    }
                }
            }

            if (!foundIt)
            {
                comboBoxRoomList.SelectedIndex = 0;
            }
        }

        private bool Enable_ComboBoxRoomList_SelectedIndexChanged = true;

        private void comboBoxRoomList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Enable_ComboBoxRoomList_SelectedIndexChanged)
            {
                lastSelected = comboBoxRoomList.SelectedItem;
            }

            bool isRoomSelected = (comboBoxRoomList.SelectedItem is RoomModel);
            buttonLoad.Enabled = isRoomSelected;
            buttonLoadComplete.Enabled = isRoomSelected;
            buttonLoadCompleteArrow.Enabled = isRoomSelected;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            applyInclusionChecks();
            Close();
        }

        private void blockInput()
        {
            buttonLoad.Enabled = false;
            if (buttonLoadComplete != null) buttonLoadComplete.Enabled = false;
            if (buttonLoadCompleteArrow != null) buttonLoadCompleteArrow.Enabled = false;
            if (comboFileSource != null) comboFileSource.Enabled = false;
            buttonCancel.Enabled = false;
            comboBoxMainList.Enabled = false;
            comboBoxRoomList.Enabled = false;
        }

        private void buttonLoad_Click(object sender, EventArgs e)
        {
            if (comboFileSource != null) Globals.useDataQingshengSource = comboFileSource.SelectedIndex == 1;

            blockInput();

            LoadRoomModel();
        }

        private void comboFileSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            Globals.useDataQingshengSource = comboFileSource.SelectedIndex == 1;
            Globals.BackupConfigs.UseDataQingshengSource = Globals.useDataQingshengSource;
            try { JSON.ConfigsFile.writeConfigsFile(Consts.ConfigsFileDirectory, Globals.BackupConfigs); }
            catch (Exception) { }
        }

        private async void buttonLoadComplete_Click(object sender, EventArgs e)
        {
            blockInput();

            //remember the chosen file source (session + config file)
            if (comboFileSource != null)
            {
                Globals.useDataQingshengSource = comboFileSource.SelectedIndex == 1;
                Globals.BackupConfigs.UseDataQingshengSource = Globals.useDataQingshengSource;
                try { JSON.ConfigsFile.writeConfigsFile(Consts.ConfigsFileDirectory, Globals.BackupConfigs); }
                catch (Exception) { }
            }

            try
            {
                EditorConsole.Log("Load With Objects: starting...");
                await LoadRoomObjects();
                EditorConsole.Log("Load With Objects: object files done, loading room model...");
            }
            catch (Exception ex)
            {
                EditorConsole.Error("Load With Objects failed while preparing object files: " + ex);
            }

            try
            {
                LoadRoomModel();
            }
            catch (Exception ex)
            {
                EditorConsole.Error("Load With Objects failed while loading the room model: " + ex);
            }
        }

        private void buttonLoadCompleteArrow_Click(object sender, EventArgs e)
        {
            advancedLoadContextMenu.Show(buttonLoadCompleteArrow, new Point(0, buttonLoadCompleteArrow.Height));
        }

        private void advancedLoadContextMenu_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
            {
                e.Cancel = true;
            }
        }

        // -----------------------------------------------------------------
        //  Builds the "Load With Objects" split button + its dropdown
        //  checklist (plain WinForms port of Re4QuadX's PowerLib XButton:
        //  main area loads, the small arrow opens the include checklist).
        // -----------------------------------------------------------------
        private System.Windows.Forms.Button buttonLoadCompleteArrow;

        private void BuildCompleteLoadControls()
        {
            buttonLoadComplete = new System.Windows.Forms.Button();
            buttonLoadComplete.Name = "buttonLoadComplete";
            buttonLoadComplete.Text = "Load With Objects";
            buttonLoadComplete.UseVisualStyleBackColor = true;
            buttonLoadComplete.Click += buttonLoadComplete_Click;

            buttonLoadCompleteArrow = new System.Windows.Forms.Button();
            buttonLoadCompleteArrow.Name = "buttonLoadCompleteArrow";
            buttonLoadCompleteArrow.Text = "▾";
            buttonLoadCompleteArrow.UseVisualStyleBackColor = true;
            buttonLoadCompleteArrow.Click += buttonLoadCompleteArrow_Click;

            Controls.Add(buttonLoadComplete);
            Controls.Add(buttonLoadCompleteArrow);

            advancedLoadContextMenu = new System.Windows.Forms.ContextMenuStrip();
            advancedLoadContextMenu.Items.Add(includeITA = new ToolStripMenuItem("Include ITA") { CheckOnClick = true });
            advancedLoadContextMenu.Items.Add(includeAEV = new ToolStripMenuItem("Include AEV") { CheckOnClick = true });
            advancedLoadContextMenu.Items.Add(includeLIT = new ToolStripMenuItem("Include LIT") { CheckOnClick = true });
            advancedLoadContextMenu.Items.Add(includeETS = new ToolStripMenuItem("Include ETS") { CheckOnClick = true });
            advancedLoadContextMenu.Items.Add(includeESE = new ToolStripMenuItem("Include ESE") { CheckOnClick = true });
            advancedLoadContextMenu.Items.Add(includeEMI = new ToolStripMenuItem("Include EMI") { CheckOnClick = true });
            advancedLoadContextMenu.Items.Add(includeDSE = new ToolStripMenuItem("Include DSE") { CheckOnClick = true });
            advancedLoadContextMenu.Items.Add(includeSMX = new ToolStripMenuItem("Include SMX") { CheckOnClick = true });
            advancedLoadContextMenu.Items.Add(includeFSE = new ToolStripMenuItem("Include FSE") { CheckOnClick = true });
            advancedLoadContextMenu.Items.Add(includeSAR = new ToolStripMenuItem("Include SAR") { CheckOnClick = true });
            advancedLoadContextMenu.Items.Add(includeEAR = new ToolStripMenuItem("Include EAR") { CheckOnClick = true });
            advancedLoadContextMenu.Items.Add(includeCAM = new ToolStripMenuItem("Include CAM") { CheckOnClick = true });
            advancedLoadContextMenu.Items.Add(includeAVL = new ToolStripMenuItem("Include AVL") { CheckOnClick = true });
            advancedLoadContextMenu.Items.Add(includeRTP = new ToolStripMenuItem("Include RTP") { CheckOnClick = true });
            advancedLoadContextMenu.Closing += advancedLoadContextMenu_Closing;

            labelFileSource = new System.Windows.Forms.Label();
            labelFileSource.Name = "labelFileSource";
            labelFileSource.Text = "File source:";
            labelFileSource.AutoSize = true;

            comboFileSource = new System.Windows.Forms.ComboBox();
            comboFileSource.Name = "comboFileSource";
            comboFileSource.DropDownStyle = ComboBoxStyle.DropDownList;
            comboFileSource.Width = 170;
            comboFileSource.Items.Add("Default (RE4)");
            comboFileSource.Items.Add("data Qingsheng DLL");
            comboFileSource.SelectedIndex = Globals.useDataQingshengSource ? 1 : 0;
            //remember the last used file source across sessions: saved the
            //moment it changes, so every load path (plain Load, Load With
            //Objects or even closing the form) keeps the choice for next time
            comboFileSource.SelectedIndexChanged += comboFileSource_SelectedIndexChanged;

            Controls.Add(labelFileSource);
            Controls.Add(comboFileSource);

            getInclusionChecks();
        }

        private void getInclusionChecks()
        {
            includeAEV.Checked = Globals.includeAEV;
            includeAVL.Checked = Globals.includeAVL;
            includeCAM.Checked = Globals.includeCAM;
            includeDSE.Checked = Globals.includeDSE;
            includeSMX.Checked = Globals.includeSMX;
            includeEAR.Checked = Globals.includeEAR;
            includeEMI.Checked = Globals.includeEMI;
            includeESE.Checked = Globals.includeESE;
            includeETS.Checked = Globals.includeETS;
            includeFSE.Checked = Globals.includeFSE;
            includeITA.Checked = Globals.includeITA;
            includeLIT.Checked = Globals.includeLIT;
            includeRTP.Checked = Globals.includeRTP;
            includeSAR.Checked = Globals.includeSAR;
        }

        private void applyInclusionChecks()
        {
            Globals.includeAEV = includeAEV.Checked;
            Globals.includeAVL = includeAVL.Checked;
            Globals.includeCAM = includeCAM.Checked;
            Globals.includeDSE = includeDSE.Checked;
            Globals.includeSMX = includeSMX.Checked;
            Globals.includeEAR = includeEAR.Checked;
            Globals.includeEMI = includeEMI.Checked;
            Globals.includeESE = includeESE.Checked;
            Globals.includeETS = includeETS.Checked;
            Globals.includeFSE = includeFSE.Checked;
            Globals.includeITA = includeITA.Checked;
            Globals.includeLIT = includeLIT.Checked;
            Globals.includeRTP = includeRTP.Checked;
            Globals.includeSAR = includeSAR.Checked;
        }

        public async void LoadRoomModel()
        {
            // Clear current room if existent
            if (DataBase.SelectedRoom != null)
            {
                EditorConsole.Log($"Clearing previously loaded room: {DataBase.SelectedRoom.GetRoomModel().Description} (ID: {DataBase.SelectedRoom.GetRoomModel().HexID})");
                await Task.Delay(1); //wait 1 frame to give time to log

                //clear room
                DataBase.SelectedRoom.ClearGL();
                DataBase.SelectedRoom = null;


            }

            // Create new room
            if (comboBoxMainList.SelectedItem is RoomInfo r && comboBoxRoomList.SelectedItem is RoomModel rm)
            {
                EditorConsole.Log($"Loading new Room: {comboBoxRoomList.SelectedItem}");
                await Task.Delay(1); //wait 1 frame to give time to log

                Cursor.Current = Cursors.WaitCursor;
                //select desired room
                try
                {
                    DataBase.SelectedRoom = new RoomSelectedObj(rm, r.RoomListObj, GetQingshengDirectoryFor(rm));
                }
                catch (OperationCanceledException) { Cursor.Current = Cursors.Default; Close(); return; }


            }

            onLoadButtonClick?.Invoke(comboBoxRoomList.SelectedItem, new EventArgs());

            Cursor.Current = Cursors.Default;
            Close();
        }

        private async Task LoadRoomObjects()
        {
            if (!(comboBoxMainList.SelectedItem is RoomInfo roomInfo) || !(comboBoxRoomList.SelectedItem is RoomModel roomModel) || roomInfo.RoomListObj == null)
            {
                EditorConsole.Log("No room selected for complete load, or room list is invalid. Loading model only.");
                return;
            }

            EditorConsole.Log($"Starting complete load for room: {roomModel.Description}");
            UndoSystem.Clear();

            //clear all object files present to replace with new room
            EditorConsole.Log("Clearing previously loaded object files..");
            if (Application.OpenForms["MainForm"] is MainForm main)
            {
                try
                {
                    main.TreeViewUpdateSelectedsClear();
                }
                catch (Exception ex)
                {
                    EditorConsole.Error("Failed to clear the current selection: " + ex);
                }
            }
            FileManager.ClearETS();
            FileManager.ClearITA();
            FileManager.ClearAEV();
            FileManager.ClearDSE();
            FileManager.ClearSMX();
            FileManager.ClearFSE();
            FileManager.ClearSAR();
            FileManager.ClearEAR();
            FileManager.ClearEMI();
            FileManager.ClearESE();
            FileManager.ClearLIT();
            FileManager.ClearCAM();
            FileManager.ClearRTP();

            //get game version and base path (derived straight from the selected
            //room's JSON type - each of our room lists carries its own version,
            //so no manual PreferredVersion pick is needed)
            string rootGamePath = "";
            bool isUhd = false;
            bool isPs4NsAdapted = false;
            EditorRe4Ver gameVersion;

            switch (roomModel.Type)
            {
                case RoomModel.EType.UHD:
                case RoomModel.EType.R100UHD:
                    gameVersion = EditorRe4Ver.UHD;
                    rootGamePath = Globals.DirectoryUHDRE4;
                    isUhd = true;
                    break;
                case RoomModel.EType.PS4NS:
                case RoomModel.EType.R100PS4NS:
                    gameVersion = EditorRe4Ver.PS4NS;
                    rootGamePath = Globals.DirectoryPS4NSRE4;
                    isUhd = true;
                    isPs4NsAdapted = true;
                    break;
                case RoomModel.EType.V2007:
                    gameVersion = EditorRe4Ver.SourceNext2007;
                    rootGamePath = Globals.Directory2007RE4;
                    break;
                case RoomModel.EType.PS2:
                    gameVersion = EditorRe4Ver.PS2;
                    rootGamePath = Globals.DirectoryPS2RE4;
                    break;
                default:
                    EditorConsole.Log("Unknown room version. Loading model only.");
                    return;
            }

            if (string.IsNullOrEmpty(rootGamePath))
            {
                EditorConsole.Error($"Game directory for version '{gameVersion}' is not configured (check the options). Skipping object file load.");
                return;
            }

            //find the room directory (combine root game path + /bio4 for UHD)
            string basePath = (gameVersion == EditorRe4Ver.UHD || gameVersion == EditorRe4Ver.PS4NS)
            ? Path.Combine(rootGamePath, "BIO4")
            : rootGamePath;

            string rawFileName = Path.GetFileNameWithoutExtension(roomModel.JsonFileName);
            string roomName = rawFileName.Split('_')[0]; //gets raw room ID without extra shit (for r100 mostly)

            bool qingshengMode = Globals.useDataQingshengSource;

            if (qingshengMode)
            {
                //"data Qingsheng DLL" layout: <base>\data\<room>\0000.<ext>
                string dataDirectory = Path.Combine(basePath, "data", roomName);

                if (!Directory.Exists(dataDirectory))
                {
                    EditorConsole.Log($"data Qingsheng DLL folder not found for '{roomName}'. Extracting the room and converting its files to 0000.* ...");
                    string extractedDir = await FindOrExtractStRoomDirectory(basePath, roomName, gameVersion);
                    if (extractedDir == null)
                    {
                        EditorConsole.Error($"Could not find directory for room '{roomName}' in any 'St' folder for version '{gameVersion}'. Skipping object file load.");
                        return;
                    }
                    CopyRoomToDataFolder(extractedDir, dataDirectory);
                }
                else
                {
                    EditorConsole.Log($"Using existing data Qingsheng DLL directory: {dataDirectory}");
                }

                if (!Directory.Exists(dataDirectory))
                {
                    EditorConsole.Error("data Qingsheng DLL folder could not be created. Skipping object file load.");
                    return;
                }

                LoadAllFilesForRoom(dataDirectory, isUhd, isPs4NsAdapted);
            }
            else
            {
                //default RE4 layout: search St0-St7 with auto-extraction
                string roomDirectory = await FindOrExtractStRoomDirectory(basePath, roomName, gameVersion);
                if (roomDirectory == null)
                {
                    EditorConsole.Error($"Could not find directory for room '{roomName}' in any 'St' folder for version '{gameVersion}'. Skipping object file load.");
                    return;
                }

                LoadAllFilesForRoom(roomDirectory, isUhd, isPs4NsAdapted);
            }

            applyInclusionChecks();
        }

        /// <summary>
        /// Returns the "data Qingsheng DLL" directory for this room when that
        /// mode is selected and the folder exists; otherwise null.
        /// </summary>
        private string GetQingshengDirectoryFor(RoomModel rm)
        {
            return ProjectManager.GetDataQingshengDirectory(rm);
        }

        /// <summary>
        /// Searches St0-St7 for the room folder, auto-extracting from
        /// .udas/.udas.lfs/.dat when needed. Returns null when not found.
        /// </summary>
        private async Task<string> FindOrExtractStRoomDirectory(string basePath, string roomName, EditorRe4Ver gameVersion)
        {
            for (int i = 0; i <= 7; i++)
            {
                string stagePath = Path.Combine(basePath, "St" + i);

                bool extracted = await ExternalToolManager.EnsureRoomExtracted(stagePath, roomName, gameVersion, i);
                if (!extracted) continue;

                string potentialPath = Path.Combine(stagePath, roomName);
                if (Directory.Exists(potentialPath))
                {
                    EditorConsole.Log($"Found room directory at: {potentialPath}");
                    return potentialPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Copies every file of an already extracted room into the
        /// "data Qingsheng DLL" layout: every file becomes 0000.<original ext>.
        /// </summary>
        private static void CopyRoomToDataFolder(string sourceDirectory, string targetDirectory)
        {
            try
            {
                Directory.CreateDirectory(targetDirectory);

                int copied = 0;
                foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
                {
                    string extension = Path.GetExtension(file).ToLowerInvariant();
                    string destination = Path.Combine(targetDirectory, "0000" + extension);
                    File.Copy(file, destination, true);
                    copied++;
                }

                EditorConsole.Log($"Converted {copied} file(s) into '{targetDirectory}' as 0000.*");
            }
            catch (Exception ex)
            {
                EditorConsole.Error($"Failed building data Qingsheng DLL folder: {ex.Message}");
            }
        }

        private void LoadAllFilesForRoom(string roomPath, bool isUhd, bool isPs4Ns)
        {
            RoomObjectFileLoader.LoadAllFilesForRoom(roomPath, (RoomModel)comboBoxRoomList.SelectedItem, delegate (string extension)
            {
                switch (extension.ToUpperInvariant())
                {
                    //.esl is not in the context menu
                    case ".ITA": return includeITA.Checked;
                    case ".AEV": return includeAEV.Checked;
                    case ".AVL": return includeAVL.Checked;
                    case ".CAM": return isUhd && includeCAM.Checked;
                    case ".RTP": return isUhd && includeRTP.Checked;
                    case ".LIT": return includeLIT.Checked;
                    case ".ETS": return includeETS.Checked;
                    case ".ESE": return includeESE.Checked;
                    case ".EMI": return includeEMI.Checked;
                    case ".DSE": return includeDSE.Checked;
                    case ".SMX": return includeSMX.Checked;
                    case ".FSE": return includeFSE.Checked;
                    case ".SAR": return includeSAR.Checked;
                    case ".EAR": return includeEAR.Checked;
                    default: return true;
                }
            });
        }

        private void SelectRoomForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        }

        private void StyleModernDialog(bool themed)
        {
            if (themed)
            {
                // Primary action gets the accent fill; secondary stays a ghost card.
                buttonLoad.UseVisualStyleBackColor = false;
                buttonLoad.FlatStyle = FlatStyle.Flat;
                buttonLoad.FlatAppearance.BorderSize = 0;
                buttonLoad.BackColor = DarkTheme.Accent;
                buttonLoad.ForeColor = Color.White;
                buttonLoad.FlatAppearance.MouseOverBackColor = DarkTheme.AccentHover;
                buttonLoad.FlatAppearance.MouseDownBackColor = DarkTheme.AccentPressed;

                buttonCancel.FlatStyle = FlatStyle.Flat;
                buttonCancel.BackColor = DarkTheme.Surface2;
                buttonCancel.FlatAppearance.BorderColor = DarkTheme.Border;

                labelInfo.ForeColor = DarkTheme.TextSecondary;

                // Inset every control so the frosted glass frame is visible
                // around and between the rows instead of a thin 6px sliver.
                const int mInset = 14;
                labelText1.Location = new Point(mInset, labelText1.Location.Y + 2);
                comboBoxMainList.Location = new Point(mInset, comboBoxMainList.Location.Y + 2);
                comboBoxMainList.Width = Math.Max(100, ClientSize.Width - mInset * 2);

                labelText2.Location = new Point(mInset, labelText2.Location.Y + 2);
                comboBoxRoomList.Location = new Point(mInset, comboBoxRoomList.Location.Y + 2);
                comboBoxRoomList.Width = Math.Max(100, ClientSize.Width - mInset * 2);

                labelInfo.Location = new Point(mInset, labelInfo.Location.Y + 3);

                //accent fill for the primary "Load With Objects" action
                buttonLoadComplete.UseVisualStyleBackColor = false;
                buttonLoadComplete.FlatStyle = FlatStyle.Flat;
                buttonLoadComplete.FlatAppearance.BorderSize = 0;
                buttonLoadComplete.BackColor = DarkTheme.Accent;
                buttonLoadComplete.ForeColor = Color.White;
                buttonLoadComplete.FlatAppearance.MouseOverBackColor = DarkTheme.AccentHover;
                buttonLoadComplete.FlatAppearance.MouseDownBackColor = DarkTheme.AccentPressed;

                //ghost card for the dropdown arrow
                buttonLoadCompleteArrow.FlatStyle = FlatStyle.Flat;
                buttonLoadCompleteArrow.BackColor = DarkTheme.Surface2;
                buttonLoadCompleteArrow.ForeColor = DarkTheme.Text;
                buttonLoadCompleteArrow.FlatAppearance.BorderColor = DarkTheme.Border;
            }

            // Bottom action row: [Load With Objects][▾][Load][Cancel]
            const int m = 14;
            const int sourceRowSpace = 38; // dedicated row height for the File source selector

            //grow the dialog so the selector gets its own clear band
            if (comboFileSource != null && ClientSize.Height < buttonLoad.Bottom + sourceRowSpace + m)
            {
                //the designer caps the window height (MaximumSize) - lift it
                //(0,0) means "no maximum"; a zero HEIGHT component would
                //collapse the whole window to its title bar!
                MaximumSize = new Size(0, 0);
                ClientSize = new Size(ClientSize.Width, buttonLoad.Bottom + sourceRowSpace + m);
            }

            int buttonY = buttonLoad.Location.Y + 2 + (comboFileSource != null ? sourceRowSpace : 0);
            buttonCancel.Location = new Point(ClientSize.Width - m - buttonCancel.Width, buttonY);
            buttonLoad.Location = new Point(buttonCancel.Left - 8 - buttonLoad.Width, buttonY);
            buttonLoadCompleteArrow.Size = new Size(28, buttonLoad.Height);
            buttonLoadCompleteArrow.Location = new Point(buttonLoad.Left - 8 - buttonLoadCompleteArrow.Width, buttonY);
            buttonLoadComplete.Height = buttonLoad.Height;
            buttonLoadComplete.Width = 150;
            buttonLoadComplete.Location = new Point(buttonLoadCompleteArrow.Left - 8 - buttonLoadComplete.Width, buttonY);

            //file-source selector gets its own row just above the action buttons;
            //the info line moves onto that same row, right of the selector,
            //so nothing overlaps anything below it
            if (comboFileSource != null && labelFileSource != null)
            {
                int selectorY = buttonY - comboFileSource.Height - 9;
                comboFileSource.Location = new Point(m, selectorY);
                labelFileSource.Location = new Point(m, selectorY + (comboFileSource.Height - labelFileSource.Height) / 2);

                labelInfo.Location = new Point(comboFileSource.Right + 12,
                                               selectorY + (comboFileSource.Height - labelInfo.Height) / 2);

                labelFileSource.BringToFront();
                comboFileSource.BringToFront();
            }
        }

        private void StartUpdateTranslation()
        {
            this.Text = Lang.GetText(eLang.SelectRoomForm);
            labelInfo.Text = Lang.GetText(eLang.labelInfo);
            labelText1.Text = Lang.GetText(eLang.labelSelectAList);
            labelText2.Text = Lang.GetText(eLang.labelSelectARoom);
            buttonLoad.Text = Lang.GetText(eLang.SelectRoomButtonLoad);
            buttonCancel.Text = Lang.GetText(eLang.SelectRoomButtonCancel);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
        }
    }
}
