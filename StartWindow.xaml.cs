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

namespace Edda {
    /// <summary>
    /// Interaction logic for StartWindow.xaml
    /// </summary>
    public partial class StartWindow : Window {

        UserSettings userSettings;
        public StartWindow() {
            InitializeComponent();
            userSettings = new UserSettings(Const.Program.SettingsFile);
            TxtVersionNumber.Text = $"version {Const.Program.DisplayVersionString}";
        }

        private void ButtonExit_Click(object sender, RoutedEventArgs e) {
            Environment.Exit(0);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            this.DragMove();
        }

        private void ButtonNewMap_Click(object sender, RoutedEventArgs e) {
            string newMapFolder = Helper.ChooseNewMapFolder();
            if (newMapFolder != null) {
                MainWindow main = new();
                this.Close();
                // NOTE: the window must be shown first before any processing can be done
                main.Show();
                main.InitNewMap(newMapFolder);
            }
        }

        private void ButtonOpenMap_Click(object sender, RoutedEventArgs e) {
            string mapFolder = Helper.ChooseOpenMapFolder();
            if (mapFolder != null) {
                MainWindow main = new();
                this.Close();
                // NOTE: the window must be shown first before any processing can be done
                main.Show();
                main.InitOpenMap(mapFolder);
            }
        }
    }
}
