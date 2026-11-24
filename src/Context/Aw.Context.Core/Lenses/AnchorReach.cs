using Aw.Context.Core.Engine;
using Aw.Context.Core.Walks;

namespace Aw.Context.Core.Lenses;

/// <summary>How <see cref="AnchorSet.Resolve"/> reads the anchors it resolves: one record, so the label scrubber fits under the parameter cap (#4196).</summary>
/// <param name="Depth">How far each anchor's scope reaches.</param>
/// <param name="Options">What the cascade may do, or null for no cascade.</param>
/// <param name="Walk">How many anchors' walks may run at once.</param>
/// <param name="Label">The answer's scrubber for a "Did you mean" choice label (MapText.ChoiceLabel), or null to leave it as stored.</param>
internal sealed record AnchorReach(int Depth, CascadeOptions? Options, WalkOptions Walk, Func<string, string>? Label);
