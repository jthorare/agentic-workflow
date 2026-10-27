namespace Aw.Context.Core.ConceptScan;

/// <summary>A product concept the scan found in a draft map: a name and the features it groups. The map's own grouping reads it as a hint, never as a decision.</summary>
/// <param name="Name">The concept's name, one line.</param>
/// <param name="Members">The keys of the draft's features the concept groups ('feature:…'), each in no other hint.</param>
/// <param name="Evidence">Paths or search hits the model named for it.</param>
/// <param name="Confidence">0 to 1, as the model rated it.</param>
public sealed record ConceptHint(string Name, IReadOnlyList<string> Members, IReadOnlyList<string> Evidence, double Confidence);
