using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListLinearProjects;

/// <summary>The From Linear tab's rows (projects page spec §3.2, §6.2).</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Error">The one line Context failed with (the tab shows it in place of the rows), or null.</param>
/// <param name="Projects">Stored projects in scope that the owner doesn't lead, hasn't attached and aren't finished, by name.</param>
public sealed record ListLinearProjectsResponse(AsOf AsOf, string? Error, IReadOnlyList<LinearProjectChoice> Projects);
