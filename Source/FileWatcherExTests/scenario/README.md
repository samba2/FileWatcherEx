# Scenarios For Integration Testing

For each scenario:
- a fresh recording was started in an empty directory
- the listed commands were executed in a separate terminal
- the recording was stopped (CTRL + C)

Then the recorded CSV files were used in integration tests using the `ReplayFileSystemWatcherWrapper.cs`.

Example for starting a recording:
````powershell
PS C:\Projects\FileWatcherEx\Source\FileSystemEventRecorder> dotnet run C:\temp\fwtest\ C:\Projects\FileWatcherEx\Source\FileWatcherExTests\scenario\create_rename_and_remove_file_wsl2.csv
````
# List of Scenarios

## `create_file.csv`
````powershell
New-Item -Path 'c:\temp\fwtest\a.txt' -ItemType File
````

## `create_and_remove_file.csv`
````powershell
New-Item -Path 'c:\temp\fwtest\a.txt' -ItemType File
Remove-Item -Path 'c:\temp\fwtest\a.txt' -Recurse
````

## `create_rename_and_remove_file.csv`
````powershell
New-Item -Path 'c:\temp\fwtest\a.txt' -ItemType File
Rename-Item -Path 'c:\temp\fwtest\a.txt' -NewName 'c:\temp\fwtest\b.txt'
Remove-Item -Path 'c:\temp\fwtest\b.txt' -Recurse
````

## `create_file_wsl2.csv`
Create file in WSL 2. On file creation, a second "changed" event is written.
````sh
touch /mnt/c/temp/fwtest/a.txt
````


## `create_and_rename_file_wsl2.csv`
Create and rename file in WSL 2. On file creation, a second "changed" event is written.
````sh
touch /mnt/c/temp/fwtest/a.txt
mv /mnt/c/temp/fwtest/a.txt /mnt/c/temp/fwtest/b.txt 
````

## `create_rename_and_remove_file_wsl2.csv`
Create rename and remove file in WSL 2. On file creation, a second "changed" event is written.
````sh
touch /mnt/c/temp/fwtest/a.txt
mv /mnt/c/temp/fwtest/a.txt /mnt/c/temp/fwtest/b.txt 
rm /mnt/c/temp/fwtest/b.txt
````


