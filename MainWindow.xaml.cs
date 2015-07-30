using System;
using System.Windows;
using System.Text;
using System.Windows.Forms;
using Advanced_Combat_Tracker;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.RegularExpressions;
using System.Linq;

namespace CDTracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 


    public class CDTrackerMain : System.Windows.Forms.UserControl, IActPluginV1
    {
        private SynchronizedCollection<Combatant> PartyMemberInfo = new SynchronizedCollection<Combatant>();
        private SynchronizedCollection<Combatant> FocusInfo = new SynchronizedCollection<Combatant>();
        private static ActPluginData _plugin = null;
        private Thread checkStatus = null;
        private Thread getStats = null;
        private Thread CDupdate = null;

        private Process ffxivProcess = null;
        Random rand = new Random();
        Stopwatch stopwatch_1 = new Stopwatch();
        Display display = new Display();
        public bool IsACTVisible { get; set; }
        public bool IsFFXIVPluginStarted { get; set; }
        public bool IsFFXIVProcessStarted { get; set; }
        public bool IsLoggedIn { get; set; }

        #region Designer Created Code (Avoid editing)
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        /// 

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();

            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(4, 4);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(434, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Show the main window.";

            this.button1.AutoSize = true;
            this.button1.Location = new System.Drawing.Point(4, 20);
            this.button1.Name = "button1";
            this.button1.Text = "Show";
            this.button1.Click += new System.EventHandler(this.button_Clicked);

            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.button1);
            this.Controls.Add(this.label1);
            this.Name = "CDTracker";
            this.Size = new System.Drawing.Size(686, 384);
            this.ResumeLayout(false);
            this.PerformLayout();

            display.Show();
        }

        private void button_Clicked(object sender, EventArgs e)
        {
            if (!display.IsLoaded)
            {
                display = new Display();
                display.Show();
            }
        }

        #endregion
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button button1;
        #endregion

        public CDTrackerMain()
        {
            InitializeComponent();

            //window.Show();

            stopwatch_1.Stop();

            ActGlobals.oFormActMain.OnLogLineRead += act_OnLogLineRead;

            checkStatus = new Thread(new ThreadStart(CheckProcess));
            checkStatus.Name = "Check Status";
            checkStatus.IsBackground = true;
            checkStatus.Start();

            getStats = new Thread(new ThreadStart(GetMemberStats));
            getStats.Name = "Get Stats";
            getStats.IsBackground = true;
            getStats.Start();

            CDupdate = new Thread(new ThreadStart(UpdateCDList));
            CDupdate.Name = "UpdateCDList";
            CDupdate.IsBackground = true;
            CDupdate.Start();
            //display.Show();
        }
        #region Reflection
        private void CheckProcess()
        {
            while (true)
            {

                IsACTVisible = ActGlobals.oFormActMain.Visible;
                IsFFXIVPluginStarted = false;
                if (ActGlobals.oFormActMain.Visible)
                {
                    IsFFXIVPluginStarted = Instance == null ? false : true;
                }
                ffxivProcess = GetFFXIVProcess;
                if (ffxivProcess != null)
                {
                    IsFFXIVProcessStarted = true;
                    Combatant player = GetPlayerData();
                    if (player != null && player.Job != 0)
                    {
                        IsLoggedIn = true;
                    }
                    else
                    {
                        IsLoggedIn = false;
                    }
                }
                else
                {
                    IsFFXIVProcessStarted = false;
                    IsLoggedIn = false;
                    foreach (Combatant combatant in PartyMemberInfo)
                    {
                        combatant.ID = 0;
                        combatant.OwnerID = 0;
                        combatant.Order = 0;
                        combatant.type = (int)TYPE.Unknown;
                        combatant.Level = 0;
                        combatant.Job = (int)JOB.Unknown;
                        combatant.Name = string.Empty;
                        combatant.CurrentHP = 0;
                        combatant.MaxHP = 0;
                        combatant.CurrentMP = 0;
                        combatant.MaxMP = 0;
                        combatant.CurrentTP = 0;
                    }
                }
                new System.Threading.ManualResetEvent(false).WaitOne(1000);

            }
        }
        private void GetMemberStats()
        {
            while (true)
            {
                if (ffxivProcess == null)
                {
                    new System.Threading.ManualResetEvent(false).WaitOne(1000);
                    continue;
                }

                List<Combatant> combatantList = GetCombatantList();
                List<uint> partyListIDs = GetCurrentPartyList();
                SynchronizedCollection<Combatant> partyTemp = new SynchronizedCollection<Combatant>();
                SynchronizedCollection<Combatant> partyTempSorted = new SynchronizedCollection<Combatant>();

                SynchronizedCollection<Combatant> enemyTemp = new SynchronizedCollection<Combatant>();

                if (partyListIDs != null)
                {
                    foreach (uint ID in partyListIDs) //Find party members
                    {
                        foreach (Combatant combatant in combatantList)
                        {
                            if (ID == combatant.ID)
                            {
                                partyTemp.Add(combatant);

                                break;
                            }
                        }
                    }

                    if (partyTemp.Count > 0)
                    {

                        { //SCH
                            foreach (Combatant c in partyTemp)
                            {
                                if (c.Job == 0x1C)
                                {
                                    partyTempSorted.Add(c);
                                }
                            }
                            { //WHM
                                foreach (Combatant c in partyTemp)
                                {
                                    if (c.Job == 0x18)
                                    {
                                        partyTempSorted.Add(c);

                                    }
                                }
                            }

                        }
                        { //AST
                            foreach (Combatant c in partyTemp)
                            {
                                if (c.Job == 0x21)
                                {
                                    partyTempSorted.Add(c);
                                }
                            }
                            { //PLD
                                foreach (Combatant c in partyTemp)
                                {
                                    if (c.Job == 0x13)
                                    {
                                        partyTempSorted.Add(c);
                                    }
                                }
                            }
                            { //WAR
                                foreach (Combatant c in partyTemp)
                                {
                                    if (c.Job == 0x15)
                                    {
                                        partyTempSorted.Add(c);
                                    }
                                }
                            }
                            { //DRK
                                foreach (Combatant c in partyTemp)
                                {
                                    if (c.Job == 0x20)
                                    {
                                        partyTempSorted.Add(c);
                                    }
                                }
                            }
                            { //MNK
                                foreach (Combatant c in partyTemp)
                                {
                                    if (c.Job == 0x14)
                                    {
                                        partyTempSorted.Add(c);
                                    }
                                }

                            }
                            { //DRG
                                foreach (Combatant c in partyTemp)
                                {
                                    if (c.Job == 0x16)
                                    {
                                        partyTempSorted.Add(c);
                                    }
                                }

                            }
                            { //NIN
                                foreach (Combatant c in partyTemp)
                                {
                                    if (c.Job == 0x1E)
                                    {
                                        partyTempSorted.Add(c);
                                    }
                                }
                            }
                            { //BRD
                                foreach (Combatant c in partyTemp)
                                {
                                    if (c.Job == 0x17)
                                    {
                                        partyTempSorted.Add(c);
                                    }
                                }
                            }
                            { //BLM
                                foreach (Combatant c in partyTemp)
                                {
                                    if (c.Job == 0x19)
                                    {
                                        partyTempSorted.Add(c);
                                    }
                                }

                            }
                            { //SMN
                                foreach (Combatant c in partyTemp)
                                {
                                    if (c.Job == 0x1B)
                                    {
                                        partyTempSorted.Add(c);
                                    }
                                }
                            }
                            { //MCH
                                foreach (Combatant c in partyTemp)
                                {
                                    if (c.Job == 0x1F)
                                    {
                                        partyTempSorted.Add(c);
                                    }
                                }
                            }
                        }
                        if (partyTemp[0].Job == 0x1C || partyTemp[0].Job == 0x1B && partyTempSorted.Count > 0)
                        {
                            foreach (Combatant combatant in combatantList) //Add user's pet
                            {
                                if (combatant.OwnerID == partyTempSorted[0].ID && (combatant.Name == "Eos" || combatant.Name == "Selene" || combatant.Name == "Garuda-Egi" || combatant.Name == "Ifrit-Egi" || combatant.Name == "Titan-Egi"))
                                {
                                    combatant.Job = 100;
                                    partyTempSorted.Add(combatant);

                                    break;
                                }
                            }
                        }

                        PartyMemberInfo = partyTempSorted;
                        FocusInfo = enemyTemp;
                    }
                    new System.Threading.ManualResetEvent(false).WaitOne(50);
                }

            }
        }
        public static object Instance
        {
            get
            {
                if (_plugin == null && ActGlobals.oFormActMain.Visible)
                {
                    foreach (ActPluginData plugin in ActGlobals.oFormActMain.ActPlugins)
                    {
                        if (plugin.pluginFile.Name == "FFXIV_ACT_Plugin.dll" && plugin.lblPluginStatus.Text == "FFXIV Plugin Started.")
                        {
                            _plugin = plugin;
                            break;
                        }
                    }
                }
                return _plugin;
            }
        }
        public static Combatant GetPlayerData()
        {
            Combatant player = new Combatant();

            var scanCombatants = GetScanCombatants();
            if (scanCombatants == null) return null;

            var item = scanCombatants.GetType().InvokeMember("GetPlayerData", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null, scanCombatants, null);
            FieldInfo fi = item.GetType().GetField("JobID", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetField);
            player.Job = (int)fi.GetValue(item);

            return player;
        }
        public static Process GetFFXIVProcess
        {
            get
            {
                try
                {
                    FieldInfo fi = _plugin.pluginObj.GetType().GetField("_Memory", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);
                    var memory = fi.GetValue(_plugin.pluginObj);
                    if (memory == null) return null;

                    fi = memory.GetType().GetField("_config", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);
                    var config = fi.GetValue(memory);
                    if (config == null) return null;

                    fi = config.GetType().GetField("Process", BindingFlags.GetField | BindingFlags.Public | BindingFlags.Instance);
                    var process = fi.GetValue(config);
                    if (process == null) return null;

                    return (Process)process;
                }
                catch
                {
                    return null;
                }
            }
        }
        private static object GetScanCombatants()
        {
            FieldInfo fi = _plugin.pluginObj.GetType().GetField("_Memory", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);
            var memory = fi.GetValue(_plugin.pluginObj);
            if (memory == null) return null;

            fi = memory.GetType().GetField("_config", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);
            var config = fi.GetValue(memory);
            if (config == null) return null;

            fi = config.GetType().GetField("ScanCombatants", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);
            var scanCombatants = fi.GetValue(config);
            if (scanCombatants == null) return null;

            return scanCombatants;
        }
        public List<uint> GetCurrentPartyList()
        {
            FieldInfo fi = _plugin.pluginObj.GetType().GetField("_Memory", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);
            var memory = fi.GetValue(_plugin.pluginObj);
            if (memory == null) return null;

            fi = memory.GetType().GetField("_config", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);
            var config = fi.GetValue(memory);
            if (config == null) return null;

            fi = config.GetType().GetField("ScanCombatants", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);
            var scanCombatants = fi.GetValue(config);
            if (scanCombatants == null) return null;

            MethodInfo mi = scanCombatants.GetType().GetMethod("GetCurrentPartyList");
            object[] _params = new object[] { 0 };
            List<uint> partyList = (List<uint>)mi.Invoke(scanCombatants, _params);

            return partyList;
        }
        public class Cooldown
        {
            public uint userID = 0;
            public string userName = "";
            public uint targetID = 0;
            public string targetName = "";
            public string cooldownName = "";
            public DateTime TimeUsed;
            public TimeSpan TimeRemaining;
            public DateTime TimeAvailable;
        }
        public List<string> CDStringList = new List<string>();
        public static List<Combatant> GetCombatantList()
        {
            List<Combatant> result = new List<Combatant>();
            try
            {
                var scanCombatants = GetScanCombatants();
                if (scanCombatants == null) return null;

                var item = scanCombatants.GetType().InvokeMember("GetCombatantList", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null, scanCombatants, null);
                FieldInfo fi = item.GetType().GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);

                Type[] nestedType = item.GetType().GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                object tmp = fi.GetValue(item);
                if (tmp.GetType().IsArray)
                {
                    foreach (object temp in (Array)tmp)
                    {
                        if (temp == null) break;

                        Combatant combatant = new Combatant();

                        fi = temp.GetType().GetField("ID", BindingFlags.Public | BindingFlags.Instance);
                        combatant.ID = (uint)fi.GetValue(temp);
                        fi = temp.GetType().GetField("OwnerID", BindingFlags.Public | BindingFlags.Instance);
                        combatant.OwnerID = (uint)fi.GetValue(temp);
                        fi = temp.GetType().GetField("Order", BindingFlags.Public | BindingFlags.Instance);
                        combatant.Order = (int)fi.GetValue(temp);
                        fi = temp.GetType().GetField("type", BindingFlags.Public | BindingFlags.Instance);
                        combatant.type = (byte)fi.GetValue(temp);
                        fi = temp.GetType().GetField("Level", BindingFlags.Public | BindingFlags.Instance);
                        combatant.Level = (int)fi.GetValue(temp);
                        fi = temp.GetType().GetField("Job", BindingFlags.Public | BindingFlags.Instance);
                        combatant.Job = (int)fi.GetValue(temp);
                        fi = temp.GetType().GetField("Name", BindingFlags.Public | BindingFlags.Instance);
                        combatant.Name = (string)fi.GetValue(temp);
                        fi = temp.GetType().GetField("CurrentHP", BindingFlags.Public | BindingFlags.Instance);
                        combatant.CurrentHP = (int)fi.GetValue(temp);
                        fi = temp.GetType().GetField("MaxHP", BindingFlags.Public | BindingFlags.Instance);
                        combatant.MaxHP = (int)fi.GetValue(temp);
                        fi = temp.GetType().GetField("CurrentMP", BindingFlags.Public | BindingFlags.Instance);
                        combatant.CurrentMP = (int)fi.GetValue(temp);
                        fi = temp.GetType().GetField("MaxMP", BindingFlags.Public | BindingFlags.Instance);
                        combatant.MaxMP = (int)fi.GetValue(temp);
                        fi = temp.GetType().GetField("CurrentTP", BindingFlags.Public | BindingFlags.Instance);
                        combatant.CurrentTP = (int)fi.GetValue(temp);

                        result.Add(combatant);
                    }
                }
            }
            catch { }
            return result;
        }
        public Regex CDRegex1 = new Regex(@"\[(\d\d):(\d\d):(\d\d).(\d\d\d)\] 15:([0-Z][0-Z][0-Z][0-Z][0-Z][0-Z][0-Z][0-Z]):([A-z,',\s]+):\w+:([A-z,',\s,\-,_,0-9]+):([0-Z][0-Z][0-Z][0-Z][0-Z][0-Z][0-Z][0-Z]):([A-z,',\s]+):", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public Regex CDRegex2 = new Regex(@"\[(\d\d):(\d\d):(\d\d).(\d\d\d)\] 16:([0-Z][0-Z][0-Z][0-Z][0-Z][0-Z][0-Z][0-Z]):([A-z,',\s]+):\w+:([A-z,',\s,\-,_,0-9]+):([0-Z][0-Z][0-Z][0-Z][0-Z][0-Z][0-Z][0-Z]):([A-z,',\s]+):", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public void act_OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            if (!isImport)
            {
                Match match1 = CDRegex1.Match(logInfo.logLine);
                if (match1.Success && match1.Value != "")
                {
                    ProcessLogLine(match1);
                }
                else
                {
                    Match match2 = CDRegex2.Match(logInfo.logLine);
                    if (match2.Success && match2.Value != "")
                    {
                        ProcessLogLine(match2);
                    }
                }

            } 
        }

        public void ProcessLogLine(Match match)
        {
            List<uint> plist = GetCurrentPartyList();
            
            uint userID = uint.Parse(match.Groups[5].Value, System.Globalization.NumberStyles.HexNumber);

            foreach (uint i in plist)
            {
                if (userID == i)
                {
                    Regex PotionRegex = new Regex(@"Unknown_F\w+");
                    Match m1 = PotionRegex.Match(match.Groups[7].Value);
                    bool IsPotion = false;
                    if(m1.Success)
                    {
                        IsPotion = true;
                    }
                    DateTime timeu = new DateTime();
                    timeu = timeu.AddYears(DateTime.Now.Year - 1);
                    timeu = timeu.AddMonths(DateTime.Now.Month - 1);
                    timeu = timeu.AddDays(DateTime.Now.Day - 1);
                    timeu = timeu.AddHours(Convert.ToDouble(match.Groups[1].Value));
                    timeu = timeu.AddMinutes(Convert.ToDouble(match.Groups[2].Value));
                    timeu = timeu.AddSeconds(Convert.ToDouble(match.Groups[3].Value));
                    timeu = timeu.AddMilliseconds(Convert.ToDouble(match.Groups[4].Value));

                    DateTime timee = new DateTime();
                    timee = timee.AddYears(DateTime.Now.Year - 1);
                    timee = timee.AddMonths(DateTime.Now.Month - 1);
                    timee = timee.AddDays(DateTime.Now.Day - 1);
                    timee = timee.AddHours(Convert.ToDouble(match.Groups[1].Value));
                    timee = timee.AddMinutes(Convert.ToDouble(match.Groups[2].Value));
                    timee = timee.AddSeconds(Convert.ToDouble(match.Groups[3].Value));
                    timee = timee.AddMilliseconds(Convert.ToDouble(match.Groups[4].Value));
                    string LookupString = "";
                    if(!IsPotion)
                    {
                        LookupString = match.Groups[7].Value;
                    } else
                    {
                        LookupString = "Potion";
                    }
                    if (CDLookup(LookupString, uint.Parse(match.Groups[5].Value, System.Globalization.NumberStyles.HexNumber)) > 1) {
                        timee = timee.AddSeconds(CDLookup(LookupString, uint.Parse(match.Groups[5].Value, System.Globalization.NumberStyles.HexNumber))); //****

                        Cooldown tempCD = new Cooldown()
                        {
                            userID = uint.Parse(match.Groups[5].Value, System.Globalization.NumberStyles.HexNumber),
                            userName = match.Groups[6].Value,
                            targetID = uint.Parse(match.Groups[8].Value, System.Globalization.NumberStyles.HexNumber),
                            targetName = match.Groups[9].Value,
                            cooldownName = LookupString,
                            TimeUsed = timeu,
                            TimeAvailable = timee,
                            TimeRemaining = timeu.Subtract(timee)
                        };

                        lock (CDList)
                        {
                                CDList.Add(tempCD);                           
                        }
                        break; }
                }
            }
        }

        public List<Cooldown> CDList = new List<Cooldown>();

        public void UpdateCDList()
        {
            while (true)
            {
                DateTime time_now = DateTime.Now;
                List<int> index_of_expired = new List<int>();
                int index = 0;
                lock (CDList)
                {
                    foreach (Cooldown cd in CDList)
                    {
                        cd.TimeRemaining = time_now.Subtract(cd.TimeAvailable);
                        if (cd.TimeRemaining.TotalSeconds >= 0)
                        {
                            index_of_expired.Add(index);
                        }
                        index++;
                    }
                    index_of_expired.Reverse();

                    foreach (int i in index_of_expired)
                    {
                        CDList.RemoveAt(i);
                    }

                    CDList = CDList.GroupBy(d => new { d.cooldownName, d.userName })
                                  .Select(d => d.First()).ToList().OrderByDescending(x => x.TimeRemaining.TotalSeconds).ToList();
                    string tempText = "";

                        tempText = String.Format("{0,-30}\t| {1,-30}\t| {2,10}", "CD Name", "User's Name", "Time Left") + Environment.NewLine;
                        tempText += "----------------------------------------------------------------------------------------------".PadRight(5) + Environment.NewLine;

                    foreach (Cooldown cd in CDList)
                    {

                        tempText += String.Format("{0,-30}\t| {1,-30}\t| {2,10}" ,cd.cooldownName, cd.userName, (cd.TimeRemaining.TotalSeconds * -1).ToString("F1")) + Environment.NewLine;

                    }
                    display.Dispatcher.Invoke((Action)(() =>
                    {
                        display.CooldownTB.Text = tempText;
                    }));
                }
                new System.Threading.ManualResetEvent(false).WaitOne(333);
            }
        }
        public int CDLookup(string cdname, uint userID)
        {
            int i = 0;
            List<Combatant> combatants = GetCombatantList();
            JOB job = JOB.Unknown;
            foreach (Combatant c in combatants)
            {
                if (c.ID == userID)
                {
                    job = (JOB)c.Job;
                    break;
                }
            }
            foreach (Buff buff in GlobalBuffList)
            {
                if (cdname.Equals(buff.name, StringComparison.InvariantCultureIgnoreCase) && job == buff.job)
                {
                    i = buff.CD;
                    break;
                } 
            }
            return i;
        }

        public void MakeBuff(Match match)
        {

        }

        public List<Buff> GlobalBuffList = new List<Buff>() {
                        new Buff()
            {
                name = "Potion",
                job = JOB.AST,
                CD = 270
            },
                        new Buff()
            {
                name = "Potion",
                job = JOB.BRD,
                CD = 270
            },
                        new Buff()
            {
                name = "Potion",
                job = JOB.MCH,
                CD = 270
            },
                        new Buff()
            {
                name = "Potion",
                job = JOB.SCH,
                CD = 270
            },
                                                new Buff()
            {
                name = "Potion",
                job = JOB.WHM,
                CD = 270
            },
                        new Buff()
            {
                name = "Potion",
                job = JOB.NIN,
                CD = 270
            },
                        new Buff()
            {
                name = "Potion",
                job = JOB.MNK,
                CD = 270
            },
                                                new Buff()
            {
                name = "Potion",
                job = JOB.DRG,
                CD = 270
            },
                        new Buff()
            {
                name = "Potion",
                job = JOB.SMN,
                CD = 270
            },
                        new Buff()
            {
                name = "Potion",
                job = JOB.BLM,
                CD = 270
            },
                        new Buff()
            {
                name = "Potion",
                job = JOB.DRK,
                CD = 270
            },
                        new Buff()
            {
                name = "Potion",
                job = JOB.PLD,
                CD = 270
            },
                        new Buff()
            {
                name = "Potion",
                job = JOB.WAR,
                CD = 270
            },
            new Buff()
            {
                name = "Eye for an Eye",
                job = JOB.SCH,
                CD = 120
            },
            new Buff()
            {
                name = "Eye for an Eye",
                job = JOB.BLM,
                CD = 180
            },
            new Buff()
            {
                name = "Eye for an Eye",
                job = JOB.WHM,
                CD = 180
            },
            new Buff()
            {
                name = "Rouse",
                job = JOB.SCH,
                CD = 60
            },
            new Buff()
            {
                name = "Rouse",
                job = JOB.SMN,
                CD = 60
            },
                        new Buff()
            {
                name = "Virus",
                job = JOB.SMN,
                CD = 90
            },
                        new Buff()
            {
                name = "Virus",
                job = JOB.SCH,
                CD = 90
            },
new Buff()
            {
                name = "Spur",
                job = JOB.SMN,
                CD = 120
            },
new Buff()
            {
                name = "Enkindle",
                job = JOB.SMN,
                CD = 180
            },
new Buff()
            {
                name = "Tri-disaster",
                job = JOB.SMN,
                CD = 60
            },
new Buff()
            {
                name = "Dissipation",
                job = JOB.SCH,
                CD = 180
            },
new Buff()
            {
                name = "Deployment Tactics",
                job = JOB.SCH,
                CD = 120
            },
new Buff()
            {
                name = "Aetherflow",
                job = JOB.SMN,
                CD = 60
            },
new Buff()
            {
                name = "Aetherflow",
                job = JOB.SCH,
                CD = 60
            },
new Buff()
            {
                name = "Presence of Mind",
                job = JOB.WHM,
                CD = 150
            },
new Buff()
            {
                name = "Divine Seal",
                job = JOB.WHM,
                CD = 60
            },
new Buff()
            {
                name = "Benediction",
                job = JOB.WHM,
                CD = 300
            },
new Buff()
            {
                name = "Asylum",
                job = JOB.WHM,
                CD = 90
            },
new Buff()
            {
                name = "Assize",
                job = JOB.WHM,
                CD = 90
            },
new Buff()
            {
                name = "Tetragrammaton",
                job = JOB.WHM,
                CD = 60
            },
new Buff()
            {
                name = "Shroud of Saints",
                job = JOB.WHM,
                CD = 120
            },
new Buff()
            {
                name = "Swiftcast",
                job = JOB.WHM,
                CD = 60
            },
new Buff()
            {
                name = "Swiftcast",
                job = JOB.SCH,
                CD = 60
            },
new Buff()
            {
                name = "Swiftcast",
                job = JOB.SMN,
                CD = 60
            },
new Buff()
            {
                name = "Swiftcast",
                job = JOB.BLM,
                CD = 60
            },
new Buff()
            {
                name = "Swiftcast",
                job = JOB.AST,
                CD = 60
            },
new Buff()
            {
                name = "Convert",
                job = JOB.BLM,
                CD = 180
            },
new Buff()
            {
                name = "Apocatastasis",
                job = JOB.BLM,
                CD = 180
            },
new Buff()
            {
                name = "Manaward",
                job = JOB.BLM,
                CD = 120
            },
new Buff()
            {
                name = "Manawall",
                job = JOB.BLM,
                CD = 120
            },
new Buff()
            {
                name = "Ley Lines",
                job = JOB.BLM,
                CD = 90
            },
new Buff()
            {
                name = "Sharpcast",
                job = JOB.BLM,
                CD = 60
            },
new Buff()
            {
                name = "Enochian",
                job = JOB.BLM,
                CD = 60
            },
new Buff()
            {
                name = "Celestial Opposition",
                job = JOB.AST,
                CD = 150
            },
new Buff()
            {
                name = "Collective Unconscious",
                job = JOB.AST,
                CD = 90
            },
new Buff()
            {
                name = "Time Dilation",
                job = JOB.AST,
                CD = 90
            },
new Buff()
            {
                name = "Synnastry",
                job = JOB.AST,
                CD = 120
            },
new Buff()
            {
                name = "Shuffle",
                job = JOB.AST,
                CD = 60
            },
new Buff()
            {
                name = "Spread",
                job = JOB.AST,
                CD = 60
            },
new Buff()
            {
                name = "Disable",
                job = JOB.AST,
                CD = 60
            },
new Buff()
            {
                name = "Luminiferous Aether",
                job = JOB.AST,
                CD = 120
            },
new Buff()
            {
                name = "Lightspeed",
                job = JOB.AST,
                CD = 150
            },
new Buff()
            {
                name = "Raging Strikes",
                job = JOB.BLM,
                CD = 180
            },
new Buff()
            {
                name = "Raging Strikes",
                job = JOB.SMN,
                CD = 180
            },
new Buff()
            {
                name = "Raging Strikes",
                job = JOB.MCH,
                CD = 120
            },
new Buff()
            {
                name = "Raging Strikes",
                job = JOB.BRD,
                CD = 120
            },
new Buff()
            {
                name = "Hawk's Eye",
                job = JOB.BRD,
                CD = 90
            },
new Buff()
            {
                name = "Hawk's Eye",
                job = JOB.MCH,
                CD = 90
            },
new Buff()
            {
                name = "Quelling Strikes",
                job = JOB.BRD,
                CD = 120
            },
new Buff()
            {
                name = "Quelling Strikes",
                job = JOB.BLM,
                CD = 90
            },
new Buff()
            {
                name = "Quelling Strikes",
                job = JOB.SMN,
                CD = 90
            },
new Buff()
            {
                name = "Barrage",
                job = JOB.BRD,
                CD = 90
            },
new Buff()
            {
                name = "Flaming Arrow",
                job = JOB.BRD,
                CD = 60
            },
new Buff()
            {
                name = "Sidewinder",
                job = JOB.BRD,
                CD = 60
            },
new Buff()
            {
                name = "Battle Voice",
                job = JOB.BRD,
                CD = 300
            },
new Buff()
            {
                name = "Dark Mind",
                job = JOB.DRK,
                CD = 60
            },
new Buff()
            {
                name = "Shadow Wall",
                job = JOB.DRK,
                CD = 180
            },
new Buff()
            {
                name = "Living Dead",
                job = JOB.DRK,
                CD = 300
            },
new Buff()
            {
                name = "Carve and Spit",
                job = JOB.DRK,
                CD = 60
            },
new Buff()
            {
                name = "Dark Dance",
                job = JOB.DRK,
                CD = 60
            },
new Buff()
            {
                name = "Elusive Jump",
                job = JOB.DRG,
                CD = 180
            },
new Buff()
            {
                name = "Spineshatter Dive",
                job = JOB.DRG,
                CD = 60
            },
new Buff()
            {
                name = "Power Surge",
                job = JOB.DRG,
                CD = 60
            },
new Buff()
            {
                name = "Dragonfire Dive",
                job = JOB.DRG,
                CD = 120
            },
new Buff()
            {
                name = "Battle Litany",
                job = JOB.DRG,
                CD = 180
            },
new Buff()
            {
                name = "Blood of the Dragon",
                job = JOB.DRG,
                CD = 60
            },
new Buff()
            {
                name = "Blood for Blood",
                job = JOB.DRG,
                CD = 80
            },
new Buff()
            {
                name = "Blood for Blood",
                job = JOB.BRD,
                CD = 80
            },
new Buff()
            {
                name = "Blood for Blood",
                job = JOB.MNK,
                CD = 80
            },
new Buff()
            {
                name = "Blood for Blood",
                job = JOB.MCH,
                CD = 80
            },
new Buff()
            {
                name = "Blood for Blood",
                job = JOB.NIN,
                CD = 80
            },
new Buff()
            {
                name = "Invigorate",
                job = JOB.DRG,
                CD = 120
            },
new Buff()
            {
                name = "Invigorate",
                job = JOB.BRD,
                CD = 120
            },
new Buff()
            {
                name = "Invigorate",
                job = JOB.MNK,
                CD = 120
            },
new Buff()
            {
                name = "Invigorate",
                job = JOB.MCH,
                CD = 120
            },
new Buff()
            {
                name = "Invigorate",
                job = JOB.NIN,
                CD = 120
            },
new Buff()
            {
                name = "Rampart",
                job = JOB.PLD,
                CD = 90
            },
new Buff()
            {
                name = "Fight or Flight",
                job = JOB.PLD,
                CD = 90
            },
new Buff()
            {
                name = "Convalescence",
                job = JOB.PLD,
                CD = 120
            },
new Buff()
            {
                name = "Convalescence",
                job = JOB.DRK,
                CD = 120
            },
new Buff()
            {
                name = "Convalescence",
                job = JOB.WAR,
                CD = 120
            },
new Buff()
            {
                name = "Cover",
                job = JOB.PLD,
                CD = 120
            },
new Buff()
            {
                name = "Bulwark",
                job = JOB.PLD,
                CD = 180
            },
new Buff()
            {
                name = "Sentinel",
                job = JOB.PLD,
                CD = 180
            },
new Buff()
            {
                name = "Awareness",
                job = JOB.PLD,
                CD = 120
            },
new Buff()
            {
                name = "Awareness",
                job = JOB.DRK,
                CD = 120
            },
new Buff()
            {
                name = "Awareness",
                job = JOB.WAR,
                CD = 120
            },
new Buff()
            {
                name = "Hallowed Ground",
                job = JOB.PLD,
                CD = 420
            },
new Buff()
            {
                name = "Rapid Fire",
                job = JOB.MCH,
                CD = 90
            },
new Buff()
            {
                name = "Dismantle",
                job = JOB.MCH,
                CD = 90
            },
new Buff()
            {
                name = "Rend Mind",
                job = JOB.MCH,
                CD = 90
            },
new Buff()
            {
                name = "Hypercharge",
                job = JOB.MCH,
                CD = 120
            },
new Buff()
            {
                name = "Ricochet",
                job = JOB.MCH,
                CD = 60
            },
new Buff()
            {
                name = "Reassemble",
                job = JOB.MCH,
                CD = 90
            },
new Buff()
            {
                name = "Reload",
                job = JOB.MCH,
                CD = 60
            },
new Buff()
            {
                name = "Unchained",
                job = JOB.WAR,
                CD = 120
            },
new Buff()
            {
                name = "Infuriate",
                job = JOB.WAR,
                CD = 60
            },
new Buff()
            {
                name = "Raw Intuition",
                job = JOB.WAR,
                CD = 90
            },
new Buff()
            {
                name = "Equilibrium",
                job = JOB.WAR,
                CD = 60
            },
new Buff()
            {
                name = "Holmgang",
                job = JOB.WAR,
                CD = 180
            },
new Buff()
            {
                name = "Berserk",
                job = JOB.WAR,
                CD = 90
            },
new Buff()
            {
                name = "Thrill of Battle",
                job = JOB.WAR,
                CD = 120
            },
new Buff()
            {
                name = "Foresight",
                job = JOB.WAR,
                CD = 90
            },
new Buff()
            {
                name = "Foresight",
                job = JOB.DRK,
                CD = 120
            },
new Buff()
            {
                name = "Foresight",
                job = JOB.PLD,
                CD = 120
            },
new Buff()
            {
                name = "Vengeance",
                job = JOB.WAR,
                CD = 120
            },
new Buff()
            {
                name = "Bloodbath",
                job = JOB.WAR,
                CD = 90
            },
new Buff()
            {
                name = "Bloodbath",
                job = JOB.DRK,
                CD = 90
            },
new Buff()
            {
                name = "Bloodbath",
                job = JOB.PLD,
                CD = 90
            },
new Buff()
            {
                name = "Kassatsu",
                job = JOB.NIN,
                CD = 120
            },
new Buff()
            {
                name = "Smoke Screen",
                job = JOB.NIN,
                CD = 180
            },
new Buff()
            {
                name = "Shadewalker",
                job = JOB.NIN,
                CD = 120
            },
new Buff()
            {
                name = "Duality",
                job = JOB.NIN,
                CD = 90
            },
new Buff()
            {
                name = "Dream Within a Dream",
                job = JOB.NIN,
                CD = 90
            },
new Buff()
            {
                name = "Trick Attack",
                job = JOB.NIN,
                CD = 60
            },
new Buff()
            {
                name = "Goad",
                job = JOB.NIN,
                CD = 180
            },
new Buff()
            {
                name = "Internal Release",
                job = JOB.NIN,
                CD = 60
            },
new Buff()
            {
                name = "Internal Release",
                job = JOB.MNK,
                CD = 60
            },
new Buff()
            {
                name = "Internal Release",
                job = JOB.DRG,
                CD = 60
            },
new Buff()
            {
                name = "Internal Release",
                job = JOB.BRD,
                CD = 60
            },
new Buff()
            {
                name = "Internal Release",
                job = JOB.WAR,
                CD = 60
            },
new Buff()
            {
                name = "Mantra",
                job = JOB.MNK,
                CD = 120
            },
new Buff()
            {
                name = "Perfect Balance",
                job = JOB.MNK,
                CD = 180
            },
new Buff()
            {
                name = "Purification",
                job = JOB.MNK,
                CD = 120
            },
new Buff()
            {
                name = "Tornado Kick",
                job = JOB.MNK,
                CD = 60
            },
new Buff()
            {
                name = "Whispering Dawn",
                job = JOB.PET,
                CD = 60
            },
new Buff()
            {
                name = "Fey Illumination",
                job = JOB.PET,
                CD = 120
            },
new Buff()
            {
                name = "Fey Covenant",
                job = JOB.PET,
                CD = 60
            },
new Buff()
            {
                name = "Fey Wind",
                job = JOB.PET,
                CD = 60
            },
        };

        public class Buff
        {
            public string name = "";
            public JOB job = JOB.Unknown;
            public int CD = 0;

        }
        #endregion
        #region IActPluginV1 Members
        System.Windows.Forms.Label lblStatus;   // The status label that appears in ACT's Plugin tab
        string settingsFile = System.IO.Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, "Config\\CDTracker.config.xml");
        SettingsSerializer xmlSettings;
        public void InitPlugin(TabPage pluginScreenSpace, System.Windows.Forms.Label pluginStatusText)
        {
            lblStatus = pluginStatusText;   // Hand the status label's reference to our local var
            pluginScreenSpace.Controls.Add(this);   // Add this UserControl to the tab ACT provides
            this.Dock = DockStyle.Fill; // Expand the UserControl to fill the tab's client space
            xmlSettings = new SettingsSerializer(this); // Create a new settings serializer and pass it this instance
            LoadSettings();

            // Create some sort of parsing event handler.  After the "+=" hit TAB twice and the code will be generated for you.
            ActGlobals.oFormActMain.AfterCombatAction += new CombatActionDelegate(oFormActMain_AfterCombatAction);

            lblStatus.Text = "Plugin Started";
        }
        public void DeInitPlugin()
        {
            // Unsubscribe from any events you listen to when exiting!
            ActGlobals.oFormActMain.AfterCombatAction -= oFormActMain_AfterCombatAction;

            SaveSettings();
            lblStatus.Text = "Plugin Exited";
        }
        #endregion
        #region Settings
        void oFormActMain_AfterCombatAction(bool isImport, CombatActionEventArgs actionInfo)
        {
            // throw new NotImplementedException();
        }
        void LoadSettings()
        {

            if (File.Exists(settingsFile))
            {
                FileStream fs = new FileStream(settingsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                XmlTextReader xReader = new XmlTextReader(fs);

                try
                {
                    while (xReader.Read())
                    {
                        if (xReader.NodeType == XmlNodeType.Element)
                        {
                            if (xReader.LocalName == "SettingsSerializer")
                            {
                                xmlSettings.ImportFromXml(xReader);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    lblStatus.Text = "Error loading settings: " + ex.Message;
                }
                xReader.Close();
            }
        }
        void SaveSettings()
        {
            FileStream fs = new FileStream(settingsFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            XmlTextWriter xWriter = new XmlTextWriter(fs, Encoding.UTF8);
            xWriter.Formatting = Formatting.Indented;
            xWriter.Indentation = 1;
            xWriter.IndentChar = '\t';
            xWriter.WriteStartDocument(true);
            xWriter.WriteStartElement("Config");    // <Config>
            xWriter.WriteStartElement("SettingsSerializer");    // <Config><SettingsSerializer>
            xmlSettings.ExportToXml(xWriter);   // Fill the SettingsSerializer XML
            xWriter.WriteEndElement();  // </SettingsSerializer>
            xWriter.WriteEndElement();  // </Config>
            xWriter.WriteEndDocument(); // Tie up loose ends (shouldn't be any)
            xWriter.Flush();    // Flush the file buffer to disk
            xWriter.Close();
        }
        #endregion
    }
    public class Combatant
    {
        public uint ID;
        public uint OwnerID;
        public int Order;
        public byte type;
        public int Job;
        public int Level;
        public string Name;
        public int CurrentHP;
        public int MaxHP;
        public int CurrentMP;
        public int MaxMP;
        public int CurrentTP;
    }
    public class NativeMethods
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern uint GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        public static string GetActiveWindowTitle()
        {
            const int nChars = 64;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }
    }
    public enum JOB : byte
    {
        Unknown = 0x0,
        GLD = 0x1,
        PGL = 0x2,
        MRD = 0x3,
        LNC = 0x4,
        ARC = 0x5,
        CNJ = 0x6,
        THM = 0x7,
        CPT = 0x8,
        BSM = 0x9,
        ARM = 0xA,
        GSM = 0xB,
        LTW = 0xC,
        WVR = 0xD,
        ALC = 0xE,
        CUL = 0xF,
        MIN = 0x10,
        BOT = 0x11,
        FSH = 0x12,
        PLD = 0x13,
        MNK = 0x14,
        WAR = 0x15,
        DRG = 0x16,
        BRD = 0x17,
        WHM = 0x18,
        BLM = 0x19,
        ACN = 0x1A,
        SMN = 0x1B,
        SCH = 0x1C,
        ROG = 0x1D,
        NIN = 0x1E,
        MCH = 0x1F,
        DRK = 0x20,
        AST = 0x21,
        PET = 100
    }
    public enum TYPE : byte
    {
        Unknown = 0x00,
        Player = 0x01,
        MOB = 0x02,
        NPC = 0x03,
        Aetheryte = 0x05,
        Gathering = 0x06,
        Minion = 0x09
    }
}
