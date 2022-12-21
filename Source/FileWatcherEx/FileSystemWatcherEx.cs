
using System.Collections.Concurrent;
using System.ComponentModel;
using FileWatcherEx.Helpers;

namespace FileWatcherEx;

/// <summary>
/// A wrapper of <see cref="FileSystemWatcher"/> to standardize the events
/// and avoid false change notifications.
/// </summary>
public class FileSystemWatcherEx : IDisposable, IFileSystemWatcherEx
{
    #region Private Properties

    private Thread? _thread;
    private EventProcessor? _processor;
    private readonly BlockingCollection<FileChangedEvent> _fileEventQueue = new();

    private FileWatcher? _watcher;
    private IFileSystemWatcherWrapper? _fsw;
    private Func<IFileSystemWatcherWrapper>? _fswFactory;

    // Define the cancellation token.
    private CancellationTokenSource? _cancelSource;

    // allow injection of FileSystemWatcherWrapper
    internal Func<IFileSystemWatcherWrapper> FileSystemWatcherFactory
    {
        // default to production FileSystemWatcherWrapper (which wrapped the native FileSystemWatcher)
        get { return _fswFactory ?? (() => new FileSystemWatcherWrapper()); }
        set => _fswFactory = value;
    }

    #endregion


    #region Public Properties

    /// <summary>
    /// Gets or sets the path of the directory to watch.
    /// </summary>
    public string FolderPath { get; set; } = "";


    /// <summary>
    /// Gets the collection of all the filters used to determine what files are monitored in a directory.
    /// </summary>
    public System.Collections.ObjectModel.Collection<string> Filters { get; } = new();


    /// <summary>
    /// Gets or sets the filter string used to determine what files are monitored in a directory.
    /// </summary>
    public string Filter
    {
        get => Filters.Count == 0 ? "*" : Filters[0];
        set
        {
            Filters.Clear();
            Filters.Add(value);
        }
    }


    /// <summary>
    /// Gets or sets the type of changes to watch for.
    /// The default is the bitwise OR combination of
    /// <see cref="NotifyFilters.LastWrite"/>,
    /// <see cref="NotifyFilters.FileName"/>,
    /// and <see cref="NotifyFilters.DirectoryName"/>.
    /// </summary>
    public NotifyFilters NotifyFilter { get; set; } = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;


    /// <summary>
    /// Gets or sets a value indicating whether subdirectories within the specified path should be monitored.
    /// </summary>
    public bool IncludeSubdirectories { get; set; } = false;


    /// <summary>
    /// Gets or sets the object used to marshal the event handler calls issued as a result of a directory change.
    /// </summary>
    public ISynchronizeInvoke? SynchronizingObject { get; set; }

    #endregion

    #region Public Events

    /// <summary>
    /// Occurs when a file or directory in the specified
    /// <see cref="FolderPath"/> is changed.
    /// </summary>
    public event DelegateOnChanged? OnChanged;
    public delegate void DelegateOnChanged(object? sender, FileChangedEvent e);


    /// <summary>
    /// Occurs when a file or directory in the specified
    /// <see cref="FolderPath"/> is deleted.
    /// </summary>
    public event DelegateOnDeleted? OnDeleted;
    public delegate void DelegateOnDeleted(object? sender, FileChangedEvent e);


    /// <summary>
    /// Occurs when a file or directory in the specified
    /// <see cref="FolderPath"/> is created.
    /// </summary>
    public event DelegateOnCreated? OnCreated;
    public delegate void DelegateOnCreated(object? sender, FileChangedEvent e);


    /// <summary>
    /// Occurs when a file or directory in the specified
    /// <see cref="FolderPath"/> is renamed.
    /// </summary>
    public event DelegateOnRenamed? OnRenamed;
    public delegate void DelegateOnRenamed(object? sender, FileChangedEvent e);


    /// <summary>
    /// Occurs when the instance of <see cref="FileSystemWatcherEx"/> is unable to continue
    /// monitoring changes or when the internal buffer overflows.
    /// </summary>
    public event DelegateOnError? OnError;
    public delegate void DelegateOnError(object? sender, ErrorEventArgs e);

    #endregion


    /// <summary>
    /// Initialize new instance of <see cref="FileSystemWatcherEx"/>
    /// </summary>
    /// <param name="folderPath"></param>
    public FileSystemWatcherEx(string folderPath = "")
    {
        FolderPath = folderPath;
    }


