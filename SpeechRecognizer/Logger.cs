using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Mycroft.App
{
    /// <summary>
    /// Log messages to log files
    /// by default logs are in the log folder, and labeled by day.
    /// Log messages are automatically time stamped
    /// </summary>
    class Logger
    {
        /// <summary>
        /// The severity level of a message to log, in order of increasing
        /// severity.
        /// </summary>
        public enum Level { Debug, Info, Warning, Error, Exception, WTF }

        /// <summary>
        /// The minimum level at which to log, inclusive
        /// </summary>
        public Level LogLevel { get; set; }

        /// <summary>
        /// A lock to ensure only one write to the stream can happen at once
        /// </summary>
        public object WriteLock;

        private string path = System.IO.Path.Combine("logs");
        private string filename;
        private DateTime date;
        private StreamWriter os;
        private FileStream fs;

        static private Logger Instance = null;

        private Logger()
        {
            LogLevel = Level.Debug;
            WriteLock = new Object();
            CheckFile();
        }

        public static Logger GetInstance()
        {
            if (Instance == null)
                Instance = new Logger();
            return Instance;
        }

        /// <summary>
        /// Checks file to confirm correct log file.
        /// </summary>
        private void CheckFile()
        {
            if (!DateTime.Today.Equals(this.date))
            {
                if (os != null)
                {
                    Close();
                }
                this.date = DateTime.Today;

                Directory.CreateDirectory(path);
                this.filename = System.IO.Path.Combine(path, DateTime.Now.ToString("yyyy-MM-dd") + ".log");

                lock (WriteLock)
                {
                    fs = new FileStream(filename, FileMode.Append);
                    os = new StreamWriter(fs);
                    os.AutoFlush = true;
                }
            }
        }

        /// <summary>
        /// Log given message.
        /// Example of a log message:
        /// [2008-04-10 13:30:00Z] WARNING: this is the message
        /// </summary>
        /// <param name="level">The level of severity of this message</param>
        /// <param name="message">The message to log</param>
        /// <returns>true if this message was logged</returns>
        private bool Log(Level level, string message)
        {
            CheckFile();

            if (level.CompareTo(LogLevel) < 0)
            {
                return false;
            }

            try
            {
                // assemble message
                var sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("u"));
                sb.Append(" ");
                sb.Append(Enum.GetName(typeof(Level), level).ToUpper());
                sb.Append(": ");
                sb.Append(message);
                message = sb.ToString();

                lock (WriteLock)
                {
                    // write to console
                    var oldColor = Console.BackgroundColor;
                    Console.ForegroundColor = GetColor(level);
                    Console.WriteLine(message);
                    Console.ForegroundColor = oldColor;

                    // write to diagnostics log
                    System.Diagnostics.Debug.WriteLine(message);

                    // write to log file
                    os.WriteLine(sb.ToString());
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Get the console color associated with this log level
        /// </summary>
        /// <param name="level">the log level</param>
        /// <returns>the color</returns>
        private ConsoleColor GetColor(Level level)
        {
            switch (level)
            {
                case Level.Debug:
                    return ConsoleColor.White;
                case Level.Info:
                    return ConsoleColor.Cyan;
                case Level.Warning:
                    return ConsoleColor.Yellow;
                default:
                    return ConsoleColor.Red;
            }
        }

        /// <summary>
        /// Close the underlying connection write stream.
        /// </summary>
        /// <returns>true when closing succeeded</returns>
        public bool Close()
        {
            try
            {
                lock (WriteLock)
                {
                    os.Flush();
                    os.Close();
                }
            }
            catch
            {
                // return false;
            }
            return true;
        }

        /// <summary>
        /// Log a debug-level message
        /// </summary>
        /// <param name="message">the message to log</param>
        /// <returns>true if the message was logged</returns>
        public Task<bool> Debug(string message)
        {
            return Task<bool>.Run(() => Log(Level.Debug, message));
        }

        /// <summary>
        /// Log an info-level message
        /// </summary>
        /// <param name="message">the message to log</param>
        /// <returns>true if the message was logged</returns>
        public Task<bool> Info(string message)
        {
            return Task<bool>.Run(() => Log(Level.Info, message));
        }

        /// <summary>
        /// Log a warning-level message
        /// </summary>
        /// <param name="message">the message to log</param>
        /// <returns>true if the message was logged</returns>
        public Task<bool> Warning(string message)
        {
            return Task<bool>.Run(() => Log(Level.Warning, message));
        }

        /// <summary>
        /// Log an error-level message
        /// </summary>
        /// <param name="message">the message to log</param>
        /// <returns>true if the message was logged</returns>
        public Task<bool> Error(string message)
        {
            return Task<bool>.Run(() => Log(Level.Error, message));
        }

        /// <summary>
        /// Log a WTF-level message
        /// </summary>
        /// <param name="message">the message to log</param>
        /// <returns>true if the message was logged</returns>
        public Task<bool> WTF(string message)
        {
            return Task<bool>.Run(() => Log(Level.WTF, message));
        }
    }
}
