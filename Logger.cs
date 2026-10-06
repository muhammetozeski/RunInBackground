using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
namespace RunInBackGround;
public static class Logger
{
    /// <summary> enable or disable logging by changing this </summary>
    private static int _activateLogging = 1; // Bool yerine int kullanıyoruz (0 false, 1 true)

    public static bool ActivateLoggingToDisk
    {
        get => Interlocked.CompareExchange(ref _activateLogging, 0, 0) == 1;
        set => Interlocked.Exchange(ref _activateLogging, value ? 1 : 0);
    }

    // Eğer dinamik olarak değistirmen gerekirse Const olmaktan çıkartabilirsin, bunu yapmak güvenli.
    const bool PrintDebugModStyle = false;

    public static readonly string startTime = DateTime.Now.ToString("yyyy.MM.dd HH.mm.ss.ff");

    public static readonly string LogsFolder = Path.Combine(AppPaths.AppCache, "Logs");
    public const string LogFileNamePrefix = "RunInBackground Log";
    public readonly static string LogFileName = LogsFolder + "\\" + LogFileNamePrefix + " " + startTime + ".txt";

    // enter -1 to disable
    const int DeleteOlderThanLastXFile = 3;

    public static readonly ConcurrentQueue<string> AllLogs = new();

    public static readonly ConcurrentQueue<string?> AllLogsUserFriendly = new();

    private static readonly BlockingCollection<(string Message, ManualResetEventSlim? SyncEvent)> _logQueue = [];

    static Logger()
    {
        if (!ActivateLoggingToDisk)
            return;

        if (!string.IsNullOrWhiteSpace(LogsFolder))
        {
            Directory.CreateDirectory(LogsFolder);
        }

        if (DeleteOlderThanLastXFile > -1)
            try
            {
                DeleteOldestFiles(LogsFolder, DeleteOlderThanLastXFile, LogFileNamePrefix);
            }
            catch (Exception e)
            {
                Log("An Error occured while deleting the old log files. The exception here:\n\n" + e);
            }


        new Thread(() =>
        {
            foreach (var (Message, SyncEvent) in _logQueue.GetConsumingEnumerable())
            {
                try
                {
                    File.AppendAllText(LogFileName, Message + "\n");
                }
                catch (IOException error)
                {
                    Debug.WriteLine(error);
                }
                catch (UnauthorizedAccessException error)
                {
                    Debug.WriteLine(error);
                }
                finally { SyncEvent?.Set(); }
            }
        })
        { IsBackground = true }.Start();
    }

