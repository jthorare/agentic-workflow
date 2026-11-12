using System.Globalization;
using Aw.Meta.Features.ReadEvents;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'events' command: the event log after a cursor, oldest first. Read-only. Exit 0, 2 on a bad option.</summary>
internal static class EventsCommand
{
    internal const int DefaultMax = 500;
    private const string Usage = "events [--after N] [--kind K] [--lane L] [--max N] [--json]";

    internal static CommandEntry Entry { get; } = new(
        "events",
        new CommandSpec(Usage, [], ["after", "kind", "lane", "max"], []),
        NeedsConfig: false,   // the log is meta.db; no team config is needed to read it
        Run);

    private static Task<int> Run(CommandContext context)
    {
        if (!TryNumber(context, "after", 0, out var after) || !TryNumber(context, "max", DefaultMax, out var max))
        {
            return Task.FromResult(CommandOutput.Failed);
        }

        var maxArgument = max is > int.MaxValue ? int.MaxValue : (int)max;
        return CommandOutput.Call<ReadEventsRequest, ReadEventsResponse>(context, new ReadEventsRequest(after, context.Line["kind"], context.Line["lane"], maxArgument), Text);
    }

    private static bool TryNumber(CommandContext context, string option, long fallback, out long value)
    {
        value = fallback;
        if (context.Line[option] is not { } given)
        {
            return true;
        }

        if (long.TryParse(given, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        _ = CommandOutput.Usage(context.Io, $"--{option} needs a whole number of 0 or more, got '{given}'", Usage);
        return false;
    }

    private static string Text(ReadEventsResponse response)
    {
        var lines = response.Events.Select(static e => string.Create(
            CultureInfo.InvariantCulture,
            $"{e.Id} {e.At.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss'Z'} {e.Kind} {e.Lane ?? "-"} {e.Fields.GetRawText()}"));
        if (response.Gap)
        {
            lines = lines.Prepend(string.Create(CultureInfo.InvariantCulture, $"events before #{response.Oldest} were dropped (7-day retention): re-read status"));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
