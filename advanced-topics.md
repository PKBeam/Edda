---
---
# Advanced Topics

Now that you're comfortable with using Edda, here are some more advanced topics to help you make the most of your mapping experience.  

- [Backups](#backups)
- [Timing changes](#timing-changes)
- [Note runes](#note-runes)
- [Audio latency](#audio-latency)
  - [Technical details](#technical-details)
- [Drum hitsounds](#drum-hitsounds)

## Backups

Edda periodically keeps backups of your maps in the `autosaves` folder that is located in the folder your map is in.  

In this folder there will be up to ten folders with names that look like `Backup - 08 October 2021 4.23PM`. Inside these backup folders are `.dat` files for each mapped difficulty.  

This is so you can recover your work if a bug or issue causes a data wipe. (Please feel free to [report](https://github.com/PKBeam/Edda/issues) any such issues).

Backups are made every time a map is saved, *except when the save is an autosave*, so make sure to occasionally do a manual save (e.g. Ctrl-S, click the "Save Map" button, or save when prompted on closing Edda).

> **NOTE**: You can turn autosaves on and off in the Settings menu, under **Editor**. Autosaves happen every 30 seconds.

To restore a backup, you must manually copy the `.dat` files in the backup folders back into the root of your map folder.


## Timing changes 

> **NOTE**: Prior to Edda v1.0.0, timing changes were referred to as "BPM changes".

You're probably already familiar with using timing changes to align your song to the editing grid, but let's go over it in-depth.

Most songs will have a constant BPM. These songs are the easiest to map, especially for beginners.

Many songs, however, will change their BPM at some point. You don't want to respecify the BPM and realign the song every time you want to map this part of the song, so a timing change will let Edda know that it has to draw the editing grid a bit differently for certain parts of the song.

To add a timing change, you can either use `Ctrl-T` or `Ctrl-Shift-T` or click the "Edit Song Timing" button in the right sidebar.

To specify a timing change the latter way, you need three things:

- the *Global Beat* where the timing change begins,
- the new *BPM* that will come into effect, and
- the *Beat Division* during this timing change.

> **NOTE**: Edda's timing changes are mostly compatible with MMA2's BPM changes, except for the `_metronomeOffset` field (which is unused by Edda).

The *Global Beat* of a point in the song is the beat it would be located on if the *Song BPM* (or Global BPM) was in use for the entire song. 
You can think of it as an alternative to a minutes/seconds timestamp for referencing a point in the song.

To find the *Global Beat*, you can use the timing bar.

When you move your mouse over the editing grid (or navigational waveform), the timing bar will show the timestamp and global beat of the point your mouse is on.  

Two values are shown for the global beat:
- the first value is the global beat after the mouse position is snapped to the gridlines.
- the second value (in brackets) is the global beat before snapping.

Typically, you want to use the second, unsnapped value when specifying a timing change.

The *BPM* field is, predictably, the new BPM that you want to change to. You can find this with the help of Edda's BPM finder tool in the toolbar.

Often a timing change will coincide with a time signature change. This could mean that beats are now divided differently. For example, a beat is now divided into thirds instead of quarters. If this happens, you can use the *Beat Division* field to specify the new beat division.

If the beats are not divided differently after the timing change, you should keep the *Beat Division* the same as before.   

> **NOTE**: You can use a timing change to temporarily adjust the *Beat Division* by leaving the *BPM* field the same as before.  
> This can be useful if there are just a few beats in the song that are divided differently than the rest.

## Note runes

Why bother with BPMs or timing changes?

Why not just turn off grid snapping and carefully position each and every note on the grid, exactly where you want it?

Well, the most obvious answer is that it's a lot of tedious work - nobody wants to do that for thousands of notes.

But there's another reason why: it causes the runes on the notes to display incorrectly in-game.

Each note in Ragnarock has a rune inscribed on it. The runes indicate what beat the note is placed on.

|Rune|Fractional Beat|
---|---
|ᛄ  |0 (or 1)|
|ᚼ  |1/2|
|ᛝ  |1/3|
|ᛞ  |2/3|
|ᛁ   |1/4|
|ᚾ  |3/4|
|ᚷ  |other|

For example: 
- A note on the 6th beat (no fractional part) would have the symbol ᛄ.
- A note on beat 2.50 (fractional part 1/2) would have the symbol ᚼ.
- A note on beat 7.75 (fractional part 3/4) has the symbol ᚾ.
- A note on beat 150.23412 has the symbol ᚷ.

(Edda supports the display of these runes - you can check for yourself in the editing grid.)

Runes are helpful for sight-reading a map. It lets the player know what runes are on off-beats, when notes are divided in triplets instead of quarters, and - importantly - it alerts players to arpeggiated notes.

Arpeggiated notes are meant to be played very close to each other, but not quite at the same time (think of a slow guitar strum). They aren't uncommon in songs with guitars - the beginning of the OST song *Loki* (difficulty 7) has a lot of them.

Arpeggiated notes are so close together that they can look like notes that supposed to be hit at the same time (chords). It's easiest to tell the difference by looking at the runes - if the  notes are arpeggiated then one will have an ᚷ rune, while notes in chords will both have the same rune (usually not an ᚷ rune).

Knowing *which* note has the ᚷ rune also informs players preciesly how they should hit the runes.

If you don't tell Ragnarock what the BPM is or where the timing changes are, then it won't know which beat a note is placed on. That means all the notes will probably have an ᚷ rune on them, which makes it harder for players to do things like play arpeggiated notes. 

So the lesson here is to map out your BPM and timing changes properly! It makes it much nicer for the people that play your map.  

## Audio latency

Rhythm games often demand precise inputs from players. In Ragnarock, [the window for a perfect hit is just 30ms wide](https://docs.google.com/presentation/d/e/2PACX-1vSOCYKao-OzY0zaGYT_rl5J8CFyKKxXS6Rct1TmdeujVYZ3JOWVmJlLjFDCojHPa7lpeOsGbqShbMdb/pub?start=false&loop=false&delayms=3000&slide=id.gb56bad2588_0_2) (15ms before or after).  

When dealing with such small increments of time, audio playback latency becomes an issue. 

This is why most rhythm games have user-configurable audio latencies, and Edda is no different.

Click the "Settings" button in the toolbar, and under **Audio Playback** you can see a field called *Audio Latency*. This is the difference in time between song playback and note/drum playback. The default value is -20ms, which means that the notes will play 20ms *before* the song.

You can change this freely, but you'll have to experiment with in-game playback to get the best latency value; every application handles audio latency differently.

### Technical details

Edda uses [WASAPI](https://docs.microsoft.com/en-us/windows/win32/coreaudio/wasapi) via the [NAudio library](https://github.com/naudio/NAudio) to try and obtain the lowest audio latency.

WASAPI produces the lowest latency results but is relatively more intensive on your CPU. This means you'll tend to have better synced audio, but at the cost of higher CPU usage. If your CPU has weak single-threaded performance, you may experience stuttering or audio drop-outs. 

 WASAPI performs best when your audio output's sample rate matches the sample rate of the music file that it wants to play. If these sample rates don't match, WASAPI needs to resample on-the-fly, which incurs a latency penalty.
 
 Audio files typically use 44.1kHz or 48kHz sample rates, but most *music* tends to use 44.1kHz (including the drum hitsounds used by Edda). 
 
 For optimal latency, you should set your sound output (e.g. DAC, sound card, or motherboard sound) to 44.1kHz in Windows' audio settings.

## Drum hitsounds

When you play back a map in Edda, a snare drum sound will play when the scan line passes a note.

That's the default hitsound for Edda, and it can be changed in the Settings menu (under **Audio Playback**, then *Playback Sound*).

By default Edda also provides a bass drum sound and a hi-hat sound, but you can add your own hitsounds if you aren't satisfied with either.

To add your own hitsounds, go to the `Resources` folder in your Edda install and place `.mp3` or `.wav` files there.

To specify multiple samples together, number them e.g. `file1.wav`, `file2.wav`, `file3.wav` or `my_drum1.mp3`, `my_drum2.mp3`.

It is recommended to provide at least two distinct samples per hitsound, which makes them sound more natural when multiple drums are played simultaneously.

### Panned audio

By default, drum hitsounds are panned. That means if a note is played on the left column, the drum hitsound will play closer to your left ear (and vice versa for the right columns).  

Some users may find panned audio harder to hear or may experience audio playback issues with panned audio. Panned audio can be disabled in the Settings menu (under **Audio Playback**, then *Pan Note Sound*).  

When panned audio is disabled, all drum hitsounds will be played in the middle of both ears.  

