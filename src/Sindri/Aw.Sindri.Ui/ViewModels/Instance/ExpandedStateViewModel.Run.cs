using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Takeover;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The expanded state's agent run (<see cref="ExpandedStateViewModel"/>, continued): the run line, the launch's state, Attach and what the agent's Steps came to.</summary>
internal sealed partial class ExpandedStateViewModel
{
    private ItemView? _item;
    private bool _itemUnreadable;
    private bool _sending;
    private bool _sent;

    /// <summary>Sends Attach; set by the frame, null when nothing can be sent.</summary>
    internal AgentAttacher? Attacher { get; set; }

    /// <summary>The Attach component on a live agent Step (an agent run, or a step.agent Step not taken over); null otherwise.</summary>
    public AttachControlViewModel? Attach { get; private set; }

    /// <summary>True when the Attach component shows.</summary>
    public bool HasAttach => Attach is not null;

    /// <summary>"An agent is working on this step" when an agent session runs the Step; empty otherwise.</summary>
    public string RunLine { get; private set; } = string.Empty;

    /// <summary>"Agent session LCH-…": the launch id, for Stage details; empty without a run.</summary>
    public string RunDetail { get; private set; } = string.Empty;

    /// <summary>True when an agent runs the Step.</summary>
    public bool HasRun => RunLine.Length > 0;

    /// <summary>The run line's accessible name.</summary>
    public string RunName { get; private set; } = string.Empty;

    /// <summary>Raised when a review has nothing to open and the person is taken to its fact; the frame opens its facts panel there.</summary>
    internal event EventHandler<FrameFactEventArgs>? FactRequested;

    /// <summary>Opens links and files for the reviews; the frame gives its own, nothing opens without one.</summary>
    internal ILauncher Launcher { get; init; } = NullLauncher.Instance;

    /// <summary>A button for each thing the desk step asks the person to read (the design, the plan, the PR); empty when it asks none.</summary>
    public IReadOnlyList<DeskReviewViewModel> Reviews => field ??= DeskReviewViewModel.Of(_current.Desk?.Reviews, Launcher, fact => FactRequested?.Invoke(this, new FrameFactEventArgs(fact)));

    /// <summary>True when there is a review button.</summary>
    public bool HasReviews => Reviews.Count > 0;

    /// <summary>Why the step stands as it does, in words for a code this build knows and as the code for any other; empty when the engine serves none.</summary>
    public string ReasonLine => _current.Status.ReasonCode is { Length: > 0 } code ? Labels.ReasonCodeWords(code) : string.Empty;

    /// <summary>True when there is a reason line.</summary>
    public bool HasReasonLine => ReasonLine.Length > 0;

    /// <summary>Raised when the session link is followed.</summary>
    internal event EventHandler<FrameSessionEventArgs>? SessionRequested;

    /// <summary>The ref of the session running this Step, once a session row with the run's launch id is known; else null.</summary>
    internal string? SessionRef { get; private set; }

    /// <summary>True when the session link shows.</summary>
    public bool HasSession => SessionRef is not null;

    /// <summary>The session link's visible words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string SessionLinkText => Labels.SessionLink;
#pragma warning restore CA1822, MA0041

    /// <summary>The session link's accessible name: "Session ▸ cursor/pid-333-100".</summary>
    public string SessionLinkName => SessionRef is { } sessionRef ? Labels.SessionLinkName(sessionRef) : Labels.SessionLink;

    /// <summary>Opens the session's sheet; nothing without a known session.</summary>
    internal void OpenSession()
    {
        if (SessionRef is { } sessionRef)
        {
            SessionRequested?.Invoke(this, new FrameSessionEventArgs(sessionRef));
        }
    }

    /// <summary>The launch's state in words; empty until it is read.</summary>
    public string LaunchLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the launch's state shows.</summary>
    public bool HasLaunchLine => LaunchLine.Length > 0;

    /// <summary>What the agent's Steps came to ("via agent · 1,200 tokens", "The session was lost. …"), oldest first.</summary>
    public IReadOnlyList<string> RunEvents { get; private set => Set(ref field, value); } = [];

    /// <summary>True when there are run events to show.</summary>
    public bool HasRunEvents => RunEvents.Count > 0;

