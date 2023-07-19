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
			Details = GetPresenceDetails(songName),
			State = songName == null ? "" : $"{numNotes} notes placed",
			Assets = new Assets() {
				LargeImageKey = Edda.Const.DiscordRPC.IconKey
			}
		}.WithTimestamps(new Timestamps(startTime));

		client.SetPresence(rp);
	}

	private string GetPresenceDetails(string songName)
	{
		switch (songName)
		{
			case null: return "No song open";
			case "": return "Mapping: *Untitled*";
			default:
				return $"Mapping: {songName}";

        }
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
