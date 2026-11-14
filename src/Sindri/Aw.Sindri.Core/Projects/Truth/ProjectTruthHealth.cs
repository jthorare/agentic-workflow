using Aw.Sindri.Core.Projects.Cards;

namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>The health Sindri derives from a project's own PRs and items, beside Linear's.</summary>
/// <param name="Level">On track, at risk or off track.</param>
/// <param name="Reason">The reason in words, "At risk: 3 of 9 PRs red".</param>
public sealed record ProjectTruthHealth(ProjectHealth Level, string Reason);
