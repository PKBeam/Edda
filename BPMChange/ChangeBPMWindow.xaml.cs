﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Edda {
    /// <summary>
    /// Interaction logic for WindowChangeBPM.xaml
    /// </summary>
    public partial class ChangeBPMWindow : Window {

        MainWindow caller;
        double globalBPM;
        List<BPMChange> BPMChanges;

        public ChangeBPMWindow(MainWindow caller, List<BPMChange> BPMChanges) {
            InitializeComponent();
            this.caller = caller;
            this.globalBPM = caller.globalBPM;
            this.BPMChanges = BPMChanges;
            dataBPMChange.ItemsSource = this.BPMChanges;
            lblGlobalBPM.Content = $"{Math.Round(globalBPM, 3)}";
            
            //dataBPMChange.Items.SortDescriptions.Add(new SortDescription("Global Beat", ListSortDirection.Ascending));
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
                    if ((int)pendingEdit != pendingEdit || !Helper.DoubleRangeCheck(pendingEdit, 1, Const.Editor.GridDivisionMax)) {
                        throw new Exception($"The grid division amount must be an integer from 1 to {Const.Editor.GridDivisionMax}.");
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

        private void dataBPMChange_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e) {
            // commit edit
            dataBPMChange.RowEditEnding -= dataBPMChange_RowEditEnding;
            dataBPMChange.CommitEdit();
            dataBPMChange.RowEditEnding += dataBPMChange_RowEditEnding;


            dataBPMChange.ItemsSource = null;
            BPMChanges.Sort();
            caller.DrawEditorGrid();
            dataBPMChange.ItemsSource = this.BPMChanges;
        }
    }
}
