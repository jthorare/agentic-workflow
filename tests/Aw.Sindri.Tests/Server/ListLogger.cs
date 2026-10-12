using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Tests.Server;

/// <summary>A logger that keeps each formatted message, so a test can see what a background service logged.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    private readonly Lock _gate = new();
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines { get { lock (_gate) { return [.. _lines]; } } }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_gate)
        {
            _lines.Add(formatter(state, exception));
        }
    }
}
