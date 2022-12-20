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
        RegisterFileWatchersForSymbolicLinkDirs(path);
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
        fileWatcher.Created += RegisterWatcherForSymbolicLinkDir;
        fileWatcher.Deleted += RemoveWatcherForSymbolicLinkDir;

        //changing this to a higher value can lead into issues when watching UNC drives
        fileWatcher.InternalBufferSize = 32768;

        _fwDictionary.Add(path, fileWatcher);
        return fileWatcher;
    }
    
    
    private void RegisterFileWatchersForSymbolicLinkDirs(string path)
    {
        // TODO check if test for nested sym links exists
        // then make it just one recursive function
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
    }

    private void MakeWatcher(string path)
    {
        RegisterFileWatcherIfNotExists(path);

        foreach (var item in GetDirectoryInfosFunc(path))
        {
            if (IsSymbolicLinkDirectory(item.FullName))
            {
                RegisterFileWatcherIfNotExists(item.FullName);
                MakeWatcher(item.FullName);
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
    
    
    private void RegisterFileWatcherIfNotExists(string path)
    {
        if (! _fwDictionary.ContainsKey(path))
        {
            RegisterFileWatcher(path);
        }
    }

    
    private bool IsSymbolicLinkDirectory(string path)
    {
        var attrs = GetFileAttributesFunc(path);
        return attrs.HasFlag(FileAttributes.Directory)
               && attrs.HasFlag(FileAttributes.ReparsePoint);
    }
    
    
    /// <summary>
    /// Register additional filewatcher if the file event is a symbolic link directory.
    /// </summary>
    internal void RegisterWatcherForSymbolicLinkDir(object sender, FileSystemEventArgs e)
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

    /// <summary>
    /// Cleanup filewatcher if a symbolic link dir is deleted
    /// </summary>
    internal void RemoveWatcherForSymbolicLinkDir(object sender, FileSystemEventArgs e)
    {
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