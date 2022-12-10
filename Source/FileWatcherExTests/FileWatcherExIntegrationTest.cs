using System.Collections.Concurrent;
using FileWatcherEx;
using Xunit;

namespace FileWatcherExTests;

// TODO subdirectory tests

/// <summary>
/// Integration/ Golden master test for FileWatcherEx
/// Considers C:\temp\fwtest to be the test directory
/// </summary>
public class FileWatcherExIntegrationTest : IDisposable
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
        
        const string recordingDir = @"C:\temp\fwtest";
        _fileWatcher = new FileSystemWatcherEx(recordingDir, _replayer);

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

    
    [Fact]
    public void Create_Rename_And_Remove_Single_File_With_Wait_Time_Via_WSL2()
    {
        _fileWatcher.Start();
        _replayer.Replay(@"scenario\create_rename_and_remove_file_with_wait_time_wsl2.csv");
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
    
    [Fact]
    public void ManuallyCreateAndRenameFileViaWindowsExplorer()
    {
        _fileWatcher.Start();
        _replayer.Replay(@"scenario\create_and_rename_file_via_explorer.csv");
        _fileWatcher.Stop();

        Assert.Equal(2, _events.Count);

        var ev1 = _events.ToList()[0];
        var ev2 = _events.ToList()[1];

        Assert.Equal(ChangeType.CREATED, ev1.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\New Text Document.txt", ev1.FullPath);

        Assert.Equal(ChangeType.RENAMED, ev2.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\foo.txt", ev2.FullPath);
        Assert.Equal(@"C:\temp\fwtest\New Text Document.txt", ev2.OldFullPath);
    }

    [Fact]
    public void ManuallyCreateRenameAndDeleteFileViaWindowsExplorer()
    {
        _fileWatcher.Start();
        _replayer.Replay(@"scenario\create_rename_and_delete_file_via_explorer.csv");
        _fileWatcher.Stop();

        Assert.Equal(3, _events.Count);
        var ev1 = _events.ToList()[0];
        var ev2 = _events.ToList()[1];
        var ev3 = _events.ToList()[2];

        Assert.Equal(ChangeType.CREATED, ev1.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\New Text Document.txt", ev1.FullPath);

        Assert.Equal(ChangeType.RENAMED, ev2.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\foo.txt", ev2.FullPath);
        Assert.Equal(@"C:\temp\fwtest\New Text Document.txt", ev2.OldFullPath);

        Assert.Equal(ChangeType.DELETED, ev3.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\foo.txt", ev3.FullPath);
        Assert.Equal("", ev3.OldFullPath);
    }

    [Fact]
    public void DownloadImageViaEdgeBrowser()
    {
        _fileWatcher.Start();
        _replayer.Replay(@"scenario\download_image_via_Edge_browser.csv");
        _fileWatcher.Stop();

        Assert.Equal(2, _events.Count);
        var ev1 = _events.ToList()[0];
        var ev2 = _events.ToList()[1];

        Assert.Equal(ChangeType.CREATED, ev1.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\test.png.crdownload", ev1.FullPath);
        
        Assert.Equal(ChangeType.RENAMED, ev2.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\test.png", ev2.FullPath);
        Assert.Equal(@"C:\temp\fwtest\test.png.crdownload", ev2.OldFullPath);
    }

    // instantly removed file is not in the events list
    [Fact]
    public void CreateSubDirectoryAddAndRemoveFile()
    {
        _fileWatcher.Start();
        _replayer.Replay(@"scenario\create_subdirectory_add_and_remove_file.csv");
        _fileWatcher.Stop();

        Assert.Equal(2, _events.Count);
        var ev1 = _events.ToList()[0];
        var ev2 = _events.ToList()[1];
        
        Assert.Equal(ChangeType.CREATED, ev1.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\subdir", ev1.FullPath);
        
        Assert.Equal(ChangeType.CHANGED, ev2.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\subdir", ev2.FullPath);
        Assert.Equal(@"", ev2.OldFullPath);
    }
    
    [Fact]
    public void CreateSubDirectoryAddAndRemoveFileWithSleep()
    {
        _fileWatcher.Start();
        _replayer.Replay(@"scenario\create_subdirectory_add_and_remove_file_with_sleep.csv");
        _fileWatcher.Stop();

        Assert.Equal(4, _events.Count);
        var ev1 = _events.ToList()[0];
        var ev2 = _events.ToList()[1];
        var ev3 = _events.ToList()[2];
        var ev4 = _events.ToList()[3];
        
        Assert.Equal(ChangeType.CREATED, ev1.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\subdir", ev1.FullPath);
        
        Assert.Equal(ChangeType.CREATED, ev2.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\subdir\a.txt", ev2.FullPath);
        Assert.Equal(@"", ev2.OldFullPath);

        // TODO this could be filtered out
        Assert.Equal(ChangeType.CHANGED, ev3.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\subdir", ev3.FullPath);
        Assert.Equal(@"", ev3.OldFullPath);

        Assert.Equal(ChangeType.DELETED, ev4.ChangeType);
        Assert.Equal(@"C:\temp\fwtest\subdir\a.txt", ev4.FullPath);
        Assert.Equal(@"", ev4.OldFullPath);
    }

    // TODO This scenario tries to test the code paths checking for a "reparse point"
    // which is a symbolic link in NTFS: https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-points
    // Currently, the test setup does not support this. Namely, calls to new DirectoryInfo(path).GetDirectories() and
    // File.GetAttributes(...) would need to be wrapped and passed in e.g. as a Func
    [Fact (Skip = "test setup needs to be extended")]
    public void CreateFileInsideSymbolicLinkDirectory()
    {
        _fileWatcher.Start();
        _replayer.Replay(@"scenario\create_file_inside_symbolic_link_directory.csv");
        _fileWatcher.Stop();

        Assert.Equal(6, _events.Count);
    }

    // cleanup
    public void Dispose()
    {
        _fileWatcher.Dispose();
    }
}