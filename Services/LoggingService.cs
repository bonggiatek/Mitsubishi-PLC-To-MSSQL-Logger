using System;
using System.IO;

namespace PLCDataLogger.Services
{
    public class LoggingService
    {
        private static readonly Lazy<LoggingService> _instance = new(() => new LoggingService());
        public static LoggingService Instance => _instance.Value;

        private readonly string _logFilePath;
        private readonly object _lockObject = new object();

        private LoggingService()
        {
            string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logFolder);
            _logFilePath = Path.Combine(logFolder, $"PLCLog_{DateTime.Now:yyyyMMdd}.txt");
        }

        public void LogInfo(string message)
        {
            Log("INFO", message);
        }

        public void LogError(Exception? ex, string message)
        {
            string fullMessage = ex != null ? $"{message} - Exception: {ex.Message}\n{ex.StackTrace}" : message;
            Log("ERROR", fullMessage);
        }

        public void LogWarning(string message)
        {
            Log("WARNING", message);
        }

        private void Log(string level, string message)
        {
            lock (_lockObject)
            {
                try
                {
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);

                    // Also write to console for debugging
                    Console.WriteLine(logEntry);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write to log: {ex.Message}");
                }
            }
        }
    }
}
