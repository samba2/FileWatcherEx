/*---------------------------------------------------------
 * Copyright (C) Microsoft Corporation. All rights reserved.
 *--------------------------------------------------------*/

using static FileWatcherEx.ChangeType;

namespace FileWatcherEx.Helpers;

internal class EventProcessor
{
    /// <summary>
    /// Aggregate and only emit events when changes have stopped for this duration (in ms)
    /// </summary>
    private const int EventDelay = 50;

    /// <summary>
    /// Warn after certain time span of event spam (in ticks)
    /// </summary>
    private const int EventSpamWarningThreshold = 60 * 1000 * 10000;

    private readonly object _lock = new();
    private Task? _delayTask = null;

    private readonly List<FileChangedEvent> _events = new();
    private readonly Action<FileChangedEvent> _handleEvent;

    private readonly Action<string> _logger;

    private long _lastEventTime = 0;
    private long _delayStarted = 0;

    private long _spamCheckStartTime = 0;
    private bool _spamWarningLogged = false;
    
    internal static IEnumerable<FileChangedEvent> NormalizeEvents(FileChangedEvent[] events)
    {
        var eventRepo = new FileEventRepository();

        // Normalize duplicates
        foreach (var newEvent in events)
        {
            var oldEvent = eventRepo.Find(newEvent.FullPath);
            // original file event from which we renamed
            var renameFromEvent = newEvent.ChangeType == RENAMED 
                ? eventRepo.Find(newEvent.OldFullPath) 
                : null;

            switch (newEvent.ChangeType)
            {
                // CREATED followed by DELETED => remove
                case DELETED when oldEvent?.ChangeType == CREATED:
                    eventRepo.Remove(oldEvent);
                    break;

                // DELETED followed by CREATED => CHANGED
                case CREATED when oldEvent?.ChangeType == DELETED:
                    oldEvent.ChangeType = CHANGED;
                    break;

                // CREATED followed by CHANGED => CREATED
                case CHANGED when oldEvent?.ChangeType == CREATED:
                    // Do nothing
                    break;

                // rename from CREATED file
                case RENAMED when renameFromEvent?.ChangeType == CREATED:
                    // Remove data about the CREATED file 
                    eventRepo.Remove(renameFromEvent);
                    // Handle new event as CREATED
                    newEvent.ChangeType = CREATED;
                    newEvent.OldFullPath = null;

                    if (oldEvent?.ChangeType == DELETED)
                    {
                        // DELETED followed by CREATED => CHANGED
                        newEvent.ChangeType = CHANGED;
                    }

                    eventRepo.AddOrUpdate(newEvent);
                    break;

                case RENAMED when renameFromEvent?.ChangeType == RENAMED:
                    newEvent.OldFullPath = renameFromEvent.OldFullPath;
                    // Remove data about the RENAMED file 
                    eventRepo.Remove(renameFromEvent);
                    eventRepo.AddOrUpdate(newEvent);
                    break;

                // TODO why does "LOG" need to be in the filewevent ?
                case LOG:

                default:
                    eventRepo.AddOrUpdate(newEvent);
                    break;
            }
        }

        return FilterDeleted(eventRepo.Events());
    }

    private class FileEventRepository
    {
        private readonly Dictionary<string, FileChangedEvent> _mapPathToEvents = new();

        public void AddOrUpdate(FileChangedEvent newEvent)
        {
            if (_mapPathToEvents.TryGetValue(newEvent.FullPath, out var oldEvent))
            {
                // update existing
                oldEvent.ChangeType = newEvent.ChangeType;
                oldEvent.OldFullPath = newEvent.OldFullPath;
            }
            else
            {
                // add
                _mapPathToEvents[newEvent.FullPath] = newEvent;
            }
        }

        public void Remove(FileChangedEvent ev)
        {
            _mapPathToEvents.Remove(ev.FullPath);
        }

        public FileChangedEvent? Find(string? path)
        {
            _mapPathToEvents.TryGetValue(path ?? "", out var oldEvent);
            return oldEvent;
        }

        public List<FileChangedEvent> Events()
        {
            return _mapPathToEvents.Values.ToList();
        }
    }

    // This algorithm will remove all DELETE events up to the root folder
    // that got deleted if any. This ensures that we are not producing
    // DELETE events for each file inside a folder that gets deleted.
    //
    // 1.) split ADD/CHANGE and DELETED events
    // 2.) sort short deleted paths to the top
    // 3.) for each DELETE, check if there is a deleted parent and ignore the event in that case
    internal static IEnumerable<FileChangedEvent> FilterDeleted(IEnumerable<FileChangedEvent> eventsWithoutDuplicates)
    {
        // Handle deletes
        var deletedPaths = new List<string>();
        return eventsWithoutDuplicates
            .Select((e, n) => new KeyValuePair<int, FileChangedEvent>(n, e)) // store original position value
            .OrderBy(e => e.Value.FullPath.Length) // shortest path first
            .Where(e => IsParent(e.Value, deletedPaths))
            .OrderBy(e => e.Key) // restore original position
            .Select(e => e.Value);
    }

    internal static bool IsParent(FileChangedEvent e, List<string> deletedPaths)
    {
        if (e.ChangeType == DELETED)
        {
            if (deletedPaths.Any(d => IsParent(e.FullPath, d)))
            {
                return false; // DELETE is ignored if parent is deleted already
            }

            // otherwise mark as deleted
            deletedPaths.Add(e.FullPath);
        }

        return true;
    }


    internal static bool IsParent(string p, string candidate)
    {
        return p.IndexOf(candidate + '\\', StringComparison.Ordinal) == 0;
    }


    public EventProcessor(Action<FileChangedEvent> onEvent, Action<string> onLogging)
    {
        _handleEvent = onEvent;
        _logger = onLogging;
    }


    public void ProcessEvent(FileChangedEvent fileEvent)
    {
        lock (_lock)
        {
            var now = DateTime.Now.Ticks;

            // Check for spam
            if (_events.Count == 0)
            {
                _spamWarningLogged = false;
                _spamCheckStartTime = now;
            }
            else if (!_spamWarningLogged && _spamCheckStartTime + EventSpamWarningThreshold < now)
            {
                _spamWarningLogged = true;
                _logger(string.Format(
                    "Warning: Watcher is busy catching up with {0} file changes in 60 seconds. Latest path is '{1}'",
                    _events.Count, fileEvent.FullPath));
            }

            // Add into our queue
            _events.Add(fileEvent);
            _lastEventTime = now;

            // Process queue after delay
            if (_delayTask == null)
            {
                // Create function to buffer events
                void Func(Task value)
                {
                    lock (_lock)
                    {
                        // Check if another event has been received in the meantime
                        if (_delayStarted == _lastEventTime)
                        {
                            // Normalize and handle
                            var normalized = NormalizeEvents(_events.ToArray());
                            foreach (var e in normalized)
                            {
                                _handleEvent(e);
                            }

                            // Reset
                            _events.Clear();
                            _delayTask = null;
                        }

                        // Otherwise we have received a new event while this task was
                        // delayed and we reschedule it.
                        else
                        {
                            _delayStarted = _lastEventTime;
                            _delayTask = Task.Delay(EventDelay).ContinueWith(Func);
                        }
                    }
                }

                // Start function after delay
                _delayStarted = _lastEventTime;
                _delayTask = Task.Delay(EventDelay).ContinueWith(Func);
            }
        }
    }
}