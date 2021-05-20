# Edda

Edda is a map editor for the VR rhythm game [Ragnarock](https://www.ragnarock-vr.com/home) ([Steam page](https://store.steampowered.com/app/1345820/Ragnarock/)).

![Screenshot of Edda](https://i.imgur.com/muLrvqV.png)

It is currently under development and not fully functional. No proper testing has been conducted, so use it at your own risk.
Some things that you should be able to do are:
- Edit/change the following:
  - Song name
  - Artist name
  - Mapper name
  - Song BPM
  - Song start time offset
  - In-game environment (e.g. Midgard, Alfheim, ...)
  - Song file
  - Cover image
- Add and delete map difficulties
- Open existing Ragnarock maps and create new ones
  - Listen to the entire map with audio and mapped drum hits
     - Notes will be marked with the same rune that would appear in-game
  - Add and delete notes (Left- and Right-Click)

Edda is written in C# for Windows 10.

## Why make a new map editor?

Although [MMA2](https://github.com/Shadnix-was-taken/MediocreMapper) is an excellent mapping tool for Beat Saber, it is not optimised for Ragnarock.
Beat Saber maps are far more complex than Ragnarock ones; thus MMA2 has too much unused utility when creating Ragnarock maps. 
(e.g. Ragnarock maps are two-dimensional, so we don't need to render them in 3D!)

A dedicated editor for Ragnarock could trim down the clutter, greatly improving the user experience for new mappers.

## Usage
### Creating a new map
- Click "New Map".
- Select an `.ogg` song to map.
   - If the song does not start on an non-integer beat, incorrect runes will be shown on the notes.
     - For this reason, **it is recommended you trim the song so it begins on a beat**.
- Select an empty folder.
  - The folder's name cannot have spaces or non-alphabetical characters.
  - Ragnarock searches the directory `Documents/Ragnarock/CustomSongs` for folders containing maps.

You're now ready to start mapping!

- Fill in the Map Settings and File Info sections with the appropriate metadata.
- Fill in the Difficulty Level and Note Speed.
- You can create and switch between difficulties using the "Change Difficulty" buttons.
- Set your Grid Division, Grid Offset and Grid Spacing.

### Editing an existing map
- Click "Open Map".
- Select a folder containing a map.
  - Edda does not yet check that the folder contains a valid map, so you must do this check.

You're now ready to start mapping!

## System requirements
- Windows 10
- A decent CPU (recommended)

Edda leverages [WASAPI](https://docs.microsoft.com/en-us/windows/win32/coreaudio/wasapi) to try and minimise audio latency between the song and any mapped drum hits.  
Although WASAPI is fast, it requires more CPU power. Weak CPUs may experience crackling or popping.  
It is recommended that you match the sample rate of your sound output and your song (preferably to 44.1 kHz), in order to eliminate latency from audio resampling.  
(If you must use 48 kHz, resampling the `.wav` drum samples in the `Resources` folder to 48 kHz may help reduce latency.)  
For now, you can adjust the playback latency by changing the `defaultEditorAudioLatency` constant and recompiling the project.