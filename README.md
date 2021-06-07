# Edda

Edda is a map editor for the VR rhythm game [Ragnarock](https://www.ragnarock-vr.com/home) ([Steam page](https://store.steampowered.com/app/1345820/Ragnarock/)).  
It is written in C# for Windows 10.

![Screenshot of Edda](https://i.imgur.com/cWQ3Aki.png)

Edda is mostly functional, but is still under development and has not been tested extensively.  
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
  - Map difficulties
    - Difficulty level (1-10)
    - Note jump speed  
- Open existing Ragnarock maps and create new ones
  - Listen to the entire map with audio and mapped drum hits
     - Notes will be marked with the same rune that would appear in-game
     - Change the relative volumes of the song and mapped notes
  - Customise the editor grid
    - Toggle grid snapping
    - Change the beat division
    - Add a global offset 
      - this is not recommended - it causes incorrect runes to appear on notes
    - Change the spacing of the grid
    - Overlay the audio waveform of the song with the editor grid
  - Add and delete notes
  - Select multiple notes by dragging with the mouse
  - Copy and paste selections
  - Undo and redo edits

## Why make a new map editor?

Although [MMA2](https://github.com/Shadnix-was-taken/MediocreMapper) is an excellent mapping tool for Beat Saber, it is not optimised for Ragnarock.  
Beat Saber maps are far more complex than Ragnarock ones; thus MMA2 has too much unused utility when creating Ragnarock maps.  
(e.g. Ragnarock maps are two-dimensional, so we don't need to render them in 3D!)

A dedicated editor for Ragnarock could trim down the clutter, greatly improving the user experience for new mappers.

## Usage

Edda does not autosave maps yet. Don't forget to save frequently by clicking "Save Map" in the UI or using Ctrl-S.  
Please also keep backups of any important maps, as bugs in Edda may cause map corruption.    

### Creating a new map
- Click "New Map".
- Select an `.ogg` song to map.
   - The song file must use the Vorbis codec.
   - It is recommended that you trim the song so it begins on an integer beat.
- Select an empty folder.
  - The folder's name cannot have spaces or non-alphabetical characters.
  - FOr reference, Ragnarock searches the directory `Documents/Ragnarock/CustomSongs` for folders containing maps.

### Opening an existing map
- Click "Open Map".
- Select a folder containing a map.

### Controls

#### Mouse
- Click and drag to select multiple notes.
- Left-click to place a new note or to select an existing note.
- Shift + Left-click to add a note to the selection.
- Right-click to remove a note or to clear the selection.

#### Keyboard
- Ctrl-N: New Map
- Ctrl-O: Open Map
- Ctrl-S: Save Map
- Ctrl-C: Copy Selection
- Ctrl-V: Paste Selection
  - Notes will be pasted on the same row the mouse is currently over.
- Ctrl-M: Mirror Selection
- Ctrl-Z: Undo Edit
- Ctrl-Y: Redo Edit
  - (Ctrl-Shift-Z is also supported)

- Delete: Delete selected notes
- Escape: Unselect all notes
- Space: Play/pause song

### Adjusting audio latency
There is a slight delay between the playback of the song and the playback of individual notes.  
You can adjust this delay by opening `settings.txt` in the application folder and entering the `editorAudioLatency` in milliseconds (integer).  
For example, `editorAudioLatency=5` or `editorAudioLatency=-40`. A blank value will use the default latency of -20 ms.

## System requirements
- Windows 10
- A decent CPU (recommended)

Edda leverages [WASAPI](https://docs.microsoft.com/en-us/windows/win32/coreaudio/wasapi) to try and minimise audio latency between the song and any mapped drum hits.  
Although WASAPI is fast, it requires more CPU power. Weak CPUs may experience crackling or popping.  
It is recommended that you match the sample rate of your sound output and your song (preferably to 44.1 kHz), in order to eliminate latency from audio resampling.  
(If you must use 48 kHz, resampling the `.wav` drum samples in the `Resources` folder to 48 kHz may help reduce latency.)  
