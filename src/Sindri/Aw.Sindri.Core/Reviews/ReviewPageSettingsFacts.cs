using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The settings a PR got (spec §7.2).</summary>
/// <param name="Resolved">The resolved settings.</param>
/// <param name="Name">The settings name, or default.</param>
/// <param name="From">Where the repo matched: review.projects[0], or default.</param>
/// <param name="Choices">The settings names two project entries tie on; empty when they do not tie.</param>
public sealed record ReviewPageSettingsFacts(PrReviewResolved Resolved, string Name, string From, IReadOnlyList<string> Choices);
