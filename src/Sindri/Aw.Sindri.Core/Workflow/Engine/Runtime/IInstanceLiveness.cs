using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Whether an instance is still live (port; engine spec §5.1, §12; engine P6 unit (d)): what a claim's holder is judged
/// by, for 'effect.claim.ref' ('claimExists' only while the holder is live) and the '--attempt next' check (SND-WF-012).
/// </summary>
public interface IInstanceLiveness
{
    /// <summary>Whether <paramref name="address"/> runs: started and not yet ended.</summary>
    /// <param name="address">The instance.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True while it runs; false when it ended or was never started; SND-API-001 when the runtime doesn't answer.</returns>
    public ValueTask<Result<bool>> IsLive(InstanceAddress address, CancellationToken cancellationToken);
}
