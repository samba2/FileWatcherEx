using System;
using System.Collections.Concurrent;
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
        
        private FileWatcher _watcher;
        private FileSystemWatcher _fsw;
        #endregion


        #region Public Properties
        /// <summary>
        /// Folder path to watch
        /// </summary>
        public string FolderPath { get; set; } = "";


        public delegate void dlgOnChanged(FileChangedEvent e);
        public event dlgOnChanged OnChanged;

        public delegate void dlgOnDeleted(FileChangedEvent e);
        public event dlgOnDeleted OnDeleted;

        public delegate void dlgOnCreated(FileChangedEvent e);
        public event dlgOnDeleted OnCreated;

        public delegate void dlgOnRenamed(FileChangedEvent e);
        public event dlgOnDeleted OnRenamed;

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
            if (!Directory.Exists(this.FolderPath))
            {
                return;
            }


            _processor = new EventProcessor((e) =>
            {
                Console.WriteLine(string.Format("<<**>> {0} | {1}",
                    Enum.GetName(typeof(ChangeType), e.ChangeType),
                    e.FullPath));

                switch (e.ChangeType)
                {
                    case ChangeType.CHANGED:
                        this.OnChanged?.Invoke(e);
                        break;

                    case ChangeType.CREATED:
                        this.OnCreated?.Invoke(e);
                        break;

                    case ChangeType.DELETED:
                        this.OnDeleted?.Invoke(e);
                        break;

                    case ChangeType.RENAMED:
                        this.OnRenamed?.Invoke(e);
                        break;

                    case ChangeType.LOG:
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

            void onError(ErrorEventArgs e)
            {
                if (e != null)
                {
                    Console.WriteLine("{0}|{1}", (int)ChangeType.LOG, e.GetException().ToString());
                }
            }


            // Start watching
            this._watcher = new FileWatcher();
            this._fsw = this._watcher.Create(this.FolderPath, onEvent, onError);
            this._fsw.EnableRaisingEvents = true;
        }

        private void FileWatcherEx_OnChanged(FileChangedEvent e)
        {
            throw new NotImplementedException();
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

            if (this._watcher != null)
            {
                this._watcher.Dispose();
            }

            if (this._thread != null)
            {
                this._thread.Abort();
            }
        }
    }
}
