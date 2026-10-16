using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.FileRequest;

/// <summary>What a request is filed about (API spec §3.2): the item it names, if open and visible to the caller, the repo that follows from it, and the caps a session's request meets.</summary>
/// <param name="items">Resolves the item by id or alias, in the caller's repo scope.</param>
/// <param name="views">The 'requests' view, for the open ones.</param>
/// <param name="approvals">The approved profile, whose repos a repo-less request's header repo must be one of.</param>
internal sealed class RequestTarget(IItemResolver items, IViewReader views, IApprovedProfile approvals)
{
    /// <summary>Settles the item and repo of <paramref name="request"/> for <paramref name="caller"/>.</summary>
    /// <param name="request">The request.</param>
    /// <param name="caller">Who files it.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The item (or null) and the repo (the item's, else the caller's header; or null); SND-ITEM-404 for an item the caller can't see, SND-ITEM-409 for a closed one, SND-CLI-003 when the header repo of a request about no item isn't a repo of the approved profile, SND-REQ-002 over the caps (a session caller only).</returns>
    public async ValueTask<Result<(ItemId? Item, RepoName? Repo)>> Resolve(FileRequestRequest request, CallerContext caller, CancellationToken cancellationToken)
    {
        ItemId? item = null;
        var repo = caller.Repo;
        if (request.Item is { } reference)
        {
            var resolved = await items.Resolve(reference, caller, cancellationToken).ConfigureAwait(false);
            if (resolved.IsError)
            {
                return resolved.Error;
            }

            if (ItemStates.IsClosed(resolved.Value.State))
            {
                return SindriErrors.ItemConflict.With($"{resolved.Value.Id.Value} is already {ItemStateNames.Of(resolved.Value.State)}");
            }

            (item, repo) = (resolved.Value.Id, resolved.Value.Repo);
        }
        else if (repo is { } named && await Unknown(named, cancellationToken).ConfigureAwait(false) is { } unknown)
        {
            return unknown;
        }

        if (caller.Class == CallerClass.Session)
        {
            var capped = await Capped(repo, cancellationToken).ConfigureAwait(false);
            if (capped.IsError)
            {
                return capped.Error;
            }
        }

        return (item, repo);
    }

    /// <summary>The refusal when <paramref name="repo"/> isn't a repo of the approved profile (as CreateItem refuses one); null when it is.</summary>
    private async ValueTask<Error?> Unknown(RepoName repo, CancellationToken cancellationToken)
    {
        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var known = latest.Value?.Profile.Repos?.Any(r => string.Equals(r.Name, repo.Value, StringComparison.Ordinal)) ?? false;
        return known ? null : SindriErrors.InvalidInput.With($"'{repo.Value}' isn't a repo of the approved profile (fix: sindri profile approve, or name one of its repos)");
    }

    private async ValueTask<Result<Unit>> Capped(RepoName? repo, CancellationToken cancellationToken)
    {
        var open = await views.Where<RequestRow>(ViewNames.Requests, ViewColumn.State, "open", cancellationToken).ConfigureAwait(false);
        return open.IsError ? open.Error : RequestRules.Cap(repo is { } named ? open.Value.Count(r => r.Repo == named) : 0, open.Value.Count);
    }
}
