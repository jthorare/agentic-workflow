namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Where a concept comes from: a map key, with the span of it when the server names one.</summary>
/// <param name="Key">The cited node's key.</param>
/// <param name="Span">The cited span, as served; null when the whole node is cited.</param>
internal sealed record ConceptCitation(string Key, string? Span = null);
