using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListReviews;

/// <summary>
/// The Reviews list (PR review page spec §6.1): rows, groups, the waiting count and whether a PR claims a doc come from the stored PR records (<see cref="IReviewFacts"/>); a row's CI and open required
/// checks come from the per-head summary (<see cref="ReviewSummaryReader"/>). The login is the approved profile's <c language="text">review.login</c>; without one the list is the login prompt, with the forge credential's own login suggested.
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="facts">The stored PR records and the sources' state.</param>
/// <param name="projects">The project names for the sections.</param>
/// <param name="summaries">The per-head summaries.</param>
/// <param name="forge">The forge port, for the credential's login.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="clock">The clock.</param>
internal sealed class ListReviewsService(IApprovedProfile approvals, IReviewFacts facts, IReviewProjects projects, ReviewSummaryReader summaries, IPullRequests forge, IProjectorStatus status, TimeProvider clock)
    : IService<ListReviewsRequest, ListReviewsResponse>
{
    public async ValueTask<Result<ListReviewsResponse>> Handle(ListReviewsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var review = approved.Value?.Profile.Review;
        var read = await facts.Read(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var snapshot = read.Value;
        var header = new Header(snapshot.Github is { } github ? [ReviewFreshness.Row(github, clock.GetUtcNow())] : [], snapshot.Github is { } source ? ReviewFreshness.Stale(source) : null, ReviewLibrary.From(snapshot.Library));
        return review?.Login is { } login && !string.IsNullOrWhiteSpace(login) ? await Rows(login, review, snapshot, header, cancellationToken).ConfigureAwait(false) : await LoginPrompt(header, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ListReviewsResponse> LoginPrompt(Header header, CancellationToken cancellationToken)
    {
        var suggested = await forge.Self(cancellationToken).ConfigureAwait(false);
        return new ListReviewsResponse(status.Current.AsOf, Login: null, ReviewLoginPrompt.For(suggested.IsError ? null : suggested.Value), Notice: null, header.Freshness, header.Stale, header.Library, WaitingCount: 0, Groups: [], Empty: null);
    }

    private async ValueTask<ListReviewsResponse> Rows(string login, PrReviewSettings review, ReviewFactsSnapshot snapshot, Header header, CancellationToken cancellationToken)
    {
        var shown = snapshot.Pulls.Where(p => ReviewListBuilder.Classify(login, p) is not null || string.Equals(p.Author, login, StringComparison.OrdinalIgnoreCase)).ToList();
        var summary = await summaries.Read(shown, review, cancellationToken).ConfigureAwait(false);
        var names = await projects.Names(cancellationToken).ConfigureAwait(false);
        var parts = ReviewListBuilder.Build(login, snapshot.Pulls, p => summary.GetValueOrDefault((p.Repo, p.Number)), repo => ProjectOf(review, names.IsError ? null : names.Value, repo));
        return new ListReviewsResponse(status.Current.AsOf, login, LoginPrompt: null, parts.Notice, header.Freshness, header.Stale, header.Library, parts.WaitingCount, parts.Groups, parts.Empty);
    }

    // why: a repo named by a project the Projects page doesn't have reads as a repo section, since the section needs the project's name
    private static ReviewProjectName? ProjectOf(PrReviewSettings review, IReadOnlyDictionary<string, string>? names, string repo)
    {
        var entry = (review.Projects ?? []).FirstOrDefault(p => (p.Repos ?? []).Contains(repo, StringComparer.OrdinalIgnoreCase));
        return entry is not null && names is not null && names.TryGetValue(entry.Project, out var name) ? new ReviewProjectName(entry.Project, name) : null;
    }

    private sealed record Header(IReadOnlyList<ProjectFreshnessRow> Freshness, ReviewStale? Stale, ReviewLibrary Library);
}
