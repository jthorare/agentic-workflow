using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Starting placeholders: a run Run now started shows at once, before the server's row exists.</summary>
internal sealed partial class RunsViewModel
{
    // The saved revision a "Run revision N once" start asked for, by run id: Retry on that placeholder runs the same revision, not the live one (#3691). Absent for a plain Run now.
    private readonly Dictionary<string, int> _startRevisions = new(StringComparer.Ordinal);

    /// <summary>The saved revision the placeholder's start asked for, or null for a plain Run now (which runs the live revision).</summary>
    /// <param name="runId">The placeholder's run id.</param>
    /// <returns>The revision, or null.</returns>
    internal int? StartingRevision(string runId) => _startRevisions.TryGetValue(runId, out var revision) ? revision : null;

    /// <summary>Shows a run Run now just started as Starting at the top of the list, from the start call's reply, before the server's row exists. Reads keep it until the real row arrives, which replaces it.</summary>
    /// <param name="runId">The new run's id.</param>
    /// <param name="revision">The saved revision a "Run revision N once" asked for, kept for Retry; null for a plain Run now.</param>
    internal void AddStarting(string runId, int? revision = null)
    {
        if (_starting.Contains(runId, StringComparer.Ordinal) || Rows.Any(r => string.Equals(r.Id, runId, StringComparison.Ordinal)))
        {
            return;
        }

        _starting.Add(runId);
        if (revision is { } asked)
        {
            _startRevisions[runId] = asked;
        }

        _startTimers[runId] = clock.CreateTimer(_ => _ui.PostOrInline(() => StartDeadlinePassed(runId)), state: null, StartWait, Timeout.InfiniteTimeSpan);   // no run row by then: the placeholder says so
        Rows.Insert(0, Placeholder(runId));
        (IsEmpty, EmptyText) = (false, string.Empty);
    }

    private RunRowViewModel Placeholder(string runId)
    {
        var row = new RunRowViewModel(new WorkflowRunRowView(runId, StartingRevision(runId) ?? Shown?.Revision ?? 0, "starting", "manual", clock.GetUtcNow(), EndedAt: null, Error: null), clock);
        if (_didNotStart.Contains(runId))
        {
            row.MarkDidNotStart();
        }

        return row;
    }

    // The timer ran out: the placeholder stays in the list (a late row still replaces it) and says the run did not start.
    private void StartDeadlinePassed(string runId)
    {
        StopStartTimer(runId);
        if (!_starting.Contains(runId, StringComparer.Ordinal))
        {
            return;
        }

        _ = _didNotStart.Add(runId);
        Rows.FirstOrDefault(r => string.Equals(r.Id, runId, StringComparison.Ordinal) && string.Equals(r.Status, "starting", StringComparison.Ordinal))?.MarkDidNotStart();
        StartTimedOut?.Invoke(this, new ValueEventArgs<string>(runId));
    }

    private void StopStartTimer(string runId)
    {
        if (_startTimers.Remove(runId, out var timer))
        {
            timer.Dispose();
        }
    }

    private void ClearStarting()
    {
        foreach (var timer in _startTimers.Values)
        {
            timer.Dispose();
        }

        _starting.Clear();
        _startRevisions.Clear();
        _startTimers.Clear();
        _didNotStart.Clear();
    }

    /// <summary>The page was left: every Starting placeholder goes and its deadline stops, so no timer outlives the page and no row waits on a timer that is gone.</summary>
    internal void ForgetStarting()
    {
        foreach (var id in _starting.ToList())
        {
            DropStarting(id);
        }

        ClearStarting();
    }

    /// <summary>Drops a Starting placeholder (Retry on a run that did not start): its row goes and its timer stops.</summary>
    /// <param name="runId">The placeholder's run id.</param>
    internal void DropStarting(string runId)
    {
        _ = _starting.Remove(runId);
        _ = _startRevisions.Remove(runId);
        _ = _didNotStart.Remove(runId);
        StopStartTimer(runId);
        var row = Rows.FirstOrDefault(r => string.Equals(r.Id, runId, StringComparison.Ordinal) && string.Equals(r.Status, "starting", StringComparison.Ordinal));
        if (row is not null)
        {
            _ = Rows.Remove(row);
        }
    }
}
