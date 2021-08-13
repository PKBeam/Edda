# Edda <img src="https://img.shields.io/github/v/release/PKBeam/Edda">
<img src="https://img.shields.io/github/downloads/PKBeam/Edda/total"> <img src="https://img.shields.io/github/downloads/PKBeam/Edda/latest/total"> 


Edda is a beatmap editor for the VR rhythm game [Ragnarock](https://www.ragnarock-vr.com/home).  

It is still under development and has not been tested extensively, but most features should work well.  

If you have suggestions or bug reports, feel free to make a new issue on GitHub or come over to the [Ragnacustoms](https://ragnacustoms.com/) Discord server to discuss it.

<details open>
  <summary>Edda screenshot (click to close)</summary>

  ![Screenshot of Edda](https://i.imgur.com/Zt5228E.png)
</details>

## Installation

### Requirements
- Windows 10  
- *Recommended*: a CPU with good single-threaded performance (for low latency audio playback)
- *Recommended*: a high refresh rate monitor (for smooth animations)

### Downloading
Go to the [Releases](https://github.com/PKBeam/Edda/releases/latest) section and download the appropriate `.zip` package.
- The `NoRuntime` package requires the [.NET 5.0 runtime](https://dotnet.microsoft.com/download/dotnet/5.0/runtime) to be installed.
- If you do not have this runtime installed, you should download the larger `.zip` file.

### Troubleshooting
If you're having issues, make sure to check the following:
- antivirus
- permissions on your install folder  

## Features
<details>
  <summary>(click to expand)</summary>
  
  - Edit/change the following:
    - Song name
    - Artist name
    - Mapper name
    - Song BPM
    - Song start time offset
    - In-game environment (e.g. Midgard, Alfheim, ...)
    - Song file
    - Cover image
    - Map difficulties
      - Difficulty level (1-10)
      - Medal distances
      - Note jump speed  
  - Edit existing Ragnarock maps or create new ones
    - Listen to the entire map with audio and mapped drum hits
       - Customise the note playback sound
       - Notes are marked with the same rune that would appear in-game
       - Change the relative volumes of the song and mapped notes
    - Variable BPM support
    - Customise the editor grid
      - Toggle note placements snapping to grid
      - Change the beat division
      - Add a global offset 
        - this is not recommended - it causes incorrect runes to appear on notes
      - Change the spacing of the grid
      - Overlay the audio waveform of the song with the editor grid
    - Add and delete notes
    - Select multiple notes by dragging with the mouse
    - Operate on selected notes
      - Cut, copy, paste
      - Move up, down, left or right
      - Mirror notes
    - Undo and redo edits
    - Create bookmarks for easy navigation
  - In-built BPM finding tool
    - Press a key to a song's beat to automatically calculate its BPM
</details>

## Why make a new map editor?

Although [MMA2](https://github.com/Shadnix-was-taken/MediocreMapper) is an excellent mapping tool for Beat Saber, it is not optimised for Ragnarock.  
Beat Saber maps are much more complex than Ragnarock ones, so MMA2 has too much unused utility when creating Ragnarock maps.  
This creates lots of unnecessary clutter and affects the user experience, especially for new mappers.

## Usage

Edda does not autosave maps yet. Don't forget to save your work often.  
Please also keep backups of any important maps, as bugs may cause map corruption.  

### Creating a new map
- Click "New Map".
- Select an `.ogg` song to map.
   - The song file must use the Vorbis codec. Opus files sometimes use `.ogg` but they are not supported!.
   - It is recommended that you either:
     - trim the song (e.g. in [Audacity](https://www.audacityteam.org/)) so it begins on an integer beat, or  
     - use the variable BPM feature and place a BPM change at the start of the song.
- Select an empty folder.
  - The folder's name cannot have spaces or non-alphabetical characters.
  - For reference, Ragnarock searches the directory `Documents/Ragnarock/CustomSongs` for folders containing maps.

### Opening an existing map
- Click "Open Map".
- Select a folder containing a map.

### Controls

<details>
  <summary>(click to expand)</summary>
  
#### Mouse
- Click and drag to select multiple notes.
- Left-click to place a new note or to select an existing note.
- Shift + Left-click to add a note to the selection.
- Right-click to remove a note or to clear the selection.
  
- Double-click a bookmark to rename it.
- Right-click a bookmark to delete it.

#### Keyboard
- Ctrl-N: New Map
- Ctrl-O: Open Map
- Ctrl-S: Save Map
- Ctrl-A: Select All
- Ctrl-C: Copy Selection
- Ctrl-X: Cut Selection
- Ctrl-V: Paste Clipboard
  - Notes will be pasted on the same row the mouse is currently over.
- Ctrl-M: Mirror Selection
- Ctrl-Z: Undo Edit
- Ctrl-Y: Redo Edit
  - (Ctrl-Shift-Z is also supported)
- Ctrl-B: Add Bookmark
  - The location of the bookmark is based on your mouse position.
  - You can add a bookmark using either the central mapping area or the navigational waveform on the right.

- Ctrl-[: Toggle left dock
- Ctrl-]: Toggle right dock
  
- Shift-Up: Move selection one gridline forwards
- Shift-Down: Move selection one gridline backwards
- Shift-Left: Move selection one column to the left
- Shift-Right: Move selection one column to the left
- Ctrl-Up: Move selection one beat forwards
- Ctrl-Down: Move selection one beat backwards

- Delete: Delete selected notes
- Escape: Unselect all notes
- Space: Play/pause song
</details>

### Note Playback
You can customise the note playback sounds!  
The default sound is a bass drum, but a hi-hat sample is also provided (you can choose between them in the Settings menu).  
To use your own samples, place files with names of the form `file1.wav`, `file2.wav`, ... in Edda's `Resources/` folder (both `.wav` and `.mp3` files are supported.)  
It is recommended to provide at least two distinct samples for a natural sound.  

### Audio Latency
Edda leverages [WASAPI](https://docs.microsoft.com/en-us/windows/win32/coreaudio/wasapi) to try and minimise audio latency between the song and any mapped drum hits.  
Although WASAPI is fast, it requires more CPU power than other APIs (weak CPUs may experience crackling or popping).  

Because of how WASAPI works, it is recommended that you match the sample rate of your sound output and your song, in order to eliminate latency from audio resampling. Resampling to 44.1 kHz is preferred. If you must use 48 kHz, you may be able to reduce latency by using 48 kHz files for the note playback sounds.  

There is a slight delay between the playback of the song and the playback of individual notes. You can adjust this delay in the Settings menu.  
