using System.Globalization;
using Aw.Meta.Features.Status;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'status' command: exit 1 while anything is owed.</summary>
internal static class StatusCommand
{
    internal static CommandEntry Entry { get; } = new(
        "status",
        CommandSpec.None("status [--json]"),
        NeedsConfig: true,
        static context => CommandOutput.Call<StatusRequest, StatusResponse>(context, new StatusRequest(), Text, static r => r.Obligations.Count > 0));

    private static string Text(StatusResponse status) =>
        string.Join(
            Environment.NewLine,
            [
                status.Obligations.Count == 0 ? "owed: nothing" : "owed:",
                .. status.Obligations.Select(static o => $"  [{o.Kind}] {o.Summary}{Environment.NewLine}    next: {o.NextCommand}"),
                status.MergeQueue.Count == 0 ? "merge queue: empty" : "merge queue:",
                .. status.MergeQueue.Select(static p => string.Create(CultureInfo.InvariantCulture, $"  #{p.Pr} ready (from {p.From}); gate {p.Gate}")),
                status.OwnerQueue.Count == 0 ? "owner queue: empty" : "owner queue:",
                .. status.OwnerQueue.Select(static q => string.Create(CultureInfo.InvariantCulture, $"  q{q.Number} ({q.AskedBy}): {q.Text}")),
                string.Create(CultureInfo.InvariantCulture, $"lanes: v{status.Lanes.Version} ")
                    + (status.Lanes.Entries.Count == 0 ? "(none mapped)" : string.Join(", ", status.Lanes.Entries.Select(static e => $"{e.Lane}={e.SessionName}"))),
                .. status.Train is { Length: > 0 } train ? train.Split('\n') : [],
                .. ContextLine.Of(status.Context),
                status.Consumer is { } consumer ? "consumer: " + consumer : "consumer: not computed",
                "health: " + (status.LastHealth ?? "never run (aw-meta health)"),
                .. status.Heavy is { } heavy ? [heavy.Line()] : Array.Empty<string>(),
                .. status.Builders is { } builders ? [builders.Line()] : Array.Empty<string>(),
            ]);
}
