# File System Event Recorder

Command line tool to capture the raw events of [FileSystemWatcher](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher)
in a CSV file. The CSV file can than be used to write integration tests against *FileWatcherEx*.

Usage:
````sh
dotnet run C:\temp\fwtest\ C:\temp\fwevents.csv
````

Example output:
````csv
FileName,EventName,DiffInTicks,DiffInMilliseconds
foo.txt,created,0,0
foo.txt,changed,9534,1
foo.txt,changed,41180869,4118
foo.txt,deleted,40944961,4094
````