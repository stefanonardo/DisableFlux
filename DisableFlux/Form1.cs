using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Management;
using System.Timers;
using Microsoft.Win32;

namespace FluxProcess
{
    public partial class Form1 : Form
    {
        int runningCount = 0;
        System.Timers.Timer timer = new System.Timers.Timer(3601000);

        public Form1(string[] args)
        {
            InitializeComponent();

            if (args.Length > 0 && "--start-minimized".Equals(args[0]))
            {
                WindowState = FormWindowState.Minimized;
            }
        }

        /* It loads user settings. */
        private void Form1_Load(object sender, EventArgs e)
        {
            timer.Elapsed += new ElapsedEventHandler(OnTimedEvent);

            if (Properties.Settings.Default.RunAtStartup)
            {
                checkBox1.Checked = true;
            }

            foreach (string processName in Properties.Settings.Default.ProgramsList)
            {
                listBox1.Items.Add(processName);
            }

            for (int i = listBox1.Items.Count - 1; i >= 0; --i)
            {
                OnChangedList(listBox1.Items[i].ToString(), false);
            }

            WaitForProcess();
        }

        private void buttonFileDialog_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog1 = new OpenFileDialog())
            {
                openFileDialog1.InitialDirectory = @"C:\";
                openFileDialog1.Filter = "Applications (*.exe)|*.exe";

                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    string fileName = openFileDialog1.SafeFileName;

                    if (listBox1.Items.IndexOf(fileName) >= 0)
                    {
                        return;
                    }
                    listBox1.Items.Add(fileName);
                    Properties.Settings.Default.ProgramsList.Add(fileName);
                    Properties.Settings.Default.Save();

                    OnChangedList(fileName, false);
                }
            }
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            string curItem = listBox1.SelectedItem.ToString();
            Properties.Settings.Default.ProgramsList.Remove(curItem);
            Properties.Settings.Default.Save();
            listBox1.Items.Remove(listBox1.SelectedItem);

            OnChangedList(curItem, true);
        }

        /* It starts process watchers. */
        private void WaitForProcess()
        {
            ManagementEventWatcher startWatch = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            startWatch.EventArrived
                += new EventArrivedEventHandler(startWatch_EventArrived);
            startWatch.Start();

            ManagementEventWatcher stopWatch = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
            stopWatch.EventArrived
                += new EventArrivedEventHandler(stopWatch_EventArrived);
            stopWatch.Start();
        }

        private void startWatch_EventArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                string processName = e.NewEvent.Properties["ProcessName"].Value.ToString();

                if (listBox1.Items.IndexOf(processName) >= 0)
                {
                    if (runningCount == 0)
                    {
                        SendKeys.SendWait("%({END})");
                        timer.Start();
                    }
                    ++runningCount;
                }
            }
            finally
            {
                e.NewEvent.Dispose();
            }
        }

        private void stopWatch_EventArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                string processName = e.NewEvent.Properties["ProcessName"].Value.ToString();

                if (processName.IndexOf(".exe") == -1)
                {
                    processName += ".exe";
                }

                if (listBox1.Items.IndexOf(processName) >= 0)
                {
                    if (runningCount == 1)
                    {
                        SendKeys.SendWait("%({END})");
                        timer.Stop();
                    }
                    --runningCount;
                }
            }
            finally
            {
                e.NewEvent.Dispose();
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (runningCount > 0)
            {
                SendKeys.SendWait("%({END})");
            }
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            SendKeys.SendWait("%({END})");
            timer.Stop();
            timer.Start();
        }

        /* It checks the state of the app when listBox changes. */
        private void OnChangedList(string fileName, bool deleting)
        {
            int isRunning = Process.GetProcessesByName(fileName.Substring(0, fileName.Length - 4)).Length;
            switch (deleting)
            {
                case false:
                    if (isRunning > 0 && runningCount == 0)
                    {
                        SendKeys.SendWait("%({END})");
                        ++runningCount;
                        timer.Start();
                    }
                    else if (isRunning > 0 && runningCount > 0)
                    {
                        ++runningCount;
                    }
                    break;
                case true:
                    if (isRunning > 0 && runningCount == 1)
                    {
                        SendKeys.SendWait("%({END})");
                        --runningCount;
                        timer.Stop();
                    }
                    else if (isRunning > 0 && runningCount > 1)
                    {
                        --runningCount;
                    }
                    break;
            }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            notifyIcon1.BalloonTipTitle = "Minimize to Tray App";
            notifyIcon1.BalloonTipText = "The app has been minimized.";

            if (WindowState == FormWindowState.Minimized)
            {
                notifyIcon1.Visible = true;
                Hide();
                if (Properties.Settings.Default.FirstStart)
                {
                    notifyIcon1.ShowBalloonTip(500);
                    Properties.Settings.Default.FirstStart = false;
                    Properties.Settings.Default.Save();
                }
            }
            else if (WindowState == FormWindowState.Normal)
            {
                notifyIcon1.Visible = false;
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            using (RegistryKey rkApp = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (checkBox1.Checked)
                {
                    Properties.Settings.Default.RunAtStartup = true;
                    Properties.Settings.Default.Save();
                    rkApp.SetValue("DisableFlux", Application.ExecutablePath.ToString() + " --start-minimized");
                }
                else
                {
                    Properties.Settings.Default.RunAtStartup = false;
                    Properties.Settings.Default.Save();
                    rkApp.DeleteValue("DisableFlux", false);
                }
            }
        }
    }
}
