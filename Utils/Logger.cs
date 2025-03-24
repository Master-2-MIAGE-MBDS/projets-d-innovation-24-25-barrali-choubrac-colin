using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace DeepBridgeWindowsApp.Utils
{
    /// <summary>
    /// Application-wide logging utility that provides consistent logging across the application.
    /// Logs to Debug output and optionally to a file.
    /// </summary>
    public static class Logger
    {
        private static readonly object LockObj = new object();
        private static string _logFilePath;
        private static bool _fileLoggingEnabled;

        /// <summary>
        /// Initialize the logger with optional file logging.
        /// </summary>
        /// <param name="enableFileLogging">Whether to enable logging to a file.</param>
        /// <param name="logDirectory">Directory for log files (defaults to AppData if null).</param>
        public static void Initialize(bool enableFileLogging = false, string logDirectory = null)
        {
            _fileLoggingEnabled = enableFileLogging;

            if (enableFileLogging)
            {
                string directory = logDirectory ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DeepBridgeApp", "Logs");

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _logFilePath = Path.Combine(directory, $"DicomViewer_{timestamp}.log");

                // Write header to log file
                string header = $"DeepBridge DICOM Viewer Log - Started at {DateTime.Now}\r\n" +
                                $"Thread ID: {Thread.CurrentThread.ManagedThreadId}\r\n" +
                                $"----------------------------------------\r\n";

                File.WriteAllText(_logFilePath, header);

                Debug.WriteLine($"Log file initialized at: {_logFilePath}");
            }
        }

        /// <summary>
        /// Log an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="category">Optional category for the log message.</param>
        public static void Info(string message, string category = null)
        {
            LogMessage("INFO", message, category);
        }

        /// <summary>
        /// Log a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="category">Optional category for the log message.</param>
        public static void Warning(string message, string category = null)
        {
            LogMessage("WARNING", message, category);
        }

        /// <summary>
        /// Log an error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception related to the error.</param>
        /// <param name="category">Optional category for the log message.</param>
        public static void Error(string message, Exception exception = null, string category = null)
        {
            StringBuilder sb = new StringBuilder(message);

            if (exception != null)
            {
                sb.AppendLine();
                sb.Append($"Exception: {exception.GetType().Name}: {exception.Message}");

                if (exception.StackTrace != null)
                {
                    sb.AppendLine();
                    sb.Append($"Stack Trace: {exception.StackTrace}");
                }
            }

            LogMessage("ERROR", sb.ToString(), category);
        }

        /// <summary>
        /// Log a performance measurement.
        /// </summary>
        /// <param name="operation">The operation being measured.</param>
        /// <param name="elapsedMilliseconds">The elapsed time in milliseconds.</param>
        public static void Performance(string operation, long elapsedMilliseconds)
        {
            LogMessage("PERF", $"{operation}: {elapsedMilliseconds}ms", "Performance");
        }

        private static void LogMessage(string level, string message, string category)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string threadId = Thread.CurrentThread.ManagedThreadId.ToString();
            string categoryStr = string.IsNullOrEmpty(category) ? "" : $"[{category}] ";
            string fullMessage = $"{timestamp} [{threadId}] {level}: {categoryStr}{message}";

            // Log to Debug output
            Debug.WriteLine(fullMessage);

            // Log to file if enabled
            if (_fileLoggingEnabled && !string.IsNullOrEmpty(_logFilePath))
            {
                lock (LockObj)
                {
                    try
                    {
                        File.AppendAllText(_logFilePath, fullMessage + Environment.NewLine);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to write to log file: {ex.Message}");
                        _fileLoggingEnabled = false; // Disable file logging on error
                    }
                }
            }
        }
    }
}