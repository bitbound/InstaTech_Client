using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace InstaTech_Service
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
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
                var di = Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\InstaTech\");
                try
                {
                    File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location, di.FullName + Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location), true);
                }
                catch { }

                ServiceInstaller ServiceInstallerObj = new ServiceInstaller();
                InstallContext Context = new InstallContext("", new String[] { "/assemblypath=" + di.FullName + Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location)});
                ServiceInstallerObj.Context = Context;
                ServiceInstallerObj.DisplayName = "InstaTech Service";
                ServiceInstallerObj.Description = "Background service that accepts connections for the InstaTech Client.";
                ServiceInstallerObj.ServiceName = "InstaTech_Service";
                ServiceInstallerObj.StartType = ServiceStartMode.Automatic;
                ServiceInstallerObj.Parent = new ServiceProcessInstaller();

                System.Collections.Specialized.ListDictionary state = new System.Collections.Specialized.ListDictionary();
                ServiceInstallerObj.Install(state);

                var serv = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == "InstaTech_Service");
                if (serv != null)
                {
                    serv.Start();
                }
            }
            else if (args.Exists(str => str.ToLower() == "-uninstall"))
            {
                var serv = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == "InstaTech_Service");
                if (serv != null)
                {
                    serv.Stop();
                }
                ServiceInstaller ServiceInstallerObj = new ServiceInstaller();
                ServiceInstallerObj.Context = new InstallContext("", null); ;
                ServiceInstallerObj.ServiceName = "InstaTech_Service";
                ServiceInstallerObj.Uninstall(null);
                Environment.Exit(0);
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
        static private async Task checkForUpdates()
        {
            WebClient webClient = new WebClient();
            HttpClient httpClient = new HttpClient();
            var strFilePath = System.IO.Path.GetTempPath() + Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            HttpResponseMessage response;
            if (File.Exists(strFilePath))
            {
                File.Delete(strFilePath);
            }
            try
            {
                response = await httpClient.GetAsync(Socket.versionURI);
            }
            catch
            {
                return;
            }
            var strCurrentVersion = await response.Content.ReadAsStringAsync();
            var thisVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var currentVersion = Version.Parse(strCurrentVersion);
            if (currentVersion > thisVersion)
            {

                await webClient.DownloadFileTaskAsync(new Uri(Socket.downloadURI), strFilePath);
                var serv = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == "InstaTech_Service");
                if (serv != null)
                {
                    serv.Stop();
                }
                Process.Start(strFilePath, "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"");
                Environment.Exit(0);
                return;
            }
        }
        static private void checkArgs(string[] args)
        {
            if (args.Length > 1 && File.Exists(args[1]))
            {
                var count = 0;
                var success = false;
                while (success == false)
                {
                    System.Threading.Thread.Sleep(200);
                    count++;
                    if (count > 25)
                    {
                        break;
                    }
                    try
                    {
                        File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location, args[1], true);
                        success = true;
                    }
                    catch
                    {
                        continue;
                    }
                }
                if (success)
                {
                    var serv = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == "InstaTech_Service");
                    if (serv != null)
                    {
                        serv.Start();
                    }
                    Environment.Exit(0);
                }
                return;
            }
        }
    }
}
