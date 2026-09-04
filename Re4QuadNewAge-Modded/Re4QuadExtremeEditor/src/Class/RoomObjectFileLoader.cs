using System;
using System.Collections.Generic;
using System.IO;
using Re4QuadExtremeEditor.src.Class;
using Re4QuadExtremeEditor.src.Class.Enums;
using Re4QuadExtremeEditor.src.Class.Files;
using Re4QuadExtremeEditor.src.JSON;

namespace Re4QuadExtremeEditor.src
{
    /// <summary>
    /// Loads every room object file (ETS/ITA/AEV/DSE/AVL/FSE/SAR/EAR/EMI/ESE/LIT)
    /// found inside a room directory. Shared by the Select Room dialog and
    /// by the .quad project loader.
    /// </summary>
    public static class RoomObjectFileLoader
    {
        /// <param name="shouldLoadExtension">
        /// Receives extensions like ".ITA" and returns false to skip that type.
        /// </param>
        public static void LoadAllFilesForRoom(string roomPath, RoomModel roomModel, Func<string, bool> shouldLoadExtension)
        {
            bool isUhd;
            bool isPs4Ns;
            switch (roomModel.Type)
            {
                case RoomModel.EType.UHD:
                case RoomModel.EType.R100UHD:
                    isUhd = true; isPs4Ns = false; break;
                case RoomModel.EType.PS4NS:
                case RoomModel.EType.R100PS4NS:
                    isUhd = true; isPs4Ns = true; break;
                default:
                    isUhd = false; isPs4Ns = false; break; //2007 / PS2
            }

            var fileLoadActions = new Dictionary<string, Action<string, FileStream, FileInfo>>{
                /*{ ".ESL", (path, file, info) => {
                    esl is not located inside stage folder, so later we can do some specific logic here.
                    maybe we can set a list of esl enemies based on room and them auto show defined room to desired room
                },*/

                { ".ETS", (path, file, info) => { if (isUhd) FileManager.LoadFileETS_UHD(file, info); else FileManager.LoadFileETS_2007_PS2(file, info); Globals.FilePathETS = path; } },
                { ".CAM", (path, file, info) => {
                    // camera keyframes + trigger zones (UHD layout only)
                    FileManager.LoadFileCAM(file, isUhd ? IsRe4Version.UHD : IsRe4Version.V2007PS2);
                    Globals.FilePathCAM = path;
                } },
                { ".RTP", (path, file, info) => { FileManager.LoadFileRTP(file); Globals.FilePathRTP = path; } },
                { ".ITA", (path, file, info) => {
                    if (isPs4Ns) FileManager.LoadFileITA_PS4_NS(file, info);
                    else if (isUhd) FileManager.LoadFileITA_UHD(file, info);
                    else FileManager.LoadFileITA_2007_PS2(file, info);
                    Globals.FilePathITA = path;
                } },
                { ".AEV", (path, file, info) => {
                    if (isPs4Ns) FileManager.LoadFileAEV_PS4_NS(file, info);
                    else if (isUhd) FileManager.LoadFileAEV_UHD(file, info);
                    else FileManager.LoadFileAEV_2007_PS2(file, info);
                    Globals.FilePathAEV = path;
                } },
                { ".DSE", (path, file, info) => { FileManager.LoadFileDSE(file, info); Globals.FilePathDSE = path; } },
                { ".AVL", (path, file, info) => { FileManager.LoadFileAVL(file, info); Globals.FilePathAVL = path; } },
                { ".FSE", (path, file, info) => { FileManager.LoadFileFSE(file, info); Globals.FilePathFSE = path; } },
                { ".SAR", (path, file, info) => { FileManager.LoadFileSAR(file, info); Globals.FilePathSAR = path; } },
                { ".EAR", (path, file, info) => { FileManager.LoadFileEAR(file, info); Globals.FilePathEAR = path; } },
                { ".EMI", (path, file, info) => { if (isUhd) FileManager.LoadFileEMI_UHD(file, info); else FileManager.LoadFileEMI_2007_PS2(file, info); Globals.FilePathEMI = path; } },
                { ".ESE", (path, file, info) => { if (isUhd) FileManager.LoadFileESE_UHD(file, info); else FileManager.LoadFileESE_2007_PS2(file, info); Globals.FilePathESE = path; } },
                { ".LIT", (path, file, info) => { if (isUhd) FileManager.LoadFileLIT_UHD(file, info); else FileManager.LoadFileLIT_2007_PS2(file, info); Globals.FilePathLIT = path; } },
                { ".SMX", (path, file, info) => { FileManager.LoadFileSMX(file, info); Globals.FilePathSMX = path; } }
            };

            // descarta a AVL da sala anterior para nao manter vinculos obsoletos;
            FileManager.ClearAVL();
            Globals.FilePathAVL = null;

            // same for CAM/RTP so no stale routes/cameras linger from the previous room
            // FIX: don't clear RTP/CAM during Force Reload - it discards unsaved Ctrl+D nodes (20->18) and causes crash when reopening RTP
            if (Re4QuadExtremeEditor.src.Utils.IsForceReloading)
            {
                try { System.IO.File.AppendAllText(@"C:\Temp\RTP_Debug.log", $"{DateTime.Now:HH:mm:ss} RoomObjectFileLoader: SKIPPED ClearRTP/CAM during Force Reload (preserve N={DataBase.FileRTP?.Nodes.Count})\n"); } catch {}
            }
            else
            {
                FileManager.ClearCAM();
                Globals.FilePathCAM = null;
                FileManager.ClearRTP();
                Globals.FilePathRTP = null;
            }

            foreach (var pair in fileLoadActions)
            {
                string extension = pair.Key;

                if (shouldLoadExtension != null && !shouldLoadExtension(extension))
                {
                    continue;
                }

                string[] files = Directory.GetFiles(roomPath, "*" + extension, SearchOption.TopDirectoryOnly);

                if (files.Length > 0)
                {
                    string fileToLoad = files[0];
                    EditorConsole.Log($"Found {extension.Substring(1)} file: {Path.GetFileName(fileToLoad)}. Importing...");
                    LoadFileAction(fileToLoad, pair.Value);
                }
                else
                {
                    EditorConsole.Warning($"No {extension.Substring(1)} file found in room directory. Skipping...");
                }
            }

        }

        /// <summary>
        /// Generic wrapper to safely open a file and call the appropriate load action.
        /// </summary>
        private static void LoadFileAction(string path, Action<string, FileStream, FileInfo> loadAction)
        {
            try
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Exists && fileInfo.Length > 0)
                {
                    using (FileStream fileStream = fileInfo.OpenRead())
                    {
                        loadAction(path, fileStream, fileInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                EditorConsole.Error($"Failed to load file {Path.GetFileName(path)}: {ex.Message}");
            }
        }
    }
}
