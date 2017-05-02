using System;
using System.Collections.Generic;
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
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }
        public string Version
        {
            get
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }
        private void hyperWebsite_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://translucency.info");
        }

        private void hyperInstaTechWebsite_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://instatech.org");
        }

        private void hyperChangeLog_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://demo.instatech.org/Docs/InstaTech_Win_ChangeLog.html");
        }

        private void hyperLicense_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://instatech.org/Docs/InstaTech_License.html");
        }

        private void buttonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
