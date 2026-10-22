using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>Brings the managed Onyx stack up (Onyx spec §4.1 steps 1 to 5), as `sindri onyx up` does; the Onyx setup job's way to it without naming that slice.</summary>
public interface IOnyxStackUp
{
    /// <summary>Runs the stack's `up`: idempotent and resumable.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>Where Onyx answers and its release, or the error that stopped `up`.</returns>
    public ValueTask<Result<OnyxStackInfo>> Up(CancellationToken cancellationToken);
}
