using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>Which item and repo each launch belongs to, read from the ledger (a denial counter is keyed by the launch only).</summary>
public interface ILaunchOwners
{
    /// <summary>Reads the owners of every launch that was recorded.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The owner by launch id; a launch the ledger never saw has none.</returns>
    public ValueTask<IReadOnlyDictionary<LaunchId, LaunchOwner>> Read(CancellationToken cancellationToken);
}
