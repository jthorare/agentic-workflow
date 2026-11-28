namespace Aw.Sindri.Core.Projects.TruthSources;

/// <summary>Everything <see cref="ProjectTruths.Build"/> reads for one project.</summary>
/// <param name="Sources">The project's sources.</param>
/// <param name="Facts">The page's facts, read once for every project.</param>
/// <param name="Now">The page's clock reading, for how long the oldest red PR has been red (#4333); null leaves the age out of the health reason.</param>
public sealed record ProjectTruthInputs(ProjectSources Sources, ProjectTruthFacts Facts, DateTimeOffset? Now = null);
