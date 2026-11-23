using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListHome;

/// <summary>A page of Home rows, split by group, each group in ranked order (home spec §8).</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Counts">How many rows each bucket holds over the whole view.</param>
/// <param name="NeedsYou">The page's needs-you rows.</param>
/// <param name="Moving">The page's moving rows.</param>
/// <param name="Cursor">The id to pass as 'after' for the next page, or null at the end.</param>
/// <param name="Degraded">What makes the page less than whole; empty when nothing does.</param>
public sealed record ListHomeResponse(AsOf AsOf, HomeCounts Counts, IReadOnlyList<HomeRow> NeedsYou, IReadOnlyList<HomeRow> Moving, string? Cursor, IReadOnlyList<HomeDegraded> Degraded);
