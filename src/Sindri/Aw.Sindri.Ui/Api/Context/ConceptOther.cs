namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The synthetic concept that holds the nodes no concept claims (the concept band).</summary>
/// <param name="Id">Always "concept:other"; the value of the map read's concept parameter.</param>
/// <param name="Name">The name the server gives it.</param>
/// <param name="Size">What it holds.</param>
internal sealed record ConceptOther(string Id, string Name, ConceptSize Size);
