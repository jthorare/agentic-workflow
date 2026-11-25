using System.Globalization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The Reviews list's rules, pure (PR review page spec §6.1, §5.6): who waits on the owner, which rows show, and how they group.</summary>
public static class ReviewListBuilder
{
    /// <summary>The line above the list until Sindri has read requested reviewers (spec §5.6).</summary>
    public const string BeforeReviewersNotice = "Review requests show here once Sindri reads requested reviewers.";

    /// <summary>Whether <paramref name="login"/> is waited on for <paramref name="pull"/>: asked to review it, or reviewed it on a commit other than the head. A team request is not read.</summary>
    /// <param name="login">The owner's GitHub login.</param>
    /// <param name="pull">The PR record.</param>
    /// <returns>Why it waits, or null.</returns>
    public static ReviewWait? Classify(string login, ReviewPull pull)
    {
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(pull);
        if (pull.RequestedReviewers.Any(r => Same(r, login)))
        {
            return new ReviewWait(ReviewWaitingKind.Requested, "review requested", NewCommits: null);
        }

        var reviewed = pull.Reviews.FirstOrDefault(r => Same(r.Login, login));
        if (reviewed is null || pull.HeadSha is null || Same(reviewed.Commit, pull.HeadSha))
        {
            return null;
        }

        var text = reviewed.NewCommits switch
        {
            null => "reviewed, new commits",
            1 => "reviewed, 1 new commit",
            { } n => string.Create(CultureInfo.InvariantCulture, $"reviewed, {n} new commits"),
        };
        return new ReviewWait(ReviewWaitingKind.NewCommits, text, reviewed.NewCommits);
    }

    /// <summary>Builds the groups from the PR records.</summary>
    /// <param name="login">The owner's GitHub login.</param>
    /// <param name="pulls">The open PR records.</param>
    /// <param name="summaryOf">The per-head summary to show for a PR, or null for none.</param>
    /// <param name="projectOf">The project that names a repo, or null for none.</param>
    /// <returns>The groups and what follows from them.</returns>
    public static ReviewListParts Build(string login, IReadOnlyList<ReviewPull> pulls, Func<ReviewPull, ReviewSummary?> summaryOf, Func<string, ReviewProjectName?> projectOf)
    {
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(pulls);
        ArgumentNullException.ThrowIfNull(summaryOf);
        ArgumentNullException.ThrowIfNull(projectOf);
        List<(ReviewPull Pull, ReviewWait? Wait)> waiting = [];
        List<(ReviewPull Pull, ReviewWait? Wait)> yours = [];
        foreach (var pull in pulls)
        {
            var wait = Same(pull.Author, login) ? null : Classify(login, pull);
            if (wait is not null)
            {
                waiting.Add((pull, wait));
            }
            else if (Same(pull.Author, login))
            {
                yours.Add((pull, null));
            }
        }

        ReviewListGroup[] groups = [new(ReviewGroupKind.Waiting, "Waiting on you", Sections(waiting, summaryOf, projectOf)), new(ReviewGroupKind.Yours, "Yours", Sections(yours, summaryOf, projectOf))];
        var notice = pulls.Count > 0 && !pulls.Any(static p => p.ReviewersRead) ? BeforeReviewersNotice : null;
        return new ReviewListParts(groups, waiting.Count, notice, waiting.Count + yours.Count == 0 ? ReviewEmpty.Words : null);
    }

    private static List<ReviewSection> Sections(List<(ReviewPull Pull, ReviewWait? Wait)> rows, Func<ReviewPull, ReviewSummary?> summaryOf, Func<string, ReviewProjectName?> projectOf)
    {
        var sections = rows
            .Select(r => (r.Pull, r.Wait, Project: projectOf(r.Pull.Repo)))
            .GroupBy(static r => r.Project is { } p ? (ReviewSectionBy.Project, p.Key, p.Name) : (ReviewSectionBy.Repo, null, r.Pull.Repo))
            .Select(g => new ReviewSection(
                g.Key.Item1, g.Key.Item2, g.Key.Item3,
                [.. g.OrderByDescending(static r => r.Pull.UpdatedAt).ThenByDescending(static r => r.Pull.Number).Select(r => Row(r.Pull, r.Wait, summaryOf(r.Pull)))]));
        return [.. sections.OrderBy(static s => s.By).ThenBy(static s => s.Name, StringComparer.OrdinalIgnoreCase)];
    }

    private static ReviewRow Row(ReviewPull pull, ReviewWait? wait, ReviewSummary? summary) =>
        new(pull.Repo, pull.Number, pull.Title, pull.Author, pull.Draft, pull.OpenedAt, pull.UpdatedAt, wait?.Kind, wait?.Text, wait?.NewCommits,
            summary?.Ci ?? ReviewCi.None, summary?.RequiredOpen, RequiredState(summary), summary?.RequiredText, pull.Claims.Count > 0, string.Create(CultureInfo.InvariantCulture, $"/reviews/{pull.Repo}/{pull.Number}"));

    // why: a null count means two things on the row, so the word says which: no summary at all, or a summary of a PR whose settings name no checks
    private static string RequiredState(ReviewSummary? summary) =>
        summary is null ? ReviewRequiredState.NotRead : summary.RequiredOpen is null ? ReviewRequiredState.NoneNamed : ReviewRequiredState.Counted;

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
