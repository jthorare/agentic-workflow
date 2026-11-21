using System.Globalization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;

namespace Aw.Sindri.Features.HomeLanes;

/// <summary>The three lane triggers of Home spec §3.2, as pure rules over a <see cref="LaneSnapshot"/>.</summary>
internal static class LaneTriggers
{
    /// <summary>The rows that hold now. No lane name is special: the meta lane counts like any other.</summary>
    /// <param name="snapshot">What aw-meta said.</param>
    /// <returns>One row per recycling lane, per limited subject and per queued PR.</returns>
    public static IReadOnlyList<LaneRow> Rows(LaneSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return
        [
            .. snapshot.Lanes.Where(static l => string.Equals(l.Verdict, "Recycle", StringComparison.OrdinalIgnoreCase)).Select(Recycle),
            .. snapshot.Usage.Where(static u => string.Equals(u.State, "Limited", StringComparison.OrdinalIgnoreCase)).Select(Limited),
            .. snapshot.Queue.Select(Queued),
        ];
    }

    /// <summary>The row id: the kind's wire name, a colon and the key ('lane-recycle:api').</summary>
    /// <param name="kind">One of the three lane kinds.</param>
    /// <param name="key">The row's key.</param>
    /// <returns>The id.</returns>
    public static NeedsYouRowId Id(NeedsYouKind kind, string key) => NeedsYouRowId.From(Wire(kind) + ":" + key);

    /// <summary>The wire name of a lane kind ('lane-recycle', 'agent-limited', 'pr-ready-merge').</summary>
    /// <param name="kind">One of the three lane kinds.</param>
    /// <returns>The wire name.</returns>
    public static string Wire(NeedsYouKind kind)
    {
        if (kind == NeedsYouKind.LaneRecycle)
        {
            return "lane-recycle";
        }

        if (kind == NeedsYouKind.AgentLimited)
        {
            return "agent-limited";
        }

        return kind == NeedsYouKind.PrReadyMerge ? "pr-ready-merge" : throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a lane row kind");
    }

    private static LaneRow Recycle(LaneContext lane) => new(
        NeedsYouKind.LaneRecycle,
        lane.Lane,
        $"Lane {lane.Lane} is at its recycle mark",
        string.Create(CultureInfo.InvariantCulture, $"Context is {lane.Tokens} tokens, the mark is {lane.Threshold}."));

    private static LaneRow Limited(UsageSubject subject) => new(
        NeedsYouKind.AgentLimited,
        subject.Subject,
        $"{subject.Subject} hit a usage limit",
        $"{subject.Kind ?? "A"} limit" + (subject.ResetsAt is { } at ? string.Create(CultureInfo.InvariantCulture, $", resets {at.UtcDateTime:yyyy-MM-dd HH:mm} UTC.") : ", reset time unknown."));

    private static LaneRow Queued(QueuedPr pr) => new(
        NeedsYouKind.PrReadyMerge,
        pr.Pr.ToString(CultureInfo.InvariantCulture),
        string.Create(CultureInfo.InvariantCulture, $"PR #{pr.Pr} is ready to merge"),
        $"{pr.From} sent ready for it" + (pr.Github is { } g ? $" ({g} in GitHub's merge queue)." : "."));
}
