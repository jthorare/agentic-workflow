using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Extract;

/// <summary>A pin with the id its concept and its home leaves carry.</summary>
/// <param name="Id">The concept id ('billing' makes concept:billing).</param>
/// <param name="Pin">The pin.</param>
internal sealed record ResolvedPin(string Id, ConceptPin Pin);
