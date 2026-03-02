using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace IosPhotoImporter.App.Logging;

public sealed class LocalFileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly ConcurrentDictionary<string, LocalFileLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

    public LocalFileLoggerProvider(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);
        var filePath = Path.Combine(logDirectory, "importer.log");
        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, string.Empty);
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, _ => new LocalFileLogger(_logDirectory, categoryName));
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
        if (!File.Exists(_filePath))
        {
            File.WriteAllText(_filePath, string.Empty);
        }
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
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(_filePath, line + Environment.NewLine);
            }
            catch
            {
                // Avoid crashing the app due to logging I/O errors.
            }
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
