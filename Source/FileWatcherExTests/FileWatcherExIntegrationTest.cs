using System.Collections.Concurrent;
using FileWatcherEx;
using Xunit;

namespace FileWatcherExTests;

public class FileWatcherExIntegrationTest
{
    private ConcurrentQueue<FileChangedEvent> _events;
    private ReplayFileSystemWatcherWrapper _replayer;
    private FileSystemWatcherEx _fileWatcher;

    public FileWatcherExIntegrationTest()
    {
        // setup before each test run
        _events = new();
        _replayer = new();
        var unusedDir = Path.GetTempPath();
        _fileWatcher = new FileSystemWatcherEx(unusedDir, _replayer);

        _fileWatcher.OnCreated += (_, ev) => _events.Enqueue(ev);
        _fileWatcher.OnDeleted += (_, ev) => _events.Enqueue(ev);
        _fileWatcher.OnChanged += (_, ev) => _events.Enqueue(ev);
        _fileWatcher.OnRenamed += (_, ev) => _events.Enqueue(ev);
    }
    
    
    [Fact]
    public void Create_Single_File()
    {
        _fileWatcher.Start();
        _replayer.Replay(@"scenario\create_file.csv");
        _fileWatcher.Stop();

        Assert.Single(_events);
        var ev = _events.First();
        Assert.Equal(ChangeType.CREATED, ev.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\a.txt", ev.FullPath);
        Assert.Equal("", ev.OldFullPath);
    }
    
    
    [Fact (Skip = "requires real (Windows) file system")]
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