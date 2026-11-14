using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListReviews;

/// <summary>
/// The Reviews list (PR review page spec §6.1): rows, groups, the waiting count and whether a PR claims a doc come from the stored PR records (<see cref="IReviewFacts"/>); a row's CI and open required
/// checks come from the per-head summary (<see cref="ReviewSummaryReader"/>). The login is the approved profile's <c language="text">review.login</c> when set, else the GitHub connection's login; with neither the list is the login prompt. With no usable GitHub connection the list is one connect action.
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="facts">The stored PR records and the sources' state.</param>
/// <param name="projects">The project names for the sections.</param>
/// <param name="summaries">The per-head summaries.</param>
/// /// <param name="account">The GitHub connection: its state, /user login and the connect action.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="clock">The clock.</param>
internal sealed class ListReviewsService(IApprovedProfile approvals, IReviewFacts facts, IReviewProjects projects, ReviewSummaryReader summaries, ReviewAccountReader account, IProjectorStatus status, TimeProvider clock)
    : IService<ListReviewsRequest, ListReviewsResponse>
{
    public async ValueTask<Result<ListReviewsResponse>> Handle(ListReviewsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var approved = await approvals.ForRead(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var read = await facts.Read(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        return await Respond(approved.Value?.Profile, read.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<ListReviewsResponse>> Respond(SindriProfile? profile, ReviewFactsSnapshot snapshot, CancellationToken cancellationToken)
    {
        var header = Header.Of(snapshot, clock.GetUtcNow());
        var who = await account.Identity(profile, snapshot.Github?.LastCode, cancellationToken).ConfigureAwait(false);
        if (who.IsError)
        {
            return who.Error;
        }

        if (who.Value.Connect is { } connect)
        {
            return new ListReviewsResponse(status.Current.AsOf, Login: null, LoginPrompt: null, Notice: null, header.Freshness, Stale: null, header.Library, WaitingCount: 0, Groups: [], Empty: null, connect);
        }

        return who.Value.Login is { } login && !string.IsNullOrWhiteSpace(login)
            ? await Rows(login, profile?.Review, snapshot, header, cancellationToken).ConfigureAwait(false)
            : await LoginPrompt(header, who.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ListReviewsResponse> LoginPrompt(Header header, ReviewIdentity who, CancellationToken cancellationToken)
    {
        var suggested = await account.Suggested(cancellationToken).ConfigureAwait(false);
        return new ListReviewsResponse(status.Current.AsOf, Login: null, ReviewLoginPrompt.For(suggested, who.Connection, who.Miss), Notice: null, header.Freshness, header.Stale, header.Library, WaitingCount: 0, Groups: [], Empty: null, Connect: null);
    }

    private async ValueTask<ListReviewsResponse> Rows(string login, PrReviewSettings? review, ReviewFactsSnapshot snapshot, Header header, CancellationToken cancellationToken)
    {
        var shown = snapshot.Pulls.Where(p => ReviewListBuilder.Classify(login, p) is not null || string.Equals(p.Author, login, StringComparison.OrdinalIgnoreCase)).ToList();
        var summary = await summaries.Read(shown, review, cancellationToken).ConfigureAwait(false);
        var names = await projects.Names(cancellationToken).ConfigureAwait(false);
        var parts = ReviewListBuilder.Build(login, snapshot.Pulls, p => summary.GetValueOrDefault((p.Repo, p.Number)), repo => ProjectOf(review, names.IsError ? null : names.Value, repo));
        return new ListReviewsResponse(status.Current.AsOf, login, LoginPrompt: null, parts.Notice, header.Freshness, header.Stale, header.Library, parts.WaitingCount, parts.Groups, parts.Empty, Connect: null);
    }

    // why: a repo named by a project the Projects page doesn't have reads as a repo section, since the section needs the project's name
    private static ReviewProjectName? ProjectOf(PrReviewSettings? review, IReadOnlyDictionary<string, string>? names, string repo)
    {
        var entry = (review?.Projects ?? []).FirstOrDefault(p => (p.Repos ?? []).Contains(repo, StringComparer.OrdinalIgnoreCase));
        return entry is not null && names is not null && names.TryGetValue(entry.Project, out var name) ? new ReviewProjectName(entry.Project, name) : null;
    }

    private sealed record Header(IReadOnlyList<ProjectFreshnessRow> Freshness, ReviewStale? Stale, ReviewLibrary Library)
    {
        internal static Header Of(ReviewFactsSnapshot snapshot, DateTimeOffset now) =>
            new(snapshot.Github is { } github ? [ReviewFreshness.Row(github, now)] : [], snapshot.Github is { } source ? ReviewFreshness.Stale(source) : null, ReviewLibrary.From(snapshot.Library));
    }
}
