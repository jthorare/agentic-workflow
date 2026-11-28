using System.Globalization;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Projects.Truth;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Core.Projects.TruthSources;

/// <summary>Derives a project's truth from Sindri's own data (issue 3718), pure.</summary>
public static class ProjectTruths
{
    /// <summary>A project's sources: its link urls that name a GitHub PR or repo, its Linear repos and the profile's 'review.projects[].repos' for it.</summary>
    /// <param name="linkUrls">The urls of the card's links.</param>
    /// <param name="linearRepos">The Linear card's repos.</param>
    /// <param name="profileRepos">The repos 'review.projects' names for this project.</param>
    /// <returns>The sources, each list without duplicates.</returns>
    public static ProjectSources SourcesOf(IReadOnlyList<string> linkUrls, IReadOnlyList<string> linearRepos, IReadOnlyList<string> profileRepos)
    {
        ArgumentNullException.ThrowIfNull(linkUrls);
        ArgumentNullException.ThrowIfNull(linearRepos);
        ArgumentNullException.ThrowIfNull(profileRepos);
        var repos = new List<string>();
        var pulls = new List<string>();
        var branches = new List<BranchSource>();
        foreach (var link in linkUrls)
        {
            if (BranchLink(link) is { } branch)
            {
                branches.Add(branch);
                continue;
            }

            switch (GithubLink(link))
            {
                case (_, true):
                    pulls.Add(link.TrimEnd('/'));
                    break;
                case (var repo, false):
                    repos.Add(repo);
                    break;
                default:
                    break;
            }
        }

        repos.AddRange(linearRepos);
        repos.AddRange(profileRepos);
        return new ProjectSources([.. repos.Distinct(StringComparer.OrdinalIgnoreCase)], [.. pulls.Distinct(StringComparer.OrdinalIgnoreCase)], [.. branches.Distinct()]);
    }

    // "https://github.com/o/n/tree/<branch>" is a branch (its name may hold '/'), "https://github.com/o/n/branches/all?query=<prefix>" is GitHub's own branch search, read as a prefix (#4333)
    private static BranchSource? BranchLink(string url)
    {
        if (!url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Fragment.Length > 0)
        {
            return null;
        }

        var parts = uri.AbsolutePath.Trim('/').Split('/').Select(Uri.UnescapeDataString).ToArray();
        return parts is [{ Length: > 0 } owner, { Length: > 0 } name, ..] ? BranchOf(owner + "/" + name, parts[2..], uri.Query) : null;
    }

    // what follows owner/repo: "tree/<branch...>" with no query, or "branches/all" with exactly "?query=<prefix>"
    private static BranchSource? BranchOf(string repo, string[] rest, string query)
    {
        const string Query = "?query=";
        if (query.Length == 0)
        {
            return rest is ["tree", _, ..] ? new BranchSource(repo, string.Join('/', rest[1..]), IsPrefix: false) : null;
        }

        var prefix = query.StartsWith(Query, StringComparison.Ordinal) && !query.Contains('&', StringComparison.Ordinal) ? Uri.UnescapeDataString(query[Query.Length..]) : string.Empty;
        return rest is ["branches", "all"] && prefix.Length > 0 ? new BranchSource(repo, prefix, IsPrefix: true) : null;
    }

    // a PR counts for a project that names its head branch, or a prefix of it, in the PR's repo
    private static bool OnNamedBranch(ProjectSources sources, ReviewPull pull) =>
        pull.HeadRef is { } head && (sources.Branches ?? []).Any(b => string.Equals(b.Repo, pull.Repo, StringComparison.OrdinalIgnoreCase) && (b.IsPrefix ? head.StartsWith(b.Name, StringComparison.Ordinal) : string.Equals(head, b.Name, StringComparison.Ordinal)));

