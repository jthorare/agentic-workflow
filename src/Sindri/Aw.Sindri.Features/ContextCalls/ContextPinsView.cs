namespace Aw.Sindri.Features.ContextCalls;

/// <summary>'GET /sindri/context/pins': the live profile's pins, how each stands against the approved profile and the map, and why the map is or isn't being rebuilt.</summary>
/// <param name="ApiVersion">1.</param>
/// <param name="Repo">The repo this answer is for, or null when the profile has several and none was asked for.</param>
/// <param name="FileHash">The hash of the profile file the pins were read from (the one load the pins came from): the Pin sheet sends it as ifHash.</param>
/// <param name="MapVersion">The current map's version ('v13'), or null when no map is built.</param>
/// <param name="Pins">Every pin of the live profile and its repos in file order, then the approved pins the live profile removed.</param>
/// <param name="Rebuild">Why the map is, or is not, being rebuilt.</param>
internal sealed record ContextPinsView(int ApiVersion, string? Repo, string FileHash, string? MapVersion, IReadOnlyList<ContextPinRow> Pins, ContextRebuildView Rebuild);
