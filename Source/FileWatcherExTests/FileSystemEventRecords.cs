using System.Collections.Concurrent;
using FileWatcherEx;

namespace FileWatcherExTests;

public class FileSystemEventRecords
{
    private static ConcurrentQueue<string> queue = new();

    public static void Main()
    {
            var watcher = new FileSystemWatcherWrapper();
            watcher.Path = @"c:\temp\fwtest";  // TODO via CLI args
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.LastWrite
                                   | NotifyFilters.FileName
                                   | NotifyFilters.DirectoryName;

            // Bind internal events to manipulate the possible symbolic links
            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;

            watcher.Changed += OnChanged;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;

            //changing this to a higher value can lead into issues when watching UNC drives
            watcher.InternalBufferSize = 32768;
    }

    private static  void OnCreated(object sender, FileSystemEventArgs fileSystemEventArgs)
    {
        Console.WriteLine(fileSystemEventArgs);
    }

    private static void OnDeleted(object sender, FileSystemEventArgs fileSystemEventArgs)
    {
        
    }
    private static void OnChanged(object sender, FileSystemEventArgs fileSystemEventArgs)
    {
        
    }
    private static void OnRenamed(object sender, RenamedEventArgs renamedEventArgs)
    {
        
    }
    private static void OnError(object sender, ErrorEventArgs errorEventArgs)
    {
        
    }
}