using System.Collections.Concurrent;
using FileWatcherEx;
using Xunit;
using Xunit.Abstractions;

namespace FileWatcherExTests;

public class FileWatcherExIntegrationTest
{
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