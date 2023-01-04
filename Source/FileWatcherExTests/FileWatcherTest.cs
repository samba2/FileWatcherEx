using System.Collections.ObjectModel;
using System.ComponentModel;
using FileWatcherEx;
using FileWatcherEx.Helpers;
using FileWatcherExTests.Helper;
using Microsoft.VisualBasic;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace FileWatcherExTests;

public class FileWatcherTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    // TODO can this be on a test level
    private FileWatcher? _uut;
    private readonly List<Mock<IFileSystemWatcherWrapper>> _mocks;

    public FileWatcherTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _mocks = new List<Mock<IFileSystemWatcherWrapper>>();
    }

    [Fact]
    public void Root_Watcher_Is_Created()
    {
        using var dir = new TempDir();
        _uut = CreateFileWatcher(dir.FullPath);
        AssertContainsWatcherFor(dir.FullPath);
    }


    // creates 2 sub-directories and 2 nested symlinks
    // file watchers are registered for the root dir and the symlinks
    // for the sub-directories, no extra file watchers are created
    // since the normal file watcher already emits events for subdirs
    //
    // this handles the initial setup after start.
    // for registering sym-link watchers during runtime, MakeWatcher_Created is used.
    [Fact]
    public void FileWatchers_For_SymLink_Dirs_Are_Created_On_Startup()
    {
        using var dir = new TempDir();

        // {tempdir}/subdir1
        var subdirPath1 = dir.CreateSubDir("subdir1");

        // {tempdir}/subdir2
        var subdirPath2 = dir.CreateSubDir("subdir2");

        // symlink {tempdir}/sym1 to {tempdir}/subdir1
        var symlinkPath1 = dir.CreateSymlink(symLink: "sym1", target: subdirPath1);

        // symlink {tempdir}/sym1/sym2 to {tempdir}/subdir2
        var symlinkPath2 = dir.CreateSymlink(symLink: new []{"sym1", "sym2"}, target: subdirPath2);

        _uut = CreateFileWatcher(dir.FullPath);

        AssertContainsWatcherFor(dir.FullPath);
        AssertContainsWatcherFor(symlinkPath1);
        AssertContainsWatcherFor(symlinkPath2);
    }

    [Fact]
    public void FileWatchers_For_SymLink_Dirs_Are_Created_During_Runtime()
    {
        using var dir = new TempDir();
        _uut = CreateFileWatcher(dir.FullPath);

        var subdirPath = dir.CreateSubDir("subdir");

        // simulate file watcher trigger
        _uut.TryRegisterFileWatcherForSymbolicLinkDir(subdirPath);

        // subdir is ignored
        Assert.Single(_uut.FwDictionary);
        AssertContainsWatcherFor(dir.FullPath);

        var symlinkPath = dir.CreateSymlink(symLink: "sym", target: subdirPath);

        // simulate file watcher trigger
        _uut.TryRegisterFileWatcherForSymbolicLinkDir(symlinkPath);

        // symlink dir is registered
        Assert.Equal(2, _uut.FwDictionary.Count);
        AssertContainsWatcherFor(dir.FullPath);
        AssertContainsWatcherFor(symlinkPath);

        //  remove the symlink again
        Directory.Delete(symlinkPath);

        // simulate file watcher trigger
        _uut.UnregisterFileWatcherForSymbolicLinkDir(null,
            new FileSystemEventArgs(WatcherChangeTypes.Deleted, dir.FullPath, "sym"));

        // sym-link file watcher is removed
        Assert.Single(_uut.FwDictionary);
        AssertContainsWatcherFor(dir.FullPath);

        _uut.Dispose();
    }

    [Fact]
    public void MakeWatcher_Create_Exceptions_Are_Silently_Ignored()
    {
        _uut = CreateFileWatcher("/bar");
        _uut.TryRegisterFileWatcherForSymbolicLinkDir("/not/existing/foo");
    }

    [Fact]
    public void Properties_Are_Propagated()
    {
        using var dir = new TempDir();

        var subDir = dir.CreateSubDir("subdir");

        // symlink for detection at startup
        dir.CreateSymlink(
            symLink: "sym1",
            target: subDir); 

        _uut = new FileWatcher(dir.FullPath,
            _ => { },
            _ => { },
            WatcherFactoryWithMemory,
            _ => { });

        // perform settings. all, except SynchronizingObject are propagated
        // to all registered watchers
        _uut.NotifyFilter = NotifyFilters.LastAccess;
        _uut.Filters.Add("*.foo");
        _uut.Filters.Add("*.bar");
        _uut.EnableRaisingEvents = true;
        var syncObj = new Mock<ISynchronizeInvoke>().Object;
        _uut.SynchronizingObject = syncObj;

        // finish object initialization
        _uut.Init();
        
        // create symlink at runtime
        var symlinkPath2 = dir.CreateSymlink(
            symLink: "sym2",
            target: subDir); 

        // simulate that a new symlink dir was added
        _uut.TryRegisterFileWatcherForSymbolicLinkDir(symlinkPath2);
        
        // 1x root watcher, 1x sym link at startup, 1x sym link at runtime 
        Assert.Equal(3, _mocks.Count);
        // all watchers have properties set
        Assert.All(
            _mocks,
            mock =>
                mock.VerifySet(w => w.NotifyFilter = NotifyFilters.LastAccess));
        Assert.All(
            _mocks,
            mock =>
                mock.VerifySet(w => w.EnableRaisingEvents = true));
        Assert.All(
            _mocks, 
            mock => Assert.Equal(mock.Object.Filters, new Collection<string> { "*.foo", "*.bar" }));

        // sync. object is only set for root watcher
        // TODO rename root watcher
        Assert.Collection(_mocks, 
              mock =>
              {
                  mock.VerifySet(w => w.Path = dir.FullPath);
                  mock.VerifySet(w => w.SynchronizingObject = syncObj);
              },
              mock => mock.VerifySet(w => w.SynchronizingObject = syncObj, Times.Never),
              mock => mock.VerifySet(w => w.SynchronizingObject = syncObj, Times.Never));
    }

    private FileWatcher CreateFileWatcher(string path)
    {
        var fw = new FileWatcher(path,
            _ => { },
            _ => { },
            WatcherFactoryWithMemory,
            _ => { });
        fw.Init();
        return fw;
    }

    private IFileSystemWatcherWrapper WatcherFactoryWithMemory()
    {
        var mock = new Mock<IFileSystemWatcherWrapper>();
        // this did the trick to have the 'Filters' property be recorded
        mock.SetReturnsDefault(new Collection<string>());
        _mocks.Add(mock);
        return mock.Object;
    }

    private void AssertContainsWatcherFor(string path)
    {
        var _ = _uut.FwDictionary[path];
        var foundMocks = (
                from mock in _mocks
                where HasPropertySetTo(mock, watcher => watcher.Path = path)
                select mock)
            .Count();
        Assert.Equal(1, foundMocks);
    }

    private static bool HasPropertySetTo(Mock<IFileSystemWatcherWrapper> mock, Action<IFileSystemWatcherWrapper> setterExpression)
    {
        try
        {
            mock.VerifySet(setterExpression);
            return true;
        }
        catch (MockException)
        {
            return false;
        }
    }

}