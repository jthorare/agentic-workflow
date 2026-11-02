using System.Globalization;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>
/// Doctor's sandbox rows (sandbox spec §8): 'sandbox ready: …' when nothing blocks a Step, else one row per blocker with the same sentence the Check again card says and its
/// copyable command as the fix. A -017 blocker is the 'sandbox:login' row. A gray sentence (macOS: not yet supported) is information, never a fault. Beside them: the limits, hooks, agents'-own-sandboxes and (on a ready box) sessions-you-open rows.
/// </summary>
/// <param name="checks">The checks Check again runs.</param>
/// <param name="sandbox">The mechanism, for the ready line's version.</param>
/// <param name="clock">Times the checks.</param>
/// <param name="approved">The approved profile's sandbox section, for the limits and the agents' own sandboxes.</param>
/// <param name="machine">The memory and cores the limits are read against.</param>
internal sealed class SandboxDoctorChecks(ISandboxChecks checks, ISandbox sandbox, TimeProvider clock, IApprovedProfile approved, ISandboxMachine machine) : IDoctorChecks
{
    /// <summary>The check groups a ready box passed: the probe, the form check, the login probe and the fixture self-check.</summary>
    internal const int Groups = 4;

    private const string LoginCode = "SND-LAUNCH-017";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var started = clock.GetTimestamp();
        var report = await checks.Run(cancellationToken).ConfigureAwait(false);
        var profile = await approved.Latest(cancellationToken).ConfigureAwait(false);
        var settings = profile.IsSuccess ? profile.Value?.Profile.Sandbox : null;
        // why: sessions the human opens are sandboxed only when the sandbox works, so the row follows the ready line (a blocked or unsupported box says nothing about them).
        return [.. Probe(report, started), Limits(settings), Hooks(), Inner(settings), .. Attended(report)];
    }

    private IEnumerable<DoctorCheck> Probe(SandboxCheckReport report, long started)
    {
        if (report.Ready)
        {
            var probe = sandbox.Probe();
            var what = probe.IsSuccess ? probe.Value.Detail ?? "available" : "available";
            var ms = (long)clock.GetElapsedTime(started).TotalMilliseconds;
            return [new DoctorCheck("sandbox", CheckState.Ok, string.Create(CultureInfo.InvariantCulture, $"sandbox ready: {what}, {Groups} checks passed in {ms} ms"), Fix: null)];
        }

        return report.Blockers.Select(static b => SandboxBlockerLine.From(b)).Select(Row);
    }

    /// <summary>'3 sessions × 12 GiB = 36 GiB of 62 GiB', or the warning when that is more than the machine has (the heavy wrapper still gates the last slot).</summary>
    private DoctorCheck Limits(SandboxSettings? settings)
    {
        var limits = LimitsResolver.Resolve(new LimitsInput(settings?.Limits, AttendedSettings: null, Attended: false, StepTimeout: null, LongMax: null, machine.RamBytes, machine.Cores));
        var total = limits.Sessions * limits.MemoryMaxBytes;
        var each = Gib(limits.MemoryMaxBytes);
        return total > machine.RamBytes
            ? new DoctorCheck("sandbox:limits", CheckState.Warn, string.Create(CultureInfo.InvariantCulture, $"{limits.Sessions} × {each} is more than this machine's memory (the heavy wrapper still gates slot {limits.Sessions})"), Fix: null)
            : new DoctorCheck("sandbox:limits", CheckState.Ok, string.Create(CultureInfo.InvariantCulture, $"{limits.Sessions} sessions × {each} = {Gib(total)} of {Gib(machine.RamBytes)}"), Fix: null);
    }

    /// <summary>Nothing records the hooks-run canary's result yet, so the row says so instead of guessing.</summary>
    private static DoctorCheck Hooks() =>
        new("sandbox:hooks", CheckState.Ok, "not checked yet (the first Step's self-check verifies them)", Fix: null);

    /// <summary>The agents' own sandboxes are off unless the profile turns them on ('auto' stays off until the self-check keeps them); either way it is not a fault.</summary>
    private static DoctorCheck Inner(SandboxSettings? settings) =>
        new("sandbox:inner", CheckState.Ok, (settings?.Inner ?? SandboxDefaults.Inner) == InnerSandbox.On ? "on" : "off (not needed; Sindri's covers it)", Fix: null);

    /// <summary>A session the human opens is sandboxed too and has no time limit while they are attached (spec §8); information, never a fault.</summary>
    private static IEnumerable<DoctorCheck> Attended(SandboxCheckReport report) =>
        report.Ready ? [new DoctorCheck("sandbox:attended", CheckState.Ok, "sandboxed too, no time limit while you're attached", Fix: null)] : [];

    private static string Gib(long bytes) => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024 * 1024):0.#} GiB");

    private static DoctorCheck Row(SandboxBlockerLine line) => new(
        string.Equals(line.Code, LoginCode, StringComparison.Ordinal) ? "sandbox:login" : "sandbox",
        line.Quiet ? CheckState.Ok : CheckState.Fail,
        line.Detail is null ? line.Text : $"{line.Text} ({line.Detail})",
        line.Quiet ? null : line.Command);
}
