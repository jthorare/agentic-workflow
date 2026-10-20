using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>
/// Folds an instance's ledger events into its Step runs (the instance detail's 'stepRuns'). The engine keeps no per-run
/// record, so a run is derived: a frame that opens a run token starts it; a 'step.completed' or 'step.failed' trigger
/// carrying that token ends it (a refused report ends it refused); a frame that moves on without a report ends it
/// abandoned, or cancelled when the trigger was a cancel. A takeover, an escalation or a park leaves the frame as it
/// was, so the run stays running while the instance is suspended. A pure function of the events, so a rebuild of any view over the
/// same ledger gives the same runs.
/// </summary>
public static class StepRuns
{
    private const string Refused = "workflow.refused";

    /// <summary>The runs <paramref name="events"/> (the instance's, in ledger order) open and close.</summary>
    /// <param name="events">The instance's events.</param>
    /// <param name="caps">The loop cap of each looped state (<see cref="StepRunCaps.Of"/>), which gives each run's max attempts.</param>
    /// <returns>One run per Step run, in start order.</returns>
    public static IReadOnlyList<StepRun> Fold(IEnumerable<RecordedWorkflowEvent> events, IReadOnlyDictionary<string, int> caps)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(caps);
        var runs = new List<StepRun>();
        foreach (var recorded in events)
        {
            for (var i = 0; i < runs.Count; i++)
            {
                runs[i] = Closed(runs[i], recorded);
            }

            if (recorded.Payload.Detail?.Delta.Frame is { Run: { } token } frame && !runs.Exists(r => string.Equals(r.Token, token, StringComparison.Ordinal)))
            {
                runs.Add(Opened(frame, token, recorded.At, runs, caps));
            }
        }

        return runs;
    }

    private static StepRun Opened(FrameSummary frame, string token, DateTimeOffset at, List<StepRun> runs, IReadOnlyDictionary<string, int> caps) =>
        new(token, frame.State, runs.Count(r => string.Equals(r.Step, frame.State, StringComparison.Ordinal)) + 1, caps.TryGetValue(frame.State, out var cap) ? cap : null,
            StepRunState.Running, frame.Runtime, at, EndedAt: null, DurationMs: null, Outcome: null, Tokens: null, Actor: null);

    /// <summary>The run after <paramref name="recorded"/>: ended by its report, ended by the frame moving on, or as it was.</summary>
    private static StepRun Closed(StepRun run, RecordedWorkflowEvent recorded)
    {
        if (run.State != StepRunState.Running)
        {
            return run;
        }

        var payload = recorded.Payload;
        var actor = payload.Detail?.TriggerActor ?? recorded.Actor;
        if (IsReportOn(payload, run))
        {
            return Reported(run, recorded, actor);
        }

        return payload.Detail?.Delta.Frame is { } frame && !string.Equals(frame.Run, run.Token, StringComparison.Ordinal)
            ? Ended(run, MovedOn(payload), recorded.At, actor)
            : run;
    }

    private static bool IsReportOn(WorkflowEventPayload payload, StepRun run) =>
        (string.Equals(payload.Trigger, TriggerNames.StepCompleted, StringComparison.Ordinal) || string.Equals(payload.Trigger, TriggerNames.StepFailed, StringComparison.Ordinal))
        && string.Equals(payload.Run, run.Token, StringComparison.Ordinal);

    private static StepRun Reported(StepRun run, RecordedWorkflowEvent recorded, string actor)
    {
        var payload = recorded.Payload;
        var failed = string.Equals(payload.Trigger, TriggerNames.StepFailed, StringComparison.Ordinal);
        var refused = string.Equals(recorded.Type, Refused, StringComparison.Ordinal);
        var ended = Ended(run, failed ? StepRunState.Failed : refused ? StepRunState.Refused : StepRunState.Passed, recorded.At, actor);
        return ended with
        {
            Outcome = failed ? null : payload.Outcome,
            Tokens = payload.Tokens ?? payload.Detail?.Tokens,
        };
    }

    private static StepRunState MovedOn(WorkflowEventPayload payload) =>
        string.Equals(payload.Trigger, TriggerNames.Cancel, StringComparison.Ordinal) ? StepRunState.Cancelled : StepRunState.Abandoned;

    private static StepRun Ended(StepRun run, StepRunState state, DateTimeOffset at, string actor) =>
        run with { State = state, EndedAt = at, DurationMs = (long)(at - run.StartedAt).TotalMilliseconds, Actor = actor };
}
