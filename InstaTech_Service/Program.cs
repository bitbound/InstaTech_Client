using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
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
                try
                {

                    Socket.WriteToLog("Install initiated.");
                    Process.Start("cmd.exe", "/c sc delete instatech_service").WaitForExit();
                    var procs = Process.GetProcessesByName("InstaTech_Service").Where(proc => proc.Id != Process.GetCurrentProcess().Id);
                    foreach (var proc in procs)
                    {
                        proc.Kill();
                    }

                    var di = Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\InstaTech\");
                    var installPath = di.FullName + Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    while (File.Exists(installPath))
                    {
                        try
                        {
                            File.Delete(installPath);
                        }
                        catch
                        {
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                    File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location, installPath, true);

                    foreach (var proc in Process.GetProcessesByName("Notifier"))
                    {
                        proc.Kill();
                    }
                    while (File.Exists(Path.Combine(di.FullName, "Notifier.exe")))
                    {
                        try
                        {
                            File.Delete(Path.Combine(di.FullName, "Notifier.exe"));
                        }
                        catch
                        {
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                    using (var rs = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("InstaTech_Service.Resources.Notifier.exe"))
                    {
                        using (var fs = new FileStream(Path.Combine(di.FullName, "Notifier.exe"), FileMode.Create))
                        {
                            rs.CopyTo(fs);
                            fs.Close();
                            rs.Close();
                        }
                    }

                    var serv = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == "InstaTech_Service");
                    if (serv == null)
                    {
                        string[] command;
                        if (args.Exists(str => str.ToLower() == "-once"))
                        {
                            command = new String[] { "/assemblypath=\"" + installPath + "\" -once" };
                        }
                        else
                        {
                            command = new String[] { "/assemblypath=" + installPath };
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
                    Socket.WriteToLog("Install completed.");
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Socket.WriteToLog(ex);
                    Environment.Exit(1);
                }
            }
            else if (args.Exists(str => str.ToLower() == "-uninstall"))
            {
                try
                {
                    Socket.WriteToLog("Uninstall initiated.");
                    Process.Start("cmd.exe", "/c sc delete instatech_service").WaitForExit();
                    var procs = Process.GetProcessesByName("InstaTech_Service").Where(proc => proc.Id != Process.GetCurrentProcess().Id);
                    foreach (var proc in procs)
                    {
                        proc.Kill();
                    }

                    // Remove Secure Attention Sequence policy to allow app to simulate Ctrl + Alt + Del.
                    var subkey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
                    if (subkey.GetValue("SoftwareSASGeneration") != null)
                    {
                        subkey.DeleteValue("SoftwareSASGeneration");
                    }
                    Socket.WriteToLog("Uninstall completed.");
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Socket.WriteToLog(ex);
                    Environment.Exit(1);
                }
            }
            else
            {
#if DEBUG
                Socket.StartService().Wait();
#else
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                new Service1()
                };
                ServiceBase.Run(ServicesToRun);
#endif
            }
        }
    }
}
