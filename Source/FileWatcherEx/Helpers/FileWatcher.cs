/*---------------------------------------------------------
 * Copyright (C) Microsoft Corporation. All rights reserved.
 *--------------------------------------------------------*/

namespace FileWatcherEx.Helpers;

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
        get => _getFileAttributesFunc ?? File.GetAttributes;
        set => _getFileAttributesFunc = value;
    }

    internal Func<string, DirectoryInfo[]> GetDirectoryInfosFunc
    {
        get
        {
            DirectoryInfo[] DefaultFunc(string p) => new DirectoryInfo(p).GetDirectories();
            return _getDirectoryInfosFunc ?? DefaultFunc;
        }
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

        var watcher = RegisterFileWatcher(_watchPath, enableRaisingEvents: false);

        foreach (var dirInfo in GetDirectoryInfosFunc(path))
        {
            // TODO: consider skipping hidden/system folders? 
            // See IG Issue #405 comment below

            if (IsSymbolicLinkDirectory(dirInfo.FullName))
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
            RegisterFileWatcher(path);
        }

        foreach (var item in GetDirectoryInfosFunc(path))
        {
            if (IsSymbolicLinkDirectory(item.FullName))
            {
                if (!_fwDictionary.ContainsKey(item.FullName))
                {
                    RegisterFileWatcher(item.FullName);
                }

                MakeWatcher(item.FullName);
            }
        }
    }


    internal void MakeWatcher_Created(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (IsSymbolicLinkDirectory(e.FullPath))
            {
                RegisterFileWatcher(e.FullPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("<<ERROR>>: " + ex.Message);
        }
    }

    private IFileSystemWatcherWrapper RegisterFileWatcher(string path, bool enableRaisingEvents = true)
    {
        var fileWatcher = _watcherFactory();
        fileWatcher.Path = path;
        // this is identical to the default value:
        // https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher.notifyfilter?view=net-7.0#property-value
        fileWatcher.NotifyFilter = NotifyFilters.LastWrite
                                   | NotifyFilters.FileName
                                   | NotifyFilters.DirectoryName;
        
        fileWatcher.IncludeSubdirectories = true;
        fileWatcher.EnableRaisingEvents = enableRaisingEvents;

        // Bind internal events to manipulate the possible symbolic links
        fileWatcher.Created += MakeWatcher_Created;
        fileWatcher.Deleted += MakeWatcher_Deleted;

        fileWatcher.Changed += (_, e) => ProcessEvent(e, ChangeType.CHANGED);
        fileWatcher.Created += (_, e) => ProcessEvent(e, ChangeType.CREATED);
        fileWatcher.Deleted += (_, e) => ProcessEvent(e, ChangeType.DELETED);
        fileWatcher.Renamed += (_, e) => ProcessEvent(e);
        fileWatcher.Error += (_, e) => _onError?.Invoke(e);
        
        //changing this to a higher value can lead into issues when watching UNC drives
        fileWatcher.InternalBufferSize = 32768;

        _fwDictionary.Add(path, fileWatcher);
        return fileWatcher;
    }

    private bool IsSymbolicLinkDirectory(string path)
    {
        var attrs = GetFileAttributesFunc(path);
        return attrs.HasFlag(FileAttributes.Directory)
               && attrs.HasFlag(FileAttributes.ReparsePoint);
    }
    
    internal void MakeWatcher_Deleted(object sender, FileSystemEventArgs e)
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