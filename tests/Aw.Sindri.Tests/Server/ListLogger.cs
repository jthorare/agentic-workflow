using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Tests.Server;

/// <summary>A logger that keeps each formatted message, so a test can see what a background service logged.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    private readonly Lock _gate = new();
    private readonly List<string> _lines = [];
    private readonly List<Exception> _exceptions = [];

    public IReadOnlyList<string> Lines { get { lock (_gate) { return [.. _lines]; } } }

    /// <summary>The exceptions logged with a message, in order (the formatted line leaves them out).</summary>
    public IReadOnlyList<Exception> Exceptions { get { lock (_gate) { return [.. _exceptions]; } } }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_gate)
        {
            _lines.Add(formatter(state, exception));
            if (exception is not null)
            {
                _exceptions.Add(exception);
            }
        }
    }
}
