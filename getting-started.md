---
---
# Getting Started

Want to make your own Ragnarock maps? Let's get started.  

- [System requirements](#system-requirements)
- [Installation](#installation)

## System requirements

### Windows

For the best experience with Edda, you'll need a computer with Windows 10 or Windows 11.

Older versions of Windows (back to Windows 7) may work, but are not officially supported.

### Linux

Linux users can use [Proton](https://github.com/ValveSoftware/Proton) to run Edda, although it has considerable audio latency and may not function perfectly. WINE alone is not sufficient to run Edda.

> **NOTE**: Testing was last performed with Arch Linux using Edda v1.1.0. Users reported issues getting Proton to run Edda v1.2.0+.

### macOS

Edda could possibly run on a Mac with software like [Parallels](https://www.parallels.com/).

Otherwise, Intel Macs can run Windows natively with Boot Camp. Apple Silicon Macs will have to run Windows through a VM.

## Installation

Start by going to the GitHub repository's [Releases section](https://github.com/PKBeam/Edda/releases). There will be two `.zip` packages for you to choose between. 

If you are unsure of which to download, pick the larger package - the one that is **not** named `NoRuntime`. This package bundles the .NET runtime which is required for Edda to run.

If you want to use the smaller `NoRuntime` zip package, you need to have the .NET runtime installed. Which .NET version you need is dependent on the version of Edda.

|Edda version|.NET version|
---|---
|after 1.2.4    |[8.0]((https://dotnet.microsoft.com/download/dotnet/8.0/runtime)) or later|
|1.1.0 to 1.2.4 |[7.0]((https://dotnet.microsoft.com/download/dotnet/7.0/runtime)) or later|
|1.0.0 to 1.1.0 |[6.0]((https://dotnet.microsoft.com/download/dotnet/6.0/runtime)) or later|
|before 1.0.0   |[5.0]((https://dotnet.microsoft.com/download/dotnet/5.0/runtime)) or later|

Once you've downloaded the zip package, extract it somewhere on your PC.

Make sure your antivirus doesn't interfere with anything, and that you don't extract Edda into a privileged folder (such as `Program Files`).
 
---

Now that you have Edda installed and ready to go, head on over to [Using Edda](using-edda) to start making your map!  
