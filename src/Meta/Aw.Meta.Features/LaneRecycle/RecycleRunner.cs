using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneRecycle;

/// <summary>
/// Types /clear into the lane's pane, waits for Claude Code to report a new session id (that id, not the pane text, decides the clear went through, so a failed submit check alone ends the run only when no id shows up), records it in lane-sessions (so a relaunch resumes the
/// new session, not the huge old one), types the line that continues from the digest and logs a note. If the new id never shows up nothing more is sent.
/// </summary>
/// <param name="pane">Types into the lane's tmux session and checks the line was submitted.</param>
/// <param name="sessions">The session the lane's Claude runs now.</param>
/// <param name="files">The lane records.</param>
/// <param name="log">The running log.</param>
/// <param name="delay">Waits between polls for the new session id.</param>
/// <param name="clock">The current time.</param>
/// <param name="settler">Settles the lane's request for this recycle in the inbox.</param>
internal sealed class RecycleRunner(PaneSubmitter pane, ILaneSessions sessions, ILaneFiles files, IRunningLog log, IDelay delay, TimeProvider clock, RecycleSettler settler)
{
    internal const string ClearLine = "/clear";
    internal static readonly TimeSpan Wait = TimeSpan.FromSeconds(180);
    private const string WaitSeconds = "180";
    internal static readonly TimeSpan Poll = TimeSpan.FromSeconds(2);

    internal static IReadOnlyList<string> Steps(RecyclePlan plan) =>
    [
        $"type {ClearLine} + Enter into tmux session {plan.Session} (the new session id, not the pane text, decides the clear went through)",
        $"wait up to {WaitSeconds} s for the lane's Claude to report a session id other than {plan.OldSession.Value} (~/.claude/sessions)",
        $"record the new id in lane-sessions/{plan.Lane}",
        $"type \"{plan.ContinueLine}\" + Enter (and check it was submitted)",
        "log a note to the running log",
    ];

    internal async ValueTask<Result<LaneRecycleResponse>> Run(RecyclePlan plan, CancellationToken cancellationToken)
    {
        var started = clock.GetUtcNow();
        var cleared = await pane.Submit(plan.Session, ClearLine, cancellationToken, exact: true).ConfigureAwait(false);
        if (cleared.IsError && !IsInputBoxMisread(cleared.Error))
        {
            // A tmux failure or a vanished pane: nothing was typed, so there is no new id to wait for.
            return cleared.Error;
        }

        var fresh = await NewSession(plan, cancellationToken).ConfigureAwait(false);
        if (fresh.IsError)
        {
            return fresh.Error;
        }

        if (fresh.Value is not { } newSession)
        {
            // The new session id decides whether /clear went through; a failed submit check only matters when no id shows up.
            return cleared.IsError ? cleared.Error : TimedOut(plan);
        }

        var recorded = await files.RecordSession(plan.Lane, SessionId.From(newSession), clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        // The id is recorded, so the recycle worked; a continue line the pane check misreads is a warning, not a failure.
        var continued = await pane.Submit(plan.Session, plan.ContinueLine, cancellationToken).ConfigureAwait(false);
        var outcome = await Settle(plan, started, continued.IsError ? ContinueWarning(newSession) : null, cancellationToken).ConfigureAwait(false);
        return await LogRecycle(plan, newSession, outcome, cancellationToken).ConfigureAwait(false);
    }

    private readonly record struct Outcome(string? ContinueWarning, string? SettleWarning, IReadOnlyList<string> Settled);

    // Settling is cleanup: the lane is already cleared and resumed, so a failure here must not fail the recycle (a retry would clear it twice).
    private async ValueTask<Outcome> Settle(RecyclePlan plan, DateTimeOffset started, string? continueWarning, CancellationToken cancellationToken)
    {
        var settled = await settler.Settle(plan, started, cancellationToken).ConfigureAwait(false);
        return settled.IsError
            ? new Outcome(continueWarning, $"the recycle worked but settling the lane's request failed: {settled.Error.Code}: {settled.Error.Message}; dismiss it with aw-meta inbox-dismiss", [])
            : new Outcome(continueWarning, SettleWarning: null, settled.Value);
    }

    private static string ContinueWarning(string newSession) =>
        $"new session {newSession} recorded; the continue line was typed but the pane check misread it: look at the pane before resending";

    private static bool IsInputBoxMisread(Error error) => error.Message.Contains("still in the input box", StringComparison.Ordinal);

    private async ValueTask<Result<LaneRecycleResponse>> LogRecycle(RecyclePlan plan, string newSession, Outcome outcome, CancellationToken cancellationToken)
    {
        var size = plan.OldTokens is { } tokens ? (tokens / 1000).ToString(CultureInfo.InvariantCulture) + "k" : "unknown";
        var settled = outcome.Settled;
        var warning = outcome.ContinueWarning is null ? outcome.SettleWarning : outcome.SettleWarning is null ? outcome.ContinueWarning : $"{outcome.ContinueWarning}; {outcome.SettleWarning}";
        var misread = outcome.ContinueWarning is null ? string.Empty : " (the continue line's pane check misread it; check the pane)";
        var note = LogLine.Event(LogKind.Note, clock.GetUtcNow(), $"recycled lane {plan.Lane}: session {plan.OldSession.Value} -> {newSession} (recorded in lane-sessions), context {size} tokens, digest {plan.Digest}{misread}{(settled.Count == 0 ? string.Empty : $", settled requests {string.Join(", ", settled)}")}");
        var logged = await log.Append(note, cancellationToken).ConfigureAwait(false);
        return logged.IsError ? logged.Error : new LaneRecycleResponse(plan.Lane, DryRun: false, plan.OldSession.Value, newSession, plan.OldTokens, plan.Digest, Steps(plan), warning, SettledMessages: settled.Count == 0 ? null : settled);
    }

    private static Error TimedOut(RecyclePlan plan) =>
        MetaErrors.RecycleTimedOut.With(
            $"lane {plan.Lane} did not report a new session id within {WaitSeconds} s of /clear; nothing more was sent. Once it is up, send the line by hand: tmux -L {plan.Team} send-keys -t '={plan.Session}:' -l '{plan.ContinueLine}' && tmux -L {plan.Team} send-keys -t '={plan.Session}:' Enter");

    // Polls the lane's Claude until it reports a session id other than the old one; null when the time runs out.
    private async ValueTask<Result<string?>> NewSession(RecyclePlan plan, CancellationToken cancellationToken)
    {
        var deadline = clock.GetUtcNow() + Wait;
        while (true)
        {
            var found = await sessions.Current(plan.Session, cancellationToken).ConfigureAwait(false);
            if (found.IsError)
            {
                return found.Error;
            }

            if (found.Value is { } current && !string.Equals(current.Value, plan.OldSession.Value, StringComparison.Ordinal))
            {
                return current.Value;
            }

            if (clock.GetUtcNow() >= deadline)
            {
                return Results.Success<string?>(value: null);
            }

            await delay.Wait(Poll, cancellationToken).ConfigureAwait(false);
        }
    }
}
