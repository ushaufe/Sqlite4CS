﻿using System;
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




// This is a C# project to demonstrage how well SQLite can handle Multithreading
// The documentation for this project and the accompanying C++ projects can be found here:
// (the location may be moved by the webmaster of the wiki)
// https://mywiki.grp.haufemg.com/pages/viewpage.action?pageId=156088657


// Two different approaches should be shown:
// Pooling: Using different connections for different threads and
// Using the same connection with multiple threads simultaneously

namespace MultiSQLite_CS
{

    public partial class frmMain : Form
    {
        

        public DBThreads threads;


        // These threads are used for simultaneous writing...
        List<Thread> listThreads = new List<Thread>();

        // This thread is used for reading
        // ( Constantly polling the data in 
        //   the database and refreshing them )
        // Can be run simultanously to writing threads
        ViewThread viewThread;
        public static String strVersion = "";


        

               

#if DEBUG
    public const String DEBUG_LEVEL = "Debug";        
#else
    public const String DEBUG_LEVEL = "Release";
#endif

        
        private int appID = -1;
        private string appName = "";
        String strAppData;

        static public String getVersion() { return strVersion; }

        private delegate void AddListDelegate(List<String> list);

        private ConnectionClass connection;

        public PromptCommands prompt;

        String strAppDir;

        String APPNAME_MULTISQLITE_CS = "MultiSQLite_CS.exe";
        String APPNAME_MULTISQLITE_CPP = "MultiSQLite_CPP.exe";


        public void AddListSafe(List<String> list)
        {
            if (rePrompt.InvokeRequired)
            {
                var d = new AddListDelegate(AddListSafe);
                rePrompt.Invoke(d, new object[] { list });                
            }
            else
            {
                rePrompt.Clear();
                foreach (string str in list)
                {
                   prompt.Out(str);
                }

            }
        }

 
        
        public frmMain()
        {
            InitializeComponent();

            strAppDir = System.Reflection.Assembly.GetExecutingAssembly().Location;
            strAppDir = Path.GetDirectoryName(strAppDir);
            if (strAppDir.Length == 0)
                return;
            if (strAppDir[strAppDir.Length - 1] != '\\')
                strAppDir += "\\";

            strAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            if (strAppData.Length == 0)
                strAppData = "C:";
            if (strAppData[strAppData.Length - 1] != '\\')
                strAppData += '\\';
            strAppData += "Haufe\\MultiSQLite\\";
            if (!System.IO.Directory.Exists(strAppData))
                System.IO.Directory.CreateDirectory(strAppData);

            this.ActiveControl = rePrompt;

            appName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;

            prompt = new PromptCommands(ref rePrompt, ref connection);
            connection = new ConnectionClass(this, prompt, appName, strAppData);

            var versionInfo = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            strVersion = versionInfo.FileVersion;
            lblVersion.Text = "Version: " + strVersion + " (" + DEBUG_LEVEL + ")";
            prompt.Out(appName + " version: " + strVersion);

            tiDelayGUILoad.Enabled = true;
        }




        private void Form1_Load(object sender, EventArgs e)
        {
            frmMain frm1 = this;
            viewThread = new ViewThread(frm1, appID, connection.getDatabaseFile());
        }

        
        private void btnShowContent_Click(object sender, EventArgs e)
        {

        }

        private void btnStopThreads_Click(object sender, EventArgs e)
        {
            if (threads == null)
                return;
            tcTabs.SelectedTab = tsApps;
            threads.running = false;
            btnStartThreads.Enabled = !threads.running;
            btnStopThreads.Enabled = threads.running;
            mnuStartThread.Enabled = btnStartThreads.Enabled;
            mnuStopThread.Enabled = btnStopThreads.Enabled;
            lblNumThreads.Enabled = !threads.running;
            numThreads.Enabled = !threads.running;
        }





        String unprompt(String str)
        {
            str.Trim();
            if (str.Length < 3)
                return str;
            if (str.ToUpper().Substring(0, 3).Equals("$:>"))
                str = str.Substring(3, str.Length - 3).Trim();
            return str;
        }

        private void btnShowStatus_Click(object sender, EventArgs e)
        {
            tcTabs.SelectedIndex = 0;
            rePrompt.Clear();

            String strRunning = "NO";
            if (threads.running)
                strRunning = "YES";

            rePrompt.Clear();
            prompt.Out("Running: " + strRunning);

            SQLiteCommand cmd = null;
            cmd = new SQLiteCommand("Select count(text) as cnt from multisqlite_entries", connection.get());
            SQLiteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string str = "Count=" + reader["cnt"];

                prompt.Out(str);
            }
        }

        void updateNodeRecursive(TreeNode node)
        {
            updateNode(node, NodeDefinition.NodeAction.LiveView);
            foreach (TreeNode subNode in node.Nodes)
            {
                updateNodeRecursive(subNode);
            }
        }

