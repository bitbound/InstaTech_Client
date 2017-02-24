using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Win32_Classes;

namespace InstaTech_Service
{
    static class Program
    {
        static void Main()
        {
            var args = Environment.GetCommandLineArgs().ToList();
            // If "-interactive" switch present, run service as an interactive console app.
            if (args.Exists(str => str.ToLower() == "-interactive"))
            {
                Socket.StartInteractive().Wait();
            }
            else if (args.Exists(str=>str.ToLower() == "-install"))
            {
                var di = Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\InstaTech\");
                try
                {
                    File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location, di.FullName + Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location), true);
                    using (var rs = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("InstaTech_Service.Resources.Notifier.exe"))
                    {
                        using (var fs = new FileStream(Path.Combine(di.FullName, "Notifier.exe"), FileMode.Create))
                        {
                            rs.CopyTo(fs);
                            fs.Close();
                            rs.Close();
                        }
                    }
                }
                catch { }
                

                var serv = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == "InstaTech_Service");
                if (serv == null)
                {
                    string[] command;
                    if (args.Exists(str => str.ToLower() == "-once"))
                    {
                        command = new String[] { "/assemblypath=\"" + di.FullName + Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\" -once" };
                    }
                    else
                    {
                        command = new String[] { "/assemblypath=" + di.FullName + Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location) };
                    }
                    ServiceInstaller ServiceInstallerObj = new ServiceInstaller();
                    InstallContext Context = new InstallContext("", command);
                    ServiceInstallerObj.Context = Context;
                    ServiceInstallerObj.DisplayName = "InstaTech Service";
                    ServiceInstallerObj.Description = "Background service that accepts connections for the InstaTech Client.";
                    ServiceInstallerObj.ServiceName = "InstaTech_Service";
                    ServiceInstallerObj.StartType = ServiceStartMode.Automatic;
                    ServiceInstallerObj.DelayedAutoStart = true;
                    ServiceInstallerObj.Parent = new ServiceProcessInstaller();

                    System.Collections.Specialized.ListDictionary state = new System.Collections.Specialized.ListDictionary();
                    ServiceInstallerObj.Install(state);
                }
                serv = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == "InstaTech_Service");
                if (serv != null && serv.Status != ServiceControllerStatus.Running)
                {
                    serv.Start();
                }
                var psi = new ProcessStartInfo("cmd.exe", "/c sc.exe failure \"InstaTech_Service\" reset=5 actions=restart/5000");
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);

                // Set Secure Attention Sequence policy to allow app to simulate Ctrl + Alt + Del.
                var subkey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
                subkey.SetValue("SoftwareSASGeneration", "3", Microsoft.Win32.RegistryValueKind.DWord);
            }
            else if (args.Exists(str => str.ToLower() == "-uninstall"))
            {
                var serv = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == "InstaTech_Service");
                if (serv != null)
                {
                    serv.Stop();
                    ServiceInstaller ServiceInstallerObj = new ServiceInstaller();
                    ServiceInstallerObj.Context = new InstallContext("", null); ;
                    ServiceInstallerObj.ServiceName = "InstaTech_Service";
                    ServiceInstallerObj.Uninstall(null);
                }
                
                // Remove Secure Attention Sequence policy to allow app to simulate Ctrl + Alt + Del.
                var subkey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
                if (subkey.GetValue("SoftwareSASGeneration") != null)
                {
                    subkey.DeleteValue("SoftwareSASGeneration");
                }
                Environment.Exit(0);
            }
            else if (args.Exists(str => str.ToLower() == "-update"))
            {
                Socket.WriteToLog("Update install initiated.");
                var procs = Process.GetProcessesByName("InstaTech_Service").Where(proc => proc.Id != Process.GetCurrentProcess().Id);
                foreach (var proc in procs)
                {
                    proc.Kill();
                }
                Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location, "-uninstall").WaitForExit();
                Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location, "-install");
                Socket.WriteToLog("Update completed.");
                Environment.Exit(0);
                return;
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                new Service1()
                };
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}
