using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Host.Commands.Probe;

/// <summary>'sindri doctor --probe' and the harness lines plain 'sindri doctor' adds (engine spec §6.2). Neither calls the control plane.</summary>
/// <param name="ancestry">This process's ancestry.</param>
/// <param name="versions">Reads installed harness versions.</param>
/// <param name="environment">Reads an environment variable (the home directory).</param>
/// <param name="interactive">Whether stdin and stdout are a terminal.</param>
internal sealed class ProbeCommands(IProcessAncestry ancestry, IHarnessVersions versions, Func<string, string?> environment, bool interactive)
{
    /// <summary>This process's probe, which 'doctor --probe' prints as JSON (exit 0).</summary>
    /// <param name="harness">The harness name the owner gave, or null.</param>
    /// <param name="cancellationToken">Cancels the version read.</param>
    /// <returns>The report.</returns>
    internal ValueTask<HarnessProbeReport> Probe(string? harness, CancellationToken cancellationToken) => Live(harness, cancellationToken);

    /// <summary>The doctor's harness lines against the pinned probe.</summary>
    /// <param name="dev">Whether this is the toolkit developers' run ('sindri doctor --dev').</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The checks.</returns>
    internal async ValueTask<IReadOnlyList<DoctorCheck>> DoctorChecks(bool dev, CancellationToken cancellationToken) =>
        await HarnessDoctor.Checks(PinnedProbe.All, await Live(harness: null, cancellationToken).ConfigureAwait(false), versions, dev, cancellationToken).ConfigureAwait(false);

    private ValueTask<HarnessProbeReport> Live(string? harness, CancellationToken cancellationToken) =>
        HarnessProbe.Take(
            new ProbeInput(harness, [.. Environment.GetEnvironmentVariables().Keys.Cast<string>()], environment("HOME"),
                ancestry.Ancestors(Environment.ProcessId, HarnessProbe.Depth), interactive),
            versions, cancellationToken);
}
