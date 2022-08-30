using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Edda.Const;
using NAudio.Gui;

namespace Edda
{
    /// <summary>
    /// Interaction logic for StartWindow.xaml
    /// </summary>
    public partial class StartWindow : Window {
        RecentOpenedFolders RecentMaps = ((RagnarockEditor.App)Application.Current).RecentMaps;

        // these definitions are to apply Windows 11-style rounded corners
        // https://docs.microsoft.com/en-us/windows/apps/desktop/modernize/apply-rounded-corners
        public enum DWMWINDOWATTRIBUTE {
            DWMWA_WINDOW_CORNER_PREFERENCE = 33
        }
        public enum DWM_WINDOW_CORNER_PREFERENCE {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,
            DWMWCP_ROUNDSMALL = 3
        }
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        internal static extern void DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref DWM_WINDOW_CORNER_PREFERENCE pvAttribute, uint cbAttribute);

        public StartWindow() {
            InitializeComponent();
            TxtVersionNumber.Text = $"version {Program.DisplayVersionString}";
            PopulateRecentlyOpenedMaps();

            // apply rounded corners
            IntPtr hWnd = new WindowInteropHelper(GetWindow(this)).EnsureHandle();
            var attribute = DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE;
            var preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
            DwmSetWindowAttribute(hWnd, attribute, ref preference, sizeof(uint));
        }

        private void CreateRecentMapItem(string name, string path) {
            /* The XAML we're creating
                < StackPanel Height = "30" Margin = "5" Orientation = "Horizontal" >
                    < Image Source = "/Resources/blankMap.png" />
                    < StackPanel Margin = "7 0 0 0" VerticalAlignment = "Center" >
                        < TextBlock Foreground = "#002668" FontSize = "14" FontWeight = "Bold" FontFamily = "Bahnschrift" > Song Name </ TextBlock >
                        < TextBlock FontSize = "11" FontFamily = "Bahnschrift SemiLight" > C:/ SongPath </ TextBlock >
                    </ StackPanel >
                </ StackPanel >
            */
            StackPanel sp1 = new();
            sp1.Height = 30;
            sp1.Margin = new Thickness(5);
            sp1.Orientation = Orientation.Horizontal;

            Image img = new();
            img.Source = Helper.BitmapGenerator("blankMap.png");

            StackPanel sp2 = new();
            sp2.Margin = new(7, 0, 0, 0);
            sp2.VerticalAlignment = VerticalAlignment.Center;

            TextBlock tb1 = new();
            tb1.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#002668");
            tb1.FontSize = 14;
            tb1.FontWeight = FontWeights.Bold;
            tb1.FontFamily = new("Bahnschrift");
            if (string.IsNullOrWhiteSpace(name)) {
                tb1.FontStyle = FontStyles.Italic;
            }
            tb1.Text = string.IsNullOrWhiteSpace(name) ? "Untitled Map" : name;

            TextBlock tb2 = new();
            tb2.FontSize = 11;
            tb2.FontFamily = new("Bahnschrift SemiLight");
            tb2.Text = path;

            sp2.Children.Add(tb1);
            sp2.Children.Add(tb2);

            sp1.Children.Add(img);
            sp1.Children.Add(sp2);

            ListViewItem item = new();
            item.Content = sp1;
            item.MouseLeftButtonUp += new MouseButtonEventHandler((sender, e) => { 
                item.IsSelected = false; 
                OpenMap(path); 
            });
            item.MouseRightButtonUp += new MouseButtonEventHandler((sender, e) => { 
                var res = MessageBox.Show("Are you sure you want to remove this map from the list of recently opened maps?", "Confirm Removal", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes) {
                    ListViewRecentMaps.Items.Remove(item);
                    RecentMaps.RemoveRecentlyOpened(path);
                    RecentMaps.Write();
                }
            });
            ListViewRecentMaps.Items.Add(item);
        }

        private void PopulateRecentlyOpenedMaps() {
            ListViewRecentMaps.Items.Clear();
            foreach (var recentMap in RecentMaps.GetRecentlyOpened()) {
                CreateRecentMapItem(recentMap.Item1, recentMap.Item2);
            }
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
            OpenMap(mapFolder);
        }

        private void OpenMap(string folder, string mapName = null) {
            if (folder == null) {
                return;
            }
            MainWindow main = new();
            this.Close();
            // NOTE: the window must be shown first before any processing can be done
            main.Show();
            try {
                main.InitOpenMap(folder);
            } catch (Exception ex) {
                MessageBox.Show($"An error occured while opening the map:\n{ex.Message}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                RecentMaps.RemoveRecentlyOpened(folder);
                RecentMaps.Write();
                
                new StartWindow().Show();
                main.Close();

            }
        }
    }
}
