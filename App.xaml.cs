using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace RagnarockEditor {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        internal RecentOpenedFolders RecentMaps;
        internal UserSettings UserSettings;
        internal DiscordClient DiscordClient;

        App() {
            this.RecentMaps = new(Const.Program.RecentOpenedMapsFile, Const.Program.MaxRecentOpenedMaps);
            this.UserSettings = new UserSettings(Const.Program.SettingsFile);
            this.DiscordClient = new DiscordClient();

            if (UserSettings.GetValueForKey(Const.UserSettings.EnableDiscordRPC) == null) {
                UserSettings.SetValueForKey(Const.UserSettings.EnableDiscordRPC, Const.DefaultUserSettings.EnableDiscordRPC);
            }
            SetDiscordRPC(UserSettings.GetBoolForKey(Const.UserSettings.EnableDiscordRPC));

        }

        public void SetDiscordRPC(bool enable) {
            if (enable) {
                DiscordClient.Enable();
            } else {
                DiscordClient.Disable();
            }
        }
    }
}
