# FileWatcherEx for Windows
A wrapper of C# `FileSystemWatcher` for Windows, used in [ImageGlass](https://github.com/d2phap/ImageGlass) project.

This project is based on the *VSCode FileWatcher*: https://github.com/Microsoft/vscode-filewatcher-windows

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
