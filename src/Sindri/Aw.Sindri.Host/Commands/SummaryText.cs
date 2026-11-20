using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Host.Commands;

/// <summary>The startup summary (factory addendum §17.29.1): one labelled line per part, state always as words.</summary>
internal static class SummaryText
{
    internal static string Render(GetStatusResponse status, ControlEndpoint endpoint, string? ui)
    {
        var lines = new StringBuilder();
        Line(lines, "profile", Profile(status.Profile));
        Line(lines, "services", status.Services.Count == 0
            ? "none scheduled"
            : string.Join(" · ", status.Services.Select(s => $"{s.Name.Value} every {s.Every}{LastRun(s.LastRun)}")));
        if (status.Views is { } views)
        {
            Line(lines, "views", Views(views));
        }

        if (status.Health is { } health)
        {
            Health(lines, health);
        }

        Line(lines, "api", endpoint.Url.Authority);
        if (ui is not null)
        {
            Line(lines, "ui", ui);
        }

        Line(lines, "lease", status.Running
            ? string.Create(CultureInfo.InvariantCulture, $"held by pid {status.Pid}, epoch {status.Epoch}")
            : "not held");
        Line(lines, "ready", Ready(status.Profile));
        return lines.ToString();
    }

    private static string Views(ViewsStatus v) => v switch
    {
        { Stalled: true } => $"{v.StalledPhrase()}: run `{v.StalledFix()}`",
        { Rebuilding: true } => string.Create(CultureInfo.InvariantCulture, $"rebuilding (cursor {v.Cursor} of {v.LedgerHead})"),
        _ => string.Create(CultureInfo.InvariantCulture, $"cursor {v.Cursor} of {v.LedgerHead}"),
    };

    private static void Health(StringBuilder lines, HealthReport health)
    {
        Line(lines, "health", health.Word.ToString().ToLowerInvariant());
        foreach (var problem in health.Problems)
        {
            lines.Append(' ', 14).Append(problem.Code).Append(' ').AppendLine(problem.Text);
        }
    }

    private static string Profile(ProfileStatus p) => p switch
    {
        { Problem: { } problem } => $"{problem}",
        { Live: null } => $"{p.Source} (no hash)",
        _ => $"{p.Source} (hash {p.Live.Value.Abbreviated})",
    };

    private static string Ready(ProfileStatus p) => p switch
    {
        { Problem: not null } => "running; the live profile file has a problem",
        _ => "running",
    };

    private static string LastRun(DateTimeOffset? at) =>
        at is { } t ? string.Create(CultureInfo.InvariantCulture, $" (last {t:HH:mm:ss}Z)") : string.Empty;

    private static void Line(StringBuilder lines, string label, string value) =>
        lines.Append("  ").Append(label.PadRight(9)).Append(' ').AppendLine(value);
}
