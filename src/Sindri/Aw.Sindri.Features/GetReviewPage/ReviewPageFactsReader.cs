using System.Globalization;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetReviewPage;

/// <summary>Puts the forge's reads, the stored side, the evidence on disk, the credential's own login and the settings the PR got into the one set of facts the page is built from (spec §6.2, §7.2).</summary>
/// <param name="forge">The forge port, for the credential's login.</param>
/// <param name="evidence">The evidence on disk.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="clock">The clock.</param>
internal sealed class ReviewPageFactsReader(IPullRequests forge, IReviewEvidence evidence, IProjectorStatus status, TimeProvider clock)
{
    internal async ValueTask<ReviewPageFacts> Read(ReviewPageAsk ask, ReviewPageReads reads, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);
        ArgumentNullException.ThrowIfNull(reads);
        var self = await forge.Self(cancellationToken).ConfigureAwait(false);
        var found = await evidence.Find(ask.Pull, reads.View.HeadSha, cancellationToken).ConfigureAwait(false);
        var slug = ask.Pull.Owner + "/" + ask.Pull.Repo;
        return new ReviewPageFacts(
            status.Current.AsOf, slug, ask.Pull.Number, ask.Login, self.IsError ? null : self.Value, Header(ask, reads.View), Settings(ask.Review, slug), reads.Files, reads.Threads, Runs(reads.View), ask.Stored.Library,
            found.IsSuccess ? new ReviewPageRead<IReadOnlyList<ReviewPageEvidenceItem>>(found.Value) : new ReviewPageRead<IReadOnlyList<ReviewPageEvidenceItem>>(Value: [], found.Error.Message),
            new HashSet<string>(StringComparer.Ordinal), reads.Moving);
    }

    private ReviewPagePrFacts Header(ReviewPageAsk ask, PullView view) =>
        new(
            view.Title.Length > 0 ? view.Title : ask.Stored.Title ?? string.Empty, view.Author.Length > 0 ? view.Author : ask.Stored.Author ?? string.Empty, view.Draft || ask.Stored.Draft, State(view.State),
            view.HeadSha, view.HeadRef, view.Base, ask.Link, view.Body, ask.Stored.IssueKeys, ask.Stored.UpdatedAt ?? clock.GetUtcNow());

    private static ReviewPageRead<IReadOnlyList<PullCheckRun>> Runs(PullView view) =>
        view.Checks.Runs is { } list
            ? new ReviewPageRead<IReadOnlyList<PullCheckRun>>(list)
            : new ReviewPageRead<IReadOnlyList<PullCheckRun>>(Value: [], "github sent no check runs for " + (view.HeadSha.Length > 7 ? view.HeadSha[..7] : view.HeadSha));

    private static string State(PullState state) => state switch
    {
        PullState.Merged => "merged",
        PullState.Closed => "closed",
        PullState.Open => "open",
        _ => "open",
    };

    // why: the built-in 'default' always resolves
    private static PrReviewResolved BuiltIn(PrReviewSettings review) => PrReviewDefaults.Resolve(review, PrReviewDefaults.DefaultName)!;

    // why: two project entries naming one repo tie (§7.2): the page uses the defaults and offers the entries' settings to pick from
    private static ReviewPageSettingsFacts Settings(PrReviewSettings review, string repo)
    {
        var pick = PrReviewDefaults.For(review, repo);
        var named = (review.Projects ?? []).Select((p, i) => (Entry: p, Index: i)).Where(x => (x.Entry.Repos ?? []).Contains(repo, StringComparer.OrdinalIgnoreCase)).ToList();
        if (pick.Settings is { } resolved && named.Count == 1)
        {
            return new ReviewPageSettingsFacts(resolved, resolved.Name, string.Create(CultureInfo.InvariantCulture, $"review.projects[{named[0].Index}]"), Choices: []);
        }

        var choices = pick.Choices.Select(static c => c.Settings ?? PrReviewDefaults.DefaultName).Distinct(StringComparer.Ordinal).ToList();
        return new ReviewPageSettingsFacts(pick.Settings ?? BuiltIn(review), pick.Settings?.Name ?? PrReviewDefaults.DefaultName, "default", choices);
    }
}
