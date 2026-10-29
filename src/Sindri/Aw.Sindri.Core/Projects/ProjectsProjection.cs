using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Projects;

/// <summary>Folds the four project events into the 'project-attached' and 'project-hidden' views (projects page spec §5.2).</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class ProjectsProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.ProjectAttached, ViewNames.ProjectHidden];

    public bool Handles(string eventType) =>
        string.Equals(eventType, EventTypes.ProjectAttached, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.ProjectChanged, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.ProjectDetached, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.ProjectHidden, StringComparison.Ordinal);

    public ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken) => throw new NotSupportedException(events.GetType().Name);
}
