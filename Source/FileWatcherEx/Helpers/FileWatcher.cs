/*---------------------------------------------------------
 * Copyright (C) Microsoft Corporation. All rights reserved.
 *--------------------------------------------------------*/

namespace FileWatcherEx;

internal class FileWatcher : IDisposable
{
    private string _watchPath = string.Empty;
    private Action<FileChangedEvent>? _eventCallback = null;
    private readonly Dictionary<string, IFileSystemWatcherWrapper> _fwDictionary = new();
    private Action<ErrorEventArgs>? _onError = null;
    private Func<string, FileAttributes>? _getFileAttributesFunc;
    private Func<string, DirectoryInfo[]>? _getDirectoryInfosFunc;
    private Func<IFileSystemWatcherWrapper> _watcherFactory;

    internal Func<string, FileAttributes> GetFileAttributesFunc
    {
        get => _getFileAttributesFunc ?? (p => File.GetAttributes(p));
        set => _getFileAttributesFunc = value;
    }

    internal Func<string, DirectoryInfo[]> GetDirectoryInfosFunc
    {
        get => _getDirectoryInfosFunc ?? (p => new DirectoryInfo(p).GetDirectories());
        set => _getDirectoryInfosFunc = value;
    }

    internal Dictionary<string, IFileSystemWatcherWrapper> FwDictionary => _fwDictionary;

    /// <summary>
    /// Create new instance of FileSystemWatcherWrapper
    /// </summary>
    /// <param name="path">Full folder path to watcher</param>
    /// <param name="onEvent">onEvent callback</param>
    /// <param name="onError">onError callback</param>
    /// <param name="watcherFactory">how to create a FileSystemWatcher</param>
    /// <returns></returns>
    public IFileSystemWatcherWrapper Create(string path, Action<FileChangedEvent> onEvent, Action<ErrorEventArgs> onError, Func<IFileSystemWatcherWrapper> watcherFactory)
    {
        _watcherFactory = watcherFactory;
        _watchPath = path;
        _eventCallback = onEvent;        
        _onError = onError;

        var watcher = watcherFactory();
        watcher.Path = _watchPath;
        watcher.IncludeSubdirectories = true;
        watcher.NotifyFilter = NotifyFilters.LastWrite
                               | NotifyFilters.FileName
                               | NotifyFilters.DirectoryName;

        // Bind internal events to manipulate the possible symbolic links
        watcher.Created += new(MakeWatcher_Created);
        watcher.Deleted += new(MakeWatcher_Deleted);

        watcher.Changed += new((object _, FileSystemEventArgs e) => ProcessEvent(e, ChangeType.CHANGED));
        watcher.Created += new((object _, FileSystemEventArgs e) => ProcessEvent(e, ChangeType.CREATED));
        watcher.Deleted += new((object _, FileSystemEventArgs e) => ProcessEvent(e, ChangeType.DELETED));
        watcher.Renamed += new((object _, RenamedEventArgs e) => ProcessEvent(e));
        watcher.Error += new ErrorEventHandler((object source, ErrorEventArgs e) => onError(e));

        //changing this to a higher value can lead into issues when watching UNC drives
        watcher.InternalBufferSize = 32768;
        // root watcher
        _fwDictionary.Add(path, watcher);

        // this handles sub directories. Probably needs cleanup
        foreach (var dirInfo in GetDirectoryInfosFunc(path))
        {
            var attrs = GetFileAttributesFunc(dirInfo.FullName);

            // TODO: consider skipping hidden/system folders? 
            // See IG Issue #405 comment below
            // https://github.com/d2phap/ImageGlass/issues/405
            if (attrs.HasFlag(FileAttributes.Directory)
                && attrs.HasFlag(FileAttributes.ReparsePoint))
            {
                try
                {
                    MakeWatcher(dirInfo.FullName);
                }
                catch
                {
                    // IG Issue #405: throws exception on Windows 10
                    // for "c:\users\user\application data" folder and sub-folders.
                }
            }
        }

        return watcher;
    }


    /// <summary>
    /// Process event for type = [CHANGED; DELETED; CREATED]
    /// </summary>
    /// <param name="e"></param>
    /// <param name="changeType"></param>
    private void ProcessEvent(FileSystemEventArgs e, ChangeType changeType)
    {
        _eventCallback?.Invoke(new()
        {
            ChangeType = changeType,
            FullPath = e.FullPath,
        });
    }


    /// <summary>
    /// Process event for type = RENAMED
    /// </summary>
    /// <param name="e"></param>
    private void ProcessEvent(RenamedEventArgs e)
    {
        _eventCallback?.Invoke(new()
        {
            ChangeType = ChangeType.RENAMED,
            FullPath = e.FullPath,
            OldFullPath = e.OldFullPath,
        });
    }


