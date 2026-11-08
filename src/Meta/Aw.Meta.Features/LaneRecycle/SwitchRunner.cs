using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneRecycle;

/// <summary>
/// Moves a lane to the agent routing.lanes assigns it: types /exit into its Claude, waits for the Claude to be gone, then runs aw-meta-launch
/// for that lane, which restarts the exited pane on the routed agent and model. The new agent starts from the kickoff prompt, not the digest.
/// </summary>
/// <param name="pane">Types into the lane's tmux session and checks the line was submitted.</param>
/// <param name="sessions">The session the lane's Claude runs now.</param>
/// <param name="commands">Runs aw-meta-launch.</param>
/// <param name="log">The running log.</param>
/// <param name="delay">Waits between polls for the Claude to exit.</param>
/// <param name="clock">The current time.</param>
internal sealed class SwitchRunner(PaneSubmitter pane, ILaneSessions sessions, ICommandRunner commands, IRunningLog log, IDelay delay, TimeProvider clock)
{
    internal const string ExitLine = "/exit";
    internal static readonly TimeSpan Wait = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan Poll = TimeSpan.FromSeconds(2);
    private const string WaitSeconds = "60";
    private static readonly TimeSpan _launchTimeout = TimeSpan.FromMinutes(5);

    internal static IReadOnlyList<string> Launch(RecyclePlan plan) => ["aw-meta-launch", "--team", plan.Team, "--lanes", plan.Lane, "--no-open"];

    internal static IReadOnlyList<string> Steps(RecyclePlan plan) =>
    [
        $"type {ExitLine} + Enter into tmux session {plan.Session}: routing.lanes assigns the lane to {plan.SwitchTo}, so it is not cleared",
        $"wait up to {WaitSeconds} s for the lane's Claude to be gone",
        $"run {string.Join(' ', Launch(plan))} (restarts the exited pane on {plan.SwitchTo}{(plan.SwitchModel is { } m ? $" with model {m}" : string.Empty)})",
        "log a note to the running log",
    ];

    internal async ValueTask<Result<LaneRecycleResponse>> Run(RecyclePlan plan, CancellationToken cancellationToken)
    {
        var typed = await pane.Submit(plan.Session, ExitLine, cancellationToken).ConfigureAwait(false);
        if (typed.IsError && !typed.Error.Message.Contains("still in the input box", StringComparison.Ordinal))
        {
            return MetaErrors.RecycleRefused.With($"{typed.Error.Message}; if the lane's Claude did exit, run {string.Join(' ', Launch(plan))} once its pane is at a shell");
        }

        var gone = await ClaudeGone(plan, cancellationToken).ConfigureAwait(false);
        if (gone.IsError)
        {
            return gone.Error;
        }

        if (!gone.Value)
        {
            return MetaErrors.RecycleTimedOut.With($"lane {plan.Lane}'s Claude did not exit within {WaitSeconds} s of {ExitLine}; nothing was launched. Look at the pane, then run {string.Join(' ', Launch(plan))} once it is at its shell");
        }

        var launch = Launch(plan);
        var started = await commands.Run(new CommandRequest(launch, _launchTimeout) { Env = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_META_TEAM"] = plan.Team } }, cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            return started.Error;
        }

        if (started.Value.ExitCode != 0)
        {
            return MetaErrors.CommandFailed.With($"{string.Join(' ', launch)} exited {started.Value.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)}: {(started.Value.Stderr + started.Value.Stdout).Trim().ReplaceLineEndings(" ")}");
        }

        var note = LogLine.Event(LogKind.Note, clock.GetUtcNow(), $"switched lane {plan.Lane} from claude-code to {plan.SwitchTo}{(plan.SwitchModel is { } model ? $" ({model})" : string.Empty)} (routing.lanes): session {plan.OldSession.Value} ended, digest {plan.Digest}");
        var logged = await log.Append(note, cancellationToken).ConfigureAwait(false);
        var warning = $"lane {plan.Lane} now starts on {plan.SwitchTo}; it begins from its kickoff prompt, not the digest. Check its hooks first: scripts/install-meta.sh --provider {plan.SwitchTo}, then trust them with /hooks in {plan.SwitchTo}. Once it is up, tell it: aw-meta lane-send --lane {plan.Lane} --text '{plan.ContinueLine}'";
        return logged.IsError ? logged.Error : new LaneRecycleResponse(plan.Lane, DryRun: false, plan.OldSession.Value, NewSession: null, plan.OldTokens, plan.Digest, Steps(plan), warning);
    }

    // True once the pane shows no Claude session; false when the time runs out.
    private async ValueTask<Result<bool>> ClaudeGone(RecyclePlan plan, CancellationToken cancellationToken)
    {
        var deadline = clock.GetUtcNow() + Wait;
        while (true)
        {
            var found = await sessions.Current(plan.Session, cancellationToken).ConfigureAwait(false);
            if (found.IsError)
            {
                return found.Error;
            }

            if (found.Value is null)
            {
                return true;
            }

            if (clock.GetUtcNow() >= deadline)
            {
                return false;
            }

            await delay.Wait(Poll, cancellationToken).ConfigureAwait(false);
        }
    }
}
