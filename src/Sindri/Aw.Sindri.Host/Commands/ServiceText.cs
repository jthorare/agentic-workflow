using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.ListServices;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri services' as text: one line per service, 'name  every  [paused  ]last: &lt;outcome&gt; [&lt;code&gt;]  next: &lt;slot|paused|-&gt;[  running JOB-…]'.</summary>
internal static class ServiceText
{
    /// <summary>The list as lines.</summary>
    /// <param name="list">The answer.</param>
    /// <returns>The text.</returns>
    internal static string Render(ListServicesResponse list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var text = new StringBuilder();
        if (list.Empty is { } sentence)
        {
            _ = text.AppendLine(sentence);
        }

        foreach (var service in list.Services)
        {
            _ = text.AppendLine(Line(service));
        }

        return text.ToString();
    }

    private static string Line(ServiceView s) =>
        $"{s.Name.Value}  every {s.Every}{(s.Paused ? "  paused" : string.Empty)}  last: {Last(s)}  next: {Next(s)}{(s.Job is { } job ? "  running " + job.Value : string.Empty)}";

    private static string Last(ServiceView s) => s.LastRun switch
    {
        null => "never",
        { Outcome: ServiceOutcome.Ok } => "ok",
        { Code: { } code } => "failed " + code,
        _ => "failed",
    };

    private static string Next(ServiceView s) =>
        s.Paused ? "paused" : s.OverdueSince is { } missed ? "overdue since " + Stamp(missed) : s.NextSlot is { } slot ? Stamp(slot) : "-";

    private static string Stamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
