using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Surfaces;

/// <summary>Turns Jev's verdicts into override rows (spec §6.9, apply step). No graph surgery: the rows hide or fold nodes on read, so a person can see and undo them.</summary>
public static class SurfaceApply
{
    /// <summary>
    /// A reject writes one reject row per candidate of the group; a merge writes one merge row per absorbed candidate into the survivor (a survivor key not in the group, or a separate verdict, writes nothing).
    /// A row already present from Jev and not undone is not written again.
    /// </summary>
    /// <param name="groups">The groups that were asked.</param>
    /// <param name="verdicts">The verdicts, matched to a group by its key; one with no group is ignored.</param>
    /// <param name="overrides">Every override of the map.</param>
    /// <param name="at">When the rows are written.</param>
    /// <returns>The new rows, in verdict order.</returns>
    public static IReadOnlyList<MapOverride> Rows(IReadOnlyList<SurfaceGroup> groups, IReadOnlyList<SurfaceVerdict> verdicts, IReadOnlyList<MapOverride> overrides, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(verdicts);
        ArgumentNullException.ThrowIfNull(overrides);
        var byKey = groups.DistinctBy(static g => g.Key, StringComparer.Ordinal).ToDictionary(static g => g.Key, StringComparer.Ordinal);
        var present = OverrideLog.Active(overrides).Where(static o => o.Who == MapOverride.JevWho).Select(Identity).ToHashSet(StringComparer.Ordinal);
        List<MapOverride> rows = [];
        foreach (var verdict in verdicts)
        {
            if (!byKey.TryGetValue(verdict.GroupKey, out var group))
            {
                continue;
            }

            rows.AddRange(Wanted(group, verdict, at).Where(row => present.Add(Identity(row))));
        }

        return rows;
    }

    private static IEnumerable<MapOverride> Wanted(SurfaceGroup group, SurfaceVerdict verdict, DateTimeOffset at)
    {
        var keys = group.Candidates.Select(static c => c.NodeKey).ToList();
        return verdict switch
        {
            { Choice: SurfaceChoice.Reject } => keys.Select(k => Row(OverrideAction.Reject, k, other: null, at)),
            { Choice: SurfaceChoice.Merge, SurvivorKey: { } survivor } when keys.Contains(survivor, StringComparer.Ordinal) =>
                keys.Where(k => !string.Equals(k, survivor, StringComparison.Ordinal)).Select(k => Row(OverrideAction.Merge, k, survivor, at)),
            { Choice: SurfaceChoice.Separate or SurfaceChoice.Merge } => [],
            _ => [],
        };
    }

    private static MapOverride Row(OverrideAction action, string target, string? other, DateTimeOffset at) =>
        new(action, Graph.NodeKey.From(target), MapOverride.JevWho, at, Other: other is null ? null : Graph.NodeKey.From(other));

    private static string Identity(MapOverride o) => $"{o.Action}\n{o.Target.Value}\n{o.Other?.Value}";
}
