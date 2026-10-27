namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A weighted link between two concepts, by id (the concept band: the filter keeps a match's links).</summary>
/// <param name="From">One end's id.</param>
/// <param name="To">The other end's id.</param>
/// <param name="Weight">How strong the link is.</param>
internal sealed record ConceptLink(string From, string To, int Weight = 0);
