using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Pages;

/// <summary>One page: a surface of the product, with its actions.</summary>
/// <param name="Id">Its id ('schedule-week'): the profile's for an owner page, the slug of its name for a suggested one.</param>
/// <param name="Key">The surface's key ('surface:route/GET:/schedule/week').</param>
/// <param name="Name">What the map shows (fenced like a concept's name).</param>
/// <param name="Repo">The repo it is in, or null when the map gives none.</param>
/// <param name="Mark">'owner' (the profile names it) or 'suggested'.</param>
/// <param name="Code">True when a code file implements the surface.</param>
/// <param name="Actions">Its actions (at most 40): owner ones first in profile order, then by key.</param>
public sealed record PageRow(PageId Id, NodeKey Key, string Name, string? Repo, string Mark, bool Code, IReadOnlyList<PageActionRow> Actions);
