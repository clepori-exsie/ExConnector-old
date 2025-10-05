using Microsoft.Extensions.Logging;
using System.Text;

namespace ExConnector.Utils;

public sealed class SimpleFileLoggerProvider : ILoggerProvider
{
    private readonly string _dir;
    private readonly object _lock = new();

    public SimpleFileLoggerProvider(string dir)
    {
        _dir = dir;
        Directory.CreateDirectory(_dir);
    }

    public ILogger CreateLogger(string categoryName) => new SimpleFileLogger(_dir, _lock, categoryName);
    public void Dispose() { }

    private sealed class SimpleFileLogger : ILogger
    {
        private readonly string _dir;
        private readonly object _lock;
        private readonly string _cat;

        public SimpleFileLogger(string dir, object l, string cat)
        {
            _dir = dir; _lock = l; _cat = cat;
        }

public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_cat} - {formatter(state, exception)}";
            if (exception != null) line += Environment.NewLine + exception;
            var path = Path.Combine(_dir, $"exconnector-{DateTime.Now:yyyyMMdd}.log");
            lock (_lock) { File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8); }
        }
    }
}