        private void btnViewThread_Click(object sender, EventArgs e)
        {
            tiLiveUpdate.Enabled = !tiLiveUpdate.Enabled;

            if (tiLiveUpdate.Enabled)
                btnLiveUpdate.BackColor = Color.LightBlue;
            else
            {
                btnLiveUpdate.BackColor = SystemColors.ButtonFace;
                btnLiveUpdate.UseVisualStyleBackColor = true;
            }

            /*
            tcTabs.SelectedIndex = 0;
            Thread vt = new Thread(viewThread.view);
            viewThread.running = !viewThread.running;
            return;
            btnLiveUpdate.Enabled = viewThread.running;
            vt.Start();
            */
        }

        private void tiUpdateApps_Tick(object sender, EventArgs e)
        {
            lblVersion.ForeColor = Color.Red;
            if (connection.get() == null)
                return;
            if (connection.get().State != ConnectionState.Open)
                return;
            ((System.Windows.Forms.Timer)sender).Enabled = false;
            try
            {

                SQLiteCommand cmd = null;
                cmd = new SQLiteCommand("update multisqlite_apps set tsLastPoll = CURRENT_TIMESTAMP where id=" + appID, connection.get());
                cmd.ExecuteNonQuery();
                cmd = new SQLiteCommand("update multisqlite_apps set isActive=0 where strftime('%s', 'now') -strftime('%s', tsLastPoll)  > 30 ", connection.get());
                cmd.ExecuteNonQuery();
                tiConnectionQuery.Enabled = true;
            }
            finally
            {
                ((System.Windows.Forms.Timer)sender).Enabled = true;
            }
        }

        private void tiPollApps_Tick(object sender, EventArgs e)
        {
            ((System.Windows.Forms.Timer)sender).Enabled = false;

            try            
            {
                if (tcTabs.SelectedTab == tsApps)
                {
                    List<TreeNode> activeNodes = new List<TreeNode>();
                    bool bFound = false;


                    TreeNode nodeAppHeadline = NodeDefinition.Add(NodeDefinition.NodeType.ntAppHeadline, "Apps", true, treeApps.Nodes, ref activeNodes, Color.Black, "", "");
                    nodeAppHeadline.Expand();

                    NodeDefinition.Add(NodeDefinition.NodeType.ntTotalStatusHeadline, "Total Statistics:", true, treeApps.Nodes, ref activeNodes, Color.SteelBlue, "", "");
                    NodeDefinition.Add(NodeDefinition.NodeType.ntStatusHeadline, "Status:", true, treeApps.Nodes, ref activeNodes, Color.Coral, "", "");
                    NodeDefinition.Add(NodeDefinition.NodeType.ntTablesHeadline, "Tables: ", true, treeApps.Nodes, ref activeNodes, Color.BlueViolet, "");

                    treeApps_BeforeExpand_1(sender, new TreeViewCancelEventArgs(nodeAppHeadline, false, new TreeViewAction()));
                }
            }
            finally
            {
                ((System.Windows.Forms.Timer)sender).Enabled = true;
            }            
        }

        private void treeApps_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {

        }



        private void buttonStartThreads_Click(object sender, EventArgs e)
        {
            listThreads.Clear();
            if (connection.get() == null)
                return;
            if (threads == null)
                return;
            if (connection.get().State != ConnectionState.Open)
                return;


            tcTabs.SelectedTab = tsApps;

            for (int i = 0; i < numThreads.Value; i++)
            {
                listThreads.Add(new Thread(threads.insert_thread_function));
            }
            threads.running = true;

            foreach (Thread thread in listThreads)
            {
                thread.Start();
            }

            btnStartThreads.Enabled = !threads.running;
            lblNumThreads.Enabled = !threads.running;
            numThreads.Enabled = !threads.running;
            mnuStartThread.Enabled = btnStartThreads.Enabled;
            btnStopThreads.Enabled = threads.running;
            mnuStopThread.Enabled = btnStopThreads.Enabled;
        }

        void updateNode(TreeNode node, NodeDefinition.NodeAction nodeAction)
        {
            if (connection.get() == null)
                return;
            if (connection.get().State != ConnectionState.Open)
                return;

            NodeDefinition nd = (NodeDefinition)node.Tag;
            if (nd == null)
                return;
            if ((nodeAction == NodeDefinition.NodeAction.LiveView) && (!NodeDefinition.isLiveable(nd.nodeType)))
                return;
            if (nodeAction == NodeDefinition.NodeAction.LiveView)
            {
                TreeNode currentNode = node;
                while (currentNode.Parent!=null)
                {
                    currentNode = currentNode.Parent;
                    if (currentNode.IsExpanded == false)
                        return;
                }
            }

            if (nd.nodeType == NodeDefinition.NodeType.ntAppHeadline)
            {
                List<TreeNode> activeNodes = new List<TreeNode>();
                TreeNode nodeAppActiveHeadline = NodeDefinition.Add(NodeDefinition.NodeType.ntAppActiveHeadline, "Active Apps", true, node.Nodes, ref activeNodes, Color.Green);                
                TreeNode nodeAppInactiveHeadline = NodeDefinition.Add(NodeDefinition.NodeType.ntAppInactiveHeadline, "Inactive Apps", true, node.Nodes, ref activeNodes, Color.Red);
                updateNode(nodeAppActiveHeadline, NodeDefinition.NodeAction.Update);                    
                nodeAppActiveHeadline.Expand();
            }
            if ( (nd.nodeType == NodeDefinition.NodeType.ntAppActiveHeadline) || ((nd.nodeType == NodeDefinition.NodeType.ntAppInactiveHeadline)) )
            {
                bool bFound = false;
                List<TreeNode> activeNodes = new List<TreeNode>();
                SQLiteCommand cmd = null;
                if (nd.nodeType == NodeDefinition.NodeType.ntAppActiveHeadline)
                    cmd = new SQLiteCommand("Select distinct multisqlite_apps.id as AppID,multisqlite_apps.name as AppName from multisqlite_apps where isActive=1 ", connection.get());
                else if (nd.nodeType == NodeDefinition.NodeType.ntAppInactiveHeadline)
                    cmd = new SQLiteCommand("Select distinct multisqlite_apps.id as AppID,multisqlite_apps.name as AppName from multisqlite_apps where isActive=0 ", connection.get());
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string strID = (string)reader["AppID"].ToString();
                    string strName = (string)reader["AppName"].ToString();

                    NodeDefinition.Add(NodeDefinition.NodeType.ntAppActive, strName + " <ID: " + strID + ">", true, node.Nodes, ref activeNodes, nd.parentColor, strID);
                }

                NodeDefinition.removeInactives(node.Nodes, ref activeNodes, NodeDefinition.NodeType.ntAppActive);
            }

