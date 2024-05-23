using Edda.Const;
using Edda.Windows;
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
        private void MenuItemImportMap_Click(object sender, RoutedEventArgs e) {
            PauseSong();
            ImportMap();
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
            gridController.AddNoteAt(0, false);
            drummer?.Play(0);
        }
        private void MenuItemAddNote2_Click(object sender, RoutedEventArgs e) {
            gridController.AddNoteAt(1, false);
            drummer?.Play(1);
        }
        private void MenuItemAddNote3_Click(object sender, RoutedEventArgs e) {
            gridController.AddNoteAt(2, false);
            drummer?.Play(2);
        }
        private void MenuItemAddNote4_Click(object sender, RoutedEventArgs e) {
            gridController.AddNoteAt(3, false);
            drummer?.Play(3);
        }
        private void MenuItemAddBookmark_Click(object sender, RoutedEventArgs e) {
            PauseSong();
            gridController.CreateBookmark(false);
        }
        private void MenuItemAddTimingChange_Click(object sender, RoutedEventArgs e) {
            PauseSong();
            gridController.CreateBPMChange(false, false);
        }
        private void MenuItemAddSnappedTimingChange_Click(object sender, RoutedEventArgs e) {
            PauseSong();
            gridController.CreateBPMChange(true, false);
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
            mapEditor.SelectAllNotes();
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
        private void MenuItemQuantize_Click(object sender, RoutedEventArgs e) {
            if (songIsPlaying) {
                return;
            }
            mapEditor.QuantizeSelection();
        }
        private void MenuItemPaste_Click(object sender, RoutedEventArgs e) {
            if (songIsPlaying) {
                return;
            }
            gridController.PasteClipboardWithOffset(false);
        }

        private void MenuItemPasteOnColumn_Click(object sender, RoutedEventArgs e) {
            if (songIsPlaying) {
                return;
            }
            gridController.PasteClipboardWithOffset(true);
        }
        private void MenuItemMirrorSelected_Click(object sender, RoutedEventArgs e) {
            if (songIsPlaying) {
                return;
            }
            mapEditor.MirrorSelection();
        }
        private void MenuItemSnapToGrid_Click(object sender, RoutedEventArgs e) {
            bool newVal = MenuItemSnapToGrid.IsChecked == true;
            checkGridSnap.IsChecked = newVal;
            gridController.snapToGrid = newVal;
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
        private void MenuItemDifficultyPredictor_Click(object sender, RoutedEventArgs e) {
            ShowUniqueWindow(() => new DifficultyPredictorWindow(this, userSettings));
        }

        private void MenuItemClearCache_Click(object sender, RoutedEventArgs e) {
            MessageBoxResult res = MessageBox.Show(this, "This will delete all of the cached spectrogram images, which will cause it to load slower next time you open this map. Do you want to proceed?", "Warning", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes) {
                ClearSongCache();
            }
        }

        private void MenuItemSettings_Click(object sender, RoutedEventArgs e) {
            ShowUniqueWindow(() => new SettingsWindow(this, userSettings));
        }

        // Help
        private void MenuItemCheckUpdates_Click(object sender, RoutedEventArgs e) {
            try {
                if (!Helper.CheckForUpdates()) {
                    MessageBox.Show(this, $"You are already using the latest version of Edda.", "No updates found", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            } catch {
                MessageBox.Show(this, $"Could not check for updates.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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