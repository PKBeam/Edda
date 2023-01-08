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
            PauseSong();
            CreateNewMap();
        }
        private void MenuItemOpenMap_Click(object sender, RoutedEventArgs e) {
            PauseSong();
            OpenMap();
        }
        private void MenuItemSaveMap_Click(object sender, RoutedEventArgs e) {
            BackupAndSaveBeatmap();
        }
        private void MenuItemCloseMap_Click(object sender, RoutedEventArgs e) {
            PauseSong();
            returnToStartMenuOnClose = true;
            this.Close();
        }
        private void MenuItemExportMap_Click(object sender, RoutedEventArgs e) {
            PauseSong();
            ExportMap();
        }

        // Edit
        private void MenuItemAddNote1_Click(object sender, RoutedEventArgs e) {
            editorUI.CreateNote(0, false);
            drummer.Play(0);
        }
        private void MenuItemAddNote2_Click(object sender, RoutedEventArgs e) {
            editorUI.CreateNote(1, false);
            drummer.Play(1);
        }
        private void MenuItemAddNote3_Click(object sender, RoutedEventArgs e) {
            editorUI.CreateNote(2, false);
            drummer.Play(2);
        }
        private void MenuItemAddNote4_Click(object sender, RoutedEventArgs e) {
            editorUI.CreateNote(3, false);
            drummer.Play(3);
        }
        private void MenuItemAddBookmark_Click(object sender, RoutedEventArgs e) {
            PauseSong();
            editorUI.CreateBookmark(false);
        }
        private void MenuItemAddTimingChange_Click(object sender, RoutedEventArgs e) {
            PauseSong();
            editorUI.CreateBPMChange(false, false);
        }
        private void MenuItemAddSnappedTimingChange_Click(object sender, RoutedEventArgs e) {
            PauseSong();
            editorUI.CreateBPMChange(true, false);
        }
        private void MenuItemUndo_Click(object sender, RoutedEventArgs e) {
            if (songIsPlaying) {
                return;
            }
            mapEditor.Undo();
        }
        private void MenuItemRedo_Click(object sender, RoutedEventArgs e) {
            if (songIsPlaying) {
                return;
            }
            mapEditor.Redo();
        }
        private void MenuItemSelectAll_Click(object sender, RoutedEventArgs e) {
            if (songIsPlaying) {
                return;
            }
            mapEditor.SelectNewNotes(mapEditor.currentMapDifficulty.notes);
        }
        private void MenuItemCut_Click(object sender, RoutedEventArgs e) {
            if (songIsPlaying) {
                return;
            }
            mapEditor.CutSelection();
        }
        private void MenuItemCopy_Click(object sender, RoutedEventArgs e) {
            if (songIsPlaying) {
                return;
            }
            mapEditor.CopySelection();
        }
        private void MenuItemPaste_Click(object sender, RoutedEventArgs e) {
            if (songIsPlaying) {
                return;
            }
            editorUI.PasteClipboardWithOffset(false);
        }

        private void MenuItemPasteOnColumn_Click(object sender, RoutedEventArgs e) {
            if (songIsPlaying) {
                return;
            }
            editorUI.PasteClipboardWithOffset(true);
        }
        private void MenuItemMirrorSelected_Click(object sender, RoutedEventArgs e) {
            if (songIsPlaying) {
                return;
            }
            mapEditor.TransformSelection(NoteTransforms.Mirror());
        }
        private void MenuItemSnapToGrid_Click(object sender, RoutedEventArgs e) {
            bool newVal = MenuItemSnapToGrid.IsChecked == true;
            checkGridSnap.IsChecked = newVal;
            editorUI.snapToGrid = newVal;
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
        private void MenuItemSettings_Click(object sender, RoutedEventArgs e) {
            ShowUniqueWindow(() => new SettingsWindow(this, userSettings));
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
