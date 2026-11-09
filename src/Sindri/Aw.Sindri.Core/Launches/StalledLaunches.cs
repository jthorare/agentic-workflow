using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Launches;

/// <summary>
/// The agent sessions that sat idle and were ended with no report, read from the launches view (issue #1265): the one place status and doctor decide which launches count.
/// A launch counts when its session ended as idle for a workflow Step and no newer launch of that Step's instance exists (a newer one is the engine's retry, so the Step is not abandoned).
/// These are ended launches, so they never overlap the live ones the marker files find.
/// </summary>
public static class StalledLaunches
{
    /// <summary>How long after its end a stalled launch still counts: past it the Step was answered or dropped by a person, and the view can't tell which.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    /// <summary>The launches of <paramref name="rows"/> that ended idle for a Step and were not relaunched.</summary>
    /// <param name="rows">The launches view's rows.</param>
    /// <param name="now">The time to judge the window at; null counts every one however old.</param>
    /// <returns>The stalled launches, oldest end first.</returns>
    public static IReadOnlyList<StalledLaunch> Find(IReadOnlyList<LaunchRow> rows, DateTimeOffset? now)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var newest = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.Step.Instance is { } instance && (!newest.TryGetValue(instance.Value, out var seen) || row.RequestedAt > seen))
            {
                newest[instance.Value] = row.RequestedAt;
            }
        }

        var stalled = new List<StalledLaunch>();
        foreach (var row in rows)
        {
            if (row is { Ended: { Reason: SessionEndReason.Idle } end, Step.Instance: { } instance } && newest[instance.Value] <= row.RequestedAt && (now is not { } at || at - end.At <= Window))
            {
                stalled.Add(new StalledLaunch(row.Id, instance, row.Step.State, end.At, row.Item));
            }
        }

        return [.. stalled.OrderBy(static s => s.EndedAt)];
    }
}
