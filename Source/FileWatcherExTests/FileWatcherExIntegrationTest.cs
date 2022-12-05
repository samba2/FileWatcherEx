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

    [Fact]
    public void Create_And_Remove_Single_File()
    {
        _fileWatcher.Start();
        _replayer.Replay(@"scenario\create_and_remove_file.csv");
        _fileWatcher.Stop();

        Assert.Equal(2, _events.Count);
        var ev1 = _events.ToList()[0];
        var ev2 = _events.ToList()[1];

        Assert.Equal(ChangeType.CREATED, ev1.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\a.txt", ev1.FullPath);
        Assert.Equal("", ev1.OldFullPath);

        Assert.Equal(ChangeType.DELETED, ev2.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\a.txt", ev2.FullPath);
        Assert.Equal("", ev2.OldFullPath);
    }


    [Fact]
    public void Create_Rename_And_Remove_Single_File()
    {
        _fileWatcher.Start();
        _replayer.Replay(@"scenario\create_rename_and_remove_file.csv");
        _fileWatcher.Stop();

        Assert.Equal(3, _events.Count);
        var ev1 = _events.ToList()[0];
        var ev2 = _events.ToList()[1];
        var ev3 = _events.ToList()[2];

        Assert.Equal(ChangeType.CREATED, ev1.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\a.txt", ev1.FullPath);

        Assert.Equal(ChangeType.RENAMED, ev2.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\b.txt", ev2.FullPath);
        Assert.Equal(@"C:\temp\fwtest\a.txt", ev2.OldFullPath);

        Assert.Equal(ChangeType.DELETED, ev3.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\b.txt", ev3.FullPath);
        Assert.Equal("", ev3.OldFullPath);
    }

    [Fact]
    // filters out 2nd "changed" event
    public void Create_Single_File_Via_WSL2()
    {
        _fileWatcher.Start();
        _replayer.Replay(@"scenario\create_file_wsl2.csv");
        _fileWatcher.Stop();

        Assert.Single(_events);
        var ev = _events.First();
        Assert.Equal(ChangeType.CREATED, ev.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\a.txt", ev.FullPath);
        Assert.Equal("", ev.OldFullPath);
    }

    [Fact]
    // scenario creates "created" "changed" and "renamed" event.
    // resulting event is just "created" with the filename taken from "renamed"
    public void Create_And_Rename_Single_File_Via_WSL2()
    {
        _fileWatcher.Start();
        _replayer.Replay(@"scenario\create_and_rename_file_wsl2.csv");
        _fileWatcher.Stop();

        Assert.Single(_events);
        var ev = _events.First();
        Assert.Equal(ChangeType.CREATED, ev.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\b.txt", ev.FullPath);
        Assert.Null(ev.OldFullPath);
    }

    [Fact]
    public void Create_Rename_And_Remove_Single_File_Via_WSL2()
    {
        _fileWatcher.Start();
        _replayer.Replay(@"scenario\create_rename_and_remove_file_wsl2.csv");
        _fileWatcher.Stop();

        Assert.Empty(_events);
    }

    
    [Fact(Skip = "requires real (Windows) file system")]
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