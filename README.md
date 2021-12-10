# FileWatcherEx for Windows
A wrapper of C# `FileSystemWatcher` for Windows, used in [ImageGlass](https://github.com/d2phap/ImageGlass) project.

This project is based on the *VSCode FileWatcher*: https://github.com/Microsoft/vscode-filewatcher-windows

![Nuget](https://img.shields.io/nuget/dt/FileWatcherEx?color=%2300a8d6&logo=nuget)
[![Build status](https://ci.appveyor.com/api/projects/status/t20tf9qyta8enhu1?svg=true)](https://ci.appveyor.com/project/d2phap/filewatcherex)


## Resource links
- Project url: [https://github.com/d2phap/FileWatcherEx](https://github.com/d2phap/FileWatcherEx)
- Website: [https://imageglass.org](https://imageglass.org)
- Nuget package: [https://www.nuget.org/packages/FileWatcherEx](https://www.nuget.org/packages/FileWatcherEx/)

## Features
- Standardize the events of C# `FileSystemWatcher`.
- No false change notifications when a file system item is created, deleted, changed or renamed.
- Support .NET 6.0.

## Installation
Run the command
```bat
Install-Package FileWatcherEx
```

## Usage
See Demo project for full details

```cs
using FileWatcherEx;


var _fw = new FileSystemWatcherEx(@"C:\path\to\watch");

// event handlers
_fw.OnRenamed += FW_OnRenamed;
_fw.OnCreated += FW_OnCreated;
_fw.OnDeleted += FW_OnDeleted;
_fw.OnChanged += FW_OnChanged;
_fw.OnError += FW_OnError;

// thread-safe for event handlers
_fw.SynchronizingObject = this;

// start watching
_fw.Start();



void FW_OnRenamed(object sender, FileChangedEvent e)
{
  // do something here
}
...

```

## License
[MIT](LICENSE)

## Support this project

<a href="https://github.com/sponsors/d2phap" target="_blank" title="Become a sponsor">
<img src="https://img.shields.io/badge/Github-@d2phap-24292e.svg?maxAge=3600&logo=github" height="30" alt="Become a sponsor">
</a>

<a href="https://www.patreon.com/d2phap" target="_blank" title="Become a patron">
<img src="https://img.shields.io/badge/Patreon-@d2phap%20-e85b46.svg?maxAge=3600&logo=patreon" height="30" alt="Become a patron">
</a>

<a href="https://www.paypal.me/d2phap" target="_blank" title="Buy me a beer?">
<img src="https://img.shields.io/badge/PayPal-Donate%20$10%20-0070ba.svg?maxAge=3600&logo=paypal" height="30" alt="Buy me a beer?">
</a>

<a href="https://donorbox.org/imageglass" target="_blank" title="Wire transfer">
<img src="https://img.shields.io/badge/DonorBox-@imageglass%20-005384.svg?maxAge=3600&logo=donorbox" height="30" alt="Wire transfer">
</a>

Thanks for your gratitude and finance help!