    /// <summary>What the last Attach came to, in words; empty before one.</summary>
    public string AttachOutcomeLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The attach command the print viewer left to run, or empty.</summary>
    public string AttachCommand { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when there is an attach command to show.</summary>
    public bool HasAttachCommand => AttachCommand.Length > 0;

    /// <summary>The attach command's Copy.</summary>
    public CopyAction AttachCommandCopy { get; }

    /// <summary>The attach command's Copy button name.</summary>
    public string CopyAttachName { get; }

    private void InitRun(CurrentState current, string address)
    {
        if (current.Status.Run is { } run && string.Equals(run.Runtime, "agent", StringComparison.Ordinal))
        {
            (RunLine, RunName, RunDetail) = (Labels.RunLine, Labels.RunName(run.Launch), Labels.RunDetailLine(run.Launch));
        }

        var live = HasRun || (current.Step is { } step && string.Equals(step.Ref, "step.agent", StringComparison.Ordinal) && current.Status.Paused is null);
        if (live)
        {
            Attach = new AttachControlViewModel(AttachFor(address));
            Attach.Chosen += (_, e) => _ = Chosen(e.Action);
        }
    }

    private AttachInput AttachFor(string address)
    {
        // A send in flight, or one answered whose item is not read again yet, is never ready: the version it carried is spent.
        var reason = _sending ? Labels.AttachSending : _sent ? Labels.AttachSent : RunWords.PendingReason(_item, _itemUnreadable);
        return new AttachInput(address, AttachStepKind.Agent, reason is null, _cancel?.Confirm, reason);
    }

    /// <summary>Takes what the frame read about the run: the launch's state, the agent Steps' events, and whether Attach is ready.</summary>
    /// <param name="read">What was read.</param>
    internal void ShowRun(AgentRunRead read)
    {
        ArgumentNullException.ThrowIfNull(read);
        (_item, _itemUnreadable, _sent) = (read.Item, read.ItemUnreadable, false);   // the item is read again: its version is the new one
        SessionRef = HasRun ? read.SessionRef : null;
        OnChanged(nameof(HasSession));
        OnChanged(nameof(SessionLinkName));
        LaunchLine = HasRun ? (read.Launch is { } launch ? RunWords.LaunchLine(launch) : Labels.LaunchUnreadable) : string.Empty;
        OnChanged(nameof(HasLaunchLine));
        RunEvents = RunWords.EventLines(read.Events);
        OnChanged(nameof(HasRunEvents));
        Attach?.Update(AttachFor(_address));
    }

    private async Task Chosen(AttachAction action)
    {
        if (action == AttachAction.Attach)
        {
            await SendAttach().ConfigureAwait(true);
            return;
        }

        if (action == AttachAction.TakeOver && Commander is not null && InstanceId is not null)
        {
            await SendTakeover().ConfigureAwait(true);
            return;
        }

        // Without a way to send: a takeover's CLI is copied; Cancel (the component asked first, with the served
        // sentence) and File a request say the send arrives later.
        if (action == AttachAction.TakeOver && Commands.FirstOrDefault(c => c.Verb == StatusVerb.Takeover) is { } takeover)
        {
            await takeover.Copy(CancellationToken.None).ConfigureAwait(true);
        }

        Pending();
    }

    private void Pending()
    {
        PendingNote = Labels.SendingArrivesNote;
        PendingVoice.Say(Labels.SendingArrivesNote);
    }

    private async Task SendAttach()
    {
        if (_sending || Attacher is not { } attacher || _item is not { } item)
        {
            return;
        }

        (_sending, AttachOutcomeLine, AttachCommand) = (true, string.Empty, string.Empty);
        OnChanged(nameof(HasAttachCommand));
        Attach?.Update(AttachFor(_address));
        try
        {
            using var source = new CancellationTokenSource();
            var outcome = await attacher.Attach(item.Version, source.Token).ConfigureAwait(true);
            (AttachOutcomeLine, AttachCommand, _sent) = (outcome.Line, outcome.Command, !outcome.Refused);
            OnChanged(nameof(HasAttachCommand));
            PendingVoice.Say(outcome.Line);
        }
        finally
        {
            _sending = false;
            Attach?.Update(AttachFor(_address));
        }
    }

    // Sends the takeover once per press and says what its viewer did: opened or unconfirmed in words; anything else, or no viewer, as the
    // reason with the attach command to copy. A server that served no viewer is asked for the launch. A refusal shows as served.
    private async Task SendTakeover()
    {
        if (_sendingCommand || Commander is not { } commander || InstanceId is not { } instance)
        {
            return;
        }

        (_sendingCommand, AttachOutcomeLine, AttachCommand) = (true, Labels.AttachSending, string.Empty);
        OnChanged(nameof(HasAttachCommand));
        try
        {
            var answer = await commander.Send(new ApplyCommandBody(instance, CommandKind.Takeover)).ConfigureAwait(true);
            if (answer is not { } result)
            {
                Pending();
                return;
            }

            (AttachOutcomeLine, AttachCommand) = result.IsError
                ? (Labels.AttachRefusal(result.Error.Code) ?? $"{result.Error.Message} ({result.Error.Code})", string.Empty)
                : await TakeoverOutcome(result.Value).ConfigureAwait(true);
            OnChanged(nameof(HasAttachCommand));
            PendingVoice.Say(AttachOutcomeLine);
        }
        finally
        {
            _sendingCommand = false;
        }
    }

    private async Task<(string Line, string Command)> TakeoverOutcome(ApplyCommandAccepted accepted)
    {
        var (viewer, attach) = (accepted.Viewer, accepted.Attach);
        var launchId = _current.Status.Run?.Launch ?? _item?.Launch?.InFlight;
        if (viewer is null && Attacher is { } attacher && launchId is not null && await attacher.ReadLaunch(launchId, CancellationToken.None).ConfigureAwait(true) is { } launch)
        {
            (viewer, attach) = (launch.Viewer, attach ?? launch.Attach);
        }

        return ViewerWords.TakeoverOutcome(viewer, attach);
    }

    /// <summary>Copies the attach command, whole.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public Task CopyAttachCommand(CancellationToken cancellationToken) => AttachCommandCopy.Run(AttachCommand, cancellationToken);
}
