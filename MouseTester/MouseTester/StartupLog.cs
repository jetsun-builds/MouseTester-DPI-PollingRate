using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MouseTester
{
    // Opt-in startup diagnostics; never log mouse movement or device identifiers.
    internal static class StartupLog
    {
        private static readonly bool enabled = Array.IndexOf(Environment.GetCommandLineArgs(), "--startup-log") >= 0;
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static readonly ConcurrentQueue<string> entries = new ConcurrentQueue<string>();
        private static readonly object fileLock = new object();
        public static void Mark(string stage)
        {
            if (!enabled || clock.ElapsedMilliseconds > 30000 || entries.Count >= 2048) return;
            entries.Enqueue(clock.Elapsed.TotalMilliseconds.ToString("F2") + "ms thread=" + Thread.CurrentThread.ManagedThreadId + " " + stage);
        }
        public static void Flush()
        {
            if (!enabled) return;
            Task.Run(delegate {
                try { lock (fileLock) File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MouseTester-startup.log"), entries.ToArray()); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            });
        }
    }
}
