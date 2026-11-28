namespace Aw.Judge.Testing;

/// <summary>
/// The clock <see cref="TraitVerdictCacheContract"/> hands the cache it checks: it moves only when the contract
/// advances it. Aw.Judge.Testing has no FakeTimeProvider package, so this is the minimal <see cref="TimeProvider"/>
/// (the same shape as Aw.Kernel.Testing's ContractClock).
/// </summary>
/// <param name="start">The first instant.</param>
internal sealed class VerdictCacheClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
