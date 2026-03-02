using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace IosPhotoImporter.App.Logging;

public sealed class LocalFileLoggerProvider(string logDirectory) : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, LocalFileLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, _ => new LocalFileLogger(logDirectory, categoryName));
    }

    public void Dispose()
    {
        foreach (var logger in _loggers.Values)
        {
            logger.Dispose();
        }

        _loggers.Clear();
    }
}

internal sealed class LocalFileLogger : ILogger, IDisposable
{
    private readonly string _categoryName;
    private readonly string _filePath;
    private readonly object _sync = new();

    public LocalFileLogger(string logDirectory, string categoryName)
    {
        _categoryName = categoryName;
        Directory.CreateDirectory(logDirectory);
        _filePath = Path.Combine(logDirectory, "importer.log");
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Information;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var line = $"{DateTimeOffset.UtcNow:O} [{logLevel}] {_categoryName}: {message}";
        if (exception is not null)
        {
            line += $"{Environment.NewLine}{exception}";
        }

        lock (_sync)
        {
            File.AppendAllText(_filePath, line + Environment.NewLine);
        }
    }

    public void Dispose()
    {
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
