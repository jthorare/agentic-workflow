using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>Everything <see cref="ProjectTruths.Build"/> reads for one project.</summary>
/// <param name="Sources">The project's sources.</param>
/// <param name="Facts">The page's facts, read once for every project.</param>
internal sealed record ProjectTruthInputs(ProjectSources Sources, ProjectTruthFacts Facts);
