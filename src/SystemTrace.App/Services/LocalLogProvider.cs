using System.IO;
using Microsoft.Extensions.Logging;

namespace SystemTrace.App.Services;

// Deliberately records only metadata, never exception messages, paths or collected evidence.
public sealed class LocalLogProvider(string directory) : ILoggerProvider
{
    private readonly object gate = new();
    public ILogger CreateLogger(string categoryName) => new MetadataLogger(this);
    public void Dispose() { }
    private void Write(LogLevel level, EventId id, Exception? exception)
    {
        if (level < LogLevel.Warning) return;
        lock (gate)
            try
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "diagnostics.log");
                if (File.Exists(path) && new FileInfo(path).Length >= 1024 * 1024)
                    File.Move(path, Path.Combine(directory, "diagnostics.previous.log"), true);
                File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {level} event={id.Id} type={exception?.GetType().Name ?? "none"}{Environment.NewLine}");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
    }
    private sealed class MetadataLogger(LocalLogProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            provider.Write(logLevel, eventId, exception);
    }
}
