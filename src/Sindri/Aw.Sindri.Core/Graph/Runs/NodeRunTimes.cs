namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>When a node row began and ended (#3683): the stores stamp a row that came without times.</summary>
public static class NodeRunTimes
{
    /// <summary>A 'running' row begins now and a final row ends now, unless the row already carries that time. A row written once at its end (skipped, collect) has an end and no start, so it has no seconds.</summary>
    /// <param name="row">The row to store.</param>
    /// <param name="now">The store's clock.</param>
    /// <returns>The row with its times.</returns>
    public static NodeRun Stamp(NodeRun row, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        var final = row.Status is NodeRunStatus.Succeeded or NodeRunStatus.Failed or NodeRunStatus.Skipped or NodeRunStatus.Cancelled;
        return row with { StartedAt = row.StartedAt ?? (row.Status == NodeRunStatus.Running ? now : null), EndedAt = row.EndedAt ?? (final ? now : null) };
    }
}
