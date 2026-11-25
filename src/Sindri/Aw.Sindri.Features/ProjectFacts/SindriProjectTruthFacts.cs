using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Projects.TruthSources;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ProjectFacts;

/// <summary>
/// <see cref="IProjectTruthFacts"/> over Sindri's own data, one read for the whole page: the stored open PR records (<see cref="IReviewFacts"/>), the newest stored CI summary of each PR in scope
/// (<see cref="IReviewSummaries"/>, no forge call), the 'items' and 'item-links' views and the collector's live sessions. A session's repo is the profile's name for it, so it is matched to the
/// "owner/name" of the sources by the name part. The owner's GitHub login is read once per page the way the Reviews read reads it (<see cref="ReviewConnectRules"/>): the profile's
/// 'review.login', else the GitHub connection's login, and none while the connection needs connecting.
/// </summary>
/// <param name="reviews">The open PR records.</param>
/// <param name="summaries">The per-head CI summaries.</param>
/// <param name="views">The 'items' and 'item-links' views.</param>
/// <param name="sessions">The collector's snapshot.</param>
/// <param name="approvals">The approved profile, for 'review.login'.</param>
/// <param name="account">The GitHub connection.</param>
internal sealed class SindriProjectTruthFacts(IReviewFacts reviews, IReviewSummaries summaries, IViewReader views, ISessionSnapshot sessions, IApprovedProfile approvals, IGitHubAccount account) : IProjectTruthFacts
{
    public async ValueTask<Result<ProjectTruthFacts>> Read(ProjectSources union, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(union);
        var read = await reviews.Read(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var inScope = read.Value.Pulls.Where(p => union.Repos.Contains(p.Repo, StringComparer.OrdinalIgnoreCase) || (p.Url is { } url && union.PullUrls.Contains(url.TrimEnd('/'), StringComparer.OrdinalIgnoreCase))).ToList();
        var found = new Dictionary<string, ReviewSummary>(StringComparer.Ordinal);
        foreach (var pull in inScope)
        {
            if (ReviewPullRef.TryParse(pull.Repo, pull.Number) is not { } reference)
            {
                continue;
            }

            var newest = await summaries.Newest(reference, cancellationToken).ConfigureAwait(false);
            if (newest is { IsSuccess: true, Value: { } summary })
            {
                found[string.Create(CultureInfo.InvariantCulture, $"{pull.Repo}#{pull.Number}")] = summary;
            }
        }

        var items = await Items(cancellationToken).ConfigureAwait(false);
        return items.IsError
            ? items.Error
            : new ProjectTruthFacts(inScope, found, items.Value, Agents(union), inScope.Count == 0 ? null : await Owner(read.Value.Github?.LastCode, cancellationToken).ConfigureAwait(false));
    }

    // why: a login Sindri can't read only turns "blocked on you" off (the page says why); it never fails the truth read
    private async ValueTask<string?> Owner(string? lastCode, CancellationToken cancellationToken)
    {
        var approved = await approvals.ForRead(cancellationToken).ConfigureAwait(false);
        var profile = approved.IsError ? null : approved.Value?.Profile;
        var current = await account.Current(cancellationToken).ConfigureAwait(false);
        if (current.IsError || ReviewConnectRules.For(current.Value, ReviewConnectRules.HasProfileToken(profile?.Context), lastCode) is not null)
        {
            return null;
        }

        var typed = profile?.Review?.Login;
        var login = string.IsNullOrWhiteSpace(typed) ? current.Value.Login : typed;
        return string.IsNullOrWhiteSpace(login) ? null : login;
    }

    private async ValueTask<Result<IReadOnlyList<ProjectTruthItem>>> Items(CancellationToken cancellationToken)
    {
        var rows = await views.List<ItemRow>(ViewNames.Items, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var links = await views.List<ItemLinkRow>(ViewNames.ItemLinks, cancellationToken).ConfigureAwait(false);
        if (links.IsError)
        {
            return links.Error;
        }

        var byItem = links.Value.Where(static l => l.Kind == ItemLinkKind.Pr && l.Url is not null).ToLookup(static l => l.Item, static l => l.Url ?? string.Empty);
        IReadOnlyList<ProjectTruthItem> items = [.. rows.Value
            .Where(static r => r.State != ItemState.Cancelled)
            .Select(r => new ProjectTruthItem(r.Repo?.Value, r.State == ItemState.Done, [.. byItem[r.Id]]))
            .Where(static i => i.LinkUrls.Count > 0)];
        return Results.Success(items);
    }

    private IReadOnlyList<ProjectTruthAgent> Agents(ProjectSources union) =>
        sessions.Current is not { } current
            ? []
            : [.. current.Sessions.Select(s => new ProjectTruthAgent(
                s.Repo is { } repo ? union.Repos.FirstOrDefault(r => string.Equals(r.Split('/')[^1], repo.Value, StringComparison.OrdinalIgnoreCase)) : null,
                s.State is SessionState.Running or SessionState.Asking))];
}
