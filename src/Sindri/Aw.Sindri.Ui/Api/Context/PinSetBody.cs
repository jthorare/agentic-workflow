namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One pin of a <see cref="PinConceptsBody"/>.</summary>
/// <param name="Name">What the map shows.</param>
/// <param name="Id">The pin's id; null derives it from the name, or finds the pin the name already has.</param>
/// <param name="Summary">The definition line.</param>
/// <param name="Aliases">The words people use for it.</param>
/// <param name="Features">Exact feature keys from the current map.</param>
internal sealed record PinSetBody(string Name, string? Id = null, string? Summary = null, IReadOnlyList<string>? Aliases = null, IReadOnlyList<string>? Features = null);
