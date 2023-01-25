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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AudioBalancer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel(this);
            App.Current.MainWindow = this;
        }

        private async void cbProcessName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await ((MainWindowViewModel)DataContext).ProcessChanged();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            await ((MainWindowViewModel)DataContext).SetVolumeBalance();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            ((MainWindowViewModel)DataContext).DoCleanUp();
        }
    }
}
