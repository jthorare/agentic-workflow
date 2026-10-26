namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One core concept of the product (the concept band).</summary>
/// <param name="Id">The concept's id, a map key ("concept:self-scheduling"), stable across renames; the value of the map read's concept parameter.</param>
/// <param name="Name">The concept's name.</param>
/// <param name="Summary">One short summary.</param>
/// <param name="Size">What it holds.</param>
/// <param name="Source">derived (the model proposed it) or override (a person set it).</param>
/// <param name="Citations">The nodes the concept was derived from; absent when none is served.</param>
internal sealed record ContextConcept(string Id, string Name, string Summary, ConceptSize Size, string Source = "derived", IReadOnlyList<ConceptCitation>? Citations = null);
