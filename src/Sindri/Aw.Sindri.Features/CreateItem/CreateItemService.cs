using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.CreateItem;

/// <summary>
/// Creates an item (API spec §3.2; desk): a new id, then 'item.created' keyed by that id and about it. The repo is the body's, else the
/// caller's X-Sindri-Repo, and must be a repo of the approved profile (G9) or the call is SND-CLI-003 and records nothing.
/// </summary>
/// <param name="approvals">The approved profile, whose repos a new item may name.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="ledger">Appends 'item.created'.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="ids">The id source for the item.</param>
/// <param name="callers">The caller: the command id and the header repo.</param>
internal sealed class CreateItemService(IApprovedProfile approvals, HeldLease held, ILedgerStore ledger, EventFactory events, IIdGenerator ids, ICallerContextAccessor callers)
    : IService<CreateItemRequest, ItemCreatedResponse>
{
    public async ValueTask<Result<ItemCreatedResponse>> Handle(CreateItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("CreateItem needs a caller");
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var repo = request.Repo ?? caller.Repo;
        if (repo is { } named && await Unknown(named, fromHeader: request.Repo is null, cancellationToken).ConfigureAwait(false) is { } unknown)
        {
            return unknown;
        }

        var item = ItemId.New(ids);
        var created = new ItemCreated(caller.CommandId, item, request.Title.Trim(), repo, request.Link, request.Body, Owner: null);
        var entry = events.Entry(EventTypes.ItemCreated, created, $"{EventTypes.ItemCreated}:{item.Value}", item.Value);
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new ItemCreatedResponse(new CommandAccepted(caller.CommandId, appended.Value.Sequence), item);
    }

    /// <summary>The refusal when <paramref name="repo"/> isn't a repo of the approved profile; null when it is. A header repo is the checkout the human ran from, so its message says that.</summary>
    private async ValueTask<Error?> Unknown(RepoName repo, bool fromHeader, CancellationToken cancellationToken)
    {
        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var known = latest.Value?.Profile.Repos?.Any(r => string.Equals(r.Name, repo.Value, StringComparison.Ordinal)) ?? false;
        return known ? null
            : fromHeader ? SindriErrors.InvalidInput.With($"this checkout, {repo.Value}, isn't a repo of the approved profile (fix: pass --repo <name>, or add it to the profile and approve it)")
            : SindriErrors.InvalidInput.With($"'{repo.Value}' isn't a repo of the approved profile (fix: sindri profile approve, or name one of its repos)");
    }
}
