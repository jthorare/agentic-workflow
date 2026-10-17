using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>What <see cref="LauncherInit"/> is composed of.</summary>
/// <param name="Catalog">The reloading catalog (its path is the file <c lang="text">init</c> writes).</param>
/// <param name="Detection">The detection's inputs.</param>
internal sealed record LauncherInitParts(ReloadingAgentCatalog Catalog, DetectionInputs Detection)
{
    /// <summary>The built-in entry's argvHash for an id, or null.</summary>
    public Func<AgentId, string?> BuiltinHash { get; init; } = static _ => null;

    /// <summary>The approved profile's <c lang="text">agents:</c> block.</summary>
    public Func<CancellationToken, ValueTask<AgentSettings?>> Approved { get; init; } = static _ => ValueTask.FromResult<AgentSettings?>(null);

    /// <summary>Finds an agent CLI the way the login shell does.</summary>
    public Func<string, CancellationToken, ValueTask<string?>> ResolveBin { get; init; } = BinResolver.Resolve;
}
