using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>A scope the test steers: it answers the reading in <see cref="Current"/> (null once the scope is gone), records every signal and every new ceiling, and can end the agent on a signal.</summary>
internal sealed class FakeScope : IScopeControl
{
    private readonly List<bool> _kills = [];
    private readonly List<TimeSpan> _ceilings = [];

    /// <summary>Gets or sets the reading the next sample answers; null is a scope that is gone.</summary>
    internal ScopeSample? Current { get; set; } = Reading();

    /// <summary>Gets or sets what systemd says of the scope after it ended ('oom-kill', 'timeout'), or null.</summary>
    internal string? Ended { get; set; }

    /// <summary>Gets or sets what happens on each signal (true is SIGKILL); a test ends the agent here.</summary>
    internal Action<bool>? OnKill { get; set; }

    /// <summary>Gets every signal sent: false for SIGTERM, true for SIGKILL.</summary>
    internal IReadOnlyList<bool> Kills { get { lock (_kills) { return [.. _kills]; } } }

    /// <summary>Gets every ceiling set, in order.</summary>
    internal IReadOnlyList<TimeSpan> Ceilings { get { lock (_ceilings) { return [.. _ceilings]; } } }

    internal static ScopeSample Reading(long memory = 0, long peak = 0, long oomKill = 0, int tasks = 1, long cpuMicroseconds = 0) =>
        new(memory, peak, new MemoryEvents(High: 0, Max: 0, Oom: 0, OomKill: oomKill), tasks, cpuMicroseconds);

    public ValueTask<ScopeSample?> Sample(CancellationToken cancellationToken) => ValueTask.FromResult(Current);

    public ValueTask<string?> Result(CancellationToken cancellationToken) => ValueTask.FromResult(Ended);

    public ValueTask SetRuntimeMax(TimeSpan runtimeMax, CancellationToken cancellationToken)
    {
        lock (_ceilings)
        {
            _ceilings.Add(runtimeMax);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask Kill(bool force, CancellationToken cancellationToken)
    {
        lock (_kills)
        {
            _kills.Add(force);
        }

        OnKill?.Invoke(force);
        return ValueTask.CompletedTask;
    }
}
