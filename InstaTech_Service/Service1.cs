using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Win32_Classes;

namespace InstaTech_Service
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            Socket.StartService();
        }
        protected override void OnStop()
        {
            if (Environment.GetCommandLineArgs().ToList().Exists(str => str.ToLower() == "-once"))
            {
                var thisProc = Process.GetCurrentProcess();
                var allProcs = Process.GetProcessesByName("InstaTech_Service");
                foreach (var proc in allProcs)
                {
                    if (proc.Id != thisProc.Id)
                    {
                        proc.Kill();
                    }
                }
                Process.Start("cmd", "/c sc delete InstaTech_Service");
            }
            base.OnStop();
        }
    }
}
