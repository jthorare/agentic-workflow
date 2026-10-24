using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>An IStaticSite that serves nothing: it hands out loopback origins on rising ports and remembers what it was asked to serve.</summary>
public sealed class FakeStaticSite : IStaticSite
{
    private int _port = 40_000;

    /// <summary>Folders that do not exist.</summary>
    public ISet<string> Missing { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Every folder served, in order.</summary>
    public IList<string> Served { get; } = [];

    /// <summary>How many sites are running now.</summary>
    public int Running { get; private set; }

    /// <inheritdoc/>
    public ValueTask<Result<IServedSite>> ServeAsync(string directory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Missing.Contains(directory))
        {
            return ValueTask.FromResult<Result<IServedSite>>(EvidenceErrors.RunFailed.With($"the design folder {directory} does not exist"));
        }

        Served.Add(directory);
        Running++;
        return ValueTask.FromResult<Result<IServedSite>>(new Site(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{++_port}"), () => Running--));
    }

    private sealed class Site(string origin, Action stopped) : IServedSite
    {
        public string Origin { get; } = origin;

        public ValueTask DisposeAsync()
        {
            stopped();
            return ValueTask.CompletedTask;
        }
    }
}
