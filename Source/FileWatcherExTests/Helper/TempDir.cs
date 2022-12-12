namespace FileWatcherExTests.Helper;

public class TempDir : IDisposable
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
