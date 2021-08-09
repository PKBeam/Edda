
using DiscordRPC;
using Edda;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class DiscordClient {
	MainWindow parent;
    DiscordRpcClient client;
	DateTime startTime;
	bool enabled;
	public DiscordClient(MainWindow parent) {
		this.parent = parent;
		UpdateStartTime();
		InitClient();
	}

	public void UpdateStartTime() {
		startTime = DateTime.UtcNow;
	}

	public void SetPresence() {
		if (!enabled) {
			return;
        }
		var songName = parent.mapName;
		var numNotes = parent.mapNoteCount;
		var rp = new RichPresence() {
			Details = songName == null ? "No song open" : $"Mapping: {songName}",
			State = songName == null ? "" : $"{numNotes} notes placed",
			Assets = new Assets() {
				LargeImageKey = Const.DiscordRPC.IconKey
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
		client = new DiscordRpcClient(Const.DiscordRPC.AppID);
		client.Initialize();
		SetPresence();
	}
	private void DeinitClient() {
		client.Deinitialize();
		client = null;
	}
}
