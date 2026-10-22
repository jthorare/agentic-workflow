using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The state's sends (<see cref="ExpandedStateViewModel"/>, continued): Cancel after its confirm and Hand back, each as ApplyCommand to the instance, once per press, never retried.</summary>
internal sealed partial class ExpandedStateViewModel
{
    private bool _sendingCommand;

    /// <summary>Sends the instance's commands; set by the frame, null when nothing can be sent.</summary>
    internal InstanceCommander? Commander { get; set; }

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
