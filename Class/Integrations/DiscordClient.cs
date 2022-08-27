using System;
using Edda;
using DiscordRPC;

public class DiscordClient {
    DiscordRpcClient client;
	DateTime startTime;
	bool enabled = false;
	public DiscordClient() {
		UpdateStartTime();
		InitClient();
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
			Details = songName == null ? "No song open" : $"Mapping: {songName}",
			State = songName == null ? "" : $"{numNotes} notes placed",
			Assets = new Assets() {
				LargeImageKey = Edda.Class.DiscordRPC.IconKey
			}
		}.WithTimestamps(new Timestamps(startTime));

		client.SetPresence(rp);
	}
	public void Enable() {
		enabled = true;
		InitClient();
	}
	public void Disable() {
		enabled = false;
		//client.ClearPresence();	this causes a null-dereference crash in the library
		DeinitClient();
	}
	private void InitClient() {
		client = new DiscordRpcClient(Edda.Class.DiscordRPC.AppID);
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
