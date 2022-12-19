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
        var mapPathToEvents = new Dictionary<string, FileChangedEvent>();
        var eventsWithoutDuplicates = new List<FileChangedEvent>();

        // Normalize duplicates
        foreach (var newEvent in events)
        {
            mapPathToEvents.TryGetValue(newEvent.FullPath,
                out FileChangedEvent? oldEvent); // Try get event from newEvent.FullPath

            if (oldEvent?.ChangeType == CREATED && newEvent.ChangeType == DELETED)
            {
                // CREATED + DELETED => remove
                mapPathToEvents.Remove(oldEvent.FullPath);
                eventsWithoutDuplicates.Remove(oldEvent);
            }
            else if (oldEvent?.ChangeType == DELETED && newEvent.ChangeType == CREATED)
            {
                // DELETED + CREATED => CHANGED
                oldEvent.ChangeType = CHANGED;
            }
            else if (oldEvent?.ChangeType == CREATED && newEvent.ChangeType == CHANGED)
            {
                // CREATED + CHANGED => CREATED
                // Do nothing
            }
            else
            {
                // Otherwise

                if (newEvent.ChangeType == RENAMED)
                {
                    // If <ANY> + RENAMED
                    do
                    {
                        mapPathToEvents.TryGetValue(newEvent.OldFullPath!,
                            out var renameFromEvent); // Try get event from newEvent.OldFullPath

                        if (renameFromEvent != null && renameFromEvent.ChangeType == CREATED)
                        {
                            // If rename from CREATED file
                            // Remove data about the CREATED file 
                            mapPathToEvents.Remove(renameFromEvent.FullPath);
                            eventsWithoutDuplicates.Remove(renameFromEvent);
                            // Handle new event as CREATED
                            newEvent.ChangeType = CREATED;
                            newEvent.OldFullPath = null;

                            if (oldEvent?.ChangeType == DELETED)
                            {
                                // DELETED + CREATED => CHANGED
                                newEvent.ChangeType = CHANGED;
                            }
                        }
                        else if (renameFromEvent != null && renameFromEvent.ChangeType == RENAMED)
                        {
                            // If rename from RENAMED file
                            // Remove data about the RENAMED file 
                            mapPathToEvents.Remove(renameFromEvent.FullPath);
                            eventsWithoutDuplicates.Remove(renameFromEvent);
                            // Change OldFullPath
                            newEvent.OldFullPath = renameFromEvent.OldFullPath;
                            // Check again
                            continue;
                        }
                        else
                        {
                            // Otherwise
                            // Do nothing
                            //mapPathToEvents.TryGetValue(newEvent.OldFullPath, out oldEvent); // Try get event from newEvent.OldFullPath
                        }
                    } while (false);
                }

                if (oldEvent != null)
                {
                    // If old event exists
                    // Replace old event data with data from the new event
                    oldEvent.ChangeType = newEvent.ChangeType;
                    oldEvent.OldFullPath = newEvent.OldFullPath;
                }
                else
                {
                    // If old event is not exist
                    // Add new event
                    mapPathToEvents.Add(newEvent.FullPath, newEvent);
                    eventsWithoutDuplicates.Add(newEvent);
                }
            }
        }


        return FilterDeleted(eventsWithoutDuplicates);
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