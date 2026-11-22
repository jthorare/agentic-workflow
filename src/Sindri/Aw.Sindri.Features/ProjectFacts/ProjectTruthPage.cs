using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.Truth;

namespace Aw.Sindri.Features.ProjectFacts;

/// <summary>Puts each card's truth on the page: ONE facts read for the union of every card's sources, then the pure build per card in memory.</summary>
internal static class ProjectTruthPage
{
    /// <summary>The row the page carries when PRs are in scope but no GitHub login is known, so no PR can show as blocked on the owner (issue 3797).</summary>
    internal static readonly ProjectFreshnessRow NoLogin = new("github-login", ProjectFreshnessState.Failing, "not known: connect GitHub or set review.login to see what is blocked on you", At: null);

    /// <summary>Attaches the truth to every card of the page.</summary>
    /// <param name="page">The page built from the owner state and Linear's facts.</param>
    /// <param name="profileRepos">The repos 'review.projects' names, by project key.</param>
    /// <param name="facts">The facts port; read once.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page with each card's <see cref="ProjectCard.Truth"/> set, plus <see cref="NoLogin"/> when PRs are in scope and no login is known; the page with one failing freshness row when the facts read failed.</returns>
    internal static async ValueTask<ProjectsPage> Attach(ProjectsPage page, IReadOnlyDictionary<string, IReadOnlyList<string>> profileRepos, IProjectTruthFacts facts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(profileRepos);
        ArgumentNullException.ThrowIfNull(facts);
        var cards = page.Projects.Concat(page.Finished).Concat(page.Hidden).ToList();
        var sources = cards.ToDictionary(static c => c.Key, c => ProjectTruths.SourcesOf(c.Links, c.Repos, profileRepos.GetValueOrDefault(c.Key) ?? []), StringComparer.Ordinal);
        var union = new ProjectSources(
            [.. sources.Values.SelectMany(static s => s.Repos).Distinct(StringComparer.OrdinalIgnoreCase)],
            [.. sources.Values.SelectMany(static s => s.PullUrls).Distinct(StringComparer.OrdinalIgnoreCase)]);
        if (union.Repos.Count == 0 && union.PullUrls.Count == 0)
        {
            return page;
        }

        var read = await facts.Read(union, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            var line = read.Error.Message.Split('\n', 2)[0].Trim();
            return page with { Freshness = [.. page.Freshness, new ProjectFreshnessRow("sindri", ProjectFreshnessState.Failing, line, At: null)] };
        }

        ProjectCard With(ProjectCard card) => card with { Truth = ProjectTruths.Build(new ProjectTruthInputs(sources[card.Key], read.Value)) };
        return page with { Projects = [.. page.Projects.Select(With)], Finished = [.. page.Finished.Select(With)], Hidden = [.. page.Hidden.Select(With)], Freshness = read.Value.Owner is null && read.Value.Pulls.Count > 0 ? [.. page.Freshness, NoLogin] : page.Freshness };
    }
}
