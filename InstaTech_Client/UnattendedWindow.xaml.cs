using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace InstaTech_Client
{
    /// <summary>
    /// Interaction logic for UnattendedWindow.xaml
    /// </summary>
    public partial class UnattendedWindow : Window
    {
        public static UnattendedWindow Current { get; set; }
        public UnattendedWindow()
        {
            InitializeComponent();
            Current = this;
        }
        System.Timers.Timer timer = new System.Timers.Timer(1000);
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            setUI();
            timer.Elapsed += (object sen, System.Timers.ElapsedEventArgs args) => { setUI(); };
            timer.Start();
            this.Closing += (object sen, System.ComponentModel.CancelEventArgs args) =>
            {
                timer.Stop();
                timer.Dispose();
            };
        }
        private void buttonInstall_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InstaTech_Service.exe"), Properties.Resources.InstaTech_Service);
            }
            catch
            {
                MessageBox.Show("Failed to unpack the service into the temp directory.  Try clearing the temp directory.", "Write Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var psi = new ProcessStartInfo(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InstaTech_Service.exe"), "-install");
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            var proc = Process.Start(psi);
            proc.WaitForExit();
            setUI();
            if (proc.ExitCode == 0)
            {
                MessageBox.Show("Service installation successful.  If necessary, remember to configure access levels in the Computer Hub.  Otherwise, only admins will have access to this computer.", "Install Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MainWindow.WriteToLog("Service installation failed with exit code " + proc.ExitCode.ToString());
                MessageBox.Show("Service installation failed.  Please try again or contact the developer for support.", "Install Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void buttonRemove_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd", "/c sc delete InstaTech_Service");
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
                var allProcs = Process.GetProcessesByName("InstaTech_Service");
                foreach (var proc in allProcs)
                {
                    proc.Kill();
                }
                setUI();
                MessageBox.Show("Service removal successful.", "Removal Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                setUI();
                MainWindow.WriteToLog(ex);
                MessageBox.Show("Service removal failed.  Please try again or contact the developer for support.", "Removal Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            var services = System.ServiceProcess.ServiceController.GetServices();
            var itService = services.ToList().Find(sc => sc.ServiceName == "InstaTech_Service");
            itService.Start();
            setUI();
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e)
        {
            var services = System.ServiceProcess.ServiceController.GetServices();
            var itService = services.ToList().Find(sc => sc.ServiceName == "InstaTech_Service");
            itService.Stop();
            setUI();
        }
        private void setUI()
        {
            try
            {
                UnattendedWindow.Current.Dispatcher.Invoke(new Action(() => {
                    var services = System.ServiceProcess.ServiceController.GetServices();
                    var itService = services.ToList().Find(sc => sc.ServiceName == "InstaTech_Service");
                    if (itService != null)
                    {
                        textInstalled.Text = "Installed";
                        textInstalled.Foreground = new SolidColorBrush(Colors.Green);
                        buttonInstall.IsEnabled = false;
                        buttonRemove.IsEnabled = true;
                        if (itService.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                        {
                            textStatus.Text = "Running";
                            textStatus.Foreground = new SolidColorBrush(Colors.Green);
                            buttonStop.IsEnabled = true;
                            buttonStart.IsEnabled = false;
                        }
                        else
                        {
                            textStatus.Text = "Not Running";
                            textStatus.Foreground = new SolidColorBrush(Colors.Black);
                            buttonStart.IsEnabled = true;
                            buttonStop.IsEnabled = false;
                        }
                    }
                    else
                    {
                        textInstalled.Text = "Not Installed";
                        textInstalled.Foreground = new SolidColorBrush(Colors.Black);
                        buttonInstall.IsEnabled = true;
                        buttonRemove.IsEnabled = false;
                        textStatus.Text = "N/A";
                        textStatus.Foreground = new SolidColorBrush(Colors.Black);
                        buttonStart.IsEnabled = false;
                        buttonStop.IsEnabled = false;
                    }
                }));
            }
            catch
            { 
            }
        }
        private void buttonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
