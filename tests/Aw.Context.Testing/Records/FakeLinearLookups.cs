using Aw.Context.Core.Linear;
using Aw.Kernel;

namespace Aw.Context.Testing.Records;

/// <summary>The <see cref="ILinearLookups"/> over one scripted answer, counting the calls. A test sets <see cref="Answer"/> (teams, or an error to fail with).</summary>
public sealed class FakeLinearLookups : ILinearLookups
{
    private int _calls;

    /// <summary>What the lookups answer.</summary>
    public Result<IReadOnlyList<LinearTeamLookups>> Answer { get; set; } = Array.Empty<LinearTeamLookups>();

    /// <summary>Runs at the start of every fetch, before it answers (a test looks at what the tick had done by then).</summary>
    public Action? OnFetch { get; set; }

    /// <summary>How many times Linear was asked.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<LinearTeamLookups>>> Fetch(LinearSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _ = Interlocked.Increment(ref _calls);
        OnFetch?.Invoke();
        return ValueTask.FromResult(Answer);
    }
}
