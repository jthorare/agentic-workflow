namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What GET /sindri/context/map/whole is asked (whole-map spec §2.1): the level of detail, one area to open, and the nodes to locate.</summary>
/// <param name="Detail">auto, nodes or areas; null leaves it to the server (auto).</param>
/// <param name="Area">One area's key to answer in full; null for the whole map.</param>
/// <param name="Locate">Node keys whose area the answer names.</param>
/// <param name="MapVersion">A retained map version, or null for the current one.</param>
/// <param name="Draft">Read the draft map.</param>
/// <param name="Repo">One repo to read; null for every repo.</param>
internal sealed record WholeMapQuery(string? Detail, string? Area, IReadOnlyList<string> Locate, string? MapVersion, bool Draft, string? Repo = null);
