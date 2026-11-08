namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A place lost features went.</summary>
/// <param name="Id">The concept's id, or null when the features left into no concept.</param>
/// <param name="Name">The concept's name, or null.</param>
/// <param name="Features">How many features went there.</param>
internal sealed record PinMovedTo(string? Id, string? Name, int Features);