    /// <summary>
    /// Start watching files
    /// </summary>
    public void Start()
    {
        if (!Directory.Exists(FolderPath)) return;


        _processor = new EventProcessor((e) =>
        {
            switch (e.ChangeType)
            {
                case ChangeType.CHANGED:

                    InvokeChangedEvent(SynchronizingObject, e);

                    void InvokeChangedEvent(object? sender, FileChangedEvent fileEvent)
                    {
                        if (SynchronizingObject != null && SynchronizingObject.InvokeRequired)
                        {
                            SynchronizingObject.Invoke(new Action<object, FileChangedEvent>(InvokeChangedEvent), new object[] { SynchronizingObject, e });
                        }
                        else
                        {
                            OnChanged?.Invoke(SynchronizingObject, e);
                        }
                    }


                    break;

                case ChangeType.CREATED:

                    InvokeCreatedEvent(SynchronizingObject, e);

                    void InvokeCreatedEvent(object? sender, FileChangedEvent fileEvent)
                    {
                        if (SynchronizingObject != null && SynchronizingObject.InvokeRequired)
                        {
                            SynchronizingObject.Invoke(new Action<object, FileChangedEvent>(InvokeCreatedEvent), new object[] { SynchronizingObject, e });
                        }
                        else
                        {
                            OnCreated?.Invoke(SynchronizingObject, e);
                        }
                    }


                    break;

                case ChangeType.DELETED:

                    InvokeDeletedEvent(SynchronizingObject, e);

                    void InvokeDeletedEvent(object? sender, FileChangedEvent fileEvent)
                    {
                        if (SynchronizingObject != null && SynchronizingObject.InvokeRequired)
                        {
                            SynchronizingObject.Invoke(new Action<object, FileChangedEvent>(InvokeDeletedEvent), new object[] { SynchronizingObject, e });
                        }
                        else
                        {
                            OnDeleted?.Invoke(SynchronizingObject, e);
                        }
                    }


                    break;

                case ChangeType.RENAMED:

                    InvokeRenamedEvent(SynchronizingObject, e);

                    void InvokeRenamedEvent(object? sender, FileChangedEvent fileEvent)
                    {
                        if (SynchronizingObject != null && SynchronizingObject.InvokeRequired)
                        {
                            SynchronizingObject.Invoke(new Action<object, FileChangedEvent>(InvokeRenamedEvent), new object[] { SynchronizingObject, e });
                        }
                        else
                        {
                            OnRenamed?.Invoke(SynchronizingObject, e);
                        }
                    }


                    break;

                default:
                    break;
            }
        }, (log) =>
        {
            Console.WriteLine(string.Format("{0} | {1}", Enum.GetName(typeof(ChangeType), ChangeType.LOG), log));
        });

        _cancelSource = new();
        _thread = new Thread(() => Thread_DoingWork(_cancelSource.Token))
        {
            // this ensures the thread does not block the process from terminating!
            IsBackground = true
        };

        _thread.Start();


        // Log each event in our special format to output queue
        void onEvent(FileChangedEvent e)
        {
            _fileEventQueue.Add(e);
        }


        // OnError
        void onError(ErrorEventArgs e)
        {
            if (e != null)
            {
                OnError?.Invoke(this, e);
            }
        }


        // TODO
        // - FileWatcher should not return underlying filewatcher 
        // - introduce stronger encapsulation
        // - `watcher` should delegate the set properties to ALL file watchers
        // - if you register a dir later, these 4 properties should be used
        // Start watcher
        _watcher = new FileWatcher();

        _fsw = _watcher.Create(FolderPath, onEvent, onError, FileSystemWatcherFactory, _ => {});

        foreach (var filter in Filters)
        {
            _fsw.Filters.Add(filter);
        }

        _fsw.NotifyFilter = NotifyFilter;
        _fsw.IncludeSubdirectories = IncludeSubdirectories;
        _fsw.SynchronizingObject = SynchronizingObject;

        // Start watching
        _fsw.EnableRaisingEvents = true;
    }

    internal void StartForTesting(
        Func<string, FileAttributes> getFileAttributesFunc, 
        Func<string, DirectoryInfo[]> getDirectoryInfosFunc)
    {
        Start();
        if (_watcher is null) return;
        _watcher.GetFileAttributesFunc = getFileAttributesFunc;
        _watcher.GetDirectoryInfosFunc = getDirectoryInfosFunc;
    }


    /// <summary>
    /// Stop watching files
    /// </summary>
    public void Stop()
    {
        if (_fsw != null)
        {
            _fsw.EnableRaisingEvents = false;
            _fsw.Dispose();
        }

        _watcher?.Dispose();

        // stop the thread
        _cancelSource?.Cancel();
        _cancelSource?.Dispose();
    }


    /// <summary>
    /// Dispose the FileWatcherEx instance
    /// </summary>
    public void Dispose()
    {
        _fsw?.Dispose();
        _watcher?.Dispose();
        _cancelSource?.Dispose();
        GC.SuppressFinalize(this);
    }



    private void Thread_DoingWork(CancellationToken cancelToken)
    {
        while (true)
        {
            if (cancelToken.IsCancellationRequested)
                return;

            try
            {
                var e = _fileEventQueue.Take(cancelToken);
                _processor?.ProcessEvent(e);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }


}
