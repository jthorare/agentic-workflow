using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The item and repo of every launch, read from the ledger (sandbox spec §3.6) and never from views.db, whose projector may lag: 'launch.started' names the item, 'item.created' the item's repo.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads a payload.</param>
internal sealed class LedgerLaunchOwners(ILedgerReader ledger, EventFactory events) : ILaunchOwners
{
    public async ValueTask<IReadOnlyDictionary<LaunchId, LaunchOwner>> Read(CancellationToken cancellationToken)
    {
        var created = await LedgerRows.Of(ledger, EventTypes.ItemCreated, cancellationToken).ConfigureAwait(false);
        var started = await LedgerRows.Of(ledger, EventTypes.LaunchStarted, cancellationToken).ConfigureAwait(false);
        if (created.IsError || started.IsError)
        {
            return new Dictionary<LaunchId, LaunchOwner>();   // an unreadable ledger names no owner: the list then shows no repo and Allow refuses, never guesses
        }

        var repos = new Dictionary<string, RepoName?>(StringComparer.Ordinal);
        foreach (var payload in created.Value.Select(events.Payload<ItemCreated>).Where(static p => p.IsSuccess))
        {
            repos[payload.Value.Item.Value] = payload.Value.Repo;
        }

        var owners = new Dictionary<LaunchId, LaunchOwner>();
        foreach (var payload in started.Value.Select(events.Payload<LaunchStarted>).Where(static p => p.IsSuccess))
        {
            owners[payload.Value.Launch] = new LaunchOwner(payload.Value.Item, repos.GetValueOrDefault(payload.Value.Item.Value));
        }

        return owners;
    }
}
