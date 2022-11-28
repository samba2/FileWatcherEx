using Xunit;
using FileWatcherEx;

namespace FileWatcherExTests;

public class EventProcessorTest
{
    [Fact]
    public void No_Events_Give_Empty_Result()
    {
        var result = EventProcessor.NormalizeEvents(Array.Empty<FileChangedEvent>());
        Assert.Empty(result);

    }

    [Fact]
    public void Single_Event_Is_Passed_Through()
    {
        var ev = BuildFileCreatedEvent(@"c:\a", @"c:\a_old");

        var result = EventProcessor.NormalizeEvents(new[] { ev }).ToList();
        Assert.Single(result);
        Assert.Equal(ev, result.First());
    }

    [Fact]
    public void On_Duplicate_Events_The_Old_Event_Is_Updated_With_The_Newest_Data()
    {
        
        var createdEvent = new FileChangedEvent
        {
            ChangeType = ChangeType.CREATED,
            FullPath = @"c:\1.txt",
            OldFullPath = @"c:\1_old_path.txt"
        };
        

        var renamedEvent = new FileChangedEvent
        {
            ChangeType = ChangeType.RENAMED,     // differs
            FullPath = @"c:\1.txt",
            OldFullPath = @"c:\1_different_old_path.txt"   // differs as well
        };
        
        
        var events = EventProcessor.NormalizeEvents(new[] { createdEvent, renamedEvent }).ToList();
        Assert.Single(events);
        
        var ev = events.First();
        Assert.Equal(ChangeType.RENAMED, ev.ChangeType);
        Assert.Equal(@"c:\1.txt", ev.FullPath);
        Assert.Equal(@"c:\1_different_old_path.txt", ev.OldFullPath);
    }

    [Fact]
    public void On_Consecutive_Renaming_The_Events_Are_Merged()
    {
        
        var renamedEvent1 = new FileChangedEvent
        {
            ChangeType = ChangeType.RENAMED,
            FullPath = @"c:\1.txt",                
            OldFullPath = @"c:\1_old_path.txt"
        };
        
        var renamedEvent2 = new FileChangedEvent
        {
            ChangeType = ChangeType.RENAMED,
            FullPath = @"c:\1_new_name.txt",
            OldFullPath = @"c:\1.txt"      // refers to previous renamedEvent1.FullPath
        };
        
        var events = EventProcessor.NormalizeEvents(new[] { renamedEvent1, renamedEvent2 }).ToList();
        Assert.Single(events);
        
        var ev = events.First();
        Assert.Equal(ChangeType.RENAMED, ev.ChangeType);
        Assert.Equal(@"c:\1_new_name.txt", ev.FullPath);
        Assert.Equal(@"c:\1_old_path.txt", ev.OldFullPath);
    }

    [Fact]
    public void Rename_After_Create_Gives_Create_Event_With_Updated_Path()
    {
        
        var createdEvent = new FileChangedEvent
        {
            ChangeType = ChangeType.CREATED,
            FullPath = @"c:\1.txt",                
            OldFullPath = @"c:\1_old_path.txt"
        };
        
        var renameEvent = new FileChangedEvent
        {
            ChangeType = ChangeType.RENAMED,
            FullPath = @"c:\1_new_name.txt",
            OldFullPath = @"c:\1.txt"      // refers to previous createdEvent.FullPath
        };
        
        var events = EventProcessor.NormalizeEvents(new[] { createdEvent, renameEvent }).ToList();
        Assert.Single(events);
        
        var ev = events.First();
        Assert.Equal(ChangeType.CREATED, ev.ChangeType);
        Assert.Equal(@"c:\1_new_name.txt", ev.FullPath);
        Assert.Null(ev.OldFullPath);
    }


    
    // TODO better name
    // TODO revisit test method names
    // TODO helper function which takes array of FileChangedEvents, directly instanciate file changes in that call
    // TODO work with foo bar as path names ?
    
    [Fact]
    public void Delete_After_Create_Gives_TODO()
    {
        
        var deleteEvent = new FileChangedEvent
        {
            ChangeType = ChangeType.DELETED,
            FullPath = @"c:\foo",                
            OldFullPath = null
        };
        
        var createEvent = new FileChangedEvent
        {
            ChangeType = ChangeType.CREATED,
            FullPath = @"c:\bar",
            OldFullPath = @"c:\fuzz"      
        };
        
        var changedEvent = new FileChangedEvent
        {
            ChangeType = ChangeType.RENAMED,
            FullPath = @"c:\foo",
            OldFullPath = @"c:\bar"      
        };
        
        var events = EventProcessor.NormalizeEvents(new[] { deleteEvent, createEvent, changedEvent }).ToList();
        Assert.Single(events);

        var ev = events.First();
        
        Assert.Equal(ChangeType.CHANGED, ev.ChangeType);
        Assert.Equal(@"c:\foo", ev.FullPath);
        Assert.Null(ev.OldFullPath);
    }

    
    
    
    private static FileChangedEvent BuildFileCreatedEvent(string fullPath, string oldFullPath)
    {
        return new FileChangedEvent
        {
            ChangeType = ChangeType.CREATED,
            FullPath = fullPath,
            OldFullPath = oldFullPath
        };
    }
}