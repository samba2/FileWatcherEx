using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Threading;


namespace FileWatcherEx
{
    public class FileWatcherEx : IDisposable
    {

        #region Private Properties

        private Thread _thread;
        private EventProcessor _processor;
        private BlockingCollection<FileChangedEvent> _fileEventQueue = new BlockingCollection<FileChangedEvent>();

        private FileWatcher _watcher = new FileWatcher();
        private FileSystemWatcher _fsw = new FileSystemWatcher();

        #endregion



        #region Public Properties

        /// <summary>
        /// Folder path to watch
        /// </summary>
        public string FolderPath { get; set; } = "";


        /// <summary>
        /// Filter string used for determining what files are monitored in a directory
        /// </summary>
        public string Filter { get; set; } = "*.*";


        /// <summary>
        /// Gets, sets the type of changes to watch for
        /// </summary>
        public NotifyFilters NotifyFilter { get; set; } = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;


        /// <summary>
        /// Gets or sets a value indicating whether subdirectories within the specified path should be monitored.
        /// </summary>
        public bool IncludeSubdirectories { get; set; } = false;


        /// <summary>
        /// Gets or sets the object used to marshal the event handler calls issued as a result of a directory change.
        /// </summary>
        public ISynchronizeInvoke SynchronizingObject { get; set; } = null;

        #endregion



        #region Public Events
        public delegate void delegateOnChanged(Object sender, FileChangedEvent e);
        public event delegateOnChanged OnChanged;

        public delegate void delegateOnDeleted(Object sender, FileChangedEvent e);
        public event delegateOnDeleted OnDeleted;

        public delegate void delegateOnCreated(Object sender, FileChangedEvent e);
        public event delegateOnCreated OnCreated;

        public delegate void delegateOnRenamed(Object sender, FileChangedEvent e);
        public event delegateOnRenamed OnRenamed;

        public delegate void delegateOnError(Object sender, ErrorEventArgs e);
        public event delegateOnError OnError;
        #endregion



        /// <summary>
        /// Initialize new instance of FileWatcherEx
        /// </summary>
        /// <param name="folder"></param>
        public FileWatcherEx(string folder = "")
        {
            this.FolderPath = folder;
        }


        /// <summary>
        /// Start watching files
        /// </summary>
        public void Start()
        {
            if (!Directory.Exists(this.FolderPath)) return;




            _processor = new EventProcessor((e) =>
            {
                switch (e.ChangeType)
                {
                    case ChangeType.CHANGED:

                        InvokeChangedEvent(this.SynchronizingObject, e);

                        void InvokeChangedEvent(object sender, FileChangedEvent fileEvent)
                        {
                            if (this.SynchronizingObject != null && this.SynchronizingObject.InvokeRequired)
                            {
                                this.SynchronizingObject.Invoke(new Action<object, FileChangedEvent>(InvokeChangedEvent), new object[] { this.SynchronizingObject, e });
                            }
                            else
                            {
                                this.OnChanged?.Invoke(this.SynchronizingObject, e);
                            }
                        }


                        break;

                    case ChangeType.CREATED:

                        InvokeCreatedEvent(this.SynchronizingObject, e);

                        void InvokeCreatedEvent(object sender, FileChangedEvent fileEvent)
                        {
                            if (this.SynchronizingObject != null && this.SynchronizingObject.InvokeRequired)
                            {
                                this.SynchronizingObject.Invoke(new Action<object, FileChangedEvent>(InvokeCreatedEvent), new object[] { this.SynchronizingObject, e });
                            }
                            else
                            {
                                this.OnCreated?.Invoke(this.SynchronizingObject, e);
                            }
                        }


                        break;

                    case ChangeType.DELETED:

                        InvokeDeletedEvent(this.SynchronizingObject, e);

                        void InvokeDeletedEvent(object sender, FileChangedEvent fileEvent)
                        {
                            if (this.SynchronizingObject != null && this.SynchronizingObject.InvokeRequired)
                            {
                                this.SynchronizingObject.Invoke(new Action<object, FileChangedEvent>(InvokeDeletedEvent), new object[] { this.SynchronizingObject, e });
                            }
                            else
                            {
                                this.OnDeleted?.Invoke(this.SynchronizingObject, e);
                            }
                        }


                        break;

                    case ChangeType.RENAMED:

                        InvokeRenamedEvent(this.SynchronizingObject, e);

                        void InvokeRenamedEvent(object sender, FileChangedEvent fileEvent)
                        {
                            if (this.SynchronizingObject != null && this.SynchronizingObject.InvokeRequired)
                            {
                                this.SynchronizingObject.Invoke(new Action<object, FileChangedEvent>(InvokeRenamedEvent), new object[] { this.SynchronizingObject, e });
                            }
                            else
                            {
                                this.OnRenamed?.Invoke(this.SynchronizingObject, e);
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


            _thread = new Thread(() =>
            {
                while (true)
                {
                    var e = _fileEventQueue.Take();
                    _processor.ProcessEvent(e);
                }
            })
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
                    this.OnError?.Invoke(this, e);
                }
            }


            // Start watcher
            this._watcher = new FileWatcher();

            this._fsw = this._watcher.Create(this.FolderPath, onEvent, onError);
            this._fsw.Filter = this.Filter;
            this._fsw.NotifyFilter = this.NotifyFilter;
            this._fsw.IncludeSubdirectories = this.IncludeSubdirectories;
            this._fsw.SynchronizingObject = this.SynchronizingObject;

            // Start watching
            this._fsw.EnableRaisingEvents = true;
        }



        /// <summary>
        /// Stop watching
        /// </summary>
        public void Stop()
        {
            if (this._fsw != null)
            {
                this._fsw.EnableRaisingEvents = false;
            }

            if (this._watcher != null)
            {
                this._watcher.Dispose();
            }

            if (this._thread != null)
            {
                this._thread.Abort();
            }
        }


        /// <summary>
        /// Dispose the FileWatcherEx instance
        /// </summary>
        public void Dispose()
        {
            if (this._fsw != null)
            {
                this._fsw.Dispose();
            }
        }


    }
}
