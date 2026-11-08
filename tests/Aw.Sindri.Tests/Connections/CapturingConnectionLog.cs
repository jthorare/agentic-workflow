using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Tests.Connections;

/// <summary>Keeps every line a connection slice logs, with its exception text and scope-free fields, so a test can search them.</summary>
internal sealed class CapturingConnectionLog : ILogger
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _lines = new();

    public IReadOnlyList<string> Lines => [.. _lines];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        var fields = state is IEnumerable<KeyValuePair<string, object?>> pairs ? string.Join(' ', pairs.Select(static p => p.Key + "=" + p.Value)) : string.Empty;
        _lines.Enqueue(string.Create(CultureInfo.InvariantCulture, $"{logLevel} {eventId.Id}: {formatter(state, exception)} [{fields}] {exception}"));
    }
}
