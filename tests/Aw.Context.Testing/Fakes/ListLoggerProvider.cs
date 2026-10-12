using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Aw.Context.Testing.Fakes;

/// <summary>Collects every log line the web host writes, at every level, into memory instead of the console.</summary>
internal sealed class ListLoggerProvider(ConcurrentQueue<string> lines) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new ListLogger(categoryName, lines);

    public void Dispose()
    {
    }
}
