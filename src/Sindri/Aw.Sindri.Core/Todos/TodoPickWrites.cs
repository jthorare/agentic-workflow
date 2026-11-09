using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Todos;

/// <summary>What the four pick slices share (todo spec §4.4): the caller and lease, the todo's row, the append that turns a lost version key into SND-TODO-409, and the unique key of an event no version key covers.</summary>
/// <param name="ledger">Appends the events.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entries.</param>
/// <param name="callers">The caller.</param>
/// <param name="clock">Sindri's clock.</param>
/// <param name="ids">Makes the keys unique.</param>
/// <param name="views">The todos.</param>
public sealed class TodoPickWrites(ILedgerStore ledger, HeldLease held, Events.EventFactory events, ICallerContextAccessor callers, TimeProvider clock, IIdGenerator ids, IViewReader views)
{
    /// <summary>The caller and the lease, or why there is none.</summary>
    /// <param name="action">The write, for the message.</param>
    /// <param name="piped">Whether the request came with fence=1: the events record 'via: pipe'.</param>
    /// <returns>The caller, SND-AUTH-002 or not running.</returns>
    public Result<TodoPickCaller> Begin(string action, bool piped)
    {
        var begun = TodoWrite.Begin(callers, held, action);
        return begun.IsError ? begun.Error : new TodoPickCaller(begun.Value.Caller, begun.Value.Grant, clock.GetUtcNow()) { Via = TodoWrite.Via(piped) };
    }

    /// <summary>The todo's row.</summary>
    /// <param name="id">The todo.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row, or SND-TODO-404 for none.</returns>
    public async ValueTask<Result<TodoRow>> Row(TodoId id, CancellationToken cancellationToken)
    {
        var read = await views.Get<TodoRow>(ViewNames.Todo, id.Value, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        return read.Value is { } row ? row : SindriErrors.TodoNotFound.With($"No todo {id.Value}.");
    }

    /// <summary>Appends one event.</summary>
    /// <typeparam name="TPayload">The payload.</typeparam>
    /// <param name="who">The caller.</param>
    /// <param name="type">The event type.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="key">The idempotency key.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The command and its sequence, or SND-TODO-409 when the key was already written.</returns>
    public ValueTask<Result<CommandAccepted>> Append<TPayload>(TodoPickCaller who, string type, TPayload payload, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(who);
        return TodoWrite.Append(ledger, who.Grant, events.Entry(type, payload, key), who.Caller.CommandId, cancellationToken);
    }

    /// <summary>Puts a todo back to <paramref name="prior"/> (an Undo, or a restore after a failed apply): a todo that moved since the change, or is gone, is left as it is.</summary>
    /// <param name="converger">Appends the reverse events.</param>
    /// <param name="prior">The todo as it was before the change.</param>
    /// <param name="versionAfter">The version the change left it at.</param>
    /// <param name="who">The caller.</param>
    /// <param name="cancellationToken">Cancels the read and the appends.</param>
    /// <returns>The first error, or null.</returns>
    public async ValueTask<Error?> Revert(TodoConverger converger, TodoRow prior, long versionAfter, TodoPickCaller who, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(converger);
        ArgumentNullException.ThrowIfNull(prior);
        ArgumentNullException.ThrowIfNull(who);
        var read = await Row(prior.Id, cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value.Version != versionAfter)
        {
            return null;
        }

        var back = await converger.Apply(read.Value, TodoNoteSides.Of(prior), who.By, who, cancellationToken, prior.DoneAt).ConfigureAwait(false);
        return back.IsError ? back.Error : null;
    }

    /// <summary>A key no other pick shares: <paramref name="prefix"/> and a fresh id.</summary>
    /// <param name="prefix">The key's fixed part.</param>
    /// <returns>The key.</returns>
    public string Key(string prefix) => $"{prefix}:{ids.NewId():N}";
}
