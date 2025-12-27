using System;
using System.IO;
using System.Text;

namespace MusicBeePlugin.Utils
{
    public static class Logger
    {
        private static string _logPath;
        private static readonly object _lock = new object();

        public static void Initialize(string path)
        {
            _logPath = path;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Logger] Failed to initialize log directory: {ex}");
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Error(string message, Exception ex = null)
        {
            Write("ERROR", message, ex);
        }

        public static void Error(Exception ex)
        {
            Write("ERROR", ex.Message, ex);
        }

        private static void Write(string level, string message, Exception ex)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}");
                if (ex != null)
                {
                    sb.Append($"{Environment.NewLine}{ex}");
                }
                sb.Append(Environment.NewLine);

                string logEntry = sb.ToString();

                // 1. Write to Debug Console (Visible in VS Output)
                System.Diagnostics.Debug.Write(logEntry);

                // 2. Write to File
                if (!string.IsNullOrEmpty(_logPath))
                {
                    lock (_lock)
                    {
                        File.AppendAllText(_logPath, logEntry);
                    }
                }
            }
            catch
            {
                // Prevent logging from crashing the app
            }
        }
    }
}