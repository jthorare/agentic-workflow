using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.DetachProject;

/// <summary>{'Attach': 'Attaches a Linear project or adds a hand-made one', 'Change': 'Changes an attached or hand-made project', 'Detach': 'Removes an attached or hand-made project', 'Hide': 'Hides or unhides a Linear project the owner leads'} (projects page spec §5.3; desk).</summary>
internal sealed class DetachProjectService(IViewReader views, ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers)
    : IService<DetachProjectRequest, ProjectAccepted>
{
    public ValueTask<Result<ProjectAccepted>> Handle(DetachProjectRequest request, CancellationToken cancellationToken)
    {
        _ = (views, ledger, held, events, callers);
        throw new NotSupportedException();
    }
}
