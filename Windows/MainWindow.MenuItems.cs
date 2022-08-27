using Edda.Const;
using Edda.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Edda {
    public partial class MainWindow : Window {
        // File
        private void MenuItemNewMap_Click(object sender, RoutedEventArgs e) {
            CreateNewMap();
        }
        private void MenuItemOpenMap_Click(object sender, RoutedEventArgs e) {
            OpenMap();
        }
        private void MenuItemSaveMap_Click(object sender, RoutedEventArgs e) {
            BackupAndSaveBeatmap();
        }
        private void MenuItemCloseMap_Click(object sender, RoutedEventArgs e) {
            returnToStartMenuOnClose = true;
            this.Close();
        }
        private void MenuItemExportMap_Click(object sender, RoutedEventArgs e) {
            ExportMap();
        }

        // Edit
        private void MenuItemUndo_Click(object sender, RoutedEventArgs e) {
            mapEditor.Undo();
        }
        private void MenuItemRedo_Click(object sender, RoutedEventArgs e) {
            mapEditor.Redo();
        }
        private void MenuItemSelectAll_Click(object sender, RoutedEventArgs e) {
            mapEditor.SelectNewNotes(mapEditor.currentMapDifficulty.notes);
        }
        private void MenuItemCut_Click(object sender, RoutedEventArgs e) {
            mapEditor.CutSelection();
        }
        private void MenuItemCopy_Click(object sender, RoutedEventArgs e) {
            mapEditor.CopySelection();
        }
        private void MenuItemPaste_Click(object sender, RoutedEventArgs e) {
            editorUI.PasteClipboardWithOffset();
        }
        private void MenuItemMirrorSelected_Click(object sender, RoutedEventArgs e) {
            mapEditor.TransformSelection(NoteTransforms.Mirror());
        }
        private void MenuItemSettings_Click(object sender, RoutedEventArgs e) {
            ShowUniqueWindow(() => new SettingsWindow(this, userSettings));
        }

        // View
        private void MenuItemToggleLeftBar_Click(object sender, RoutedEventArgs e) {
            ToggleLeftDock();
        }
        private void MenuItemToggleRightBar_Click(object sender, RoutedEventArgs e) {
            ToggleRightDock();
        }
        
        // Tools
        private void MenuItemBpmFinder_Click(object sender, RoutedEventArgs e) {
            ShowUniqueWindow(() => new BPMCalcWindow());
        }
        
        // Help
        private void MenuItemCheckUpdates_Click(object sender, RoutedEventArgs e) {
            try {
                if (!Helper.CheckForUpdates()) {
                    MessageBox.Show($"You are already using the latest version of Edda.", "No updates found", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            } catch {
                MessageBox.Show($"Could not check for updates.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void MenuItemUserGuide_Click(object sender, RoutedEventArgs e) {
            Helper.OpenWebUrl(Program.UserGuideURL);
        }
        private void MenuItemAboutPage_Click(object sender, RoutedEventArgs e) {
            ShowUniqueWindow(() => new AboutWindow());
        }
    }
}
