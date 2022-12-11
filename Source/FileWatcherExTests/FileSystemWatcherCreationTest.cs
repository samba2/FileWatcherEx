using FileWatcherEx;
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
            e => {}, 
            e => {},
            WatcherFactoryWithMemory);
            
        AssertContainsWatcherFor(dir.FullPath);
    }

    
    // creates 2 sub-directories and 2 nested symlinks
    // file watchers are registered for the root dir and the symlinks
    // for the sub-directories, no extra file watchers are created
    // since the normal file watcher already emits events for subdirs
    [Fact]
    public void Root_And_Subdir_Watcher_Are_Created()
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
            e => {}, 
            e => {},
            WatcherFactoryWithMemory);
        
        AssertContainsWatcherFor(dir.FullPath);
        AssertContainsWatcherFor(symlinkPath1);
        AssertContainsWatcherFor(symlinkPath2);
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
    
    
    private class TempDir : IDisposable
    {
        public string FullPath { get; }

        public TempDir() 
        {
            FullPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(FullPath);
        }

        public void Dispose()
        {
            Directory.Delete(FullPath, true);
        }
    }

    private IFileSystemWatcherWrapper WatcherFactoryWithMemory()
    {
        var mock = new Mock<IFileSystemWatcherWrapper>();
        _mocks.Add(mock);
        return mock.Object;
    }
}