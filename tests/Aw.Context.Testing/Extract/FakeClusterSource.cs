using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Testing.Extract;

/// <summary>An <see cref="IClusterSource"/> that answers a fixed <see cref="Set"/> (unavailable by default, like a machine without graphify) and records what it was asked.</summary>
public sealed class FakeClusterSource : IClusterSource
{
    /// <summary>What every call answers.</summary>
    public ClusterSet Set { get; set; } = new([], Available: false);

    /// <summary>When set, every call fails with it (graphify crashed in a way the source didn't absorb).</summary>
    public Error? Failure { get; set; }

    /// <summary>The repos asked about, in order.</summary>
    public IList<string> Asked { get; } = [];

    /// <summary>The limits each call was given, in order.</summary>
    public IList<ClusterLimits> AskedLimits { get; } = [];

    /// <summary>When set, every call waits for it before answering (a build held mid-way).</summary>
    public Task? Hold { get; set; }

    /// <summary>When above zero, every call takes this long on <see cref="Clock"/> before answering (a graphify run that is slow but alive).</summary>
    public TimeSpan Takes { get; set; }

    /// <summary>The clock <see cref="Takes"/> runs on (the system clock when null).</summary>
    public TimeProvider? Clock { get; set; }

    /// <summary>How many calls are inside right now.</summary>
    public int Active { get; private set; }

    /// <summary>The most calls that were inside at once.</summary>
    public int MostActive { get; private set; }

    /// <inheritdoc/>
    public async ValueTask<Result<ClusterSet>> Clusters(ContextRepo repo, string commit, ClusterLimits limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(limits);
        AskedLimits.Add(limits);
        Asked.Add($"{repo.Name.Value}@{commit}");
        Active++;
        MostActive = Math.Max(MostActive, Active);
        try
        {
            if (Hold is { } hold)
            {
                await hold.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (Takes > TimeSpan.Zero)
            {
                await Task.Delay(Takes, Clock ?? TimeProvider.System, cancellationToken).ConfigureAwait(false);
            }

            return Failure is { } failure ? failure : Set;
        }
        finally
        {
            Active--;
        }
    }
}
