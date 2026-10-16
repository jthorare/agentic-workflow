using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>The human's list (API spec §3.6).</summary>
/// <param name="Rows">The rows, in NeedsYouOrder: urgency, then since, then id.</param>
/// <param name="Empty">'Nothing needs you. N running, M queued.' when there is no row (or the stale-views sentence), else null.</param>
/// <param name="AsOf">How fresh the answer is.</param>
public sealed record GetNeedsYouResponse(IReadOnlyList<NeedsYouRow> Rows, string? Empty, AsOf AsOf);
