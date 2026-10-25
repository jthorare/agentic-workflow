using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;

namespace Aw.Context.Core.Concepts;

/// <summary>One read's inputs for <see cref="ConceptBuilder"/>.</summary>
/// <param name="Basis">The map version and repo the answer is for.</param>
/// <param name="Graph">The graph the caller may see.</param>
/// <param name="Text">The caller's text policy.</param>
public sealed record ConceptRead(ConceptsBasis Basis, GraphSnapshot Graph, MapText Text);
