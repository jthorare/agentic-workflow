using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Events;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Usage;

/// <summary>Applies the usage rules to one look: prints, remembers, logs and types the resume line into the lanes that are due.</summary>
/// <param name="pane">Types into a lane and checks the line was submitted.</param>
/// <param name="log">The running log.</param>
/// <param name="values">Keeps what was announced.</param>
/// <param name="clock">The current time.</param>
/// <param name="events">Receives usage.limited and usage.ok; null to write none.</param>
public sealed class UsageWatcher(PaneSubmitter pane, IRunningLog log, IKeyValueStore values, TimeProvider clock, MetaEventSink? events = null)
{
    internal MetaEventSink? Events { get; } = events;

    /// <summary>Takes one look's readings.</summary>
    /// <param name="team">The team.</param>
    /// <param name="readings">Each subject's standing.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The lines to print.</returns>
    public async ValueTask<IReadOnlyList<string>> Tick(TeamConfig team, IReadOnlyList<UsageReading> readings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(readings);
        var stored = await values.Get(KvKeys.UsageWatch, cancellationToken).ConfigureAwait(false);
        var before = UsageMemory.Parse(stored.IsSuccess ? stored.Value : null);
        var step = UsageWatch.Step(before, readings, clock.GetUtcNow(), team.Usage);
        var lines = new List<string>(step.Lines);
        await WriteEvents(step.Edges, cancellationToken).ConfigureAwait(false);
        foreach (var note in step.Notes)
        {
            await Note(note, cancellationToken).ConfigureAwait(false);
        }

        // Advisory: if the memory can't be kept, a re-armed watch announces the stop again.
        var text = step.Memory.Format();
        if (!string.Equals(text, before.Format(), StringComparison.Ordinal))
        {
            _ = await values.Set(KvKeys.UsageWatch, text, cancellationToken).ConfigureAwait(false);
        }

        foreach (var lane in step.Resume)
        {
            var sent = await pane.Submit(team.Team + "-" + lane, UsageWatch.ResumeLine, cancellationToken).ConfigureAwait(false);
            if (sent.IsError)
            {
                lines.Add($"USAGE RESUME FAILED {lane}: {sent.Error.Message.ReplaceLineEndings(" ")}");
            }
            else
            {
                await Note($"usage: resumed lane {lane}: typed the resume line", cancellationToken).ConfigureAwait(false);
            }
        }

        return lines;
    }

    // Provider is "claude": the readers look at Claude Code panes and transcripts only.
    private async ValueTask WriteEvents(IReadOnlyList<UsageEdge> edges, CancellationToken cancellationToken)
    {
        if (Events is null)
        {
            return;
        }

        var now = clock.GetUtcNow();
        foreach (var edge in edges)
        {
            await Events.Emit(edge.Limited ? MetaEvents.UsageLimited(now, edge.Subject, "claude", edge.Until) : MetaEvents.UsageOk(now, edge.Subject, "claude", edge.Until), cancellationToken).ConfigureAwait(false);
        }
    }

    // Advisory: a log that can't be written does not stop the watch.
    private async ValueTask Note(string text, CancellationToken cancellationToken) =>
        _ = await log.Append(LogLine.Event(LogKind.Note, clock.GetUtcNow(), text), cancellationToken).ConfigureAwait(false);
}
