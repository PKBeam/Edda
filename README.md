# Edda

Edda is a map editor for the VR rhythm game [Ragnarock](https://www.ragnarock-vr.com/home) ([Steam page](https://store.steampowered.com/app/1345820/Ragnarock/)).

It is currently under development and not fully functional.
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

Edda is written in C# for the Windows 10 operating system.

## Why make a new map editor?

Although [MMA2](https://github.com/Shadnix-was-taken/MediocreMapper) is an excellent mapping tool for Beat Saber, it is not optimised for Ragnarock.
Beat Saber maps are far more complex than Ragnarock ones; thus MMA2 has too much unused utility when creating Ragnarock maps. 
(e.g. Ragnarock maps are two-dimensional, so we don't need to render them in 3D!)

A dedicated editor for Ragnarock could trim down the clutter, greatly improving the user experience for new mappers.
