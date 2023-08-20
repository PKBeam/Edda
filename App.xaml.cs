using System.Diagnostics;
using System.Windows;
using Edda.Const;

namespace RagnarockEditor {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        internal RecentOpenedFolders RecentMaps;
        internal UserSettingsManager UserSettings;
        internal DiscordClient DiscordClient;

        App() {
            this.RecentMaps = new(Program.RecentOpenedMapsFile, Program.MaxRecentOpenedMaps);
            this.UserSettings = new UserSettingsManager(Program.SettingsFile);
            this.DiscordClient = new DiscordClient();

            if (UserSettings.GetValueForKey(UserSettingsKey.EnableDiscordRPC) == null) {
                UserSettings.SetValueForKey(UserSettingsKey.EnableDiscordRPC, DefaultUserSettings.EnableDiscordRPC);
            }
            SetDiscordRPC(UserSettings.GetBoolForKey(UserSettingsKey.EnableDiscordRPC));

            // for testing cultures
            //CultureInfo.CurrentCulture = CultureInfo.GetCultureInfoByIetfLanguageTag("fr-FR");
        }

        protected override void OnStartup(StartupEventArgs e) {
            try {
                if (UserSettings.GetBoolForKey(UserSettingsKey.CheckForUpdates)) {
                    //#if !DEBUG
                    Helper.CheckForUpdates();
                    //#endif
                }
            } catch {

                Trace.WriteLine("INFO: Could not check for updates.");
            }
            base.OnStartup(e);
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
