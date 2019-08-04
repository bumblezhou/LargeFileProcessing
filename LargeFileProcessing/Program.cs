using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace LargeFileProcessing
{
    [Flags]
    public enum LogType
    {
        None,
        L = 2,
        W = 4,
        E = 8,
        I = 16,
        C = 32,
        P = 64,
        All = L | W | E | I | C | P
    }

    public class LogItem
    {
        public LogType LogType { get; set; }
        public string Content { get; set; }
        public DateTime DateTime { get; set; }
        public override string ToString()
        {
            if (string.IsNullOrEmpty(Content))
            {
                Content = string.Empty;
            }

            return string.Format("{0} {1} {2}", LogType, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), Content);
        }
    }

    public enum LoadType
    {
        PreviousPage,
        NextPage
    }

    public class PositionWithStatus
    {
        public long Position { get; set; }
        public bool LoadCompleted { get; set; }
    }

    public class PagedLogItems
    {
        public PagedLogItems()
        {
            LogItems = new List<LogItem>();
            CurrentStartPosition = new List<PositionWithStatus>();
            CurrentEndPosition = new List<PositionWithStatus>();
        }
        public LogType LogType { get; set; }
        public LoadType LoadType { get; set; }
        public IList<LogItem> LogItems { get; set; }
        /// <summary>
        /// start byte position of the current page in the large log file.
        /// </summary>
        public IList<PositionWithStatus> CurrentStartPosition { get; set; }
        /// <summary>
        /// end byte position of the current page in the large log file.
        /// </summary>
        public IList<PositionWithStatus> CurrentEndPosition { get; set; }
        /// <summary>
        /// start byte position of the previous page in the large log file.
        /// </summary>
        public long PreviousStartPosition { get; set; }
        /// <summary>
        /// total size in byte of the large log file
        /// </summary>
        public long TotalSize { get; set; }
        public bool IsFirstPage => CurrentStartPosition.Count > 0 && CurrentStartPosition.Last().Position == 0;
        public bool IsFinalPage => CurrentEndPosition.Count > 0 && CurrentEndPosition.Last().Position == TotalSize;
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Format("CurrentStartPosition:{0}", CurrentStartPosition.Last().Position));
            sb.AppendLine(string.Format("CurrentEndPosition:{0}", CurrentEndPosition.Last().Position));
            sb.AppendLine(string.Format("TotalSize:{0}", TotalSize));
            sb.AppendLine(string.Format("IsFirstPage:{0}", IsFirstPage));
            sb.AppendLine(string.Format("IsFinalPage:{0}", IsFinalPage));
            sb.AppendLine(string.Format("LogItems.Count:{0}", LogItems.Count));
            return sb.ToString();
        }
    }

    public class ScannedResult
    {
        public int ScannedItemsCount { get; set; }
        public long ScannedByteLength { get; set; }
    }

    static class Program
    {
        private static readonly string[] logContents = {
            "Write a log item for test",
            "I'm not empty",
            "",
            "Pseudo-random numbers are chosen with equal probability from a finite set of numbers. The chosen numbers are not completely random because a mathematical algorithm is used to select them, but they are sufficiently random for practical purposes. The current implementation of the Random class is based on a modified version of Donald E. Knuth's subtractive random number generator algorithm. For more information, see D. E. Knuth. The Art of Computer Programming, Volume 2: Seminumerical Algorithms. Addison-Wesley, Reading, MA, third edition, 1997.",
            "Read something...",
            "Writing something....",
            "Something goes error!",
            "Something executed successfully",
            "There is nothing to log",
            "It's just a info record",
            "It's a warning!",
            "I'm executing a scripts command...",
            "I'm just waiting.....",
            "I expected the executing result of \"ABC\""
        };
        private static readonly int PAGED_LINES_COUNT = 500;
        private static readonly int BUFFER_SIZE_OF_96_KB = 96 * 1024;

        static void Main(string[] args)
        {
            //Write100MillionLinesOfLogItemsToFile();

            var currentPage = new PagedLogItems();
            currentPage.LoadType = LoadType.NextPage;
            currentPage.LogType = LogType.All;

            //Load next page log items of all type
            currentPage = LoadLogItemByPage(currentPage);
            Console.WriteLine(currentPage);

            //Load next page log items of all type
            currentPage = LoadLogItemByPage(currentPage);
            Console.WriteLine(currentPage);

            //Load previous page log items of all type
            currentPage.LoadType = LoadType.PreviousPage;
            currentPage = LoadLogItemByPage(currentPage);
            Console.WriteLine(currentPage);

            //Load next page log items of all type 100 times
            currentPage.LoadType = LoadType.NextPage;
            for(var i = 0; i < 10; ++i)
            {
                currentPage = LoadLogItemByPage(currentPage);
                Console.WriteLine(currentPage);
            }

            //Load next page log items of E type 100 times
            //Note: When change the log type to load, you must renew a new loading from the begin of the large file.
            currentPage = new PagedLogItems();
            currentPage.LogType = LogType.E;
            currentPage.LoadType = LoadType.NextPage;
            for (var i = 0; i < 10; ++i)
            {
                currentPage = LoadLogItemByPage(currentPage);
                Console.WriteLine(currentPage);
            }

            //Load next page log items of E type 100 times
            //Note: When change the log type to load, you must renew a new loading from the begin of the large file.
            currentPage = new PagedLogItems();
            currentPage.LogType = LogType.L;
            currentPage.LoadType = LoadType.NextPage;
            for (var i = 0; i < 10; ++i)
            {
                currentPage = LoadLogItemByPage(currentPage);
                Console.WriteLine(currentPage);
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static LogItem ToLogItem(this string line, LogType logType)
        {
            if (string.IsNullOrEmpty(line) || line.Length < 2)
            {
                return null;
            }

            var logTypeStringValue = line.Substring(0, 1);
            var logTypeFromLine = LogType.None;
            if(!Enum.TryParse<LogType>(logTypeStringValue, out logTypeFromLine)){
                return null;
            }

            if (!logType.HasFlag(logTypeFromLine))
            {
                return null;
            }

            var dateTimeContentStringValue = line.Substring(2);
            var stringArray = dateTimeContentStringValue.Split(' ');
            if(stringArray == null || stringArray.Length <= 1)
            {
                return null;
            }

            var dateTimeStringValue = stringArray[0];
            var content = stringArray[1];
            var dateTime = DateTime.MinValue;
            if(!DateTime.TryParse(dateTimeStringValue, out dateTime))
            {
                return null;
            }

            return new LogItem() { LogType = logType, Content = content, DateTime = dateTime };
        }

        private static PagedLogItems LoadLogItemByPage(PagedLogItems currentPage)
        {
            var stopWatch = new Stopwatch();
            Console.WriteLine("Start load log items from file \"test.log\"....");
            stopWatch.Start();

            var buffer = new byte[BUFFER_SIZE_OF_96_KB];
            using (var fs = new FileStream("test.log", FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                currentPage.TotalSize = fs.Length;
                var errorCount = 0;
                SetPositionOfFileStream(currentPage, fs);
                while(fs.Read(buffer, 0, BUFFER_SIZE_OF_96_KB) > 0)
                {
                    var content = System.Text.Encoding.UTF8.GetString(buffer);
                    content = content.TrimEnd('\0');
                    if (string.IsNullOrEmpty(content))
                    {
                        break;
                    }

                    var lines = content.Split('\n');
                    if(lines == null || lines.Length <= 0)
                    {
                        break;
                    }

                    ScannedResult scannedResult = null;
                    try
                    {
                        scannedResult = ScanLines(lines, currentPage);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("ScanLines Failed! error message:{0}", ex.Message);

                        errorCount++;
                        currentPage.LogItems = currentPage.LogItems.Take(currentPage.LogItems.Count - scannedResult.ScannedItemsCount).ToList();
                        buffer = new byte[BUFFER_SIZE_OF_96_KB];
                        var newStartPosition = (fs.Position - BUFFER_SIZE_OF_96_KB - errorCount) > 0 ? (fs.Position - BUFFER_SIZE_OF_96_KB - errorCount) : 0;
                        fs.Position = newStartPosition;
                        continue;
                    }

                    errorCount = 0;

                    var currentStartPosition = (fs.Position - BUFFER_SIZE_OF_96_KB);
                    var currentEndPosition = (currentStartPosition + scannedResult.ScannedByteLength) >= fs.Length ? fs.Length : currentStartPosition + scannedResult.ScannedByteLength;
                    if(currentPage.CurrentStartPosition == null || currentPage.CurrentStartPosition.Count <= 0)
                    {
                        currentPage.PreviousStartPosition = 0;
                    }
                    else
                    {
                        currentPage.PreviousStartPosition = currentPage.CurrentStartPosition.Last().Position;
                    }

                    //Load give count of log items in the first loading
                    if(currentPage.LogItems.Count >= PAGED_LINES_COUNT)
                    {
                        currentPage.CurrentStartPosition.Add(new PositionWithStatus() { Position = currentStartPosition, LoadCompleted = true });
                        currentPage.CurrentEndPosition.Add(new PositionWithStatus() { Position = currentEndPosition, LoadCompleted = true });
                        break;
                    }
                    else
                    {
                        currentPage.CurrentStartPosition.Add(new PositionWithStatus() { Position = currentStartPosition, LoadCompleted = false });
                        currentPage.CurrentEndPosition.Add(new PositionWithStatus() { Position = currentEndPosition, LoadCompleted = false });

                        buffer = new byte[BUFFER_SIZE_OF_96_KB];
                        SetPositionOfFileStream(currentPage, fs);
                    }
                }
            }

            stopWatch.Stop();
            Console.WriteLine("Loading log items from file \"test.log\" ended.");
            Console.WriteLine("Time consumed:{0} milliseconds", stopWatch.ElapsedMilliseconds);
            return currentPage;
        }

        private static void SetPositionOfFileStream(PagedLogItems currentPage, FileStream fs)
        {
            if(currentPage.LoadType == LoadType.PreviousPage)
            {
                if(currentPage.CurrentEndPosition.Count > 0)
                {
                    //if current page is not loaded completely, go on to next block and continue loading
                    if(currentPage.CurrentEndPosition.Any(p => !p.LoadCompleted))
                    {
                        currentPage.CurrentStartPosition = currentPage.CurrentStartPosition.Where(p => p.LoadCompleted).ToList();
                        currentPage.CurrentEndPosition = currentPage.CurrentEndPosition.Where(p => p.LoadCompleted).ToList();

                        fs.Position = currentPage.PreviousStartPosition - BUFFER_SIZE_OF_96_KB < 0 ? 0 : currentPage.PreviousStartPosition - BUFFER_SIZE_OF_96_KB;
                    }
                    else
                    {
                        //else, go to previous block
                        if(currentPage.CurrentEndPosition.Count >= 2)
                        {
                            currentPage.CurrentStartPosition.RemoveAt(currentPage.CurrentStartPosition.Count - 1);
                            currentPage.CurrentEndPosition.RemoveAt(currentPage.CurrentEndPosition.Count - 1);

                            currentPage.CurrentStartPosition.RemoveAt(currentPage.CurrentStartPosition.Count - 1);
                            currentPage.CurrentEndPosition.RemoveAt(currentPage.CurrentEndPosition.Count - 1);

                            fs.Position = currentPage.PreviousStartPosition;
                        }
                        else
                        {
                            fs.Position = 0;
                        }
                    }
                }
                else
                {
                    fs.Position = 0;
                }
            }
            else
            {
                if(currentPage.CurrentEndPosition.Count > 0)
                {
                    //if current page is not loaded completely, go on to next block and continue loading
                    if (currentPage.CurrentEndPosition.Any(p => !p.LoadCompleted))
                    {
                        currentPage.CurrentStartPosition = currentPage.CurrentStartPosition.Where(p => p.LoadCompleted).ToList();
                        currentPage.CurrentEndPosition = currentPage.CurrentEndPosition.Where(p => p.LoadCompleted).ToList();

                        fs.Position = currentPage.PreviousStartPosition + BUFFER_SIZE_OF_96_KB >= fs.Length ? fs.Length : currentPage.PreviousStartPosition + BUFFER_SIZE_OF_96_KB;
                    }
                    else
                    {
                        fs.Position = currentPage.CurrentEndPosition.Last().Position;
                    }
                }
                else
                {
                    fs.Position = 0;
                }
            }
        }

        private static ScannedResult ScanLines(string[] lines, PagedLogItems currentPage)
        {
            var result = new ScannedResult();
            if(lines == null || lines.Length <= 0)
            {
                return result;
            }

            if(currentPage == null || currentPage.LogItems == null)
            {
                return result;
            }

            if(currentPage.LogItems.Count >= PAGED_LINES_COUNT)
            {
                currentPage.LogItems.Clear();
            }

            foreach(var line in lines)
            {
                if(currentPage.LogItems.Count >= PAGED_LINES_COUNT)
                {
                    break;
                }

                var logItem = line.ToLogItem(currentPage.LogType);
                if(logItem == null)
                {
                    result.ScannedByteLength += System.Text.Encoding.UTF8.GetBytes(line + "\n").LongLength;
                    continue;
                }
                else
                {
                    result.ScannedItemsCount++;
                    currentPage.LogItems.Add(logItem);
                    result.ScannedByteLength += System.Text.Encoding.UTF8.GetBytes(line + "\n").LongLength;
                }
            }
            return result;
        }

        /// <summary>
        /// Write 10 * 1000 * 1000 lines of log item to log file consumed about 60~70 seconds.
        /// After written completely, the size of the log file is about 800 ~ 900 MB.
        /// </summary>
        private static void Write100MillionLinesOfLogItemsToFile()
        {
            if (File.Exists("test.log"))
            {
                File.Delete("test.log");
            }

            var stopWatch = new Stopwatch();
            Console.WriteLine("Start writing log content to file \"test.log\"....");

            stopWatch.Start();
            using (var fs = new FileStream("test.log", FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            {
                using (var sw = new StreamWriter(fs))
                {
                    for (var i = 0; i < 1000 * 1000 * 10; ++i)
                    {
                        var logType = (LogType)GetRandomEnumValue(typeof(LogType));
                        while(logType == LogType.None || logType == LogType.All)
                        {
                            logType = (LogType)GetRandomEnumValue(typeof(LogType));
                        }
                        var content = logContents[GenerateRandom(logContents.Length)];

                        //var logItem = new LogItem() { LogType = logType, Content = content, DateTime = DateTime.Now };
                        //sw.Write(logItem.ToString() + "\r\n");

                        sw.Write(string.Format("{0} {1} {2}", logType, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), content) + "\r\n");
                    }
                    sw.Flush();
                }
            }
            stopWatch.Stop();

            Console.WriteLine("Writing log content to file \"test.log\" ended.");
            Console.WriteLine("Time consumed:{0} seconds", stopWatch.ElapsedMilliseconds / 1000);
        }

        /// <summary>
        /// Generate a random integer which is smaller than the given max value
        /// </summary>
        /// <param name="max"></param>
        /// <returns></returns>
        public static int GenerateRandom(int max)
        {
            byte[] r = new byte[4];
            int value;
            using (RNGCryptoServiceProvider rnd = new RNGCryptoServiceProvider())
            {
                do
                {
                    rnd.GetBytes(r);
                    value = BitConverter.ToInt32(r, 0) & Int32.MaxValue;
                } while (value >= max * (Int32.MaxValue / max));
            }
            return value % max;
        }

        /// <summary>
        /// Returns a uniformly random integer representing one of the values in the enum.
        /// </summary>
        /// <param name="enumType"></param>
        /// <returns></returns>
        public static int GetRandomEnumValue(Type enumType)
        {
            int[] values = (int[])Enum.GetValues(enumType);
            int randomIndex = GenerateRandom(values.Length);
            return values[randomIndex];
        }
    }
}
