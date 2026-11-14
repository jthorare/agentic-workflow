using Aw.Kernel;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.Truth;

namespace Aw.Sindri.Features.ProjectFacts;

/// <summary>Puts each card's truth on the page: ONE facts read for the union of every card's sources, then the pure build per card in memory.</summary>
internal static class ProjectTruthPage
{
    /// <summary>Attaches the truth to every card of the page.</summary>
    /// <param name="page">The page built from the owner state and Linear's facts.</param>
    /// <param name="profileRepos">The repos 'review.projects' names, by project key.</param>
    /// <param name="facts">The facts port; read once.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page with each card's <see cref="ProjectCard.Truth"/> set; unchanged when the facts read failed.</returns>
    internal static ValueTask<ProjectsPage> Attach(ProjectsPage page, IReadOnlyDictionary<string, IReadOnlyList<string>> profileRepos, IProjectTruthFacts facts, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: attach the truth");
}
