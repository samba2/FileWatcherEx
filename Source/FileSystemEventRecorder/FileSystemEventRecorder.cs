using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using FileWatcherEx;

namespace FileSystemEventRecorder;

// event received from C# FileSystemWatcher
internal record EventRecord(string? FileName, string EventName, long NowInTicks);

// post processed. Calculated before closing program 
internal record EventRecordWithDiff(
    string? FileName,
    string EventName,
    long DiffInTicks, // ticks between passed by from the previous event to now
    double DiffInMilliseconds // milliseconds between previous event and now.
);

public static class FileSystemEventRecords
{
    private static readonly ConcurrentQueue<EventRecord> EventRecords = new();

    public static void Main(string[] args)
    {
        var (watchedDirectory, csvOutputFile) = ProcessArguments(args); 

        var watcher = new FileSystemWatcherWrapper();
        watcher.Path = watchedDirectory;
        watcher.IncludeSubdirectories = true;
        watcher.NotifyFilter = NotifyFilters.LastWrite
                               | NotifyFilters.FileName
                               | NotifyFilters.DirectoryName;

        watcher.Created += (_, fileSystemEventArgs) =>
            EventRecords.Enqueue(new EventRecord(fileSystemEventArgs.Name, "created", Stopwatch.GetTimestamp()));
        watcher.Deleted += (_, fileSystemEventArgs) =>
            EventRecords.Enqueue(new EventRecord(fileSystemEventArgs.Name, "deleted", Stopwatch.GetTimestamp()));

        watcher.Changed += (_, fileSystemEventArgs) =>
            EventRecords.Enqueue(new EventRecord(fileSystemEventArgs.Name, "changed", Stopwatch.GetTimestamp()));
        watcher.Renamed += (_, renamedEventArgs) =>
            EventRecords.Enqueue(new EventRecord(renamedEventArgs.Name, "rename", Stopwatch.GetTimestamp()));
        watcher.Error += (_, errorEventArgs) =>
        {
            EventRecords.Enqueue(new EventRecord(null, "error", Stopwatch.GetTimestamp()));
            Console.WriteLine($"Error: {errorEventArgs.GetException()}");
        };

        // taken from existing code 
        watcher.InternalBufferSize = 32768;
        watcher.EnableRaisingEvents = true;

        Console.WriteLine("Sleeping...");
        Console.CancelKeyPress += (_, _) =>
        {
            Console.WriteLine("Exiting.");
            ProcessQueueAndWriteToDisk(csvOutputFile);
            Environment.Exit(0);
        };

        while (true)
        {
            Thread.Sleep(200);
        }
    }

    private static (string, string) ProcessArguments(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run [directory to be watched] [output csv file]");
            Environment.Exit(1);
        }

        return (args[0], args[1]);
    }

    private static void ProcessQueueAndWriteToDisk(string csvOutputFile)
    {
        if (EventRecords.IsEmpty)
        {
            Console.WriteLine("Detected no file system events. Nothing is written.");
        }
        else
        {
            Console.WriteLine($"Recorded {EventRecords.Count} file system events.");
            var records = MapToDiffTicks();

            Console.WriteLine($"Writing CSV to {csvOutputFile}.");
            using (var writer = new StreamWriter(csvOutputFile))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(records);
            }

            Console.WriteLine("Done.");
        }
    }

    // post-process queue. Calculate difference between previous and current event 
    private static IEnumerable<EventRecordWithDiff> MapToDiffTicks()
    {
        List<EventRecordWithDiff> eventsWithDiffs = new();
        long previousTicks = 0;
        foreach (var eventRecord in EventRecords)
        {
            var diff = previousTicks switch
            {
                0 => 0, // first run
                _ => eventRecord.NowInTicks - previousTicks
            };

            previousTicks = eventRecord.NowInTicks;
            double diffInMilliseconds = Convert.ToInt64(new TimeSpan(diff).TotalMilliseconds);

            var record = new EventRecordWithDiff(
                eventRecord.FileName,
                eventRecord.EventName,
                diff,
                diffInMilliseconds);
            eventsWithDiffs.Add(record);
        }

        return eventsWithDiffs;
    }
}