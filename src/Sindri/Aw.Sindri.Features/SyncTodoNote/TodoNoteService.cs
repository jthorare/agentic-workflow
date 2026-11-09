using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>
/// The 'todo-note' built-in service (todo spec §4.5 "Reading"): it runs without a profile entry and its 24 h cadence is only a backstop, because the watch host does the real polling. Its use is
/// 'sindri services run todo-note', which forces a pass now. A pass with no 'todo.obsidian' key does nothing.
/// </summary>
/// <param name="sync">The note sync.</param>
internal sealed class TodoNoteService(ITodoNoteSync sync) : IScheduledService
{
    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Syncs the Obsidian todo note with your todos.";

    /// <summary>Gets the service's name in the schedule ('todo-note'); its actor is 'service:todo-note'.</summary>
    internal static ServiceName ServiceId { get; } = ServiceName.From("todo-note");

    /// <summary>Gets the daily backstop the scheduler uses without a profile entry.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromHours(24)), Description);

    /// <inheritdoc/>
    public ServiceName Identity() => ServiceId;

    /// <inheritdoc/>
    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        _ = await sync.Pass(cancellationToken).ConfigureAwait(false);   // a failure is in the sync's status, never thrown
        return ScheduledResult.Quiet;
    }
}
