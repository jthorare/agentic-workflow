using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.WholeMap;

namespace Aw.Context.Core.Concepts;

/// <summary>One read's inputs for <see cref="ConceptBuilder"/>.</summary>
/// <param name="Basis">The map version and repo the answer is for.</param>
/// <param name="Graph">The graph the caller may see.</param>
/// <param name="Text">The caller's text policy.</param>
public sealed record ConceptRead(ConceptsBasis Basis, GraphSnapshot Graph, MapText Text)
{
    /// <summary>The pins the caller sees, or null when the profile has none: a pin's definition and aliases are read from here, over the map's.</summary>
    public PinSet? Pins { get; init; }

    /// <summary>The repos the map the caller may see holds nodes of (<see cref="WholeMapBuilder.Repos"/>), read before the cut to the asked repo, so a pick still lists them all.</summary>
    public IReadOnlyList<WholeMapRepo> Repos { get; init; } = [];
}
