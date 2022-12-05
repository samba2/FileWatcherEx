using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CsvHelper;
using FileWatcherEx;
using Xunit;
using Xunit.Abstractions;

namespace FileWatcherExTests;

internal record EventRecordWithDiff(
    string FullPath,
    string EventName,
    string? OldFullPath,
    long DiffInTicks,
    double DiffInMilliseconds
);

class ReplayFileSystemWatcherWrapper : IFileSystemWatcherWrapper
{
    private string _csvFile;
    public ReplayFileSystemWatcherWrapper(string csvFile)
    {
        _csvFile = csvFile;
    }
    
    public string Path { get; set; }
    public Collection<string> Filters { get; }
    public bool IncludeSubdirectories { get; set; }
    public bool EnableRaisingEvents { get; set; }
    public NotifyFilters NotifyFilter { get; set; }
    public event FileSystemEventHandler? Created;
    public event FileSystemEventHandler? Deleted;
    public event FileSystemEventHandler? Changed;
    public event RenamedEventHandler? Renamed;
    public event ErrorEventHandler? Error;
    public int InternalBufferSize { get; set; }
    public ISynchronizeInvoke? SynchronizingObject { get; set; }
    public void Dispose()
    {
    }

    public void Replay()
    {
        using var reader = new StreamReader(_csvFile);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        
        var records = csv.GetRecords<EventRecordWithDiff>();
        foreach (var record in records)
        {
            var directory = System.IO.Path.GetDirectoryName(record.FullPath);
            var fileName = System.IO.Path.GetFileName(record.FullPath);
            
            switch (record.EventName)
            {
                case "created":
                {
                    var ev = new FileSystemEventArgs(WatcherChangeTypes.Created, directory, fileName);
                    Created?.Invoke(this, ev);
                    break;
                }
                case "deleted":
                {
                    var ev = new FileSystemEventArgs(WatcherChangeTypes.Deleted, directory, fileName);
                    Deleted?.Invoke(this, ev);
                    break;
                }
                case "changed":
                {
                    var ev = new FileSystemEventArgs(WatcherChangeTypes.Changed, directory, fileName);
                    Changed?.Invoke(this, ev);
                    break;
                }
                case "renamed":
                {
                    var oldFileName = System.IO.Path.GetFileName(record.OldFullPath);
                    var ev = new RenamedEventArgs(WatcherChangeTypes.Renamed, directory, fileName, oldFileName);
                    Renamed?.Invoke(this, ev);
                    break;
                }
            }
        }
    }
}

public class FileWatcherExIntegrationTest
{
    
    [Fact]
    public void Foo()
    {
        ConcurrentQueue<FileChangedEvent> events = new();
        var replayFw = new ReplayFileSystemWatcherWrapper(@"scenario\create_file.csv");
        var unusedDir = Path.GetTempPath();
        var fw = new FileSystemWatcherEx(unusedDir, replayFw);

        fw.OnCreated += (_, ev) => events.Enqueue(ev);
        fw.OnDeleted += (_, ev) => events.Enqueue(ev);
        fw.OnChanged += (_, ev) => events.Enqueue(ev);
        fw.OnRenamed += (_, ev) => events.Enqueue(ev);

        fw.Start();
        replayFw.Replay();
        Thread.Sleep(250);
        fw.Stop();

        Assert.Single(events);
        var ev = events.First();
        Assert.Equal(ChangeType.CREATED, ev.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\a.txt", ev.FullPath);
        Assert.Equal("", ev.OldFullPath);
    }

    
    [Fact]
    public void SimpleRealFileSystemTest()
    {
        ConcurrentQueue<FileChangedEvent> events = new();
        var fw = new FileSystemWatcherEx(@"c:\temp\fwtest\");

        fw.OnCreated += (_, ev) => events.Enqueue(ev);
        fw.OnDeleted += (_, ev) => events.Enqueue(ev);
        fw.OnChanged += (_, ev) => events.Enqueue(ev);
        fw.OnRenamed += (_, ev) => events.Enqueue(ev);
        fw.OnRenamed += (_, ev) => events.Enqueue(ev);

        const string testFile = @"c:\temp\fwtest\b.txt";
        if (File.Exists(testFile))
        {
            File.Delete(testFile);
        }

        fw.Start();
        File.Create(testFile);
        Thread.Sleep(250);
        fw.Stop();

        Assert.Single(events);
        var ev = events.First();
        Assert.Equal(ChangeType.CREATED, ev.ChangeType);
        Assert.Equal(@"c:\temp\fwtest\b.txt", ev.FullPath);
        Assert.Equal("", ev.OldFullPath);
    }
}