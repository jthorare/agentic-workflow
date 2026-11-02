using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

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

    /// <summary>
    /// The launches of <paramref name="stalled"/> whose instance has not moved past them (<see cref="MovedPast"/>), judged from the 'items' view's engine part
    /// of each launch's item. An item the view can't give, or that holds no engine part, leaves its launches in: nothing says they were left behind.
    /// </summary>
    /// <param name="stalled">The candidates from <see cref="Find"/>.</param>
    /// <param name="views">The views reader; null keeps every candidate.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The launches still waiting, in the same order.</returns>
    public static async ValueTask<IReadOnlyList<StalledLaunch>> Open(IReadOnlyList<StalledLaunch> stalled, IViewReader? views, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stalled);
        if (views is null || stalled.Count == 0)
        {
            return stalled;
        }

        var engines = new Dictionary<string, EngineItemPart?>(StringComparer.Ordinal);
        foreach (var item in stalled.Select(static s => s.Item.Value).Distinct(StringComparer.Ordinal))
        {
            var row = await views.Get<ItemRow>(ViewNames.Items, item, cancellationToken).ConfigureAwait(false);
            engines[item] = row.IsSuccess ? row.Value?.Engine : null;
        }

        return [.. stalled.Where(s => !MovedPast(s, engines[s.Item.Value]))];
    }

    /// <summary>
    /// Whether the instance left the stalled launch behind: its root ended (done, failed or cancelled) or is no longer one of the item's live roots, or the instance is
    /// the root itself and sits in another state than the launch's Step. A newer launch of the Step is <see cref="Find"/>'s rule, not this one.
    /// </summary>
    /// <param name="launch">The stalled launch.</param>
    /// <param name="engine">The engine part of the launch's item; null when the item has none.</param>
    /// <returns>True when the launch no longer counts.</returns>
    public static bool MovedPast(StalledLaunch launch, EngineItemPart? engine)
    {
        ArgumentNullException.ThrowIfNull(launch);
        if (engine is null)
        {
            return false;
        }

        var root = InstanceSummaries.RootOf(launch.Instance);
        if (EngineItemStates.PartOf(engine, root) is not { } part)
        {
            return true;
        }

        return part.Ended || (string.Equals(root, launch.Instance.Value, StringComparison.Ordinal) && launch.State is { } state && !string.Equals(part.State, state, StringComparison.Ordinal));
    }
}
