using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>GET /sindri/context/map.</summary>
/// <param name="Version">Version, as served.</param>
/// <param name="Counts">Counts, as served.</param>
/// <param name="Orphaned">Orphaned, as served.</param>
/// <param name="Retained">Retained, as served.</param>
/// <param name="ScopedTo">ScopedTo, as served.</param>
/// <param name="Coverage">Coverage, as served.</param>
/// <param name="Roles">Roles, as served: per repo, the role counts and the role-like files no glob covers; absent when there are none.</param>
internal sealed record MapStatusView(
    MapVersionInfo Version,
    MapCounts Counts,
    int Orphaned,
    int Retained,
    JsonElement? ScopedTo,
    CoverageView? Coverage = null,
    IReadOnlyList<RepoRolesView>? Roles = null);
