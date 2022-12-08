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
Create, rename and remove file in WSL 2. On file creation, a second "changed" event is written.
````sh
touch /mnt/c/temp/fwtest/a.txt
mv /mnt/c/temp/fwtest/a.txt /mnt/c/temp/fwtest/b.txt 
rm /mnt/c/temp/fwtest/b.txt
````

## `create_rename_and_remove_file_with_wait_time_wsl2.csv`
Create, rename and remove file in WSL 2. Additionally, some wait time is added.
````sh
touch /mnt/c/temp/fwtest/a.txt
sleep 1
mv /mnt/c/temp/fwtest/a.txt /mnt/c/temp/fwtest/b.txt 
sleep 1
rm /mnt/c/temp/fwtest/b.txt
````

## `create_and_rename_file_via_explorer.csv`
Manually create a file in the Windows explorer. Change the default name "New Text Document.txt"
to "foo.txt".

## `create_rename_and_delete_file_via_explorer.csv`
Manually create, rename and delete a file in the Windows explorer.

## `download_image_via_Edge_browser.csv`
Download (right click, "Save image as") single image via Edge 106.0.1370.42.

## `create_subdirectory_add_and_remove_file.csv`
In WSL2, create a subdirectory, create and remove a file.
````sh
mkdir -p /mnt/c/temp/fwtest/subdir/
touch /mnt/c/temp/fwtest/subdir/a.txt
rm /mnt/c/temp/fwtest/subdir/a.txt
````

## `create_subdirectory_add_and_remove_file_with_sleep.csv`
In WSL2, create a subdirectory, Create the file, wait for a while, then remove it.
````sh
mkdir -p /mnt/c/temp/fwtest/subdir/
touch /mnt/c/temp/fwtest/subdir/a.txt
sleep 1
rm /mnt/c/temp/fwtest/subdir/a.txt
````
