using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Tests.Server;

/// <summary>A logger factory whose loggers all write to one <see cref="ListLogger{T}"/>, so a test can see what the pipeline logged.</summary>
internal sealed class ListLoggerFactory : ILoggerFactory
{
    public ListLogger<ListLoggerFactory> Logger { get; } = new();

    public ILogger CreateLogger(string categoryName) => Logger;

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }
}
