---
---
# Getting Started

Want to make your own Ragnarock maps? Let's get started.  

## Table of Contents
1.  [Installation](#installation)
2. [Setting up your song](#setting-up-your-song)
3. [Mapping with Edda](#mapping-with-edda)

## Installation
To install and use Edda, you'll need a computer running Windows 10.  

> **NOTE**: If you have a Linux or macOS computer, you may be able to use something like [WINE](https://www.winehq.org) or [Parallels](https://www.parallels.com/) to run Windows programs.

Start by going to the GitHub repository's [Releases section](https://github.com/PKBeam/Edda/releases). There will be two `.zip` packages for you to download. 

Most users should download the larger of the two (the one that is **not** named `NoRuntime`).  
If you are unsure, download this one.
  
If you have the [.NET 5.0 runtime](https://dotnet.microsoft.com/download/dotnet/5.0/runtime) installed, then you can download the smaller `NoRuntime` zip package.

Once you've downloaded the zip package, extract it somewhere on your PC.

Make sure your antivirus doesn't interfere, and that you don't extract Edda into a privileged folder (such as `Program Files`).
  
## Setting up your song
### Choosing a good song
Every good map needs a good track, so before we start placing notes we need to pick a good song.  

Ragnarock is designed to be played with metal music genres but works great with many kinds of loud, energetic songs.  

Be careful when mapping quiet songs, such as background music or "chill" songs - they may not work as well as you expect.

### Getting a song file
Once you've settled on a song, you need to get an appropriate file to use for mapping.  

Edda requires the song file to be in Vorbis format. Files with the Vorbis format are characterised by a `.ogg` file extension.  

> **NOTE**: Some `.ogg` files may rarely use the Opus codec, which is not supported by Edda.  
> If you have `ffmpeg` installed, you can quickly check the codec by using `ffmpeg -i input.ogg` and reading the output.

If you can't find an `.ogg` file, you'll have to make one yourself by converting from an audio file with a different format (such as `.wav` or `.mp3`).  

There are plenty of applications that can do this:
- [Audacity](https://www.audacityteam.org) is a popular user-friendly tool.  
- If you know how to use command-line tools, [ffmpeg](https://ffmpeg.org) will do the job faster.   

> **NOTE**: If you use a music streaming service like Spotify or Apple Music, you can play the song you want on your computer and record it to a file. 
> You can do this in Audacity by setting your microphone input to your headphone/speaker output and starting a recording with the song playing.

#### Lossless encoding

If possible, you should obtain a lossless audio file (e.g. `.wav` or `.flac`) and transcode it into Vorbis format (`.ogg`).

You should aim for a quality preset somewhere from 5 to 9 (which is Spotify Premium quality).  

The quality 10 preset produces unnecessarily large files, and anything under quality 5 may result in noticeably poor sound quality.

You can set the quality
- in Audacity, by using the slider in the file save dialog (after you click "Export to OGG").
- in ffmpeg, by using the `-q` flag, e.g. `ffmpeg -i input.wav -q 9 out.ogg`.

#### Lossy encoding
If you can't get a lossless file, you may have to make do with a lossy file (e.g. `.mp3`).  

The easiest way to get one is via YouTube. There are plenty of websites that will take a YouTube link and turn it into an `.mp3` file for you.

You can then transcode to Vorbis format by using Audacity or ffmpeg as described above in [Lossless encoding](#lossless-encoding).

> **NOTE**: If possible, you should aim to get lossless files. Lossy files and lossy-to-lossy transcoding will result in a lower sound quality.  
> But if you can't find a lossless file, don't worry. It will be hard for most players to tell the difference, especially with Ragnarock's drum sounds playing over the music.   

## Mapping with Edda

### Creating the map

Now that your audio file is all set up, it's time to create the map!

Go to the folder you extracted Edda to, and open `Edda.exe`.

Click the "New Map" button.

Select a folder to store your map in. Ragnarock uses the directory `Documents/Ragnarock/CustomSongs`, so a good starting point is to create a folder in there and use it.

Select the `.ogg` Vorbis file you want to map.

Your map is now set up and ready to edit!

### Setting up the map

Before you get into note placement, there are a few things you should fill out first.

In the left sidebar under **Map Settings**, fill out the *Song Name*, *Artist Name* and *Mapper*.   

Choose an *Environment* for your song - this is the world your boat will row in when you play the song in-game.  

Enter the *Song BPM*. If you don't know the song's BPM, you can find it with Edda's BPM finder function or another similar tool.  

Under **File Info**, click the ellipsis (`...`) next to the *Image* field. Choose a cover image for your map - this will be displayed in-game.  
  


