/*---------------------------------------------------------
 * Copyright (C) Microsoft Corporation. All rights reserved.
 *--------------------------------------------------------*/

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
                            var normalized = new EventNormalizer().Normalize(_events.ToArray());
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