using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Notifier
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        System.Timers.Timer timer = new System.Timers.Timer(3000);
        public static MainWindow Current { get; set; }
        public MainWindow()
        {
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Prevent multiple Notifiers from running.
            foreach (var proc in System.Diagnostics.Process.GetProcessesByName("Notifier"))
            {
                if (proc.Id != System.Diagnostics.Process.GetCurrentProcess().Id)
                {
                    proc.Kill();
                }
            }
            Current = this;
            timer.Elapsed += (object sen, System.Timers.ElapsedEventArgs args) =>
            {
                var procs = System.Diagnostics.Process.GetProcessesByName("InstaTech_Service");
                if (procs.Where(proc => proc.SessionId == System.Diagnostics.Process.GetCurrentProcess().SessionId).Count() == 0)
                {
                    MainWindow.Current.Dispatcher.Invoke(() => { Close(); });
                }
            };
            timer.Start();
            var wa = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            Left = wa.Right - ActualWidth;
            Top = wa.Bottom - ActualHeight;
        }

        private async void gridToggleCollapse_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var wa = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            if (stackContent.ActualWidth > 0)
            {
                var rotate = (rectToggleExpanded.RenderTransform as TransformGroup).Children.FirstOrDefault(tr => tr is RotateTransform) as RotateTransform;
                rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-45, TimeSpan.FromSeconds(.5)));
                stackContent.BeginAnimation(WidthProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(.5)));
                while (stackContent.HasAnimatedProperties)
                {
                    Left = wa.Right - ActualWidth;
                    await Task.Delay(1);
                }
            }
            else
            {
                Left -= 240;
                var rotate = (rectToggleExpanded.RenderTransform as TransformGroup).Children.FirstOrDefault(tr => tr is RotateTransform) as RotateTransform;
                rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(135, TimeSpan.FromSeconds(.5)));
                stackContent.BeginAnimation(WidthProperty, new DoubleAnimation(240, TimeSpan.FromSeconds(.5)));
                while (stackContent.HasAnimatedProperties)
                {
                    Left = wa.Right - ActualWidth;
                    await Task.Delay(1);
                }
            }
        }
    }
}
