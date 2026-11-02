using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Aw.Context.Tests.Linear;

/// <summary>Keeps every line an adapter logs, with its exception's message, so a test can search them.</summary>
/// <typeparam name="T">The logger's category.</typeparam>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<string> _lines = new();

    public IReadOnlyList<string> Lines => [.. _lines];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var fields = state is IEnumerable<KeyValuePair<string, object?>> pairs ? string.Join(' ', pairs.Select(p => $"{p.Key}={p.Value}")) : "";
        _lines.Enqueue(string.Create(CultureInfo.InvariantCulture, $"{logLevel} {eventId.Id}: {formatter(state, exception)} [{fields}] {exception?.Message}"));
    }
}
