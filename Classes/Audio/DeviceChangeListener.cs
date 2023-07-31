using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Windows;

namespace Edda
{
    public class DeviceChangeListener : IMMNotificationClient, IDisposable
    {
        MainWindow caller;

        public DeviceChangeListener(MainWindow caller)
        {
            this.caller = caller;
        }

        public void Dispose()
        {
            this.caller = null;
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia && (caller.playingOnDefaultDevice || !caller.defaultDeviceAvailable))
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    caller.UpdatePlaybackDevice(defaultDeviceId, true);
                });
            }
        }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
            if (pwstrDeviceId == caller.userPreferredPlaybackDeviceID)
            {
                // User preferred device was readded, so switch to it.
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    caller.UpdatePlaybackDevice(pwstrDeviceId, false);
                });
            }
        }

        public void OnDeviceRemoved(string deviceId)
        {
            if (caller.playbackDeviceID == deviceId)
            {
                // We force an update to default device in this case.
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    caller.UpdatePlaybackDevice(null, true);
                });
            }
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            if (caller.playbackDeviceID == deviceId && newState != DeviceState.Active)
            {
                // If the current device is not active anymore, we force an update to default device.
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    caller.UpdatePlaybackDevice(null, true);
                });
            }
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
            // Not needed.
        }
    }
}