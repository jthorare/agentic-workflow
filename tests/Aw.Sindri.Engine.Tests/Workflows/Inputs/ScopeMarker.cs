namespace Aw.Sindri.Tests.Workflows.Inputs;

/// <summary>A per-scope service: every DI scope gets its own id.</summary>
internal sealed class ScopeMarker
{
    private static long _last;

    public long Id { get; } = Interlocked.Increment(ref _last);
}
