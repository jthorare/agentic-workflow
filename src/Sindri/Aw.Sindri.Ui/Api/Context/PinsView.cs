namespace Aw.Sindri.Ui.Api.Context;

/// <summary>GET /sindri/context/pins: the live profile's pins, how each stands against the approved profile and the map, and why the map is or isn't being rebuilt (concept pins spec §3.6-§3.8).</summary>
/// <param name="ApiVersion">The answer's version.</param>
/// <param name="Repo">The repo the answer is for; null when the profile has several and none was asked for.</param>
/// <param name="FileHash">The hash of the profile file the pins were read from: the Pin sheet sends it as ifHash, so a hand edit after this read is refused as stale (SND-PROFILE-003), never written over.</param>
/// <param name="MapVersion">The current map's version, or null when no map is built.</param>
/// <param name="Pins">Every index.concepts entry of the live profile and its repos, in file order, then the approved pins the live profile removed.</param>
/// <param name="Rebuild">Why the map is, or is not, being rebuilt.</param>
internal sealed record PinsView(int ApiVersion, string? Repo, string FileHash, string? MapVersion, IReadOnlyList<PinView> Pins, MapRebuildView Rebuild);
