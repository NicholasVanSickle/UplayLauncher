using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using UplayStarter.Properties;
using Timer = System.Windows.Forms.Timer;

namespace UplayStarter
{
    public partial class UplayStarterForm : Form
    {
        private Timer _timer = new Timer();

        private ContextMenu _trayMenu;
        private NotifyIcon _trayIcon;
        private MenuItem _toggleItem;

        public UplayStarterForm()
        {
            InitializeComponent();

            MinimumSize = MaximumSize = Size;
            FormBorderStyle = FormBorderStyle.Fixed3D;

            _trayMenu = new ContextMenu();
            _toggleItem = new MenuItem("Enabled", (x,y) => checkBox1.Checked = !checkBox1.Checked);
            _toggleItem.Checked = checkBox1.Checked;
            _trayMenu.MenuItems.Add(_toggleItem);
            _trayMenu.MenuItems.Add(new MenuItem("Exit", (o, e) => Close()));

            _defaultBackColor = BackColor;
            UpdateAero();

            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "Uplay Starter";
            _trayIcon.ContextMenu = _trayMenu;
            _trayIcon.Icon = Icon;
            _trayIcon.Visible = false;

            Closing += (o, e) =>
                           {
                               if (_trayIcon.Visible)
                               {
                                   _trayIcon.Visible = false;
                                   return;
                               }
                               _trayIcon.Visible = true;
                               _trayIcon.ShowBalloonTip(3000, "Uplay Starter Still Running!", "Uplay Starter will run in the background until closed.", ToolTipIcon.Info);
                               Visible = ShowInTaskbar = false;
                               e.Cancel = true;
                           };

            _trayIcon.MouseDoubleClick += (o, e) =>
                                              {
                                                  _trayIcon.Visible = false;
                                                  Visible = ShowInTaskbar = true;
                                              };

            VisibleChanged += (o, e) => UpdateAero();

            _timer.Interval = 500;
            _timer.Tick += (o, e) => processTick();
        }

        private Color _defaultBackColor;

        private void UpdateAero()
        {
            int en = 0;
            Win32API.MARGINS mg = new Win32API.MARGINS();
            mg.cxLeftWidth = mg.cxRightWidth = mg.cyTopHeight = mg.cyBottomHeight = checkBox1.Left * 2;

            bool transpaerent = false;
            //make sure you are not on a legacy OS 
            if (Environment.OSVersion.Version.Major >= 6)
            {
                Win32API.DwmIsCompositionEnabled(ref en);
                //check if the desktop composition is enabled

                if (en > 0)
                {
                    Win32API.DwmExtendFrameIntoClientArea(this.Handle, ref mg);
                    transpaerent = true;
                }
            }

            if (transpaerent)
                BackColor = Color.Black;
            else
                BackColor = DefaultBackColor;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            _toggleItem.Checked = checkBox1.Checked;
            if(checkBox1.Checked)
            {
                checkBox1.Text = Resources.Form1_checkBox1_CheckedChanged_Uplay_Starter_On;
                _timer.Enabled = true;
            }
            else
            {
                checkBox1.Text = Resources.Form1_checkBox1_CheckedChanged_Uplay_Starter_Off;
                _timer.Enabled = false;
            }
        }

        public static List<Tuple<string,int>> ListChildProcesses(int processID)
        {
            String machineName = "localhost";
            String myQuery = string.Format("select * from win32_process where ParentProcessId={0}", processID);
            ManagementScope mScope = new ManagementScope(string.Format(@"\\{0}\root\cimv2", machineName), null);
            mScope.Connect();

            var children = new List<Tuple<string, int>>();

            if (mScope.IsConnected)
            {
                ObjectQuery objQuery = new ObjectQuery(myQuery);
                using (ManagementObjectSearcher objSearcher = new ManagementObjectSearcher(mScope, objQuery))
                {
                    using (ManagementObjectCollection result = objSearcher.Get())
                    {
                        foreach (ManagementObject item in result)
                        {
                            children.Add(new Tuple<string, int>(item["Name"].ToString(), int.Parse(item["ProcessId"].ToString())));
                        }
                    }
                }
            }
            return children;
        }

        private int uplayProcess;
        private int gameProcess;
        private string gameProcessName;

        private void processTick()
        {
            if (gameProcessName != null)
            {
                foreach (var process in Process.GetProcesses().Where(x => x.Id == gameProcess))
                {
                    if (process.HasExited || process.ProcessName.ToLower() != gameProcessName)
                        break;
                    return;
                }
                gameProcessName = null;
                Thread.Sleep(5000);
                foreach (var process in Process.GetProcesses())
                {
                    if (process.Id == uplayProcess)
                    {
                        process.Kill();
                    }
                }
                return;
            }

            Win32API.EnumWindows((hwnd, data) =>
            {
                StringBuilder sb = new StringBuilder(1024);
                Win32API.GetClassName(hwnd, sb, sb.Capacity);
                if(sb.ToString() == "UplayPCWindowClass")
                {
                    Win32API.SetForegroundWindow(hwnd);

                    var inputs = new List<Win32API.INPUT>();

                    var keyInput = new Win32API.INPUT() {type = 1, U = new Win32API.InputUnion()};

                    keyInput.U.ki = new Win32API.KEYBDINPUT();
                    keyInput.U.ki.wVk = Win32API.VirtualKeyShort.TAB;
                    inputs.Add(keyInput);

                    keyInput.U.ki.time = 5;
                    keyInput.U.ki.dwFlags = Win32API.KEYEVENTF.KEYUP;
                    inputs.Add(keyInput);

                    keyInput.U.ki.time = 10;
                    keyInput.U.ki.wVk = Win32API.VirtualKeyShort.SPACE;
                    keyInput.U.ki.dwFlags = 0;
                    inputs.Add(keyInput);

                    keyInput.U.ki.time = 15;
                    keyInput.U.ki.dwFlags = Win32API.KEYEVENTF.KEYUP;
                    inputs.Add(keyInput);

                    Win32API.SendInput(inputs.Count, inputs.ToArray(), Win32API.INPUT.Size);

                    Thread.Sleep(500);

                    foreach (var process in Process.GetProcesses())
                    {
                        if (process.ProcessName.ToLower() == "uplay")
                        {
                            uplayProcess = process.Id;
                            var list = ListChildProcesses(process.Id);
                            if (list.Count == 0)
                                return true;
                            var child = list.First();
                            gameProcessName = child.Item1.ToLower().Replace(".exe","");
                            gameProcess = child.Item2;
                        }
                    }
                }
                return true;
            }, 0);
        }
    }
}
