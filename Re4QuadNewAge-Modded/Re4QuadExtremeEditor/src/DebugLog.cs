using System;

namespace Re4QuadExtremeEditor.src
{
    /// <summary>
    /// Lightweight append-only logger used to trace the RTP open/render crash
    /// that bypasses the managed unhandled-exception handlers.
    /// </summary>
    public static class DebugLog
    {
        private static readonly object _lock = new object();

        public static void Write(object message)
        {
            try
            {
                lock (_lock)
                {
                    System.IO.File.AppendAllText(
                        @"C:\Temp\RTP_Debug.log",
                        $"{DateTime.Now:HH:mm:ss} {message}\n");
                }
            }
            catch { }
        }
    }
}