    /// <summary>
    /// Logs a message or object to the console and disk asynchronously. 
    /// Handles <see cref="System.Collections.IEnumerable"/> by expanding their contents and captures caller metadata automatically.
    /// </summary>
    /// <param name="MessageObject">The object or message to be logged.</param>
    /// <param name="consoleColor">The color of the text when printing to the <see cref="Console"/>.</param>
    /// <param name="PrintToConsole">If true, outputs the log to the standard console.</param>
    /// <param name="WriteToDisk">If true, queues the log for persistent storage via <see cref="_logQueue"/>.</param>
    /// <param name="UseNewLine">If true, uses <see cref="Console.WriteLine"/>; otherwise, uses <see cref="Console.Write"/>.</param>
    /// <param name="WaitForLogging">If true, uses <see cref="ManualResetEventSlim"/> to make the calling thread wait until the disk write operation is completed.</param>
    /// <param name="callerFunction">Automatically captured name of the member calling the method via <see cref="CallerMemberNameAttribute"/>.</param>
    /// <param name="callerFilePath">Automatically captured full path of the source file via <see cref="CallerFilePathAttribute"/>.</param>
    /// <param name="callerLine">Automatically captured line number via <see cref="CallerLineNumberAttribute"/>.</param>
    /// <param name="isRun">A master switch to enable or disable the specific log call execution.</param>
    /// <returns>A string representation of the logged object, or null if <paramref name="MessageObject"/> is null.</returns>
    public static string? Log(object? MessageObject, ConsoleColor? consoleColor = null, bool PrintToConsole = false, bool WriteToDisk = true, bool UseNewLine = true, bool WaitForLogging = false,
        [CallerMemberName] string callerFunction = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLine = 0, bool isRun = true
        )
    {
        if (!ActivateLoggingToDisk)
        {
            if (PrintToConsole)
            {
                var defaultColor = Console.ForegroundColor;
                if (consoleColor != null)
                {
                    Console.ForegroundColor = consoleColor.Value;
                }
                if (UseNewLine)
                    Console.WriteLine(MessageObject ?? "null");
                else Console.Write(MessageObject ?? "null");
                Console.ForegroundColor = defaultColor;
            }
            return MessageObject?.ToString();
        }

        if (!isRun || (!PrintToConsole && !WriteToDisk)) return null;

        string? returnValue = null;

        const string prefix = "> ";
        const string suffix = "\n-----------------------------\n\n";
        string CallerFile;
        try
        {
            CallerFile = Path.GetFileName(callerFilePath);
            if (string.IsNullOrWhiteSpace(CallerFile))
            {
                CallerFile = "null";
            }
        }
        catch (Exception e)
        {
            CallerFile = "\"exception: " + e.Message + "\"";
        }

        string now = DateTime.UtcNow.AddHours(3).ToString(); //I want Türkiye time zone style

        string Message = prefix + "[" + now + "] " +
            "[" + CallerFile + "/" + callerFunction + " Line: " + callerLine +
            " Thread Id: " + Environment.CurrentManagedThreadId + "]:\n";

        try
        {

            if (MessageObject is System.Collections.IEnumerable numerable)
            {
                foreach (var item in numerable)
                {
                    try
                    {
                        string? itemString = item?.ToString();

                        if (returnValue == null)
                            returnValue = itemString ?? "null";
                        else
                            returnValue += itemString ?? "null";

                        returnValue += "\n";
                        Message += itemString ?? "item.ToString() in given numerable, has returned null\n";
                    }
                    catch (Exception e)
                    {
                        returnValue += "error";
                        Message += "An error occured while converting to string the given object in the list in the \"Log()\" function. The error:\n";
                        Message += e;
                        Message += "\n";
                    }
                }
            }
            else
            {
                returnValue = MessageObject?.ToString();
                Message += returnValue ?? "MessageObject.ToString() has returned null";
            }

        }
        catch (Exception e)
        {
            Message += "An error occured while converting to string the given object in the \"Log()\" function. The error:\n";
            Message += e;
        }

        Message += suffix;
        if (PrintToConsole)
        {
            var defaultColor = Console.ForegroundColor;
            if (consoleColor != null)
            {
                Console.ForegroundColor = consoleColor.Value;
            }

#pragma warning disable CS0162 // Ulaşılamayan kod algılandı
            if (PrintDebugModStyle)
            {
                if (UseNewLine) Console.WriteLine(Message);
                else Console.Write(Message);
            }
            else
            {
                if (UseNewLine) Console.WriteLine(MessageObject);
                else Console.Write(MessageObject);
            }

#pragma warning restore CS0162 // Ulaşılamayan kod algılandı
            Console.ForegroundColor = defaultColor;
        }

        AllLogs.Enqueue(Message);
        AllLogsUserFriendly.Enqueue(returnValue);

        if (WriteToDisk)
        {
            if (WaitForLogging)
            {
                using var syncEvent = new ManualResetEventSlim(false);
                _logQueue.Add((Message, syncEvent));
                syncEvent.Wait();
            }
            else
            {
                _logQueue.Add((Message, null));
            }
        }

        return returnValue;
    }

    static void DeleteOldestFiles(string folderPath, int filesToKeep, string prefix = "")
    {
        if (!Directory.Exists(folderPath))
        {
            Log("[ERROR] [DirectoryNotFoundException] File deletion operation: Folder not found. Folder path: " + folderPath);
            return; // Klasör bulunamazsa işlemden çık
        }

        var files = Directory.GetFiles(folderPath)
                             .Select(filePath => new
                             {
                                 Path = filePath,
                                 FileName = Path.GetFileNameWithoutExtension(filePath)
                             })
                             .ToList();

        var datedFiles = new List<(string Path, DateTime Date)>();

        foreach (var file in files)
        {
            string name = file.FileName;
            name = name.Contains(prefix) ? name.Remove(name.IndexOf(prefix), prefix.Length) : name;
            name = name.Trim();
            if (DateTime.TryParseExact(name, "yyyy.MM.dd HH.mm.ss.ff",
                                       System.Globalization.CultureInfo.InvariantCulture,
                                       System.Globalization.DateTimeStyles.None, out DateTime fileDate))
            {
                datedFiles.Add((file.Path, fileDate));
            }
            // Hatalı formatta dosya adları garanti edildiği için else bloğuna gerek yok.
        }

        var sortedFiles = datedFiles.OrderBy(f => f.Date).ToList();

        int filesToDeleteCount = sortedFiles.Count - filesToKeep;

        if (filesToDeleteCount <= 0)
        {
            return; // Silinecek dosya yok
        }

        for (int i = 0; i < filesToDeleteCount; i++)
        {
            File.Delete(sortedFiles[i].Path);
        }
    }
}
