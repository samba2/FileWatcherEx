using FileWatcherEx;
using FileWatcherEx.Helpers;
using FileWatcherExTests.Helper;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace FileWatcherExTests;

public class FileSystemWatcherCreationTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly FileWatcher _uut;
    private readonly List<Mock<IFileSystemWatcherWrapper>> _mocks;

    public FileSystemWatcherCreationTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _uut = new FileWatcher();
        _mocks = new List<Mock<IFileSystemWatcherWrapper>>();

    }

    [Fact]
    public void Root_Watcher_Is_Created()
    {
        using var dir = new TempDir();

        _uut.Create(
            dir.FullPath, 
            _ => {}, 
            _ => {},
            WatcherFactoryWithMemory,
            _ => {});
            
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
        var subdirPath1 = Path.Combine(dir.FullPath, "subdir1");
        Directory.CreateDirectory(subdirPath1);

        // {tempdir}/subdir2
        var subdirPath2 = Path.Combine(dir.FullPath, "subdir2");
        Directory.CreateDirectory(subdirPath2);

        // symlink {tempdir}/sym1 to {tempdir}/subdir1
        var symlinkPath1 = Path.Combine(dir.FullPath, "sym1");
        Directory.CreateSymbolicLink(symlinkPath1, subdirPath1);
        
        // symlink {tempdir}/sym1/sym2 to {tempdir}/subdir2
        var symlinkPath2 = Path.Combine(dir.FullPath, "sym1", "sym2");
        Directory.CreateSymbolicLink(symlinkPath2, subdirPath2);

        _uut.Create(
            dir.FullPath, 
            _ => {}, 
            _ => {},
            WatcherFactoryWithMemory,
            _ => {});
        
        AssertContainsWatcherFor(dir.FullPath);
        AssertContainsWatcherFor(symlinkPath1);
        AssertContainsWatcherFor(symlinkPath2);
    }
    
    [Fact]
    public void FileWatchers_For_SymLink_Dirs_Are_Created_During_Runtime()
    {
        using var dir = new TempDir();
        _uut.Create(
            dir.FullPath, 
            _ => {}, 
            _ => {},
            WatcherFactoryWithMemory,
            _ => {});
        
        // create subdir
        var subdirPath = Path.Combine(dir.FullPath, "subdir");
        Directory.CreateDirectory(subdirPath);
        
        // simulate file watcher trigger
        _uut.TryRegisterFileWatcherForSymbolicLinkDir(subdirPath);

        // subdir is ignored
        Assert.Single(_uut.FwDictionary);
        AssertContainsWatcherFor(dir.FullPath);

        // create symlink
        var symlinkPath = Path.Combine(dir.FullPath, "sym");
        Directory.CreateSymbolicLink(symlinkPath, subdirPath);

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
        _uut.TryRegisterFileWatcherForSymbolicLinkDir("/not/existing/foo");
    }
    
    private void AssertContainsWatcherFor(string path)
    {
        var _ = _uut.FwDictionary[path];
        var foundMocks = (
            from mock in _mocks 
            where IsMockFor(mock, path) 
            select mock)
            .Count();
        Assert.Equal(1, foundMocks);        
    }

    private bool IsMockFor(Mock<IFileSystemWatcherWrapper> mock, string path)
    {
        try
        {
            mock.VerifySet(w => w.Path = path);
            return true;
        }
        catch (MockException)
        {
            return false;
        }
    }
    
    private IFileSystemWatcherWrapper WatcherFactoryWithMemory()
    {
        var mock = new Mock<IFileSystemWatcherWrapper>();
        _mocks.Add(mock);
        return mock.Object;
    }
}