using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Todos;

/// <summary>Folds the nine todo events into the 'todo' view (todo spec §4.2): a row per todo, a pure fold a 'views rebuild' reproduces.</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class TodosProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [];

    public bool Handles(string eventType) =>
        eventType.StartsWith("todo.", StringComparison.Ordinal) && !eventType.StartsWith("todo.note.", StringComparison.Ordinal);

    public ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"TodosProjection ({events.GetType().Name}): make TodosProjectionTests pass");
}
