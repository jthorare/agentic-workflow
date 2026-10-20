using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>Where the instance read gets an instance's workflow events, in ledger order, to fold into its Step runs.</summary>
internal interface IStepRunSource
{
    /// <summary>The workflow events <paramref name="address"/> mirrored into the ledger.</summary>
    /// <param name="address">The instance.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The events, or the ledger's error.</returns>
    public ValueTask<Result<IReadOnlyList<RecordedWorkflowEvent>>> EventsOf(InstanceAddress address, CancellationToken cancellationToken);
}
