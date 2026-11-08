using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetItem;

/// <summary>Reads the projections an item's launch block needs and hands them to <see cref="LaunchOptions.For"/>, which decides.</summary>
/// <param name="launches">The item's and the repo's launches.</param>
/// <param name="targets">The last probe with budgets.</param>
/// <param name="approvals">The approved profile: launch settings and repo paths.</param>
/// <param name="catalog">The agent catalog.</param>
/// <param name="clock">Now.</param>
internal sealed class ItemLaunchSource(ILaunchViews launches, LaunchTargetsReader targets, IApprovedProfile approvals, IAgentCatalog catalog, TimeProvider clock) : IItemLaunchSource
{
    public async ValueTask<Result<ItemLaunch>> For(ItemRow item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var own = await launches.ForItem(item.Id, cancellationToken).ConfigureAwait(false);
        if (own.IsError)
        {
            return own.Error;
        }

        var lastUsed = await LastUsed(item, cancellationToken).ConfigureAwait(false);
        if (lastUsed.IsError)
        {
            return lastUsed.Error;
        }

        var probe = await targets.Read(cancellationToken).ConfigureAwait(false);
        return probe.IsError ? probe.Error : await Decide(item, own.Value, lastUsed.Value, probe.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The agent of the newest launch that started in the item's repo (any item).</summary>
    private async ValueTask<Result<AgentId?>> LastUsed(ItemRow item, CancellationToken cancellationToken)
    {
        if (item.Repo is not { } repo)
        {
            return Results.Success<AgentId?>(value: null);
        }

        var inRepo = await launches.ForRepo(repo, cancellationToken).ConfigureAwait(false);
        return inRepo.IsError ? inRepo.Error : Results.Success(inRepo.Value.FirstOrDefault(static row => row.State == LaunchState.Started)?.Target);
    }

    private async ValueTask<Result<ItemLaunch>> Decide(ItemRow item, IReadOnlyList<LaunchRow> own, AgentId? lastUsed, LaunchTargets probe, CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var profile = approved.Value?.Profile;
        return LaunchOptions.For(new LaunchOptionInputs
        {
            Item = item,
            Launches = own,
            Probe = probe,
            Settings = profile?.Launch,
            LastUsedInRepo = lastUsed,
            RepoPath = item.Repo is { } named ? profile?.Repos?.FirstOrDefault(r => string.Equals(r.Name, named.Value, StringComparison.Ordinal))?.Path : null,
            InCatalog = id => catalog.Resolve(id) is not null,
            Now = clock.GetUtcNow(),
        });
    }
}
