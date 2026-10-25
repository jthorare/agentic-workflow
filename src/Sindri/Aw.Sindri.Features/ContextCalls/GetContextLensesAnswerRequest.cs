using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The lenses of the anchors on a GET /sindri/context/lenses call: one distinct anchor answers schema 1, several answer schema 2.</summary>
/// <param name="Anchors">Zero or more node keys, names or aliases, repeats included.</param>
/// <param name="Draft">True to read the draft map.</param>
/// <param name="Depth">How far the neighbors reach and the cascade runs (0 to 4), or null for the profile's 'context.cascadeDepth'.</param>
/// <param name="MapVersion">A retained map version, or null for the current one; not valid with <paramref name="Draft"/>.</param>
/// <param name="Hops">A comma list of state, flow, dependency and component for the cascade, or null for the first three (anything else is SND-CONTEXT-140).</param>
/// <param name="Limit">How many items to keep from each list, or null for the whole answer (anything below 1 is SND-CONTEXT-006).</param>
/// <param name="Offset">How many items to skip, or null for the start (below 0 is SND-CONTEXT-006). Meaningful with <paramref name="Limit"/>.</param>
public sealed record GetContextLensesAnswerRequest(IReadOnlyList<string> Anchors, bool Draft = false, int? Depth = null, string? MapVersion = null, string? Hops = null, int? Limit = null, int? Offset = null) : IQuery<LensesAnswer>;
