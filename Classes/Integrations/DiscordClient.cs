using System;
using Edda;
using DiscordRPC;

public class DiscordClient {
    DiscordRpcClient client;
    DateTime startTime;
    bool enabled = false;
    public DiscordClient() {
        UpdateStartTime();
    }

    public void UpdateStartTime() {
        startTime = DateTime.UtcNow;
    }
    public void SetPresence() {
        SetPresence(null, 0);
    }
    public void SetPresence(string songName, int numNotes) {
        if (!enabled) {
            return;
        }
        var rp = new RichPresence() {
            Details = GetPresenceDetails(songName),
            State = songName == null ? "" : $"{numNotes} notes placed",
            Assets = new Assets() {
                LargeImageKey = Edda.Const.DiscordRPC.IconKey
            }
        }.WithTimestamps(new Timestamps(startTime));

        client.SetPresence(rp);
    }

    private string GetPresenceDetails(string songName) {
        return songName switch {
            null => "No song open",
            "" => "Working on an untitled map",
            _ => $"Mapping: {songName}",
        };
    }

    public void Enable() {
        var wasEnabled = enabled;
        enabled = true;
        if (!wasEnabled) {
            InitClient();
        }
    }
    public void Disable() {
        var wasEnabled = enabled;
        enabled = false;
        if (wasEnabled) {
            DeinitClient();
        }
    }
    private void InitClient() {
        client = new DiscordRpcClient(Edda.Const.DiscordRPC.AppID);
        client.Initialize();
        SetPresence();
    }
    private void DeinitClient() {
        if (client != null) {
            client.Deinitialize();
            client = null;
        }
    }
}
