using System.Collections.Concurrent;

namespace Aw.Sindri.Tests.Workflows.Inputs;

/// <summary>What <see cref="ScopeProbeInput"/> saw: one line per fill, naming the scope's marker.</summary>
internal sealed class ScopeLog
{
    public ConcurrentQueue<long> Scopes { get; } = new();
}