            else if (nd.nodeType == NodeDefinition.NodeType.ntAppActive)
            {
                node.Nodes.Clear();
                List<TreeNode> activeNodes = null;
                NodeDefinition.Add(NodeDefinition.NodeType.ntCountThreadHeadline, "Threads Count:", true, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID);
                NodeDefinition.Add(NodeDefinition.NodeType.ntCountAppActiveEntriesHeadline, "Entries Count:", true, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID);
                NodeDefinition.Add(NodeDefinition.NodeType.ntThreadHeadline, "Threads:", true, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID);
                NodeDefinition.Add(NodeDefinition.NodeType.ntAppActiveThroughputHeadline, "Throughput:", true, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID);
                //NodeDefinition.Add(NodeDefinition.NodeType.ntTablesHeadline, "Tables: ", true, node.Nodes, ref activeNodes, Color.BlueViolet, nd.strAppID);
            }

            else if (nd.nodeType == NodeType.ntTotalStatusHeadline)
            {
                node.Nodes.Clear();
                List<TreeNode> activeNodes = null;

                NodeDefinition.Add(NodeDefinition.NodeType.ntCountTotalEntriesHeadline, "Total Entries Count:", true, node.Nodes, ref activeNodes, nd.parentColor, "", "");
                NodeDefinition.Add(NodeDefinition.NodeType.ntCountTotalThreadsActiveHeadline, "Total Active Threads Count:", true, node.Nodes, ref activeNodes, nd.parentColor, "", "");
                NodeDefinition.Add(NodeDefinition.NodeType.ntTotalThroughputHeadline, "Total Throughput:", true, node.Nodes, ref activeNodes, nd.parentColor);
            }
            
