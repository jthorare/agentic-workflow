namespace Aw.Sindri.Core.Launch;

/// <summary>One catalog entry in <see cref="LaunchShowReport"/>.</summary>
/// <param name="Id">The agent id.</param>
/// <param name="Source">Where the entry came from.</param>
/// <param name="Bin">The entry's <c lang="bash">bin</c>.</param>
/// <param name="ResolvedBin">The path the login shell finds it at, or null when it doesn't resolve.</param>
/// <param name="ArgvHash">The entry's argvHash.</param>
/// <param name="StepOk">True when an unattended Step may run it (SL8).</param>
/// <param name="ParkLine">When not <paramref name="StepOk"/>, the line to approve ("Steps on `id` will park: approve `agents.id.stepArgvHash: sha256:…`"), else null.</param>
public sealed record LaunchShowEntry(AgentId Id, CatalogSource Source, string Bin, string? ResolvedBin, string? ArgvHash, bool StepOk, string? ParkLine);
