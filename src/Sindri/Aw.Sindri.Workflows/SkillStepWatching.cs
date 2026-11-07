using System.Security.Cryptography;
using System.Text;
using Aw.Redaction;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What 'WatchSkillStep' does (skill flows spec §3.3): the plain watch of a session, plus the result read at each turn end. A turn end counts once its marker has stood for <see cref="Settle"/> with no tool running
/// (a false turn end, with a background subagent still writing, ends within seconds in a new tool call and deletes the marker). A finished result stores its outputs and returns; a question, no result or an unreadable
/// one is recorded as 'launch.owner-waiting' once per turn end and the watch goes on; a block-author bug fails the Step.
/// </summary>
/// <param name="sessions">The session's state.</param>
/// <param name="files">The hook markers.</param>
/// <param name="facts">Where the outputs are stored.</param>
/// <param name="recorder">Records 'launch.owner-waiting'.</param>
/// <param name="options">The poll interval.</param>
/// <param name="clock">The time now.</param>
internal sealed class SkillStepWatching(IAgentSessions sessions, ILaunchFiles files, IFactStore facts, AgentLaunchRecorder recorder, AgentRuntimeOptions options, TimeProvider clock)
{
    /// <summary>How long the turn-end marker must have stood, with no tool running, before the result is read.</summary>
    internal static readonly TimeSpan Settle = TimeSpan.FromSeconds(30);

    // The scrubbed detail of each launch's result that was unreadable at its last turn end; a later turn end that reads fine (or finishes) clears it. Memory only: a restarted worker reads it again at the next turn end.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _unreadable = new(StringComparer.Ordinal);

    /// <summary>Why the launch's result file was unreadable at the last turn end the watch read, or null when it was not.</summary>
    /// <param name="launch">The launch id.</param>
    /// <returns>The scrubbed detail; null for none.</returns>
    internal string? UnreadableOf(string launch) => _unreadable.TryGetValue(launch, out var detail) ? detail : null;

    /// <summary>Waits for the session to end or for a finished result.</summary>
    /// <param name="watch">The skill launch.</param>
    /// <param name="heartbeat">Tells Temporal the watch is alive.</param>
    /// <param name="cancellationToken">Cancels the watch.</param>
    /// <returns>How the session ended, or the finished result.</returns>
    internal async Task<AgentSessionState> Watch(SkillWatch watch, Action heartbeat, CancellationToken cancellationToken)
    {
        var id = LaunchId.From(watch.Launch);
        var resultFile = SkillLaunchEnv.ResultFileOf(watch.RunDirectory, id);
        DateTimeOffset? handled = null;
        var first = true;
        using var timer = new PeriodicTimer(options.PollInterval);
        while (true)
        {
            heartbeat();
            var state = await sessions.Watch(id, cancellationToken).ConfigureAwait(false);
            if (state.IsError)
            {
                throw new InvalidOperationException($"{state.Error.Code}: {state.Error.Message}");   // retried: the facts could not be read this time
            }

            if (!state.Value.IsLive)
            {
                var ended = first ? state.Value with { FoundEnded = true } : state.Value;
                return await FinalRead(watch, resultFile, ended, cancellationToken).ConfigureAwait(false);
            }

            first = false;
            if (files.TurnEndedAt(id) is { } turn && turn != handled && !files.ToolRunning(id) && clock.GetUtcNow() - turn >= Settle)
            {
                if (await AtTurnEnd(watch, resultFile, turn, handled, cancellationToken).ConfigureAwait(false) is { } done)
                {
                    return state.Value with { Skill = done.Skill, SkillFailure = done.Failure };
                }

                handled = turn;
            }

            _ = await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // A session that ended may have left a finished result (an agent that exits when done); anything else is how it ended.
    private async Task<AgentSessionState> FinalRead(SkillWatch watch, string resultFile, AgentSessionState ended, CancellationToken cancellationToken)
    {
        var file = ReadFile(resultFile);
        var decision = SkillResultReader.Decide(Input(watch, file, lastHandled: null, lastPrompt: null));
        return decision.Kind switch
        {
            SkillResultKind.Complete when file is not null => ended with { Skill = await Finished(watch, decision, file, cancellationToken).ConfigureAwait(false) },
            SkillResultKind.StepFailed => ended with { SkillFailure = Failure(decision) },
            SkillResultKind.Complete or SkillResultKind.OwnerWaiting or SkillResultKind.StillWaiting or SkillResultKind.Working => ended,
            _ => throw new ArgumentOutOfRangeException(nameof(ended), decision.Kind, "not a result kind"),
        };
    }

    // Reads the result at a settled turn end: a finished result or a block-author bug ends the watch (returned), anything else is recorded when it needs the owner and the watch goes on (null).
    private async Task<(SkillFinished? Skill, string? Failure)?> AtTurnEnd(SkillWatch watch, string resultFile, DateTimeOffset turn, DateTimeOffset? handled, CancellationToken cancellationToken)
    {
        // why: with no turn end handled yet, the seed's own prompt is no news; only a prompt after a handled turn end says the file may be stale
        var prompt = handled is null ? null : files.PromptedAt(LaunchId.From(watch.Launch));
        var file = ReadFile(resultFile);
        var decision = SkillResultReader.Decide(Input(watch, file, handled, prompt));
        Remember(watch.Launch, decision);
        if (decision.Kind == SkillResultKind.OwnerWaiting && decision.Reason is { } reason)
        {
            var text = decision.Text is { } question ? Redactor.Default.Scrub(question).Text : null;
            var detail = decision.Detail is { } d ? Redactor.Default.Scrub(d).Text : null;
            await recorder.OwnerWaiting(watch, new OwnerWaitingRow(reason, text, resultFile, detail, turn), cancellationToken).ConfigureAwait(false);
        }

        return decision.Kind switch
        {
            SkillResultKind.Complete when file is not null => (await Finished(watch, decision, file, cancellationToken).ConfigureAwait(false), null),
            SkillResultKind.StepFailed => (null, Failure(decision)),
            SkillResultKind.Complete or SkillResultKind.OwnerWaiting or SkillResultKind.StillWaiting or SkillResultKind.Working => null,
            _ => throw new ArgumentOutOfRangeException(nameof(watch), decision.Kind, "not a result kind"),
        };
    }

    private void Remember(string launch, SkillResultDecision decision)
    {
        if (decision is { Kind: SkillResultKind.OwnerWaiting, Reason: SkillWaitReason.Unreadable, Detail: { } detail })
        {
            _unreadable[launch] = Redactor.Default.Scrub(detail).Text;
        }
        else if (decision.Kind != SkillResultKind.StillWaiting)
        {
            _ = _unreadable.TryRemove(launch, out _);   // why: a turn end already handled says nothing new, so what the last one said stands
        }
    }

    private static SkillResultInput Input(SkillWatch watch, SkillResultFile? file, DateTimeOffset? lastHandled, DateTimeOffset? lastPrompt) =>
        new(file, watch.Skill, watch.Block, watch.Started, lastHandled, static path => File.Exists(path) || Directory.Exists(path)) { LastPrompt = lastPrompt };

    private static SkillResultFile? ReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? new SkillResultFile(File.ReadAllBytes(path), new DateTimeOffset(File.GetLastWriteTimeUtc(path))) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;   // boundary: a file that can't be read yet is read again at the next turn end
        }
    }

