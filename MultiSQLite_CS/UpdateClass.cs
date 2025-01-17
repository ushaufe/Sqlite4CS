﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SQLite;
using System.Threading;
using System.IO;
using static System.Net.Mime.MediaTypeNames;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using static MultiSQLite_CS.NodeDefinition;
using System.Runtime.InteropServices.ComTypes;

namespace MultiSQLite_CS
{
    static class UpdateClass
    {
        static private int countUpdateAppTicks = 0;
        public static Color colorUpdate = Color.LightBlue;
        static System.Timers.Timer tiAppUpdate = new System.Timers.Timer();
        static PromptCommands prompt;
        static int drawPointInterval = 10;        

        const String strDownloadPath_CS = "https://github.com/ushaufe/MultiSQLite_CS/raw/master/MultiSQLite_CS/bin/";
        const String strDownloadPath_CPP = "https://github.com/ushaufe/SQlite4CPP/raw/master/";
                                           

        static public bool UpdateCpp(PromptCommands prompt, String strAppDir, String strUpdatePath, bool bPrompt = true)
        {
            String strDownloadPath = strDownloadPath_CPP;
            FileVersionInfo versionInfo;
            String strVersion = "";
            try
            {
                File.Delete(strUpdatePath + "VersionTesterRelease.ne");
                File.Delete(strUpdatePath + "VersionTesterDebug.ne");
            }
            catch (Exception ex) { }
            try
            {
                versionInfo = FileVersionInfo.GetVersionInfo(strAppDir + "MultiSQLite_CPP.exe");
                strVersion = versionInfo.FileVersion;
            } catch (Exception ex) { }
            
            prompt.Out("", colorUpdate);
            prompt.Out("Cheking for new C++ versions...", colorUpdate);
            prompt.Out("    Current version is " + (strVersion.Length>0 ? strVersion : " Not available"), colorUpdate);
            prompt.Out("    Newest version is....", colorUpdate);

            String strVersionDebug = "", strVersionRelease = "";
            using (var client = new System.Net.WebClient())
            {
                try
                {
                    String strSourceDebug = "" + strDownloadPath_CPP + "Debug/" + "MultiSQLite_CPP.exe" + "";
                    String strDestDebug = "" + strUpdatePath + "VersionTesterDebug.ne" + "";
                    client.DownloadFile(strSourceDebug, strDestDebug);
                    var versionInfoDebug = FileVersionInfo.GetVersionInfo(strDestDebug);
                    strVersionDebug = versionInfoDebug.FileVersion;
                }
                catch (Exception ex)
                {
                }
                try
                {
                    String strSourceRelease = "" + strDownloadPath_CPP + "Release/" + "MultiSQLite_CPP.exe" + "";
                    String strDestRelease = "" + strUpdatePath + "VersionTesterRelease.ne" + "";
                    client.DownloadFile(strSourceRelease, strDestRelease);
                    var versionInfoRelease = FileVersionInfo.GetVersionInfo(strDestRelease);
                    strVersionRelease = versionInfoRelease.FileVersion;
                }
                catch (Exception ex)
                {
                }
                String strBranch = "";
                String strNewVersion = "";
                if (ConnectionClass.getDBVersionNumber(strVersionDebug) > ConnectionClass.getDBVersionNumber(strVersionRelease))
                {
                    strNewVersion = strVersionDebug;
                    strBranch = "Debug";
                }
                else
                {
                    strBranch = "Release";
                    strNewVersion = strVersionRelease;
                }
                if (ConnectionClass.getDBVersionNumber(strVersion) >= ConnectionClass.getDBVersionNumber(strNewVersion))
                    if (ConnectionClass.getDBVersionNumber(strVersion) >= ConnectionClass.getDBVersionNumber(strNewVersion))
                    {
                        prompt.Out("", colorUpdate);
                        prompt.Out("This is the newest version. No upate is currently available.", colorUpdate);
                        prompt.Out("", colorUpdate);                        
                        return false;
                    }
                prompt.Out("    Updating " + strVersion + " -> " + strNewVersion + "...", colorUpdate);
                prompt.Out("", colorUpdate);
                strDownloadPath += strBranch + "/";
                
                List<String> downloadFiles = new List<String>();
                downloadFiles.Add( "MultiSQLite_CPP.exe" );

                try
                {
                    foreach (String strDownloadFile in downloadFiles)
                    {
                        if (strDownloadFile.Contains(" "))
                            continue;
                        prompt.Out("    Downloading " + strDownloadFile, colorUpdate);
                        String strSource = "" + strDownloadPath + strDownloadFile + "";
                        String strDownFile = strDownloadFile;
                        if (strDownFile.Contains("/"))
                            strDownFile = strDownFile.Remove(0, strDownFile.IndexOf("/") + 1);
                        String strDest = "" + strUpdatePath + strDownFile + "";

                        client.DownloadFile(strSource, strDest);
                    }
                }
                catch (Exception ex)
                {
                    prompt.Out("", Color.Red);
                    prompt.Out("Error During Update.", Color.Red);
                    prompt.Out("Error Message: " + ex.Message, Color.Red);
                    if (bPrompt)
                        prompt.Prompt();
                    return false;
                }

                prompt.Out("", colorUpdate);
                prompt.Out("Download completed, updating files....", colorUpdate);

                String[] strDir = Directory.GetFiles(strAppDir);
                String[] strUpdateFiles = Directory.GetFiles(strUpdatePath);
                foreach (String strFile in strUpdateFiles)
                    try
                    {
                        String strOriginal = strAppDir + Path.GetFileName(strFile);
                        if (File.Exists(strOriginal))
                        {
                            String strTempFile = strOriginal + "_temp_" + DateTime.Now.Ticks.ToString();
                            File.Move(strOriginal, strTempFile);
                        }
                        prompt.Out("Updating file " + strFile, colorUpdate);
                        File.Move(strFile, strOriginal);
                    }
                    catch (Exception ex)
                    {
                        prompt.Out("", Color.Red);
                        prompt.Out("Error: Unknown error during update", Color.Red);
                        if (bPrompt)
                            prompt.Prompt();
                        return false;
                    }
            }
            return true;
        }
        static public bool UpdateApp(PromptCommands prompt, String strAppDir, String strAppData, bool bPrompt = true)
        {
            UpdateClass.prompt = prompt;
            String strDownloadPath = strDownloadPath_CS;
            var versionInfo = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            String strVersion = versionInfo.FileVersion;
            
            prompt.Out("", colorUpdate);
            prompt.Out("Cheking for updates...", colorUpdate);
            prompt.Out("", colorUpdate);

            
            //System.Diagnostics.Process.Start("https://raw.githubusercontent.com/ushaufe/MultiSQLite_CS/master/Doc/Haufe_MultiSQLite_CS_Manual.pdf");
         
            String strUpdatePath = strAppData + "Update";
            try { System.IO.Directory.Delete(strUpdatePath, true); } catch (Exception ex) { }

            if (!System.IO.Directory.Exists(strUpdatePath))
                System.IO.Directory.CreateDirectory(strUpdatePath);
            if (strUpdatePath[strUpdatePath.Length - 1] != '\\')
                strUpdatePath += "\\";

            try
            {
                foreach (string fileDelete in Directory.GetFiles(strAppDir))
                    if (fileDelete.Contains("_temp_"))
                        File.Delete(fileDelete);
            }
            catch (Exception ex) { }            
            try
            {
                File.Delete(strUpdatePath + "VersionTesterRelease.ne");
                File.Delete(strUpdatePath + "VersionTesterDebug.ne");
            }
            catch (Exception ex) { }

            String strAppFile = Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string platform = IntPtr.Size == 4 ? "x86" : "x64";
            List<String> downloadFiles = new List<String>();
            
            downloadFiles.Add(platform + "/SQLite.Interop.dll");            
            downloadFiles.Add("EnvDTE.dll");
            downloadFiles.Add("Microsoft.VisualStudio.OLE.Interop.dll");
            downloadFiles.Add("Microsoft.VisualStudio.Shell.Interop.dll");
            downloadFiles.Add("Microsoft.VisualStudio.TextManager.Interop.dll");
            downloadFiles.Add("MultiSQLite_CS.exe");
            downloadFiles.Add("MultiSQLite_CS.exe.config");
            downloadFiles.Add("MultiSQLite_CS.pdb");
            downloadFiles.Add("SQLite.Designer.dll");
            downloadFiles.Add("SQLite.Designer.pdb");
            downloadFiles.Add("SQLite.Designer.xml");
            downloadFiles.Add("SumatraPDF - settings.txt");
            downloadFiles.Add("System.Data.SQLite.dll");
            downloadFiles.Add("System.Data.SQLite.xml");
            downloadFiles.Add("stdole.dll");

            using (var client = new System.Net.WebClient())
            {
                String strVersionDebug = "", strVersionRelease = "";
                prompt.Out("    Newest version is....", colorUpdate);

                try
                {
                    String strSourceDebug = "" + strDownloadPath + "Debug/" + strAppFile + "";
                    String strDestDebug = "" + strUpdatePath + "VersionTesterDebug.ne" + "";
                    client.DownloadFile(strSourceDebug, strDestDebug);
                    var versionInfoDebug = FileVersionInfo.GetVersionInfo(strDestDebug);
                    strVersionDebug = versionInfoDebug.FileVersion;
                }
                catch (Exception ex)
                {
                }
                try
                {
                    String strSourceRelease = "" + strDownloadPath + "Release/" + strAppFile + "";
                    String strDestRelease = "" + strUpdatePath + "VersionTesterRelease.ne" + "";
                    client.DownloadFile(strSourceRelease, strDestRelease);
                    var versionInfoRelease = FileVersionInfo.GetVersionInfo(strDestRelease);
                    strVersionRelease = versionInfoRelease.FileVersion;
                }
                catch (Exception ex)
                {
                }
                String strBranch = "";
                String strNewVersion = "";
                if (ConnectionClass.getDBVersionNumber(strVersionDebug) > ConnectionClass.getDBVersionNumber(strVersionRelease))
                {
                    strNewVersion = strVersionDebug;
                    strBranch = "Debug";
                }
                else
                {
                    strBranch = "Release";
                    strNewVersion = strVersionRelease;
                }
                if (ConnectionClass.getDBVersionNumber(strVersion) >= ConnectionClass.getDBVersionNumber(strNewVersion))
                {
                    prompt.Out("", colorUpdate);
                    prompt.Out("This is the newest version. No upate is currently available.", colorUpdate);
                    prompt.Out("", colorUpdate);
                    //UpdateCpp(prompt, strAppDir, strUpdatePath, bPrompt);
                    if (bPrompt)
                        prompt.Prompt();
                    return false;
                }
                prompt.Out("    Updating " + strVersion + " -> " + strNewVersion + "...", colorUpdate);
                prompt.Out("", colorUpdate);
                strDownloadPath+= strBranch + "/";
                try
                {
                    foreach (String strDownloadFile in downloadFiles)
                    {
                        if (strDownloadFile.Contains(" "))
                            continue;
                        prompt.Out("    Downloading " + strDownloadFile, colorUpdate);
                        String strSource = "" + strDownloadPath + strDownloadFile + "";
                        String strDownFile = strDownloadFile;
                        if (strDownFile.Contains("/"))
                            strDownFile = strDownFile.Remove(0, strDownFile.IndexOf("/") + 1);
                        String strDest = "" + strUpdatePath + strDownFile + "";                           
                        
                        client.DownloadFile(strSource, strDest);
                    }
                }
                catch (Exception ex)
                {
                    prompt.Out("", Color.Red);
                    prompt.Out("Error During Update.", Color.Red);
                    prompt.Out("Error Message: " + ex.Message, Color.Red);
                    if (bPrompt)
                        prompt.Prompt();
                    return false;
                }

                prompt.Out("", colorUpdate);
                prompt.Out("Download completed, updating files....", colorUpdate);

                String[] strDir = Directory.GetFiles(strAppDir);
                String[] strUpdateFiles = Directory.GetFiles(strUpdatePath);
                foreach (String strFile in strUpdateFiles)
                    try
                    {
                        String strOriginal = strAppDir + Path.GetFileName(strFile);
                        if (File.Exists(strOriginal))
                        {
                            String strTempFile = strOriginal + "_temp_" + DateTime.Now.Ticks.ToString();
                            File.Move(strOriginal, strTempFile);
                        }
                        prompt.Out("Updating file " + strFile, colorUpdate);
                        File.Move(strFile, strOriginal);
                    }
                    catch (Exception ex)
                    {
                        prompt.Out("", Color.Red);
                        prompt.Out("Error: Unknown error during update", Color.Red);
                        if (bPrompt)
                            prompt.Prompt();
                        return false;
                    }
            }
            prompt.Out("", colorUpdate);
            prompt.Out("Update complete.", colorUpdate);
            prompt.Out("Starting new version of MultiSQLite_CS for C#", colorUpdate);
            countUpdateAppTicks = 0;
            tiAppUpdate.Elapsed += new System.Timers.ElapsedEventHandler(OnTimedEvent);
            tiAppUpdate.Interval = 1000;
            tiAppUpdate.Enabled = true;
            tiAppUpdate.Enabled = true;
            if (bPrompt)
                prompt.Prompt();
            //UpdateCpp(prompt, strAppDir, strUpdatePath, bPrompt);
            return true;
        }

        private static void OnTimedEvent(object source, System.Timers.ElapsedEventArgs e)
        {
            prompt.Out(".", colorUpdate, false);
            countUpdateAppTicks++;
            if (countUpdateAppTicks == drawPointInterval)
            {
                prompt.Out("Close this instance of MultiSQLite_CS for C#", colorUpdate);
                System.Diagnostics.Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            if (countUpdateAppTicks >= drawPointInterval * 2)
            {
                countUpdateAppTicks = 0;
                tiAppUpdate.Enabled = false;
                System.Windows.Forms.Application.Exit();
            }
        }
    }
}
