namespace Aw.Context.Core.Concepts;

/// <summary>What a stored concepts answer was built on, read without loading the graph: a version's graph never changes once written, overrides are only appended and the intent layer has its own counter.</summary>
/// <param name="Version">The map version's number.</param>
/// <param name="Overrides">The override layer: its row count and newest row ('3:41'; '0:0' when empty).</param>
/// <param name="Intent">The intent layer's counter (stamp_intent).</param>
public sealed record ConceptAnswerBasis(int Version, string Overrides, long Intent);
