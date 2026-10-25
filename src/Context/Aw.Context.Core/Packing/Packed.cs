using Aw.Context.Core.Results;

namespace Aw.Context.Core.Packing;

/// <summary>The sections of a whole ranked result set.</summary>
/// <param name="Sections">The non-empty sections, in fill order.</param>
/// <param name="Used">Tokens the packed items take.</param>
public sealed record Packed(IReadOnlyList<ResultSection> Sections, int Used);
