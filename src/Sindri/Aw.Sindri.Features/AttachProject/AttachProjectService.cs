using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.AttachProject;

/// <summary>{'Attach': 'Attaches a Linear project or adds a hand-made one', 'Change': 'Changes an attached or hand-made project', 'Detach': 'Removes an attached or hand-made project', 'Hide': 'Hides or unhides a Linear project the owner leads'} (projects page spec §5.3; desk).</summary>
internal sealed class AttachProjectService(IViewReader views, ILedgerStore ledger, HeldLease held, EventFactory events, ITextScrubber scrubber, ICallerContextAccessor callers, IIdGenerator ids)
    : IService<AttachProjectRequest, ProjectAccepted>
{
    public ValueTask<Result<ProjectAccepted>> Handle(AttachProjectRequest request, CancellationToken cancellationToken)
    {
        _ = (views, ledger, held, events, scrubber, callers, ids);
        throw new NotSupportedException();
    }
}
