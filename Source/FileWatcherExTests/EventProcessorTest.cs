using Xunit;
using FileWatcherEx;

namespace FileWatcherExTests;

public class EventProcessorTest
{
    [Fact]
    public void No_Input_Gives_No_Output()
    {
        var events = NormalizeEvents(Array.Empty<FileChangedEvent>());
        Assert.Empty(events);
    }

    [Fact]
    public void Single_Event_Is_Passed_Through()
    {
        var events = NormalizeEvents(
            new FileChangedEvent()
            {
                ChangeType = ChangeType.CREATED,
                FullPath = @"c:\foo",
                OldFullPath = @"c:\bar"
            }
        );

        Assert.Single(events);
        var ev = events.First();
        Assert.Equal(ChangeType.CREATED, ev.ChangeType);
        Assert.Equal(@"c:\foo", ev.FullPath);
        Assert.Equal(@"c:\bar", ev.OldFullPath);
    }

    [Fact]
    public void On_Duplicate_Events_The_Latest_Is_Taken()
    {
        var events = NormalizeEvents(
            new FileChangedEvent
            {
                ChangeType = ChangeType.CREATED,
                FullPath = @"c:\foo",
                OldFullPath = null
            },
            new FileChangedEvent
            {
                ChangeType = ChangeType.RENAMED, // differs
                FullPath = @"c:\foo",
                OldFullPath = @"c:\bar" // differs as well
            }
        );

        Assert.Single(events);
        var ev = events.First();
        Assert.Equal(ChangeType.RENAMED, ev.ChangeType);
        Assert.Equal(@"c:\foo", ev.FullPath);
        Assert.Equal(@"c:\bar", ev.OldFullPath);
    }

    [Fact]
    public void On_Consecutive_Renaming_The_Events_Are_Merged()
    {
        var events = NormalizeEvents(
            new FileChangedEvent
            {
                ChangeType = ChangeType.RENAMED,
                FullPath = @"c:\foo",
                OldFullPath = @"c:\bar"
            },
            new FileChangedEvent
            {
                ChangeType = ChangeType.RENAMED,
                FullPath = @"c:\bazz",
                OldFullPath = @"c:\foo" // refers to previous renamedEvent1.FullPath
            }
        );
        Assert.Single(events);
        var ev = events.First();
        Assert.Equal(ChangeType.RENAMED, ev.ChangeType);
        Assert.Equal(@"c:\bazz", ev.FullPath);
        Assert.Equal(@"c:\bar", ev.OldFullPath);
    }

    [Fact]
    public void Rename_After_Create_Gives_Created_Event_With_Updated_Path()
    {
        var events = NormalizeEvents(
            new FileChangedEvent
            {
                ChangeType = ChangeType.CREATED,
                FullPath = @"c:\foo",
                OldFullPath = @"c:\bar"
            },
            new FileChangedEvent
            {
                ChangeType = ChangeType.RENAMED,
                FullPath = @"c:\bar",
                OldFullPath = @"c:\foo" // refers to previous createdEvent.FullPath
            }
        );

        Assert.Single(events);
        var ev = events.First();
        Assert.Equal(ChangeType.CREATED, ev.ChangeType);
        Assert.Equal(@"c:\bar", ev.FullPath);
        Assert.Null(ev.OldFullPath);
    }

    [Fact]
    // This is a complex case, originally extracted by using test coverage.
    // Scenario:
    // - file foo is created
    // - file bar is deleted
    // - now foo is renamed to the just deleted bar
    // - this results into a bar changed event
    public void Rename_After_Create_Gives_Changed_Event()
    {
        var events = NormalizeEvents(
            new FileChangedEvent
            {
                ChangeType = ChangeType.CREATED,
                FullPath = @"c:\foo",
                OldFullPath = null
            },
            new FileChangedEvent
            {
                ChangeType = ChangeType.DELETED,
                FullPath = @"c:\bar",
                OldFullPath = null
            },
            new FileChangedEvent
            {
                ChangeType = ChangeType.RENAMED,
                FullPath = @"c:\bar",
                OldFullPath = @"c:\foo"
            }
        );

        Assert.Single(events);
        var ev = events.First();
        Assert.Equal(ChangeType.CHANGED, ev.ChangeType);
        Assert.Equal(@"c:\bar", ev.FullPath);
        Assert.Null(ev.OldFullPath);
    }


    private static List<FileChangedEvent> NormalizeEvents(params FileChangedEvent[] events)
    {
        return EventProcessor.NormalizeEvents(events).ToList();
    }
}