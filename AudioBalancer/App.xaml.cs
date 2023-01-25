using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace AudioBalancer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public MainWindow window { get; set; }
        public MainWindowViewModel windowViewModel => (MainWindowViewModel)window.DataContext;
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            
        }
    }
}
