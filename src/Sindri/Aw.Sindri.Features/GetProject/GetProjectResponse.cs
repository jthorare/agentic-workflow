using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetProject;

/// <summary>One project (projects page spec §6.2): its card (open, finished or hidden) with every link, and its version for a write.</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Viewer">Who Linear says the owner is, or null.</param>
/// <param name="Freshness">The freshness line's parts.</param>
/// <param name="Project">The card.</param>
/// <param name="Hidden">Whether it sits in the Hidden fold.</param>
public sealed record GetProjectResponse(AsOf AsOf, string? Viewer, IReadOnlyList<ProjectFreshnessRow> Freshness, ProjectCard Project, bool Hidden);
