using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The starts of one process in memory; nothing is read back from a ledger.</summary>
internal sealed class FakeStartedSessions : IStartedSessions
{
    private readonly Dictionary<string, StartedEntry> _entries = new(StringComparer.Ordinal);

    public ValueTask<IDisposable> Enter(ActId actId, CancellationToken cancellationToken) => ValueTask.FromResult<IDisposable>(new Release());

    public StartedEntry? InMemory(ActId actId) => _entries.GetValueOrDefault(actId.Value);

    public void Keep(ActId actId, StartedEntry entry) => _entries[actId.Value] = entry;

    public ValueTask<StartedEntry?> Find(ActId actId, CancellationToken cancellationToken) => ValueTask.FromResult(InMemory(actId));

    private sealed class Release : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
