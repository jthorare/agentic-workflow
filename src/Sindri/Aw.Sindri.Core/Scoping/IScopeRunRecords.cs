using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Scoping;

/// <summary>The agent runs of a scoping instance as the ledger records them, so the model checks (SCP-CHAL-002, SCP-GAP-002) compare what ran, not what a session says.</summary>
public interface IScopeRunRecords
{
    /// <summary>Reads the instance's agent runs.</summary>
    /// <param name="run">The instance's address.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The runs, oldest first (none when the instance has launched none), or the error.</returns>
    public ValueTask<Result<IReadOnlyList<ScopeRunRecord>>> Of(InstanceAddress run, CancellationToken cancellationToken);
}
