using Aw.Sindri.Core.Projects.Cards;

namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>Derives a project's truth from Sindri's own data (issue 3718), pure.</summary>
internal static class ProjectTruths
{
    /// <summary>A project's sources: its links that name a GitHub PR or repo, its Linear repos and the profile's 'review.projects[].repos' for it.</summary>
    /// <param name="links">The card's links.</param>
    /// <param name="linearRepos">The Linear card's repos.</param>
    /// <param name="profileRepos">The repos 'review.projects' names for this project.</param>
    /// <returns>The sources, each list without duplicates.</returns>
    internal static ProjectSources SourcesOf(IReadOnlyList<ProjectLink> links, IReadOnlyList<string> linearRepos, IReadOnlyList<string> profileRepos) =>
        throw new NotSupportedException("red: derive the sources");

    /// <summary>Builds the truth of one project.</summary>
    /// <param name="inputs">What it reads.</param>
    /// <returns>The truth.</returns>
    internal static ProjectTruth Build(ProjectTruthInputs inputs) =>
        throw new NotSupportedException("red: derive the truth");
}
