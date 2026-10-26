using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>What one gap-log round decided: the outcome, the cumulative record, the anchors to look at again and the facts only one outcome writes.</summary>
/// <param name="Outcome">'clear' or 'confirmed'.</param>
/// <param name="GapSearch">The cumulative record with this round added.</param>
/// <param name="GapAnchors">The anchors of the confirmed gaps; empty on 'clear'.</param>
/// <param name="Findings">The findings fact with one SCP-GAP-001 finding per confirmed gap; null on 'clear'.</param>
/// <param name="Closed">The latch, {"round": n}; null on 'confirmed'.</param>
internal sealed record GapLogResult(string Outcome, JsonObject GapSearch, JsonArray GapAnchors, JsonArray? Findings, JsonObject? Closed);
