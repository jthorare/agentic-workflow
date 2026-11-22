using System.Globalization;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>Derives a project's truth from Sindri's own data (issue 3718), pure.</summary>
public static class ProjectTruths
{
    /// <summary>A project's sources: its links that name a GitHub PR or repo, its Linear repos and the profile's 'review.projects[].repos' for it.</summary>
    /// <param name="links">The card's links.</param>
    /// <param name="linearRepos">The Linear card's repos.</param>
    /// <param name="profileRepos">The repos 'review.projects' names for this project.</param>
    /// <returns>The sources, each list without duplicates.</returns>
    public static ProjectSources SourcesOf(IReadOnlyList<ProjectLink> links, IReadOnlyList<string> linearRepos, IReadOnlyList<string> profileRepos)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(linearRepos);
        ArgumentNullException.ThrowIfNull(profileRepos);
        var repos = new List<string>();
        var pulls = new List<string>();
        foreach (var link in links)
        {
            switch (GithubLink(link.Url))
            {
                case (_, true):
                    pulls.Add(link.Url.TrimEnd('/'));
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
        return new ProjectSources([.. repos.Distinct(StringComparer.OrdinalIgnoreCase)], [.. pulls.Distinct(StringComparer.OrdinalIgnoreCase)]);
    }

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
        foreach (var pull in facts.Pulls)
        {
            var url = UrlOf(pull);
            if (named.Contains(url) || sources.Repos.Contains(pull.Repo, StringComparer.OrdinalIgnoreCase))
            {
                prs.Add(Pr(pull, url, facts.Summaries, facts.Owner));
            }
        }

        var prUrls = prs.Select(static p => p.Url).Concat(named).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var items = facts.Items.Where(i => i.LinkUrls.Any(u => prUrls.Contains(u.TrimEnd('/')))).ToList();
        var agents = facts.Agents.Count(a => a.Running && a.Repo is { } repo && sources.Repos.Contains(repo, StringComparer.OrdinalIgnoreCase));
        return new ProjectTruth(prs, items.Count(static i => !i.Done), items.Count(static i => i.Done), items.Count, agents, HealthOf(prs), prs.Exists(static p => p.WaitsOnYou));
    }

    private static string UrlOf(ReviewPull pull) => (pull.Url ?? string.Create(CultureInfo.InvariantCulture, $"https://github.com/{pull.Repo}/pull/{pull.Number}")).TrimEnd('/');

    // draft > ciRed > waiting > unknown > ready; a summary counts only when it was decided on the PR's current head.
    // why: waitsOnYou sits beside the state, so a red PR that asks for the owner's review still waits on the owner; a draft never does, since drafts are not open
    private static ProjectTruthPr Pr(ReviewPull pull, string url, IReadOnlyDictionary<string, ReviewSummary> summaries, string? owner)
    {
        var summary = summaries.TryGetValue(string.Create(CultureInfo.InvariantCulture, $"{pull.Repo}#{pull.Number}"), out var found) && pull.HeadSha is { } head && string.Equals(found.Sha, head, StringComparison.Ordinal) ? found : null;
        var waiting = pull.RequestedReviewers.Concat(pull.Reviews.Where(static r => r.NewCommits is > 0).Select(static r => r.Login)).Distinct(StringComparer.Ordinal).ToList();
        var (state, decidedAt, waitingOn) = (pull.Draft, summary?.Ci, waiting.Count) switch
        {
            (true, _, _) => (ProjectPrState.Draft, (DateTimeOffset?)null, (string?)null),
            (_, ReviewCi.Fail, _) => (ProjectPrState.CiRed, summary?.At, null),
            (_, _, > 0) => (ProjectPrState.Waiting, null, string.Join(", ", waiting)),
            (_, ReviewCi.Pass, _) => (ProjectPrState.Ready, null, null),
            _ => (ProjectPrState.Unknown, null, null),
        };
        var waitsOnYou = !pull.Draft && !string.IsNullOrWhiteSpace(owner) && pull.RequestedReviewers.Contains(owner, StringComparer.OrdinalIgnoreCase);
        return new ProjectTruthPr(url, pull.Repo, pull.Number, pull.Title, state, decidedAt, waitingOn, waitsOnYou);
    }

    // null when no PR is open (drafts are not open); every count and the sentence read the same open set; offTrack at half red or more, atRisk at any red,
    // unknown when none is red but some have no CI summary on their current head
    private static ProjectTruthHealth? HealthOf(List<ProjectTruthPr> prs)
    {
        var open = prs.Where(static p => p.State != ProjectPrState.Draft).ToList();
        if (open.Count == 0)
        {
            return null;
        }

        var red = open.Count(static p => p.State == ProjectPrState.CiRed);
        var unknown = open.Count(static p => p.State == ProjectPrState.Unknown);
        var total = open.Count;
        var noun = total == 1 ? "PR" : "PRs";
        var tail = unknown > 0 ? string.Create(CultureInfo.InvariantCulture, $", {unknown} not checked yet") : string.Empty;
        return red > 0 && red * 2 >= total
            ? new ProjectTruthHealth(ProjectTruthLevel.OffTrack, string.Create(CultureInfo.InvariantCulture, $"Off track: {red} of {total} {noun} red{tail}"))
            : red > 0
                ? new ProjectTruthHealth(ProjectTruthLevel.AtRisk, string.Create(CultureInfo.InvariantCulture, $"At risk: {red} of {total} {noun} red{tail}"))
                : unknown > 0
                    ? new ProjectTruthHealth(ProjectTruthLevel.Unknown, string.Create(CultureInfo.InvariantCulture, $"CI not checked yet for {unknown} of {total} {noun}"))
                    : new ProjectTruthHealth(ProjectTruthLevel.OnTrack, string.Create(CultureInfo.InvariantCulture, $"On track: {total} open {noun}, none red"));
    }
}
