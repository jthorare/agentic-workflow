using Aw.Context.Core.Trust;

namespace Aw.Context.Core.Surfaces;

/// <summary>What one build hands the picker besides the groups: whether the approved profile turned the pick on, the extra scrub patterns, and the labeling token budget the pick shares.</summary>
/// <param name="Pick">True when 'context.models.productSurfaces.pick' is jev; false asks nothing and keeps every group separate.</param>
/// <param name="ExtraPatterns">The profile's extra scrub patterns, applied to every name, path and edge.</param>
/// <param name="SpentTokens">The tokens the build has already used of 'context.labeling.maxTokens'.</param>
/// <param name="MaxTokens">'context.labeling.maxTokens'.</param>
public sealed record SurfacePickOptions(bool Pick, IReadOnlyList<ScrubPattern> ExtraPatterns, int SpentTokens, int MaxTokens);
