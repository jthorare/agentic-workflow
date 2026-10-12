using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Host.Commands;

/// <summary>The startup summary (factory addendum §17.29.1): one labelled line per part, state always as words.</summary>
internal static class SummaryText
{
    internal static string Render(GetStatusResponse status, ControlEndpoint endpoint, bool showUi)
    {
        var lines = new StringBuilder();
        Line(lines, "profile", Profile(status.Profile));
        Line(lines, "services", status.Services.Count == 0
            ? "none scheduled (the approved profile names no services)"
            : string.Join(" · ", status.Services.Select(s => $"{s.Name.Value} every {s.Every}{LastRun(s.LastRun)}")));
        Line(lines, "api", endpoint.Url.Authority);
        if (showUi)
        {
            Line(lines, "ui", "not built yet (S1b); use sindri status");
        }

        Line(lines, "lease", status.Running
            ? string.Create(CultureInfo.InvariantCulture, $"held by pid {status.Pid}, epoch {status.Epoch}")
            : "not held");
        Line(lines, "ready", Ready(status.Profile));
        return lines.ToString();
    }

    private static string Profile(ProfileStatus p) => p switch
    {
        { Problem: { } problem, Approved: null } => $"{problem}",
        { Approved: null } => $"{p.Source} (not approved; fix: sindri profile approve)",
        { ChangedSinceApproval: true } => $"{p.Source} (changed since approval {p.Approved.Value.Abbreviated}; running with the approved one; fix: sindri profile approve)",
        { Problem: { } problem } => $"saved approval {p.Approved.Value.Abbreviated}; the live file has a problem: {problem}",
        { NeedsApproval: true } => $"{p.Source} ({NotLatest}; fix: sindri profile approve)",
        _ => $"{p.Source} (approved, hash {p.Approved.Value.Abbreviated})",
    };

    /// <summary>What a saved approval that the ledger doesn't name as its latest 'profile.approved' event says.</summary>
    private const string NotLatest = "approval not recorded in the ledger as the latest one";

    private static string Ready(ProfileStatus p) => p switch
    {
        { Problem: not null } => "running; the live profile file has a problem",
        { NeedsApproval: true } => "running; the profile needs your approval",
        _ => "running",
    };

    private static string LastRun(DateTimeOffset? at) =>
        at is { } t ? string.Create(CultureInfo.InvariantCulture, $" (last {t:HH:mm:ss}Z)") : string.Empty;

    private static void Line(StringBuilder lines, string label, string value) =>
        lines.Append("  ").Append(label.PadRight(9)).Append(' ').AppendLine(value);
}
