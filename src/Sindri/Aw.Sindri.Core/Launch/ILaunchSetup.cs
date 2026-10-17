using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// The box-local side of the launcher the CLI reaches without a running control plane (launcher spec §4 "The generator"):
/// <c lang="bash">sindri launch init</c> and <c lang="bash">sindri launch show</c> (API P10a, unit U7).
/// </summary>
public interface ILaunchSetup
{
    /// <summary>Detects this box's terminal and writes (or, with <c lang="bash">ToStdout</c>, only returns) its launcher.json.</summary>
    /// <param name="request">The flags.</param>
    /// <param name="cancellationToken">Cancels the PATH probes.</param>
    /// <returns>What was chosen and why; Conflict when the file exists without Force; Invalid for an unknown viewer name.</returns>
    public ValueTask<Result<LaunchInitReport>> Init(LaunchInitRequest request, CancellationToken cancellationToken);

    /// <summary>The merged catalog with each entry's source, resolved bin, argvHash and whether Steps may run it (SL8).</summary>
    /// <param name="only">One entry's id, or null for every entry.</param>
    /// <param name="cancellationToken">Cancels the PATH probes.</param>
    /// <returns>The report; NotFound when <paramref name="only"/> names no entry.</returns>
    public ValueTask<Result<LaunchShowReport>> Show(AgentId? only, CancellationToken cancellationToken);
}
