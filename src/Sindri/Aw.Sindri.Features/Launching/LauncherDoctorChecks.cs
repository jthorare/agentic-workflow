using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.Launching;

/// <summary>Doctor's launcher rows (launcher spec section 4): the viewer and the config file, each entry's resolved bin and the Steps that would park, and the opens of the last 30 days.</summary>
/// <param name="setup">The box-local catalog and viewer report.</param>
/// <param name="launches">The launches projection.</param>
/// <param name="targets">The last probe of the viewer, host and agents.</param>
/// <param name="clock">Now.</param>
internal sealed class LauncherDoctorChecks(ILaunchSetup setup, ILaunchViews launches, LaunchTargetsReader targets, TimeProvider clock) : IDoctorChecks
{
    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var shown = await setup.Show(only: null, cancellationToken).ConfigureAwait(false);
        if (shown.IsError)
        {
            return [new DoctorCheck("launcher", CheckState.Fail, shown.Error.Message, "sindri launch show")];
        }

        var report = shown.Value;
        var rows = await launches.Since(clock.GetUtcNow() - LaunchAdoption.Window, cancellationToken).ConfigureAwait(false);
        var probed = await targets.Read(cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var facts = Facts(probed.IsError ? null : probed.Value.ProbedAt, probed.IsError, rows.IsError ? [] : rows.Value, now);
        return [Viewer(report, facts), Agents(report), new DoctorCheck("launcher:adoption", CheckState.Ok, rows.IsError ? "the launches view couldn't be read" : LaunchAdoption.Line(rows.Value), Fix: null)];
    }

    /// <summary>The three facts "run `sindri doctor`" in the launch copy leads to (UI spec E-1): when the targets were last probed, how many requests ran past their deadline, and the last failure's code.</summary>
    private static string Facts(DateTimeOffset? probedAt, bool unreadable, IReadOnlyList<LaunchRow> rows, DateTimeOffset now)
    {
        var probe = unreadable ? "probe unreadable" : probedAt is { } at ? $"probed {Age(now - at)} ago" : "not probed yet";
        var late = rows.Count(row => row.State == LaunchState.Requested && now - row.RequestedAt >= LaunchRules.Deadline);
        var last = rows.Where(static row => row is { State: LaunchState.Failed, Code: not null }).OrderByDescending(static row => row.OutcomeAt).FirstOrDefault()?.Code;
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{probe}; {late} request(s) past their deadline; last failure {last ?? "none"}");
    }

    private static string Age(TimeSpan span) => span < TimeSpan.FromMinutes(1)
        ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{(int)span.TotalSeconds} s")
        : span < TimeSpan.FromHours(1)
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{(int)span.TotalMinutes} min")
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{(int)span.TotalHours} h");

    private static DoctorCheck Viewer(LaunchShowReport report, string facts) => report.Problems.Count > 0
        ? new DoctorCheck("launcher", CheckState.Warn, string.Join("; ", report.Problems) + "; " + facts, "sindri launch show")
        : string.Equals(report.TerminalSource, "file", StringComparison.Ordinal)
            ? new DoctorCheck("launcher", CheckState.Ok, $"viewer {report.Terminal} from {report.ConfigPath}; {facts}", Fix: null)
            : new DoctorCheck("launcher", CheckState.Ok, $"no launcher.json at {report.ConfigPath}; this box's viewer is {report.Terminal} (sindri launch init writes it); {facts}", Fix: null);

    private static DoctorCheck Agents(LaunchShowReport report)
    {
        var lines = report.Entries.Select(static e => $"{e.Id.Value} ({e.Source.ToString().ToLowerInvariant()}): {e.ResolvedBin ?? "not on the login PATH"}").ToList();
        var parked = report.Entries.Where(static e => e.ParkLine is not null).Select(static e => e.ParkLine).OfType<string>().ToList();
        var missing = report.Entries.Any(static e => e.ResolvedBin is null);
        var detail = string.Join("; ", lines) + (parked.Count == 0 ? string.Empty : "; " + string.Join("; ", parked));
        return new DoctorCheck("launcher:agents", missing || parked.Count > 0 ? CheckState.Warn : CheckState.Ok, detail, missing || parked.Count > 0 ? "sindri launch show" : null);
    }
}
