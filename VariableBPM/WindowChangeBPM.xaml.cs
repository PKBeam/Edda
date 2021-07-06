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
    /// Interaction logic for WindowChangeBPM.xaml
    /// </summary>
    public partial class WindowChangeBPM : Window {

        MainWindow caller;
        double globalBPM;
        List<BPMChange> BPMChanges;

        public WindowChangeBPM(MainWindow caller, List<BPMChange> BPMChanges) {
            InitializeComponent();
            this.caller = caller;
            this.globalBPM = caller.globalBPM;
            this.BPMChanges = BPMChanges;
            dataBPMChange.ItemsSource = this.BPMChanges;
            lblGlobalBPM.Content = $"{Math.Round(globalBPM, 3)}";
        }

        private void btnExit_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }

        private void dataBPMChange_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) {
            if (e.EditAction == DataGridEditAction.Cancel) {
                return;
            }
            string col = e.Column.Header.ToString();
            string pendingEditText = ((TextBox)e.EditingElement).Text;
            // data validation
            try {
                double pendingEdit = Helper.DoubleParseInvariant(pendingEditText);
                // global beat
                if (col == dataBPMChange.Columns[0].Header.ToString()) {
                    if (pendingEdit < 0) {
                        throw new Exception("The beat must be a non-negative number.");
                    }
                // BPM
                } else if (col == dataBPMChange.Columns[1].Header.ToString()) {
                    if (pendingEdit <= 0) {
                        throw new Exception("The BPM must be a positive number.");
                    }
                // grid division
                } else if (col == dataBPMChange.Columns[2].Header.ToString()) {
                    if ((int)pendingEdit != pendingEdit || !Helper.DoubleRangeCheck(pendingEdit, 1, Constants.Editor.GridDivisionMax)) {
                        throw new Exception($"The grid division amount must be an integer from 1 to {Constants.Editor.GridDivisionMax}.");
                    }
                }
                
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                dataBPMChange.CancelEdit();
            }
        }

        private void dataBPMChange_CurrentCellChanged(object sender, EventArgs e) {
            caller.DrawEditorGrid();
        }
    }
}
