/*---------------------------------------------------------
 * Copyright (C) Microsoft Corporation. All rights reserved.
 *--------------------------------------------------------*/

namespace FileWatcherEx.Helpers;

internal class FileWatcher : IDisposable
{
    // TODO double check properties -> are they all needed ?
    private string _watchPath = string.Empty;
    private Action<FileChangedEvent>? _eventCallback = null;
    private readonly Dictionary<string, IFileSystemWatcherWrapper> _fwDictionary = new();
    private Action<ErrorEventArgs>? _onError = null;
    private Func<string, FileAttributes>? _getFileAttributesFunc;
    private Func<string, DirectoryInfo[]>? _getDirectoryInfosFunc;
    private Func<IFileSystemWatcherWrapper> _watcherFactory;
    private Action<string> _logger = _ => {};

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
    /// <param name="logger">logging callback</param>
    public FileWatcher(string path, Action<FileChangedEvent> onEvent, Action<ErrorEventArgs> onError, Func<IFileSystemWatcherWrapper> watcherFactory, Action<string> logger)
    {
        _logger = logger;
        _watcherFactory = watcherFactory;
        _watchPath = path;
        _eventCallback = onEvent;        
        _onError = onError;
    }
    
    public IFileSystemWatcherWrapper Init()
    {
        var watcher = RegisterFileWatcher(_watchPath, enableRaisingEvents: false);
        RegisterAdditionalFileWatchersForSymLinkDirs(_watchPath);
        return watcher;
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

        fileWatcher.Created += (_, e) => ProcessEvent(e, ChangeType.CREATED);
        fileWatcher.Changed += (_, e) => ProcessEvent(e, ChangeType.CHANGED);
        fileWatcher.Deleted += (_, e) => ProcessEvent(e, ChangeType.DELETED);
        fileWatcher.Renamed += (_, e) => ProcessRenamedEvent(e);
        fileWatcher.Error += (_, e) => _onError?.Invoke(e);
        
        // extra measures to handle symbolic link directories
        fileWatcher.Created += (_, e) => TryRegisterFileWatcherForSymbolicLinkDir(e.FullPath);
        fileWatcher.Deleted += UnregisterFileWatcherForSymbolicLinkDir;

        //changing this to a higher value can lead into issues when watching UNC drives
        fileWatcher.InternalBufferSize = 32768;

        _fwDictionary.Add(path, fileWatcher);
        return fileWatcher;
    }
    
    
    /// <summary>
    /// Recursively find sym link dir and register them.
    /// Background: the native filewatcher does not follow symlinks so they need to be treated separately.
    /// </summary>
    private void RegisterAdditionalFileWatchersForSymLinkDirs(string path)
    {
        TryRegisterFileWatcherForSymbolicLinkDir(path);

        if (Directory.Exists(path))
        {
            foreach (var dirInfo in GetDirectoryInfosFunc(path))
            {
                RegisterAdditionalFileWatchersForSymLinkDirs(dirInfo.FullName);
            }
        }
    }

    
    /// <summary>
    /// Process event for type = [CHANGED; DELETED; CREATED]
    /// </summary>
    private void ProcessEvent(FileSystemEventArgs e, ChangeType changeType)
    {
        _eventCallback?.Invoke(new()
        {
            ChangeType = changeType,
            FullPath = e.FullPath,
        });
    }
    
    
    private void ProcessRenamedEvent(RenamedEventArgs e)
    {
        _eventCallback?.Invoke(new()
        {
            ChangeType = ChangeType.RENAMED,
            FullPath = e.FullPath,
            OldFullPath = e.OldFullPath,
        });
    }
    
    
    internal void TryRegisterFileWatcherForSymbolicLinkDir(string path)
    {
        try
        {
            if ( IsSymbolicLinkDirectory(path) && !_fwDictionary.ContainsKey(path) )
            {
                RegisterFileWatcher(path);
            }
        }
        catch (Exception ex)
        {
            // IG Issue #405: throws exception on Windows 10
            // for "c:\users\user\application data" folder and sub-folders.
            _logger($"Error registering file system watcher for directory '{path}'. Error was: {ex.Message}");
        }
    }

    
    /// <summary>
    /// Cleanup filewatcher if a symbolic link dir is deleted
    /// </summary>
    internal void UnregisterFileWatcherForSymbolicLinkDir(object sender, FileSystemEventArgs e)
    {
        if (_fwDictionary.ContainsKey(e.FullPath))
        {
            _fwDictionary[e.FullPath].Dispose();
            _fwDictionary.Remove(e.FullPath);
        }
    }
    
    
    private bool IsSymbolicLinkDirectory(string path)
    {
        var attrs = GetFileAttributesFunc(path);
        return attrs.HasFlag(FileAttributes.Directory)
               && attrs.HasFlag(FileAttributes.ReparsePoint);
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