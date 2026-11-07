using Aw.Sindri.Core.Projects.Facts;

namespace Aw.Sindri.Core.Reviews;

/// <summary>What Context holds for the Reviews list.</summary>
/// <param name="Pulls">The open PR records of the repos in <c language="text">context.sources.github.repos</c> the caller may read.</param>
/// <param name="Github">The GitHub source's state, or null when no GitHub source is configured.</param>
/// <param name="Library">The library's state.</param>
public sealed record ReviewFactsSnapshot(IReadOnlyList<ReviewPull> Pulls, ProjectSourceFreshness? Github, ReviewLibraryFacts Library);
