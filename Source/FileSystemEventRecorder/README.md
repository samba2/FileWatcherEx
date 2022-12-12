# File System Event Recorder

Command line tool to capture the raw events of [FileSystemWatcher](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher)
in a CSV file. The CSV file can than be used to write integration tests against *FileWatcherEx*.

Usage:
````sh
dotnet run C:\temp\fwtest\ C:\temp\fwevents.csv
````

Example output:
````csv
Directory,FileName,EventName,OldFileName,DiffInTicks,DiffInMilliseconds
C:\temp\fwtest,a.txt,created,,0,0
C:\temp\fwtest,b.txt,renamed,a.txt,1265338,127
C:\temp\fwtest,b.txt,deleted,,6660690,666
````