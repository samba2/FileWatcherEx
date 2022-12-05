using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CsvHelper;
using FileWatcherEx;
using Xunit;
using Xunit.Abstractions;

namespace FileWatcherExTests;

public class FileWatcherExIntegrationTest
{
    
    [Fact]
    public void Create_Single_File()
    {
        ConcurrentQueue<FileChangedEvent> events = new();
        ReplayFileSystemWatcherWrapper replayer = new();
        var unusedDir = Path.GetTempPath();
        var fileWatcher = new FileSystemWatcherEx(unusedDir, replayer);

        fileWatcher.OnCreated += (_, ev) => events.Enqueue(ev);
        fileWatcher.OnDeleted += (_, ev) => events.Enqueue(ev);
        fileWatcher.OnChanged += (_, ev) => events.Enqueue(ev);
        fileWatcher.OnRenamed += (_, ev) => events.Enqueue(ev);

        fileWatcher.Start();
        replayer.Replay(@"scenario\create_file.csv");
        fileWatcher.Stop();

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