    // "https://github.com/o/n/pull/N" is a PR of o/n, "https://github.com/o/n" is the repo; anything else is no source
    private static (string Repo, bool IsPull)? GithubLink(string url)
    {
        const string Prefix = "https://github.com/";
        if (!url.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = url[Prefix.Length..].TrimEnd('/').Split('/');
        return parts switch
        {
            [{ Length: > 0 } owner, { Length: > 0 } name] => (owner + "/" + (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name), false),
            [{ Length: > 0 } owner, { Length: > 0 } name, "pull", var number] when number.All(char.IsAsciiDigit) && number.Length > 0 => (owner + "/" + name, true),
            _ => null,
        };
    }

    /// <summary>Builds the truth of one project.</summary>
    /// <param name="inputs">What it reads.</param>
    /// <returns>The truth.</returns>
    public static ProjectTruth Build(ProjectTruthInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var (sources, facts) = (inputs.Sources, inputs.Facts);
        var named = sources.PullUrls.Select(static u => u.TrimEnd('/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var prs = new List<ProjectTruthPr>();
        var uncheckedCount = 0;
        var blockers = new Blockers([], []);
        foreach (var pull in facts.Pulls)
        {
            var url = UrlOf(pull);
            if (named.Contains(url) || sources.Repos.Contains(pull.Repo, StringComparer.OrdinalIgnoreCase) || OnNamedBranch(sources, pull))
            {
                var (pr, ciChecked) = Pr(pull, url, facts.Summaries, facts.Owner);
                prs.Add(pr);
                blockers.Note(pull, url);
                uncheckedCount += pr.State != ProjectPrState.Draft && !ciChecked ? 1 : 0;
            }
        }

        var prUrls = prs.Select(static p => p.Url).Concat(named).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var items = facts.Items.Where(i => i.LinkUrls.Any(u => prUrls.Contains(u.TrimEnd('/')))).ToList();
        var agents = facts.Agents.Count(a => a.Running && a.Repo is { } repo && sources.Repos.Contains(repo, StringComparer.OrdinalIgnoreCase));
        return new ProjectTruth(prs, items.Count(static i => !i.Done), items.Count(static i => i.Done), items.Count, agents, HealthOf(prs, uncheckedCount, inputs.Now, blockers), prs.Exists(static p => p.WaitsOnYou));
    }

    private static string UrlOf(ReviewPull pull) => (pull.Url ?? string.Create(CultureInfo.InvariantCulture, $"https://github.com/{pull.Repo}/pull/{pull.Number}")).TrimEnd('/');

    // draft > ciRed > waiting > unknown > ready; a summary counts only when it was decided on the PR's current head. 'CiChecked' is apart from the state:
    // a PR that waits on a reviewer and has no CI result on its head is still unchecked for the health line
    // why: waitsOnYou sits beside the state, so a red PR that asks for the owner's review still waits on the owner; a draft never does, since drafts are not open
    private static (ProjectTruthPr Pr, bool CiChecked) Pr(ReviewPull pull, string url, IReadOnlyDictionary<string, ReviewSummary> summaries, string? owner)
    {
        var summary = summaries.TryGetValue(string.Create(CultureInfo.InvariantCulture, $"{pull.Repo}#{pull.Number}"), out var found) && pull.HeadSha is { } head && string.Equals(found.Sha, head, StringComparison.Ordinal) ? found : null;
        var waiting = pull.RequestedReviewers.Concat(pull.Reviews.Where(static r => r.NewCommits is > 0).Select(static r => r.Login)).Distinct(StringComparer.Ordinal).ToList();
        var (state, decidedAt, waitingOn) = (pull.Draft, summary?.Ci, waiting.Count) switch
        {
            (true, _, _) => (ProjectPrState.Draft, (DateTimeOffset?)null, (string?)null),
            (_, ReviewCi.Fail, _) => (ProjectPrState.CiRed, RedSince(summary), null),
            (_, _, > 0) => (ProjectPrState.Waiting, null, string.Join(", ", waiting)),
            (_, ReviewCi.Pass, _) => (ProjectPrState.Ready, null, null),
            _ => (ProjectPrState.Unknown, null, null),
        };
        var waitsOnYou = !pull.Draft && !string.IsNullOrWhiteSpace(owner) && pull.RequestedReviewers.Contains(owner, StringComparer.OrdinalIgnoreCase);
        return (new ProjectTruthPr(url, pull.Repo, pull.Number, pull.Title, state, decidedAt, waitingOn, waitsOnYou), summary?.Ci is ReviewCi.Pass or ReviewCi.Fail);
    }

    // #4506: when the head went red: the failing check's own completed time when the CI read carried one, else when Sindri first saw the head red
    private static DateTimeOffset? RedSince(ReviewSummary? summary) => summary?.FailedAt ?? summary?.At;

    // " for 3 d": how long the oldest red PR has been red, from its CI summary's time and the page's clock; empty without a clock or a red PR with a time
    private static string RedFor(List<ProjectTruthPr> open, DateTimeOffset? now)
    {
        var times = open.Where(static p => p.State == ProjectPrState.CiRed && p.DecidedAt is not null).Select(static p => p.DecidedAt!.Value).ToList();   // why: the filter keeps only PRs with a time
        return now is { } at && times.Count > 0 ? " for " + AgeWords.For(at - times.Min()) : string.Empty;
    }

    // PRs the forge says cannot merge (Mergeable false) and PRs where a reviewer's latest review is CHANGES_REQUESTED, by url (#4333). A null Mergeable or a review with no state (a record stored before
    // they were kept, or GitHub had not worked the merge out) is unknown and counts for neither.
    private sealed record Blockers(HashSet<string> Conflicts, HashSet<string> Changes)
    {
        internal void Note(ReviewPull pull, string url)
        {
            if (pull.Mergeable == false)
            {
                _ = Conflicts.Add(url);
            }

            if (pull.Reviews.Any(static r => string.Equals(r.State, "CHANGES_REQUESTED", StringComparison.OrdinalIgnoreCase)))
            {
                _ = Changes.Add(url);
            }
        }
    }

    // " red for 3 d, 2 with conflicts, 1 changes requested": red PRs keep their count and age, then the other causes; with no red PR the number is every blocked PR and the causes follow "blocked:"
    private static string Causes(int red, string age, int conflicts, int changes)
    {
        var extra = (conflicts > 0 ? string.Create(CultureInfo.InvariantCulture, $", {conflicts} with conflicts") : string.Empty) + (changes > 0 ? string.Create(CultureInfo.InvariantCulture, $", {changes} changes requested") : string.Empty);
        return red > 0 ? $" red{age}{extra}" : $" blocked:{extra[1..]}";   // why: no red PR and a blocked one means a cause is set, so extra starts with ", "
    }

    // null when no PR is open (drafts are not open); every count and the sentence read the same open set. A PR is blocked when its CI is red, it cannot merge or a reviewer asks for changes (#4333); offTrack at half blocked or more,
    // atRisk at any, unknown when none is blocked but some have no CI result on their current head, whatever their state ('unknown' counts those, drafts left out). The sentence keeps the red count and adds how long the oldest red
    // PR has been red ("red for 3 d") from the CI summary's own time and the page's clock, then the conflicts and changes requested, then the unchecked tail.
    private static ProjectTruthHealth? HealthOf(List<ProjectTruthPr> prs, int unknown, DateTimeOffset? now, Blockers blockers)
    {
        var open = prs.Where(static p => p.State != ProjectPrState.Draft).ToList();
        if (open.Count == 0)
        {
            return null;
        }

        var red = open.Count(static p => p.State == ProjectPrState.CiRed);
        var conflicts = open.Count(p => blockers.Conflicts.Contains(p.Url));
        var changes = open.Count(p => blockers.Changes.Contains(p.Url));
        var blocked = open.Count(p => p.State == ProjectPrState.CiRed || blockers.Conflicts.Contains(p.Url) || blockers.Changes.Contains(p.Url));
        var total = open.Count;
        var noun = total == 1 ? "PR" : "PRs";
        var tail = unknown > 0 ? string.Create(CultureInfo.InvariantCulture, $", {unknown} not checked yet") : string.Empty;
        if (blocked == 0)
        {
            return unknown > 0
                ? new ProjectTruthHealth(ProjectTruthLevel.Unknown, string.Create(CultureInfo.InvariantCulture, $"CI not checked yet for {unknown} of {total} {noun}"))
                : new ProjectTruthHealth(ProjectTruthLevel.OnTrack, string.Create(CultureInfo.InvariantCulture, $"On track: {total} open {noun}, none red"));
        }

        var lead = red > 0 ? red : blocked;
        var (level, word) = blocked * 2 >= total ? (ProjectTruthLevel.OffTrack, "Off track") : (ProjectTruthLevel.AtRisk, "At risk");
        return new ProjectTruthHealth(level, string.Create(CultureInfo.InvariantCulture, $"{word}: {lead} of {total} {noun}{Causes(red, RedFor(open, now), conflicts, changes)}{tail}"));
    }
}
