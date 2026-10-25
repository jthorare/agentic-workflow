using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.ApplyCommand;

/// <summary>
/// Opens the live session of a Step a takeover took over (issue 1004): in the viewer launcher.json names, else the terminal the box's detection chose (the one
/// viewer the launcher uses, so t3 sessions go to T3). It never fails the takeover: a viewer that fails, hangs or isn't usable is the outcome it reports, with the
/// reason and the attach command for a person to run. The outcome is recorded as 'launch.takeover-opened'; a ledger that refuses it is not the caller's problem.
/// </summary>
/// <param name="launches">The item's launches.</param>
/// <param name="viewer">Opens the viewer.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="ledger">Appends the outcome.</param>
/// <param name="events">Builds the entry.</param>
internal sealed class TakeoverViewing(ILaunchViews launches, ITerminalViewer viewer, HeldLease held, ILedgerStore ledger, EventFactory events)
{
    private static readonly TimeSpan _window = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _limit = TimeSpan.FromSeconds(10);

    /// <summary>Opens the live session of <paramref name="instance"/> (or of an instance under it).</summary>
    /// <param name="instance">The instance that was taken over.</param>
    /// <param name="nonce">The takeover's nonce: the event's key, so a resent command records nothing twice.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>What came of it, or null when the instance has no live session to open.</returns>
    public async ValueTask<OpenedSession?> Open(InstanceAddress instance, CommandNonce nonce, CancellationToken cancellationToken)
    {
        var item = ItemOf(instance);
        if (item is null || await Live(item.Value, instance, cancellationToken).ConfigureAwait(false) is not { Attach: { } attach } live)
        {
            return null;
        }

        var shown = await Show(live, attach, cancellationToken).ConfigureAwait(false);
        await Record(new LaunchTakeoverOpened(live.Id, item.Value, instance, shown, attach), nonce, cancellationToken).ConfigureAwait(false);
        return new OpenedSession(shown, attach);
    }

    private static ItemId? ItemOf(InstanceAddress instance) =>
        ItemId.TryFrom(instance.Value[..instance.Value.IndexOf('/', StringComparison.Ordinal)], out var item) ? item : null;

    private async ValueTask<LaunchRow?> Live(ItemId item, InstanceAddress instance, CancellationToken cancellationToken)
    {
        var rows = await launches.ForItem(item, cancellationToken).ConfigureAwait(false);
        return rows.IsError
            ? null
            : rows.Value.FirstOrDefault(row => row.Live && row.Attach is { Count: > 0 } && Under(row.Step.Instance, instance));   // newest first
    }

    private static bool Under(InstanceAddress? launched, InstanceAddress taken) =>
        launched is { } at && (at == taken || at.Value.StartsWith(taken.Value + "/", StringComparison.Ordinal));

    private async ValueTask<LaunchViewer> Show(LaunchRow live, IReadOnlyList<string> attach, CancellationToken cancellationToken)
    {
        using var bound = new CancellationTokenSource(_limit);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, bound.Token);
        var request = new ViewerRequest(new HostRef(HostKind.Tmux, LaunchSessionName.For(live.Id)), attach, "sindri " + live.Item.Value, _window);
        try
        {
            var outcome = await viewer.Open(request, linked.Token).ConfigureAwait(false);
            return await Described(outcome, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await Failed("the viewer did not open in time", cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<LaunchViewer> Described(ViewerOutcome outcome, CancellationToken cancellationToken)
    {
        var facts = outcome.ViewerKind is null ? await viewer.Probe(cancellationToken).ConfigureAwait(false) : null;   // a viewer that opened names itself; one that did not asks its probe
        var kind = outcome.ViewerKind ?? facts!.Kind;   // why: facts is read exactly when the outcome names no viewer
        var label = outcome.ViewerLabel ?? (string.Equals(kind, "print", StringComparison.Ordinal) ? null : facts?.Label);
        return outcome.Kind switch
        {
            ViewerOutcomeKind.Opened or ViewerOutcomeKind.Unconfirmed => new LaunchViewer(kind, label, outcome.Kind, Code: null, Detail: null),
            ViewerOutcomeKind.Failed => new LaunchViewer(kind, label, ViewerOutcomeKind.Failed, outcome.Code ?? "SND-LAUNCH-001", Detail: null) { Reason = $"the {label ?? kind} viewer failed to open" },
            ViewerOutcomeKind.Skipped or _ => new LaunchViewer(kind, label, ViewerOutcomeKind.Failed, "no-viewer", Detail: null) { Reason = "no terminal viewer can open a window on this box (fix: name one in launcher.json, or run the attach command)" },
        };
    }

    private async ValueTask<LaunchViewer> Failed(string reason, CancellationToken cancellationToken)
    {
        var facts = await viewer.Probe(cancellationToken).ConfigureAwait(false);
        return new LaunchViewer(facts.Kind, facts.Label, ViewerOutcomeKind.Failed, SindriErrors.LaunchTimedOut.Code, Detail: null) { Reason = reason };
    }

    private async ValueTask Record(LaunchTakeoverOpened opened, CommandNonce nonce, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return;   // no lease, nothing to record under: the takeover and its response stand
        }

        var key = $"{EventTypes.LaunchTakeoverOpened}:{nonce.Value}";
        _ = await ledger.Append(events.Entry(EventTypes.LaunchTakeoverOpened, opened, key, opened.Item.Value), grant, cancellationToken).ConfigureAwait(false);
    }
}