            else if (nd.nodeType == NodeDefinition.NodeType.ntTablesHeadline)
            {
                node.Nodes.Clear();
                List<TreeNode> activeNodes = null;
        
                SQLiteCommand cmd = null;
                cmd = new SQLiteCommand("SELECT name FROM sqlite_master where name not like 'multisqlite_%' and name not like 'sqlite_sequence'", connection.get());
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string strTableName = (string)reader["name"].ToString();
                    
                    NodeDefinition.Add(NodeDefinition.NodeType.ntTableHeadline, strTableName, true, node.Nodes, ref activeNodes, nd.parentColor, "", "", strTableName);
                }
            }

            else if (nd.nodeType == NodeDefinition.NodeType.ntTableFieldHeadline)
            {
                node.Nodes.Clear();
                List<TreeNode> activeNodes = null;

                SQLiteCommand cmd = null;
                cmd = new SQLiteCommand("select * from '" + nd.strID + "'", connection.get());
                try
                {
                    List<List<string>> table = new List<List<string>>();
                    List<string> columns = new List<string>();
                    SQLiteDataReader reader = cmd.ExecuteReader();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string strColumn = reader.GetName(i);
                        columns.Add(strColumn);
                    }
                    table.Add(columns.GetRange(0, columns.Count));
                    while ((reader != null) && (reader.Read()))
                    {
                        columns.Clear();
                        String strLine = "";
                        foreach (String strCol in table[0])
                        {
                            string str = (string)reader[strCol].ToString();
                            columns.Add(str);
                            strLine = strLine + strCol + ":" + " " + str + "; ";
                        }                        
                        NodeDefinition.Add(NodeDefinition.NodeType.ntTableFieldEntryLine, strLine, true, node.Nodes, ref activeNodes, nd.parentColor, "", "", nd.strID + "/" + strLine);
                        //table.Add(columns.GetRange(0, columns.Count));
                    }
                    //printTable(table);
                    //prompt.Prompt();
                }
                catch(Exception e)
                {

                }
            }

            else if (nd.nodeType == NodeDefinition.NodeType.ntTableHeadline)
            {
                node.Nodes.Clear();
                List<TreeNode> activeNodes = null;
                NodeDefinition.Add(NodeDefinition.NodeType.ntTableFieldHeadline, "Fields", true, node.Nodes, ref activeNodes, nd.parentColor, "", "", nd.strID);
            }

            else if (nd.nodeType == NodeDefinition.NodeType.ntCountThreadHeadline)
            {
                //node.Nodes.Clear();
                List<TreeNode> activeNodes = null;
                SQLiteCommand cmd = null;
                cmd = new SQLiteCommand("select count(distinct threadid) as CNT from multisqlite_entries where appID=" + nd.strAppID, connection.get());
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string strCNT = (string)reader["CNT"].ToString();
                    NodeDefinition.Add(NodeDefinition.NodeType.ntTotalThreadCount, strCNT, true, node.Nodes, ref activeNodes, Color.Black, nd.strAppID);
                }
            }

            else if (nd.nodeType == NodeDefinition.NodeType.ntThreadHeadline)
            {
                node.Nodes.Clear();
                List<TreeNode> activeNodes = null;

                NodeDefinition ndThread = new NodeDefinition();

                TreeNode treeNodeThread = NodeDefinition.Add(NodeDefinition.NodeType.ntThread, "GUI-Thread", true, node.Nodes, ref activeNodes, Color.Black, nd.strAppID, "0");

                SQLiteCommand cmd = null;
                cmd = new SQLiteCommand("select distinct threadid, isActive from multisqlite_threads where appID=" + nd.strAppID, connection.get());
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string strThreadID = (string)reader["ThreadID"].ToString();
                    string strIsActive = (string)reader["isActive"].ToString();
                    Color color = Color.Black;
                    if (strIsActive.Equals("0"))
                    {
                        color = Color.Red;
                    }
                    else
                    {
                        color = Color.Green;
                    }

                    NodeDefinition.Add(NodeDefinition.NodeType.ntThread, "Thread #" + strThreadID, true, node.Nodes, ref activeNodes, color, nd.strAppID, strThreadID);
                }
            }

            else if (nd.nodeType == NodeDefinition.NodeType.ntThread)
            {
                node.Nodes.Clear();
                List<TreeNode> activeNodes = null;
                NodeDefinition.Add(NodeDefinition.NodeType.ntEntryHeadline, "Entries:", true, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID);
                NodeDefinition.Add(NodeDefinition.NodeType.ntCountThreadEntriesHeadline, "Count Thread Entries:", true, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID);
                NodeDefinition.Add(NodeDefinition.NodeType.ntThreadThroughputHeadline, "Throughput:", true, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID);
            }

            else if (nd.nodeType == NodeDefinition.NodeType.ntEntryHeadline)
            {
                node.Nodes.Clear();
                List<TreeNode> activeNodes = null;
                SQLiteCommand cmd = null;
                cmd = new SQLiteCommand("select id,text,appid,threadid,tsCreated from multisqlite_entries where appID=" + nd.strAppID + " and threadID=" + nd.strThreadID, connection.get());
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string strID = (string)reader["ID"].ToString();
                    string strText = (string)reader["Text"].ToString();
                    string strThreadID = (string)reader["ThreadID"].ToString();
                    string strAppID = (string)reader["AppID"].ToString();
                    string strTSCreated = (string)reader["tsCreated"].ToString();
                    string str = "ID: " + strID + ", AppID: " + strAppID + ", ThreadID: " + strThreadID + ", Created: " + strTSCreated + " " + strText;
                    NodeDefinition.Add(NodeDefinition.NodeType.ntEntry, str, true, node.Nodes, ref activeNodes, nd.parentColor, strAppID, strThreadID);
                }
            }

            else if (nd.nodeType == NodeDefinition.NodeType.ntCountAppActiveEntriesHeadline)
            {
                //node.Nodes.Clear();
                List<TreeNode> activeNodes = null;
                SQLiteCommand cmd = null;
                cmd = new SQLiteCommand("select count(id) as CNT from multisqlite_entries where appID=" + nd.strAppID, connection.get());
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string strCNT = (string)reader["CNT"].ToString();
                    NodeDefinition.Add(NodeDefinition.NodeType.ntCountAppActiveEntries, strCNT, true, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID);
                }
            }

            else if (nd.nodeType == NodeDefinition.NodeType.ntCountThreadEntriesHeadline)
            {
                //node.Nodes.Clear();
                List<TreeNode> activeNodes = null;
                SQLiteCommand cmd = null;
                cmd = new SQLiteCommand("select count(id) as CNT from multisqlite_entries where appID=" + nd.strAppID + " and threadID=" + nd.strThreadID, connection.get());
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string strCNT = (string)reader["CNT"].ToString();
                    NodeDefinition.Add(NodeDefinition.NodeType.ntCountThreadEntries, strCNT, true, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID);
                }
            }

            else if (nd.nodeType == NodeDefinition.NodeType.ntCountTotalEntriesHeadline)
            {
                //node.Nodes.Clear();
                List<TreeNode> activeNodes = null;
                SQLiteCommand cmd = null;
                cmd = new SQLiteCommand("select count(id) as CNT from multisqlite_entries", connection.get());
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string strCNT = (string)reader["CNT"].ToString();
                    NodeDefinition.Add(NodeDefinition.NodeType.ntCountTotalEntries, strCNT, true, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID);
                }
            }


            else if (nd.nodeType == NodeDefinition.NodeType.ntCountTotalThreadsActiveHeadline)
            {
                //node.Nodes.Clear();
                List<TreeNode> activeNodes = null;
                SQLiteCommand cmd = null;
                cmd = new SQLiteCommand("select count(distinct th.appID|| ',' || th.threadid) as CNT from multisqlite_entries tt,multisqlite_apps ap, multisqlite_threads th where tt.appID = th.appID and tt.threadID = th.threadID and th.isActive = 1 and ap.id = th.appID and ap.isActive=1", connection.get());
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string strCNT = (string)reader["CNT"].ToString();
                    NodeDefinition.Add(NodeDefinition.NodeType.ntCountTotalThreadsActive, strCNT, true, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID);

                }
            }

            else if (nd.nodeType == NodeDefinition.NodeType.ntThreadThroughputHeadline)
            {
                //node.Nodes.Clear();
                List<TreeNode> activeNodes = null;
                SQLiteCommand cmd = null;
                String strThroughput = String.Format("select count(tt.id)/60 as CNT from multisqlite_entries tt, multisqlite_apps ap, multisqlite_threads th where tt.appID={0} and tt.threadID={1} and tt.threadID=th.threadID and tt.appID=ap.id and th.AppID=ap.ID and ap.isActive=true and th.isActive=true and (ROUND((JULIANDAY('now') -JULIANDAY(tt.tsCreated)) *86400) / 60 )<1", nd.strAppID, nd.strThreadID);
                cmd = new SQLiteCommand(strThroughput, connection.get());
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string strCNT = (string)reader["CNT"].ToString();
                    NodeDefinition.Add(NodeDefinition.NodeType.ntThreadThroughput, strCNT + " Inserts / sec.", true, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID);
                }
            }

            else if (nd.nodeType == NodeDefinition.NodeType.ntAppActiveThroughputHeadline)
            {
                //node.Nodes.Clear();
                List<TreeNode> activeNodes = null;
                SQLiteCommand cmd = null;
                String strThroughput = String.Format("select count(tt.id)/60 as CNT from multisqlite_entries tt, multisqlite_apps ap, multisqlite_threads th where tt.appID={0} and tt.threadID=th.threadID and tt.appID=ap.id and th.AppID=ap.ID and ap.isActive=true and th.isActive=true and (ROUND((JULIANDAY('now') -JULIANDAY(tt.tsCreated)) *86400) / 60 )<1", nd.strAppID);
                cmd = new SQLiteCommand(strThroughput, connection.get());
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string strCNT = (string)reader["CNT"].ToString();
                    NodeDefinition.Add(NodeDefinition.NodeType.ntThreadThroughput, strCNT + " Inserts / sec.", true, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID);
                    //currentNode.Text = strCNT + " Inserts / sec.";
                }
            }

            else if (nd.nodeType == NodeDefinition.NodeType.ntTotalThroughputHeadline)
            {
                //node.Nodes.Clear();
                List<TreeNode> activeNodes = null;
                SQLiteCommand cmd = null;
                String strThroughput = String.Format("select count(tt.id)/60 as CNT from multisqlite_entries tt, multisqlite_apps ap, multisqlite_threads th where tt.appID in (select id from multisqlite_apps where isActive=1) and tt.threadID=th.threadID and tt.appID=ap.id and th.AppID=ap.ID and ap.isActive=true and th.isActive=true and (ROUND((JULIANDAY('now') -JULIANDAY(tt.tsCreated)) *86400) / 60 )<1");
                cmd = new SQLiteCommand(strThroughput, connection.get());
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string strCNT = (string)reader["CNT"].ToString();
                    NodeDefinition.Add(NodeDefinition.NodeType.ntTotalThroughput, strCNT + " Inserts / sec.", true, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID);
                }
            }

            else if (nd.nodeType == NodeType.ntStatusHeadline)
            {
                //node.Nodes.Clear();
                List<TreeNode> activeNodes = null;
                System.IO.FileInfo fileInfo = new System.IO.FileInfo(connection.getDatabaseFile());
                var versionInfo = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);

               
                NodeDefinition.Add(NodeDefinition.NodeType.ntStatusItem, "Application: " + appName + " version: " + strVersion + " (" + DEBUG_LEVEL + ")", false, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID, NodeDefinition.NodeType.ntStatusItem.ToString() + "_1") ;
                NodeDefinition.Add(NodeDefinition.NodeType.ntStatusItem, "Application Instance: " + appID, false, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID, NodeDefinition.NodeType.ntStatusItem.ToString() + "_2");                
                NodeDefinition.Add(NodeDefinition.NodeType.ntStatusItem, "Database File: " + connection.getDatabaseFile(), false, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID, NodeDefinition.NodeType.ntStatusItem.ToString() + "_3");
                NodeDefinition.Add(NodeDefinition.NodeType.ntStatusItem, "SQLite-Version: " + connection.getSQLiteVersion(), false, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID, NodeDefinition.NodeType.ntStatusItem.ToString() + "_4");
                NodeDefinition.Add(NodeDefinition.NodeType.ntStatusItem, connection.getDBAccessMode(), false, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID, NodeDefinition.NodeType.ntStatusItem.ToString() + "_5");                
                NodeDefinition.Add(NodeDefinition.NodeType.ntStatusItem, "Database-Size: " + GetBytesReadable(fileInfo.Length), false, node.Nodes, ref activeNodes, nd.parentColor, nd.strAppID, nd.strThreadID, NodeType.ntStatusFileSize.ToString());                
            }

            else if (nd.nodeType == NodeDefinition.NodeType.ntTotalStatusHeadline)
            {

            }


            if (node.Nodes.Count == 0)
                node.Nodes.Add("");
        }

        private void treeApps_BeforeExpand_1(object sender, TreeViewCancelEventArgs e)
        {
            TreeNode node = e.Node;
            updateNode(node, NodeDefinition.NodeAction.Update);
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (threads!=null)
                threads.running = false;
            if (connection.get() == null)
                return;
            if (connection.get().State == ConnectionState.Closed)
                return;
            connection.execQuery("update multisqlite_apps set isActive=0 where id = " + appID);
        }

        private void tcTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            tiPollApps_Tick(tiPollApps, null);
        }

        private void tiConnectionQuery_Tick(object sender, EventArgs e)
        {
            lblVersion.ForeColor = Color.Green;
            tiConnectionQuery.Enabled = false;
        }

        private void btnShowManual_Click(object sender, EventArgs e)
        {            
            String strManualNew = (isInIDE() ? strAppData : strAppDir) + "Haufe_MultiSQLite_CS_Manual.new";
            String strManual = (isInIDE()?strAppData:strAppDir) + "Haufe_MultiSQLite_CS_Manual.pdf";
            String strViewer = (isInIDE() ? strAppData : strAppDir) + "Viewer.exe" ;

            if (File.Exists(strManualNew))
                File.Delete(strManualNew);

            using (var client = new System.Net.WebClient())
            {
                try
                {
                    client.DownloadFile("https://raw.githubusercontent.com/ushaufe/MultiSQLite_CS/master/Doc/Haufe_MultiSQLite_CS_Manual.pdf", strManualNew );
                }
                catch (Exception ex)
                {                   
                }
                try 
                {
                    if (!File.Exists(strViewer))
                        client.DownloadFile("https://raw.githubusercontent.com/ushaufe/MultiSQLite_CS/master/Tools/Viewer.exe", strViewer);
                }
                catch(Exception ex)
                {                   
                }
            }
            if (File.Exists(strManualNew))
            {
                try
                {                    
                    File.Delete(strManual);
                }
                catch(Exception ex)
                {
                }
                try
                {
                    File.Move(strManualNew, strManual);
                }
                catch(Exception ex)
                {

                }
            }

            try
            {                
                Process.Start("\"" + strViewer + "\"", "\"" + strManual + "\"");
            }
            catch(Exception ex)
            {               
            }
        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        void printTable(List<List<string>> table)
        {
            int selStart = rePrompt.TextLength;
            String str = "";
            foreach (List<string> column in table)
            {
                string strCol = "";
                foreach (string strField in column)
                {
                    if (strCol.Length > 0)
                        strCol += ";";
                    strCol = strCol + strField;
                }
                str += strCol + "\n";
            }
            int selEnd = rePrompt.TextLength;
            rePrompt.SelectionStart = selStart;
            rePrompt.SelectionLength = selEnd - selStart;
            rePrompt.SelectionColor = Color.White;
            rePrompt.SelectedText = str;

        }

        private void rePrompt_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {

                /*
                rePrompt.SelectionStart = rePrompt.Text.Length;
                int cursorPosition = rePrompt.SelectionStart;
                int cmdIndex = rePrompt.GetLineFromCharIndex(cursorPosition) - 2;
                if (cmdIndex < 0)
                    return;
                  int cmdIndex = rePrompt.Lines.Length - 1;

                String strCMD = unprompt(rePrompt.Lines[cmdIndex]);
                */

                String strLastLine = "";
                foreach(String strLine in rePrompt.Lines)
                {
                    if (strLine.Trim().Length > 0)
                        strLastLine = strLine;
                }

                String strCMD = unprompt(strLastLine);
                if (strCMD.Trim().Length == 0)
                {
                    prompt.Prompt();
                    return;
                }
                if (strCMD.ToUpper().Equals("DISCONNECT"))
                {
                    prompt.Disconnect(ref appID);
                    return;
                }
                if (strCMD.ToUpper().Equals("CONNECT"))
                {
                    prompt.Connect(ref appID, ref threads);
                    return;
                }
                SQLiteCommand cmd = null;
                if (connection.get() == null)
                {
                    prompt.Out("");
                    prompt.Out("Error: No Database Connection", Color.OrangeRed);
                    prompt.Prompt();
                    return;
                }
                cmd = new SQLiteCommand(strCMD, connection.get());
                try
                {
                    List<List<string>> table = new List<List<string>>();
                    List<string> columns = new List<string>();
                    SQLiteDataReader reader = cmd.ExecuteReader();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string strColumn = reader.GetName(i);
                        columns.Add(strColumn);
                    }
                    table.Add(columns.GetRange(0, columns.Count));
                    while ((reader != null) && (reader.Read()))
                    {
                        columns.Clear();
                        foreach (String strCol in table[0])
                        {
                            string str = (string)reader[strCol].ToString();
                            columns.Add(str);
                        }
                        table.Add(columns.GetRange(0, columns.Count));
                    }
                    printTable(table);
                    prompt.Prompt();
                }
                catch (Exception ex)
                {

                    prompt.Out("");
                    prompt.Out("Error: The SQLite-database throws the following error:\n" + ex.Message + "\n", Color.OrangeRed);
                    prompt.Prompt();
                    return;
                }
            }
        }

        // Returns the human-readable file size for an arbitrary, 64-bit file size 
        // The default format is "0.### XB", e.g. "4.2 KB" or "1.434 GB"
        public string GetBytesReadable(long i)
        {
            // Get absolute value
            long absolute_i = (i < 0 ? -i : i);
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absolute_i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = (i >> 50);
            }
            else if (absolute_i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = (i >> 40);
            }
            else if (absolute_i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (i >> 30);
            }
            else if (absolute_i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (i >> 20);
            }
            else if (absolute_i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (i >> 10);
            }
            else if (absolute_i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = (readable / 1024);
            // Return formatted number with suffix
            return readable.ToString("0.### ") + suffix;
        }
        

        private void tiLiveUpdate_Tick(object sender, EventArgs e)
        {
            if (connection.get().State != ConnectionState.Open)
                return;
            ((System.Windows.Forms.Timer)sender).Enabled = false;
            try
            {
                tiLiveUpdate.Enabled = false;
                btnLiveUpdate.UseVisualStyleBackColor = true;
                btnLiveUpdate.BackColor = Color.DarkBlue;
                btnLiveUpdate.Invalidate();
                btnLiveUpdate.Update();
                btnLiveUpdate.Refresh();
                System.Windows.Forms.Application.DoEvents();
                tiLiveUpdateFlicker.Enabled = true;
                foreach (TreeNode node in treeApps.Nodes)
                {
                    updateNodeRecursive(node);
                }
            }
            finally
            {
                ((System.Windows.Forms.Timer)sender).Enabled = true;
            }

        }

        void updatePollingInterval(int intervall = 0)
        {
            if (intervall>0)
            {
                tbDBInterval.Value = intervall;
            }
            LBL_DB_PollingInterval.Text = "DB Polling Interval: " + tbDBInterval.Value + " Seconds";
            tiPollApps.Interval = tbDBInterval.Value * 1000;
            tiLiveUpdate.Interval = tbDBInterval.Value * 1000;
            tiUpdateApps.Interval = tbDBInterval.Value * 1000;
        }

        private void btnLiveUpdate_Click(object sender, EventArgs e)
        {
            if (connection.get() == null)
                return;
            if (connection.get().State != ConnectionState.Open)
                return;

            tiLiveUpdate.Enabled = !tiLiveUpdate.Enabled;

            if (tiLiveUpdate.Enabled)
            {                
                btnLiveUpdate.BackColor = Color.LightBlue;
                mnuLiveUpdate.Checked = true;
            }
            else
            {
                btnLiveUpdate.BackColor = SystemColors.ButtonFace;
                btnLiveUpdate.UseVisualStyleBackColor = true;
                mnuLiveUpdate.Checked = false;
                tiLiveUpdateFlicker.Enabled = false;
            }

            /*
            tcTabs.SelectedIndex = 0;
            Thread vt = new Thread(viewThread.view);
            viewThread.running = !viewThread.running;
            return;
            btnLiveUpdate.Enabled = viewThread.running;
            vt.Start();
            */
        }

        private void menuExit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void menuExit_DropDownOpened(object sender, EventArgs e)
        {
            
        }

        private void menuExit_CheckStateChanged(object sender, EventArgs e)
        {
      
        }

        private void mnuStartThread_Click(object sender, EventArgs e)
        {
            buttonStartThreads_Click(sender, e);
        }

        private void mnuStopThread_Click(object sender, EventArgs e)
        {
            btnStopThreads_Click(sender, e);
        }

        private void mnuLiveUpdate_Click(object sender, EventArgs e)
        {
            btnLiveUpdate_Click(sender, e);
        }

        private void tiLiveUpdateFlicker_Tick(object sender, EventArgs e)
        {
            btnLiveUpdate.BackColor = Color.LightBlue;
            tiLiveUpdateFlicker.Enabled = false;
        }

        private void mnuShowManual_Click(object sender, EventArgs e)
        {
            btnShowManual_Click(sender, e);
           
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FormAbout frmAbout = new FormAbout();
            frmAbout.StartPosition = FormStartPosition.CenterScreen;
            frmAbout.ShowDialog();
        }

        private void tbDBInterval_ValueChanged(object sender, EventArgs e)
        {
            updatePollingInterval();
        }

        private void tbDBInterval_Scroll(object sender, EventArgs e)
        {

        }

        private void rePrompt_KeyDown(object sender, KeyEventArgs e)
        {
            
        }

        private void rePrompt_Enter(object sender, EventArgs e)
        {
            tiScrollCaret.Enabled = true;
        }

        private void tiScrollCaret_Tick(object sender, EventArgs e)
        {
            rePrompt.Select(this.rePrompt.Text.Length, 0);
            rePrompt.ScrollToCaret();
            tiScrollCaret.Enabled = false;
        }

        private void fileToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {            
            mnuConnect.Enabled =   !(connection.get().State==ConnectionState.Open);
            mnuDisconnect.Enabled = (connection.get().State==ConnectionState.Open);            
        }

        private void mnuConnect_Click(object sender, EventArgs e)
        {
            prompt.Connect(ref appID, ref threads);
        }

        private void mnuDisconnect_Click(object sender, EventArgs e)
        {
            prompt.Disconnect(ref appID);
        }

        private void mnuOpenExternalDB_Click(object sender, EventArgs e)
        {
            OpenFileDialog od = new OpenFileDialog();
            od.Filter = "Haufe Database Files ´(*.dbs)|*.dbs|SQLite Database Files (*.db)|*.db";
            od.Title = "Open External Database:";
            if (od.ShowDialog()==DialogResult.OK)
            {
                if (od.FileName.Length == 0)
                    return;
                prompt.Out("Opening external database: " + od.FileName);
                connection.Connect(ref appID, ref threads, od.FileName);
                prompt.Prompt();
            }
            
        }

        

        private void mnuUpdate_Click(object sender, EventArgs e)
        {
            tcTabs.SelectedTab = tsGeneral;
            UpdateClass.UpdateApp(prompt, strAppDir, strAppData);
        }

        private void tiAppUpdate_Tick(object sender, EventArgs e)
        {

        }

        private void mnuShowGitHub_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/ushaufe/MultiSQLite_CS.git");
        }

        public bool isInIDE()
        {
            return System.Diagnostics.Debugger.IsAttached;
        }

        private void tiDelayGUILoad_Tick(object sender, EventArgs e)
        {
            tiDelayGUILoad.Enabled = false;
            tcTabs.SelectedTab = tsGeneral;
            if (!isInIDE())            
                if (UpdateClass.UpdateApp(prompt, strAppDir, strAppData, false) == true)
                    return;

            connection.Connect(ref appID, ref threads);

            updatePollingInterval(10);
            tiUpdateApps.Enabled = true;
            tiUpdateApps_Tick(tiUpdateApps, null);

            tiPollApps.Enabled = true;
            tiUpdateApps_Tick(tiUpdateApps, null);
            prompt.Out("");
            prompt.Out("Type in an SQL command that will be applied directly on the database.", Color.Lime);
            prompt.Out("", Color.Lime);
            prompt.Out("Type \"SELECT name FROM sqlite_master\" to show the structure of the database.", Color.Lime);
            prompt.Out("", Color.Lime);
            prompt.Out("Note: The commands will be executed on the GUI-thread.", Color.Lime);
            prompt.Prompt();
        }

        private void mnuShowGitHub_CS_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/ushaufe/MultiSQLite_CS.git");
        }

        private void showGitHubProjectForCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/ushaufe/Sqlite4CPP");
        }

        private void mnuStartMultiSQLiteCpp_Click(object sender, EventArgs e)
        {
            prompt.Out("", UpdateClass.colorUpdate);
            prompt.Out("Starting: MultiSQLite C++...", UpdateClass.colorUpdate);
            prompt.Out("", UpdateClass.colorUpdate);          
            System.Diagnostics.Process.Start(strAppDir + APPNAME_MULTISQLITE_CPP );
            prompt.Prompt();
        }

        private void mnuActions_DropDownOpening(object sender, EventArgs e)
        {
            mnuStartMultiSQLiteCpp.Enabled = false;
            mnuStartMultiSQLiteCS.Enabled = false;

            try
            {
                //prompt.Out("Debug: " + strAppDir + APPNAME_MULTISQLITE_CPP, Color.Red);
                var versionInfoCPP = FileVersionInfo.GetVersionInfo(strAppDir + APPNAME_MULTISQLITE_CPP);
                String strVersionCPP = versionInfoCPP.FileVersion;
                if (ConnectionClass.getDBVersionNumber(strVersionCPP) > 0)
                    mnuStartMultiSQLiteCpp.Enabled = true;
            }
            catch(Exception ex)
            {

            }            
            try
            {

                var versionInfoCS = FileVersionInfo.GetVersionInfo(strAppDir + APPNAME_MULTISQLITE_CS);
                String strVersionCS = versionInfoCS.FileVersion;
                if (ConnectionClass.getDBVersionNumber(strVersionCS) > 0)
                    mnuStartMultiSQLiteCS.Enabled = true;
            }
            catch(Exception ex)
            {

            }
        }

        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void startNewInstanceOfMultiSQLiteForCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            prompt.Out("", UpdateClass.colorUpdate);
            prompt.Out("Starting: MultiSQLite C++...", UpdateClass.colorUpdate);
            prompt.Out("", UpdateClass.colorUpdate);
            System.Diagnostics.Process.Start(strAppDir + APPNAME_MULTISQLITE_CS);
            prompt.Prompt();
        }
    }
}

   