# Edda

Edda is a map editor for the VR rhythm game [Ragnarock](https://www.ragnarock-vr.com/home) ([Steam page](https://store.steampowered.com/app/1345820/Ragnarock/)).

![Screenshot of Edda](https://i.imgur.com/cjWzK6G.png)

It is currently under development and not fully functional. Naturally, it will be very buggy until it gets polished up, so use it at your own risk.
Some things that you can do are:
- Edit/change the following:
  - Song name
  - Artist name
  - Mapper name
  - In-game environment
  - Song BPM
  - Song file
  - Cover image
- Add new map difficulties
- Open existing Ragnarock maps
  - Play the entire map with notes and audio
  - Add and delete notes (Left- and Right-Click)

Edda is written in C# for Windows 10.

## Why make a new map editor?

Although [MMA2](https://github.com/Shadnix-was-taken/MediocreMapper) is an excellent mapping tool for Beat Saber, it is not optimised for Ragnarock.
Beat Saber maps are far more complex than Ragnarock ones; thus MMA2 has too much unused utility when creating Ragnarock maps. 
(e.g. Ragnarock maps are two-dimensional, so we don't need to render them in 3D!)

A dedicated editor for Ragnarock could trim down the clutter, greatly improving the user experience for new mappers.

## System requirements
- Windows 10
- A good CPU will help with lower audio latency
  - For best results, match the sample rate of your sound output to your `.ogg` file