    private static string Failure(SkillResultDecision decision) => $"{decision.Error?.Code}: {decision.Detail}";

    // Stores each output as the report's output, as 'workflow complete' stores a file, and digests the report: the outcome, the result file's sha256, each output's and each path output's own.
    private async Task<SkillFinished> Finished(SkillWatch watch, SkillResultDecision decision, SkillResultFile file, CancellationToken cancellationToken)
    {
        var outcome = decision.Outcome ?? throw new InvalidOperationException("a finished result has an outcome"); // why: SkillResultKind.Complete always carries one
        var resultSha = Convert.ToHexStringLower(SHA256.HashData(file.Bytes.Span));
        var stored = new Dictionary<FactKey, FactValue>();
        var report = new StringBuilder().Append("skill result\noutcome ").Append(outcome).Append("\nresult ").Append(resultSha).Append('\n');
        foreach (var (key, value) in decision.Facts.OrderBy(static f => f.Key, StringComparer.Ordinal))
        {
            var put = await facts.Put(Encoding.UTF8.GetBytes(value), cancellationToken).ConfigureAwait(false);
            if (put.IsError)
            {
                throw new InvalidOperationException($"{put.Error.Code}: {put.Error.Message}");   // the activity retries: the store could not take it this time
            }

            stored[FactKey.From(key)] = put.Value;
            _ = report.Append(key).Append(' ').Append(put.Value.Sha256).Append('\n');
        }

        foreach (var output in watch.Block.Outputs.Where(static o => o.Type == SkillValueType.Path))
        {
            if (decision.Facts.TryGetValue(SkillDescriptors.FactKeyOf(watch.Block.Name, output.Key).Value, out var path) && PathSha(path) is { } sha)
            {
                _ = report.Append("path ").Append(output.Key).Append(' ').Append(sha).Append('\n');
            }
        }

        return new SkillFinished(outcome, stored, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(report.ToString()))), resultSha);
    }

    // The sha256 of the file a path output names; null for a folder or a file that can't be read.
    private static string? PathSha(string path)
    {
        try
        {
            return File.Exists(path) ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
