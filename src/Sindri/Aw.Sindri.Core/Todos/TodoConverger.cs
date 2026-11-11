using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Todos;

/// <summary>
/// Brings a todo to a stated version of itself with ordinary state events (todo spec §4.1: the kept version "follows as a normal todo.changed/done/removed", and Undo is "undone by their own reverse events"): a removal or
/// a restore first, then one 'todo.changed' for the fields that differ, then a done or a reopen. Each event moves the version by one under <see cref="TodoWrite.VersionKey"/>, so a todo another caller wrote meanwhile
/// makes the append SND-TODO-409 instead of overwriting it.
/// </summary>
/// <param name="ledger">Appends the events.</param>
/// <param name="events">Builds the entries.</param>
/// <param name="input">Scrubs the text a note line brings.</param>
public sealed class TodoConverger(ILedgerStore ledger, EventFactory events, TodoInput input)
{
    /// <summary>Appends the events that take <paramref name="row"/> to <paramref name="target"/>; the 'via' of each is the caller's.</summary>
    /// <param name="row">The todo as it is.</param>
    /// <param name="target">What it should be.</param>
    /// <param name="by">The 'by' of each event.</param>
    /// <param name="who">The caller, its 'via' and its lease grant.</param>
    /// <param name="cancellationToken">Cancels the appends.</param>
    /// <returns>The version the todo is at after the events, or the first error.</returns>
    public ValueTask<Result<long>> Apply(TodoRow row, TodoSide target, string by, TodoPickCaller who, CancellationToken cancellationToken)
        => Apply(row, new TodoConvergeTarget(target, by, DoneAt: null), who, cancellationToken);

    /// <summary>Appends the events that take <paramref name="row"/> to <paramref name="to"/>'s side, keeping its done time.</summary>
    /// <param name="row">The todo as it is.</param>
    /// <param name="to">What it should be, the 'by' of each event and the done time a 'todo.done' step keeps (an Undo passes the one the todo had before; null for the time of the event).</param>
    /// <param name="who">The caller, its 'via' and its lease grant.</param>
    /// <param name="cancellationToken">Cancels the appends.</param>
    /// <returns>The version the todo is at after the events, or the first error.</returns>
    public async ValueTask<Result<long>> Apply(TodoRow row, TodoConvergeTarget to, TodoPickCaller who, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(who);
        var (target, by, doneAt) = to;
        var planned = Steps(row, target, doneAt);
        if (planned.IsError)
        {
            return planned.Error;
        }

        var version = row.Version;
        foreach (var step in planned.Value)
        {
            version++;
            var appended = await TodoWrite.Append(ledger, who.Grant, step(version, by, who.Via), who.Caller.CommandId, cancellationToken).ConfigureAwait(false);
            if (appended.IsError)
            {
                return appended.Error;
            }
        }

        return version;
    }

    /// <summary>The row as the events of <see cref="Apply(TodoRow, TodoConvergeTarget, TodoPickCaller, CancellationToken)"/> leave it, for an answer that can't wait for the views.</summary>
    /// <param name="row">The todo before.</param>
    /// <param name="target">What it was taken to.</param>
    /// <param name="version">The version after the events.</param>
    /// <param name="now">The time of the events.</param>
    /// <param name="by">Who wrote them.</param>
    /// <returns>The row.</returns>
    public static TodoRow After(TodoRow row, TodoSide target, long version, DateTimeOffset now, string by)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(target);
        if (version == row.Version)
        {
            return row;
        }

        var moved = row with { Version = version, LastBy = by, LastVia = null };
        return target.Removed
            ? moved with { Removed = true, RemovedAt = now, RemovedBy = by }
            : moved with { Removed = false, RemovedAt = null, RemovedBy = null, Text = target.Text ?? row.Text, Due = target.Due, Links = target.Links, DoneAt = target.Done ? row.DoneAt ?? now : null };
    }

    private Result<IReadOnlyList<Func<long, string, string?, LedgerEntry>>> Steps(TodoRow row, TodoSide target, DateTimeOffset? doneAt)
    {
        var steps = new List<Func<long, string, string?, LedgerEntry>>();
        if (target.Removed)
        {
            steps.AddRange(row.Removed ? [] : [(v, by, via) => Entry(row, EventTypes.TodoRemoved, v, new TodoRemoved(row.Id, v, by, via))]);
            return steps;
        }

        steps.AddRange(row.Removed ? [(v, by, via) => Entry(row, EventTypes.TodoRestored, v, new TodoRestored(row.Id, v, by, via))] : []);
        var fields = Fields(row, target);
        if (fields.IsError)
        {
            return fields.Error;
        }

        steps.AddRange(fields.Value);
        steps.AddRange(Done(row, target, doneAt));
        return steps;
    }

    private Result<IReadOnlyList<Func<long, string, string?, LedgerEntry>>> Fields(TodoRow row, TodoSide target)
    {
        string? text = null;
        if (target.Text is { } wanted && !string.Equals(wanted, row.Text, StringComparison.Ordinal))
        {
            var clean = input.Text(wanted);
            if (clean.IsError)
            {
                return clean.Error;
            }

            text = clean.Value;
        }

        var dueChanged = target.Due != row.Due;
        var linksChanged = !target.Links.SequenceEqual(row.Links, StringComparer.Ordinal);
        LedgerEntry Change(long v, string by, string? via) => Entry(
            row,
            EventTypes.TodoChanged,
            v,
            new TodoChanged(row.Id, v, text, dueChanged ? target.Due : null, ClearDue: dueChanged && target.Due is null, linksChanged ? target.Links : null, by, via));

        return text is not null || dueChanged || linksChanged ? new List<Func<long, string, string?, LedgerEntry>> { Change } : [];
    }

    private IEnumerable<Func<long, string, string?, LedgerEntry>> Done(TodoRow row, TodoSide target, DateTimeOffset? doneAt)
    {
        if (target.Done && row.DoneAt is null)
        {
            yield return (v, by, via) => Entry(row, EventTypes.TodoDone, v, new TodoDone(row.Id, v, by, via, doneAt));
        }
        else if (!target.Done && row.DoneAt is not null)
        {
            yield return (v, by, via) => Entry(row, EventTypes.TodoReopened, v, new TodoReopened(row.Id, v, by, via));
        }
    }

    private LedgerEntry Entry<TPayload>(TodoRow row, string type, long version, TPayload payload) => events.Entry(type, payload, TodoWrite.VersionKey(row.Id, version));
}
