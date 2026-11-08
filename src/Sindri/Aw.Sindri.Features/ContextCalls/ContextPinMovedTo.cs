namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One place a pin's lost features went.</summary>
/// <param name="Id">The concept's id, or null for features that are in no concept.</param>
/// <param name="Name">Its display name, or null with a null id.</param>
/// <param name="Features">How many lost features went there.</param>
internal sealed record ContextPinMovedTo(string? Id, string? Name, int Features);
