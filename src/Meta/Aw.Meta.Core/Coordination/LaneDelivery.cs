using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// The delivery to a non-Claude lane, which has no SendMessage: the message goes into the lane's inbox (its hooks deliver it at
/// the next prompt or stop) and is recorded as a send to "team-lane", which closes the meta's reply obligation the way a
/// SendMessage does. An idle lane whose agent is up is woken with one typed line, which counts only once the lane's next hook answers it.
/// lane-send and delegate both deliver through it.
/// </summary>
/// <param name="files">The lane records.</param>
/// <param name="mail">The lanes' inboxes.</param>
/// <param name="messages">Records the send.</param>
/// <param name="beats">The lane hooks' heartbeats.</param>
/// <param name="pane">The lane's tmux session.</param>
/// <param name="clock">Dates the send.</param>
public sealed class LaneDelivery(ILaneFiles files, ILaneMail mail, IMessageStore messages, LaneBeats beats, ILanePane pane, TimeProvider clock)
{
    /// <summary>The line typed into an idle lane's pane.</summary>
    public const string WakeLine = "A message from the meta is waiting (aw-meta lane-inbox). Answer it, ending your turn with a line starting REPLY TO META:";

    /// <summary>Stores the message in the lane's inbox, records it as a send to "team-lane", and wakes the lane when it is idle with its agent up.</summary>
    /// <param name="target">The lane.</param>
    /// <param name="text">The message text.</param>
    /// <param name="confirm">How long one wait for the woken lane's hook lasts; the wake waits twice.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What the delivery did (a hook state that can't be read is attention), or an error when the message could not be stored or recorded.</returns>
    public async ValueTask<Result<LaneDelivered>> Deliver(LaneTarget target, string text, TimeSpan confirm, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        ArgumentNullException.ThrowIfNull(target);
        var seq = await mail.Send(target.Lane, text, now, cancellationToken).ConfigureAwait(false);
        if (seq.IsError)
        {
            return seq.Error;
        }

        var id = MessageId.From(string.Create(CultureInfo.InvariantCulture, $"lane-send:{target.Lane}:{seq.Value}"));
        var sent = await messages.RecordOutbound(new Outbound(target.Session, text, now) { Id = id }, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        // The message is stored and recorded by now, so a hook state that can't be read is attention, not a failed delivery:
        // the result must not tell the caller nothing went out while the outbound row says it did.
        var checkedHooks = await Attention(target, cancellationToken).ConfigureAwait(false);
        var attention = checkedHooks.IsError
            ? Results.Success<string?>($"unreadable: its hook state could not be read ({checkedHooks.Error.Code}: {checkedHooks.Error.Message.ReplaceLineEndings(" ")})")
            : checkedHooks;
        var (woken, skipped, viewer) = attention.Value is null
            ? await Wake(target, confirm, cancellationToken).ConfigureAwait(false)
            : (false, "the lane's hooks can't take part, so the message is stored and waits: the worker runs `aw-meta lane-inbox` by hand, or the owner trusts its hooks (Codex: /hooks)", false);
        return new LaneDelivered(id, woken, skipped, attention.Value, viewer);
    }

    // Why the lane can't act on the message, or null when its hooks have run since launch.
    private async ValueTask<Result<string?>> Attention(LaneTarget target, CancellationToken cancellationToken)
    {
        if (!target.Agent.Hooks)
        {
            return "launch-only: no obligations";
        }

        var beat = await beats.Last(target.Lane, cancellationToken).ConfigureAwait(false);
        if (beat.IsError)
        {
            return beat.Error;
        }

        if (beat.Value is { } last && target.Record.Session is not null && !(target.Record.RecordedAt is { } recorded && last.At < recorded))
        {
            return Results.Success<string?>(value: null);
        }

        var why = await LaneHealthReader.WhyNoHooks(files, beats, target.Record, target.Agent, cancellationToken).ConfigureAwait(false);
        return why.IsError ? why.Error : Results.Success<string?>(why.Value);
    }

    // The guards: the lane is idle and its agent is up in the pane. A hook beat newer than the typed line confirms the wake; an attached viewer doesn't block it.
    private async ValueTask<(bool Woken, string? Skipped, bool Viewer)> Wake(LaneTarget target, TimeSpan confirm, CancellationToken cancellationToken)
    {
        var idle = await beats.IsIdle(target.Lane, cancellationToken).ConfigureAwait(false);
        if (idle.IsError || !idle.Value)
        {
            return (false, "the lane is not idle: the message waits for its next hook", false);
        }

        var state = await pane.Probe(target.Session, cancellationToken).ConfigureAwait(false);
        if (state.IsError || state.Value is not { } found || !string.Equals(found.Command, target.Agent.ProcessName, StringComparison.Ordinal))
        {
            return (false, "lane down: no " + target.Agent.ProcessName + " in its pane", false);
        }

        var viewer = found.Clients > 0;
        var typedAt = clock.GetUtcNow();
        var typed = await pane.Wake(target.Session, WakeLine, cancellationToken).ConfigureAwait(false);
        if (typed.IsError)
        {
            return (false, "the wake failed: " + typed.Error.Message.ReplaceLineEndings(" "), viewer);
        }

        if (!await Confirmed(target, typedAt, confirm, cancellationToken).ConfigureAwait(false))
        {
            var seconds = (int)(confirm.TotalSeconds * 2);
            return (false, string.Create(CultureInfo.InvariantCulture, $"typed the wake line but no hook answered in {seconds} s: the line may sit unsent in the pane (aw-meta lane-send again, or press Enter in it)"), viewer);
        }

        _ = await beats.Woke(target.Lane, typedAt, cancellationToken).ConfigureAwait(false);
        return (true, null, viewer);
    }

    // The lane's next hook beat after the typed line; when none comes, Enter once more (the agent's input box may have swallowed the first) and wait again.
    private async ValueTask<bool> Confirmed(LaneTarget target, DateTimeOffset typedAt, TimeSpan confirm, CancellationToken cancellationToken)
    {
        var answered = await beats.WaitForHook(target.Lane, typedAt, confirm, cancellationToken).ConfigureAwait(false);
        if (answered.IsError || answered.Value)
        {
            return answered.IsSuccess;
        }

        var again = await pane.PressEnter(target.Session, cancellationToken).ConfigureAwait(false);
        if (again.IsError)
        {
            return false;
        }

        answered = await beats.WaitForHook(target.Lane, typedAt, confirm, cancellationToken).ConfigureAwait(false);
        return answered.IsSuccess && answered.Value;
    }
}
