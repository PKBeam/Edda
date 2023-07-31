using Edda.Const;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Edda.Windows
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            TxtVersionNumber.Text = $"version {Program.DisplayVersionString}";
        }

        private void TxtGithubLink_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Helper.OpenWebUrl(TxtGithubLink.Text);
        }

        private void TxtRagnacustomsLink_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Helper.OpenWebUrl(TxtRagnacustomsLink.Text);
        }

        private void TxtGithubLink_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.Hand;
        }

        private void TxtGithubLink_MouseLeave(object sender, MouseEventArgs e)
        {
            this.Cursor = null;
        }

        private void TxtRagnacustomsLink_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.Hand;
        }

        private void TxtRagnacustomsLink_MouseLeave(object sender, MouseEventArgs e)
        {
            this.Cursor = null;
        }
    }
}
