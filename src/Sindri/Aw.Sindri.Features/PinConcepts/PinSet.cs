using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.PinConcepts;

/// <summary>
/// One pin to create or update. On an existing pin (by <paramref name="Id"/>, or a name whose derived id is a pin) a null field keeps the stored value and a list replaces the stored one (empty clears it); 'paths',
/// 'dependsOn' and 'owns' are always kept. A new pin gets the id PinIds derives from the name unless <paramref name="Id"/> is given.
/// </summary>
/// <param name="Name">What the map shows.</param>
/// <param name="Id">The pin's id; null derives it from the name, or finds the pin the name already has.</param>
/// <param name="Summary">The definition line.</param>
/// <param name="Aliases">The words people use for it.</param>
/// <param name="Features">Exact feature keys from the current map ('feature:billing.refunds').</param>
public sealed record PinSet(string Name, ConceptPinId? Id = null, string? Summary = null, IReadOnlyList<string>? Aliases = null, IReadOnlyList<string>? Features = null);
