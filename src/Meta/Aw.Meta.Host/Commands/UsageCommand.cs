using System.Globalization;
using Aw.Meta.Core.Routing;
using Aw.Meta.Core.Usage;
using Aw.Meta.Features.Usage;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'usage' command: each lane's and the meta's standing against the usage limits, and the agent, model and usage figure behind each. Exit 1 when any is limited.</summary>
internal static class UsageCommand
{
    internal static CommandEntry Entry { get; } = new(
        "usage",
        CommandSpec.None("usage [--json]"),
        NeedsConfig: true,
        static context => CommandOutput.Call<UsageRequest, UsageResponse>(context, new UsageRequest(), Text, static r => r.Attention));

    private static string Text(UsageResponse response) => string.Join(Environment.NewLine, response.Subjects.Select(Row));

    private static string Row(UsageRow row)
    {
        var line = row.State switch
        {
            UsageState.Limited => $"{row.Subject,-12} limited  {row.Kind.ToString()?.ToLowerInvariant()} limit, resets {(row.ResetsAt is { } at ? at.UtcDateTime.ToString("u", CultureInfo.InvariantCulture) : "unknown")}  (seen in {row.Source})",
            UsageState.Unknown => $"{row.Subject,-12} unknown  {row.Reason}",
            UsageState.Ok => $"{row.Subject,-12} ok",
            _ => $"{row.Subject,-12} {row.State}",
        };
        return row.Agent is { } agent ? $"{line}{Environment.NewLine}{string.Empty,-12} on {(row.Model is { } model ? $"{agent}/{model}" : agent)}: {Signal(row)}" : line;
    }

    private static string Signal(UsageRow row) =>
        row.Windows is { Count: > 0 } windows
            ? string.Join(", ", windows.Select(Window))
            : row.SignalNote ?? "unknown";

    private static string Window(ProviderWindow window) =>
        string.Create(CultureInfo.InvariantCulture, $"{window.Name} {window.UsedPct:0.#}% used") + (window.ResetsAt is { } at ? $" (resets {at.UtcDateTime.ToString("u", CultureInfo.InvariantCulture)})" : string.Empty);
}
