using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The state's sends (<see cref="ExpandedStateViewModel"/>, continued): Cancel after its confirm and Hand back, each as ApplyCommand to the instance, once per press, never retried.</summary>
internal sealed partial class ExpandedStateViewModel
{
    private bool _sendingCommand;

    /// <summary>Sends the instance's commands; set by the frame, null when nothing can be sent.</summary>
    internal InstanceCommander? Commander { get; set; }

    /// <summary>Reports the instance's desk run; set by the frame, null when nothing can be sent.</summary>
    internal DeskReporter? Desk { get; set; }

    /// <summary>
    /// Reports the open desk run: the report is sent once per press when the step has an outcome that needs no input (the recommended one, else its only one);
    /// otherwise the choice is the person's (the Needs-you action bar asks for the reason), and the note says so.
    /// </summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task CompleteAsync()
    {
        if (Desk is not { } desk || InstanceId is not { } instance)
        {
            Pending();
            return;
        }

        if (_sendingCommand)
        {
            return;   // one report per press: a second press while one is out does nothing
        }

        _sendingCommand = true;
        try
        {
            var note = await Report(desk, instance).ConfigureAwait(true);
            PendingNote = note;
            PendingVoice.Say(note);
        }
        finally
        {
            _sendingCommand = false;
        }
    }

    private async Task<string> Report(DeskReporter desk, string instance)
    {
        if (await desk.Load(instance).ConfigureAwait(true) is not { } target)
        {
            return Labels.CompleteUnreadable;
        }

        if (target.InputFreeOutcome() is not { } outcome)
        {
            return Labels.CompleteNeedsChoice;
        }

        if (await ReadOutputs(target).ConfigureAwait(true) is not { } files)
        {
            return OutputHint.Length > 0 ? OutputHint : Labels.CompleteNeedsChoice;
        }

        var answer = await desk.Complete(target, outcome, answers: null, files).ConfigureAwait(true);
        return answer is not { } result ? Labels.SendingArrivesNote : result.IsError ? result.Error.Message : Labels.CompleteSent;
    }

    // The contents of the outputs the served desk form requires, read from the inputs; null when one is missing or can't be used (its box says why).
    private async Task<Dictionary<string, byte[]>?> ReadOutputs(DeskTarget target)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var key in target.RequiredOutputs)
        {
            var input = Outputs.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.Ordinal));
            if (input is null || !input.IsSet || await input.Content(CancellationToken.None).ConfigureAwait(true) is not { } content)
            {
                return null;
            }

            files[key] = content;
        }

        return files;
    }

    /// <summary>The instance the commands go to; set by the frame.</summary>
    internal string? InstanceId { get; set; }

    /// <summary>Goes ahead with the cancel the confirm asked about: the confirm closes at once, ApplyCommand:cancel is sent once, and the answer is said. Without a way to send, the note says the send arrives later.</summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task ConfirmCancelAsync()
    {
        if (!IsConfirmingCancel)
        {
            return;
        }

        IsConfirmingCancel = false;
        await Send(CommandKind.Cancel, Labels.CancelSent).ConfigureAwait(true);
    }

    /// <summary>Hands the run back (the Hand back command of a taken-over instance): ApplyCommand:handback, once per press.</summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal Task HandBackAsync() => Send(CommandKind.Handback, Labels.HandBackSent);

    /// <summary>Take over, from the instance's command list: ApplyCommand:takeover, once per press, and the viewer's outcome or the refusal in words. Without a way to send, the line says so.</summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task TakeOverAsync()
    {
        if (Commander is null || InstanceId is null)
        {
            AttachOutcomeLine = Labels.TakeoverNotSent;
            PendingVoice.Say(Labels.TakeoverNotSent);
            return;
        }

        await SendTakeover().ConfigureAwait(true);
    }

    private async Task Send(CommandKind kind, string sent)
    {
        if (Commander is not { } commander || InstanceId is not { } instance)
        {
            Pending();
            return;
        }

        if (_sendingCommand)
        {
            return;   // one command per press: a second press while one is out does nothing
        }

        _sendingCommand = true;
        try
        {
            var answer = await commander.Send(new ApplyCommandBody(instance, kind)).ConfigureAwait(true);
            var note = answer is not { } result ? Labels.SendingArrivesNote : result.IsError ? result.Error.Message : sent;
            PendingNote = note;
            PendingVoice.Say(note);
        }
        finally
        {
            _sendingCommand = false;
        }
    }
}
