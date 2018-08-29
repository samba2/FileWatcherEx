/*---------------------------------------------------------
 * Copyright (C) Microsoft Corporation. All rights reserved.
 *--------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.IO;

namespace FileWatcherEx
{

    class FileWatcher : IDisposable
    {
        private string _watchPath;
        private Action<FileChangedEvent> _eventCallback = null;
        private Dictionary<string, FileSystemWatcher> _fileSystemWatcherDictionary = new Dictionary<string, FileSystemWatcher>();
        private Action<ErrorEventArgs> _onError = null;


        /// <summary>
        /// Create new instance of FileSystemWatcher
        /// </summary>
        /// <param name="path">Full folder path to watcher</param>
        /// <param name="onEvent">onEvent callback</param>
        /// <param name="onError">onError callback</param>
        /// <returns></returns>
        public FileSystemWatcher Create(string path, Action<FileChangedEvent> onEvent, Action<ErrorEventArgs> onError)
        {
            this._watchPath = path;
            this._eventCallback = onEvent;
            this._onError = onError;

            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = this._watchPath;
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

            // Bind internal events to manipulate the possible symbolic links
            watcher.Created += new FileSystemEventHandler(makeWatcher_Created);
            watcher.Deleted += new FileSystemEventHandler(makeWatcher_Deleted);

            watcher.Changed += new FileSystemEventHandler((object source, FileSystemEventArgs e) => {
                ProcessEvent(e, ChangeType.CHANGED);
            });

            watcher.Created += new FileSystemEventHandler((object source, FileSystemEventArgs e) => {
                ProcessEvent(e, ChangeType.CREATED);
            });

            watcher.Deleted += new FileSystemEventHandler((object source, FileSystemEventArgs e) => {
                ProcessEvent(e, ChangeType.DELETED);
            });

            watcher.Renamed += new RenamedEventHandler((object source, RenamedEventArgs e) => {
                ProcessEvent(e); }
            );

            watcher.Error += new ErrorEventHandler((object source, ErrorEventArgs e) => {
                onError(e);
            });

            //changing this to a higher value can lead into issues when watching UNC drives
            watcher.InternalBufferSize = 32768;
            this._fileSystemWatcherDictionary.Add(path, watcher);

            foreach (DirectoryInfo directoryInfo in new DirectoryInfo(path).GetDirectories())
            {
                FileAttributes fileAttributes = File.GetAttributes(directoryInfo.FullName);

                // TODO: consider skipping hidden/system folders? 
                // See IG Issue #405 comment below
                // https://github.com/d2phap/ImageGlass/issues/405
                if (fileAttributes.HasFlag(FileAttributes.Directory) && fileAttributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    try
                    {
                        this.MakeWatcher(directoryInfo.FullName);
                    }
                    catch (Exception)
                    {
                        // IG Issue #405: throws exception on Windows 10 for "c:\users\user\application data" folder and sub-folders.
                    }
                }
            }

            return watcher;
        }


        /// <summary>
        /// Process event for type = [CHANGED; DELETED; CREATED]
        /// </summary>
        /// <param name="e"></param>
        /// <param name="changeType"></param>
        private void ProcessEvent(FileSystemEventArgs e, ChangeType changeType)
        {
            this._eventCallback(new FileChangedEvent
            {
                ChangeType = changeType,
                FullPath = e.FullPath
            });
        }


        /// <summary>
        /// Process event for type = RENAMED
        /// </summary>
        /// <param name="e"></param>
        private void ProcessEvent(RenamedEventArgs e)
        {
            this._eventCallback(new FileChangedEvent
            {
                ChangeType = ChangeType.RENAMED,
                FullPath = e.FullPath,
                OldFullPath = e.OldFullPath,
            });
        }



        private void MakeWatcher(string path)
        {
            if (!this._fileSystemWatcherDictionary.ContainsKey(path))
            {
                FileSystemWatcher fileSystemWatcherRoot = new FileSystemWatcher();
                fileSystemWatcherRoot.Path = path;
                fileSystemWatcherRoot.IncludeSubdirectories = true;
                fileSystemWatcherRoot.EnableRaisingEvents = true;

                // Bind internal events to manipulate the possible symbolic links
                fileSystemWatcherRoot.Created += new FileSystemEventHandler(makeWatcher_Created);
                fileSystemWatcherRoot.Deleted += new FileSystemEventHandler(makeWatcher_Deleted);

                fileSystemWatcherRoot.Changed += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.CHANGED); });
                fileSystemWatcherRoot.Created += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.CREATED); });
                fileSystemWatcherRoot.Deleted += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.DELETED); });
                fileSystemWatcherRoot.Renamed += new RenamedEventHandler((object source, RenamedEventArgs eva) => { ProcessEvent(eva); });
                fileSystemWatcherRoot.Error += new ErrorEventHandler((object source, ErrorEventArgs eva) => { this._onError(eva); });

                this._fileSystemWatcherDictionary.Add(path, fileSystemWatcherRoot);
            }

            foreach (DirectoryInfo item in new DirectoryInfo(path).GetDirectories())
            {
                FileAttributes attributes = File.GetAttributes(item.FullName);

                // If is a directory and symbolic link
                if (attributes.HasFlag(FileAttributes.Directory) && attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    if (!this._fileSystemWatcherDictionary.ContainsKey(item.FullName))
                    {
                        FileSystemWatcher fileSystemWatcherItem = new FileSystemWatcher();
                        fileSystemWatcherItem.Path = item.FullName;
                        fileSystemWatcherItem.IncludeSubdirectories = true;
                        fileSystemWatcherItem.EnableRaisingEvents = true;

                        // Bind internal events to manipulate the possible symbolic links
                        fileSystemWatcherItem.Created += new FileSystemEventHandler(makeWatcher_Created);
                        fileSystemWatcherItem.Deleted += new FileSystemEventHandler(makeWatcher_Deleted);

                        fileSystemWatcherItem.Changed += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.CHANGED); });
                        fileSystemWatcherItem.Created += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.CREATED); });
                        fileSystemWatcherItem.Deleted += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.DELETED); });
                        fileSystemWatcherItem.Renamed += new RenamedEventHandler((object source, RenamedEventArgs eva) => { ProcessEvent(eva); });
                        fileSystemWatcherItem.Error += new ErrorEventHandler((object source, ErrorEventArgs eva) => { this._onError(eva); });

                        this._fileSystemWatcherDictionary.Add(item.FullName, fileSystemWatcherItem);
                    }

                    this.MakeWatcher(item.FullName);
                }
            }
        }


        private void makeWatcher_Created(object sender, FileSystemEventArgs e)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(e.FullPath);
                if (attributes.HasFlag(FileAttributes.Directory) && attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    FileSystemWatcher watcherCreated = new FileSystemWatcher();
                    watcherCreated.Path = e.FullPath;
                    watcherCreated.IncludeSubdirectories = true;
                    watcherCreated.EnableRaisingEvents = true;

                    // Bind internal events to manipulate the possible symbolic links
                    watcherCreated.Created += new FileSystemEventHandler(makeWatcher_Created);
                    watcherCreated.Deleted += new FileSystemEventHandler(makeWatcher_Deleted);

                    watcherCreated.Changed += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.CHANGED); });
                    watcherCreated.Created += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.CREATED); });
                    watcherCreated.Deleted += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.DELETED); });
                    watcherCreated.Renamed += new RenamedEventHandler((object source, RenamedEventArgs eva) => { ProcessEvent(eva); });
                    watcherCreated.Error += new ErrorEventHandler((object source, ErrorEventArgs eva) => { this._onError(eva); });

                    this._fileSystemWatcherDictionary.Add(e.FullPath, watcherCreated);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("<<ERROR>>: " + ex.Message);
            }
        }


        private void makeWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            // If object removed, then I will dispose and remove them from dictionary
            if (this._fileSystemWatcherDictionary.ContainsKey(e.FullPath))
            {
                this._fileSystemWatcherDictionary[e.FullPath].Dispose();
                this._fileSystemWatcherDictionary.Remove(e.FullPath);
            }
        }


        /// <summary>
        /// Dispose the instance
        /// </summary>
        public void Dispose()
        {
            foreach (var item in this._fileSystemWatcherDictionary)
            {
                item.Value.Dispose();
            }
        }
    }
}