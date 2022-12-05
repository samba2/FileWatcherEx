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

## `create_and_remove_file_wsl2.csv`
Create and remove file in WSL 2. On file creation, a second "changed" event is written.
````sh
touch /mnt/c/temp/fwtest/a.txt
rm /mnt/c/temp/fwtest/a.txt
````


