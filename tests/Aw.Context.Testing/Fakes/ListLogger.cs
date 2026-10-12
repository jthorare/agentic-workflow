using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Aw.Context.Testing.Fakes;

/// <summary>One category's logger for <see cref="ListLoggerProvider"/>.</summary>
internal sealed class ListLogger(string category, ConcurrentQueue<string> lines) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        lines.Enqueue(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{logLevel} {category}[{eventId.Id}]: {formatter(state, exception)}{(exception is null ? "" : " " + exception.Message)}"));
}
