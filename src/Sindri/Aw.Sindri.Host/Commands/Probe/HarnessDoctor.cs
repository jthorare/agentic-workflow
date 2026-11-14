using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Host.Commands.Probe;

/// <summary>
/// 'sindri doctor''s harness lines (engine spec §6.2): re-probes the harness this doctor runs in (Claude Code, when the
/// doctor runs inside it), compares every other probed harness's installed version with the pinned probe, and warns
/// (never 'ok') on a mismatch or a pending owner probe.
/// </summary>
internal static class HarnessDoctor
{
    private const string UserFix = "update the toolkit (./setup.sh) or report it: the agent's launch variables changed";

    /// <summary>One line per pinned agent harness, and per pending human terminal.</summary>
    /// <param name="pinned">The pinned probe.</param>
    /// <param name="live">A probe of this process.</param>
    /// <param name="versions">Reads installed versions.</param>
    /// <param name="dev">Whether this is the toolkit developers' run ('sindri doctor --dev').</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The checks.</returns>
    internal static async ValueTask<IReadOnlyList<DoctorCheck>> Checks(
        IReadOnlyList<PinnedHarness> pinned, HarnessProbeReport live, IHarnessVersions versions, bool dev, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pinned);
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(versions);
        var checks = new List<DoctorCheck>();
        foreach (var harness in pinned.Where(h => !h.Human || h.Pending))
        {
            checks.Add(harness switch
            {
                { Pending: true } when dev => new DoctorCheck(harness.Harness, CheckState.Warn, $"pending: {PinnedHarness.PendingNote}", Probe(harness)),
                { Pending: true } => new DoctorCheck(harness.Harness, CheckState.Ok, $"pending {PinnedHarness.PendingNote}: provisional markers", Fix: null),
                _ when string.Equals(live.Harness, harness.Harness, StringComparison.Ordinal) => Reprobed(harness, live, dev),
                _ => Installed(harness, harness.VersionCommand is { } command ? await versions.Read(command, cancellationToken).ConfigureAwait(false) : null, dev),
            });
        }

        return checks;
    }

    /// <summary>The doctor runs inside this harness: its live probe against the pinned one.</summary>
    private static DoctorCheck Reprobed(PinnedHarness harness, HarnessProbeReport live, bool dev)
    {
        List<string> missing = [.. harness.Markers.Where(m => !live.Variables.Contains(m, StringComparer.Ordinal))];
        if (missing.Count > 0)
        {
            return new DoctorCheck(harness.Harness, CheckState.Warn, $"re-probed: {string.Join(", ", missing)} {(missing.Count == 1 ? "is" : "are")} no longer set", dev ? Probe(harness) : UserFix);
        }

        if (string.Equals(live.Version, harness.Version, StringComparison.Ordinal))
        {
            return new DoctorCheck(harness.Harness, CheckState.Ok, $"re-probed: the markers are set, version {harness.Version}", Fix: null);
        }

        return dev
            ? new DoctorCheck(harness.Harness, CheckState.Warn, $"re-probed: {harness.VersionCommand} {live.Version ?? "(no version)"} differs from the pinned probe ({harness.Version})", Probe(harness))
            : new DoctorCheck(harness.Harness, CheckState.Ok, $"{harness.VersionCommand} {live.Version ?? "(no version)"} (probe pinned at {harness.Version}; the markers are all set)", Fix: null);
    }

    /// <summary>The doctor runs outside this harness: its installed version against the pinned one.</summary>
    private static DoctorCheck Installed(PinnedHarness harness, string? installed, bool dev) => installed switch
    {
        null => new DoctorCheck(harness.Harness, CheckState.Ok, $"{harness.VersionCommand} is not installed", Fix: null),
        _ when string.Equals(installed, harness.Version, StringComparison.Ordinal) => new DoctorCheck(harness.Harness, CheckState.Ok, $"{harness.VersionCommand} {installed} matches the pinned probe", Fix: null),
        _ when dev => new DoctorCheck(harness.Harness, CheckState.Warn, $"{harness.VersionCommand} {installed} differs from the pinned probe ({harness.Version})", Probe(harness)),
        _ => new DoctorCheck(harness.Harness, CheckState.Ok, $"{harness.VersionCommand} {installed} installed (probe pinned at {harness.Version})", Fix: null),
    };

    private static string Probe(PinnedHarness harness) =>
        $"in that harness, sindri doctor --probe --harness {harness.Harness} > tests/Aw.Sindri.Tests/Golden/Probe/{harness.GoldenFile}; docs/csharp/sindri.md, 'The harness probe'";
}
