using System.Text.Json.Serialization;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>The human's list (API spec §3.6).</summary>
/// <param name="Rows">The rows, in NeedsYouOrder: urgency, then since, then id; none when <paramref name="NotModified"/>.</param>
/// <param name="Empty">'Nothing needs you. N running, M queued.' when there is no row (or the stale-views sentence), else null.</param>
/// <param name="AsOf">How fresh the answer is.</param>
/// <param name="Tag">The answer's entity tag: a hash of the served rows (decisions joined), the empty sentence, the projector's state word and whether the views are behind. Never the as-of time (#2789).</param>
/// <param name="NotModified">True when the request's IfNoneMatch equals <paramref name="Tag"/>: the answer has no body.</param>
public sealed record GetNeedsYouResponse(IReadOnlyList<NeedsYouRow> Rows, string? Empty, AsOf AsOf, string? Tag = null, [property: JsonIgnore] bool NotModified = false);
