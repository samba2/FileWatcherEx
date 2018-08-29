# FileWatcherEx for Windows
A wrapper of C# `FileSystemWatcher` for Windows, used in [ImageGlass](https://github.com/d2phap/ImageGlass) project.

This project is based on the *VSCode FileWatcher*: https://github.com/Microsoft/vscode-filewatcher-windows


[![Build status](https://ci.appveyor.com/api/projects/status/t20tf9qyta8enhu1?svg=true)](https://ci.appveyor.com/project/d2phap/filewatcherex)

## Features
- Standardize the events of C# `FileSystemWatcher`
- No false change notifications when a file system item is created, deleted, changed or renamed.

## Installation
Run the command
```bat
Install-Package FileWatcherEx
```

## License
[MIT](LICENSE)
