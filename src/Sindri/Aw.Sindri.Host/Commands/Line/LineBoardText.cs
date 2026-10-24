using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Host.Commands.Line;

/// <summary>The line board as text for 'sindri line show' (line spec LN-API1): the packet chain oldest first, then the phases in plan order.</summary>
internal static class LineBoardText
{
    internal static string Board(LineView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var text = new StringBuilder("Packets\n");
        foreach (var station in view.Stations)
        {
            var packet = station.Packet is { } held ? $"{held.Hash} ({(held.Frozen ? "frozen" : "not frozen")})" : "no packet yet";
            _ = text.Append(CultureInfo.InvariantCulture, $"  {station.Station}: {packet}").Append(station.Instance is { } instance ? $", running as {instance}" : string.Empty).Append('\n');
        }

        _ = text.Append(CultureInfo.InvariantCulture, $"\nPhases (at most {view.Parallel} running, {view.MaxOpenPrs} open PRs)\n");
        if (view.Phases.Count == 0)
        {
            return text.Append("  no phases yet\n").ToString();
        }

        foreach (var phase in view.Phases)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"  {phase.Id} (attempt {phase.Attempt}): {phase.State}");
            Add(text, " parked", phase.ParkReason);
            Add(text, " PR", phase.Pr);
            Add(text, " base", phase.Base);
            Add(text, " worktree", phase.Worktree);
            Add(text, " now", phase.Now);
            _ = text.Append(CultureInfo.InvariantCulture, $", {phase.Tokens} tokens, {phase.WallMinutes} min\n");
        }

        return text.ToString();
    }

    private static void Add(StringBuilder text, string label, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"; {label.Trim()} {value}");
        }
    }
}
