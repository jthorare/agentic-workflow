using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.CreateItem;

/// <summary>
/// One run's part of the repair pass of issue 2588: reads the 'itemId' of each succeeded node run and writes what a 'create_item' node could not. Per item it appends the run's
/// 'item.linked' row under the key the node uses, so a repeat is a no-op and a link that is there is never written twice. Per run, when the run has no item, it sets the item made first
/// (the lowest 'item.created' ledger sequence), the one the node's own first-wins write would have set. A run that has an item keeps it: it was shown already.
/// A crash after the item was made and before the node output was recorded leaves an item this pass cannot see (it reads node outputs only).
/// </summary>
/// <param name="runs">The runs and their node outputs.</param>
/// <param name="ledger">Appends the link rows and orders the made items.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entries.</param>
/// <param name="callers">Enters 'service:workflow', the actor of the live path.</param>
/// <param name="ids">The command id of a link row.</param>
internal sealed class RunItemLinker(IWorkflowRunStore runs, ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers, IIdGenerator ids)
{
    private const int PageSize = 500;

    /// <summary>One run's pass.</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many writes landed and the first failure, if any (the writes that landed stay).</returns>
    internal async ValueTask<(int Writes, Error? Failure)> Repair(WorkflowRunId run, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return (0, SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)"));
        }

        var rows = await runs.NodeRuns(run, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return (0, rows.Error);
        }

        var made = rows.Value.Where(static r => r.Status == NodeRunStatus.Succeeded).Select(ItemOf).OfType<ItemId>().Distinct().ToList();
        if (made.Count == 0)
        {
            return (0, null);
        }

        using var entered = callers.EnterService("workflow");
        var (writes, failure) = await LinkAll(run, made, grant, cancellationToken).ConfigureAwait(false);
        var set = await SetRunItem(run, made, cancellationToken).ConfigureAwait(false);
        return set.IsError ? (writes, failure ?? set.Error) : (writes + set.Value, failure);
    }

    /// <summary>Appends the run's link row for each item; a row already there counts as no write. The first failure is kept and the others still run.</summary>
    private async ValueTask<(int Writes, Error? Failure)> LinkAll(WorkflowRunId run, List<ItemId> made, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var record = RecordKey.From("sindri:run/" + run.Value);
        var writes = 0;
        Error? failure = null;
        foreach (var item in made)
        {
            var link = new ItemLinked(ids.New<CommandId>(), item, record, Alias: null);
            var appended = await ledger.Append(events.Entry(EventTypes.ItemLinked, link, $"{EventTypes.ItemLinked}:{item.Value}:{record.Value}", item.Value), grant, cancellationToken).ConfigureAwait(false);
            if (appended.IsError)
            {
                failure ??= appended.Error;
            }
            else if (!appended.Value.AlreadyRecorded)
            {
                writes++;
            }
        }

        return (writes, failure);
    }

    private async ValueTask<Result<int>> SetRunItem(WorkflowRunId run, List<ItemId> made, CancellationToken cancellationToken)
    {
        var current = await runs.Get(run, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        if (current.Value.ItemId is not null)
        {
            return 0;
        }

        var first = made.Count == 1 ? (Result<ItemId>)made[0] : await FirstMade(made, cancellationToken).ConfigureAwait(false);
        if (first.IsError)
        {
            return first.Error;
        }

        var set = await runs.SetItem(run, first.Value, cancellationToken).ConfigureAwait(false);
        return set.IsError ? set.Error : 1;
    }

    /// <summary>The item of <paramref name="made"/> with the lowest 'item.created' sequence; an item the ledger has no row for sorts after the others, by id.</summary>
    private async ValueTask<Result<ItemId>> FirstMade(List<ItemId> made, CancellationToken cancellationToken)
    {
        var wanted = made.Select(static i => i.Value).ToHashSet(StringComparer.Ordinal);
        var sequences = new Dictionary<string, long>(StringComparer.Ordinal);
        var after = 0L;
        while (sequences.Count < wanted.Count)
        {
            var page = await ledger.ReadAfter(after, PageSize, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            if (page.Value.Count == 0)
            {
                break;
            }

            foreach (var recorded in page.Value)
            {
                if (recorded.Envelope.Type == EventTypes.ItemCreated && recorded.Item is { } item && wanted.Contains(item))
                {
                    sequences[item] = recorded.Sequence;
                }
            }

            after = page.Value[^1].Sequence;
        }

        return made.OrderBy(i => sequences.TryGetValue(i.Value, out var sequence) ? sequence : long.MaxValue).ThenBy(static i => i.Value, StringComparer.Ordinal).First();
    }

    /// <summary>The item id a node run's output holds in 'itemId', or null for a node that makes none.</summary>
    private static ItemId? ItemOf(NodeRun row)
    {
        if (row.Output is not { } output || row.Truncated)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(output) is JsonObject { } obj && obj["itemId"] is JsonValue value && value.TryGetValue<string>(out var text) && ItemId.TryFrom(text, out var item) ? item : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
