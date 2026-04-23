using Ryujinx.Common.Logging;
using Ryujinx.Common.Logging.Targets;
using System;
using System.IO;

namespace Ryujinx.Common.Memory
{
    public class CsvMemoryLogTarget : ILogTarget
    {
        private readonly string _logFilePath;
        private StreamWriter _writer;
        private bool _headerWritten;

        public string Name => "CsvMemoryLog";
        public bool Enabled { get; set; } = true;

        public CsvMemoryLogTarget(string logDirectory)
        {
            _logFilePath = Path.Combine(logDirectory, "memory_log.csv");
        }

        public void Log(object sender, LogEventArgs args)
        {
            if (!Enabled || args.Data is not MemorySnapshot snapshot)
            {
                return;
            }

            LogSnapshot(snapshot);
        }

        public void LogSnapshot(MemorySnapshot snapshot)
        {
            if (!Enabled)
            {
                return;
            }

            EnsureWriter();

            if (!_headerWritten)
            {
                _writer.WriteLine("Timestamp,RssBytes,GcHeapBytes,UnmanagedBytes,SwapBytes,PressureLevel");
                _headerWritten = true;
            }

            _writer.WriteLine(
                $"{snapshot.Timestamp:O},{snapshot.RssBytes},{snapshot.GcHeapBytes},{snapshot.UnmanagedBytes},{snapshot.SwapBytes},{snapshot.PressureLevel}");
        }

        private void EnsureWriter()
        {
            if (_writer == null)
            {
                _writer = new StreamWriter(_logFilePath, append: true)
                {
                    AutoFlush = true,
                };
            }
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