    private void MakeWatcher(string path)
    {
        if (!_fwDictionary.ContainsKey(path))
        {
            var fileSystemWatcherRoot = _watcherFactory();
            fileSystemWatcherRoot.Path = path;
            fileSystemWatcherRoot.IncludeSubdirectories = true;
            fileSystemWatcherRoot.EnableRaisingEvents = true;

            // Bind internal events to manipulate the possible symbolic links
            fileSystemWatcherRoot.Created += new(MakeWatcher_Created);
            fileSystemWatcherRoot.Deleted += new(MakeWatcher_Deleted);

            fileSystemWatcherRoot.Changed +=
                new((object _, FileSystemEventArgs e) => ProcessEvent(e, ChangeType.CHANGED));
            fileSystemWatcherRoot.Created +=
                new((object _, FileSystemEventArgs e) => ProcessEvent(e, ChangeType.CREATED));
            fileSystemWatcherRoot.Deleted +=
                new((object _, FileSystemEventArgs e) => ProcessEvent(e, ChangeType.DELETED));
            fileSystemWatcherRoot.Renamed += new((object _, RenamedEventArgs e) => ProcessEvent(e));
            fileSystemWatcherRoot.Error += new((object _, ErrorEventArgs e) => _onError?.Invoke(e));

            _fwDictionary.Add(path, fileSystemWatcherRoot);
        }

        foreach (var item in GetDirectoryInfosFunc(path))
        {
            var attrs = GetFileAttributesFunc(item.FullName);

            // If is a directory and symbolic link
            if (attrs.HasFlag(FileAttributes.Directory)
                && attrs.HasFlag(FileAttributes.ReparsePoint))
            {
                if (!_fwDictionary.ContainsKey(item.FullName))
                {
                    var fswItem = _watcherFactory();
                    fswItem.Path = item.FullName;
                    fswItem.IncludeSubdirectories = true;
                    fswItem.EnableRaisingEvents = true;

                    // Bind internal events to manipulate the possible symbolic links
                    fswItem.Created += new(MakeWatcher_Created);
                    fswItem.Deleted += new(MakeWatcher_Deleted);

                    fswItem.Changed += new((object _, FileSystemEventArgs e) => ProcessEvent(e, ChangeType.CHANGED));
                    fswItem.Created += new((object _, FileSystemEventArgs e) => ProcessEvent(e, ChangeType.CREATED));
                    fswItem.Deleted += new((object _, FileSystemEventArgs e) => ProcessEvent(e, ChangeType.DELETED));
                    fswItem.Renamed += new((object _, RenamedEventArgs e) => ProcessEvent(e));
                    fswItem.Error += new((object _, ErrorEventArgs e) => _onError?.Invoke(e));

                    _fwDictionary.Add(item.FullName, fswItem);
                }

                MakeWatcher(item.FullName);
            }
        }
    }


    private void MakeWatcher_Created(object sender, FileSystemEventArgs e)
    {
        try
        {
            var attrs = GetFileAttributesFunc(e.FullPath);
            if (attrs.HasFlag(FileAttributes.Directory)
                && attrs.HasFlag(FileAttributes.ReparsePoint))
            {
                var watcherCreated = _watcherFactory();
                watcherCreated.Path = e.FullPath;
                watcherCreated.IncludeSubdirectories = true;
                watcherCreated.EnableRaisingEvents = true;

                // Bind internal events to manipulate the possible symbolic links
                watcherCreated.Created += new(MakeWatcher_Created);
                watcherCreated.Deleted += new(MakeWatcher_Deleted);

                watcherCreated.Changed += new((object _, FileSystemEventArgs e) => ProcessEvent(e, ChangeType.CHANGED));
                watcherCreated.Created += new((object _, FileSystemEventArgs e) => ProcessEvent(e, ChangeType.CREATED));
                watcherCreated.Deleted += new((object _, FileSystemEventArgs e) => ProcessEvent(e, ChangeType.DELETED));
                watcherCreated.Renamed += new((object _, RenamedEventArgs e) => ProcessEvent(e));
                watcherCreated.Error += new((object _, ErrorEventArgs e) => _onError?.Invoke(e));

                _fwDictionary.Add(e.FullPath, watcherCreated);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("<<ERROR>>: " + ex.Message);
        }
    }


    private void MakeWatcher_Deleted(object sender, FileSystemEventArgs e)
    {
        // If object removed, then I will dispose and remove them from dictionary
        if (_fwDictionary.ContainsKey(e.FullPath))
        {
            _fwDictionary[e.FullPath].Dispose();
            _fwDictionary.Remove(e.FullPath);
        }
    }


    /// <summary>
    /// Dispose the instance
    /// </summary>
    public void Dispose()
    {
        foreach (var item in _fwDictionary)
        {
            item.Value.Dispose();
        }
    }
}