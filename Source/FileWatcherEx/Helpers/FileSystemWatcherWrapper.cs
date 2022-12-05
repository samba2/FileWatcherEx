namespace FileWatcherEx;

/// <summary>
/// Interface around .NET FileSystemWatcher to be able to replace it with a fake implementation
/// </summary>
public interface IFileSystemWatcherWrapper
{
    string Path { get; set; }
    bool IncludeSubdirectories { get; set; }
    bool EnableRaisingEvents { get; set; }
    NotifyFilters NotifyFilter { get; set; }

    event FileSystemEventHandler Created;
    event FileSystemEventHandler Deleted;
    event FileSystemEventHandler Changed;
    event RenamedEventHandler Renamed;
    event ErrorEventHandler Error;
    
    int InternalBufferSize { get; set; }
}

/// <summary>
/// Production implementation of  IFileSystemWrapper interface.
/// Backed by the existing FileSystemWatcher
/// </summary>
public class FileSystemWatcherWrapper : FileSystemWatcher, IFileSystemWatcherWrapper
{
    // empty on purpose
    
}

