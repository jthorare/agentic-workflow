using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>The denial counters of the launches whose run area still exists (sandbox spec §3.6): the true counts the API reads at render.</summary>
public interface IDenialFiles
{
    /// <summary>Reads the counters.</summary>
    /// <param name="launch">Only this launch, or null for every launch with a counter.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One file per launch, newest first. A launch whose run area is gone, or whose file can't be read, has none.</returns>
    public ValueTask<IReadOnlyList<DenialFile>> List(LaunchId? launch, CancellationToken cancellationToken);
}
