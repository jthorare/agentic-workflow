using Aw.Kernel;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>What happens when an action on a Home Needs-you row is pressed (<see cref="ShellViewModel"/>, continued): approve-all, a decision option, Home's own sends, or the dispatcher.</summary>
internal sealed partial class ShellViewModel
{
    // A key or a button asks to run an action: approve-all opens its dialog; a decision option is Decide, sent once with the option, and the sheet shows what came back;
    // Pause service and a row's Complete are Home's own send; every other ready action goes to the dispatcher, and the row shows what became of it (never dropped silently).
    private void Decide(NeedsYouActionEventArgs request)
    {
        if (string.Equals(request.Action.CatalogId, ApproveAllViewModel.Action, StringComparison.Ordinal))
        {
            OpenApproveAll();
            return;
        }

        if (request.Action.OptionId.Length > 0)
        {
            _ = AnswerDecision(request.RowId, request.Action.OptionId, note: null);
            return;
        }

        if (IsHomeSend(request.Action.CatalogId) && NeedsYou.Row(request.RowId) is { Entry.Subject: not null } row)
        {
            _ = SendHomeAction(request.Action.CatalogId, row);
            return;
        }

        _ = Dispatch(request.RowId, request.Action);
    }

    // Pause service and a row's Complete have Home's own send (below); the sheet's Complete with files and the other actions go to the dispatcher.
    private static bool IsHomeSend(string catalogId) =>
        string.Equals(catalogId, "PauseService", StringComparison.Ordinal) || string.Equals(catalogId, "CompleteStep", StringComparison.Ordinal);

    // The row says "Sending..." while the command is out, then the accepted sentence or the refusal as served; a desk task that needs a choice opens its sheet.
    private async Task Dispatch(string rowId, NeedsYouAction action, IReadOnlyDictionary<string, byte[]>? files = null)
    {
        if (NeedsYou.Row(rowId) is not { } row || _dispatcher is not { } dispatcher)
        {
            return;
        }

        row.ActionNote = Labels.RowSending;
        var result = await dispatcher.Run(row.Entry, action, files).ConfigureAwait(true);
        if (NeedsYou.Row(rowId) is { } shown)
        {
            shown.ActionNote = result.Outcome == DispatchOutcome.OpenSheet ? "" : result.Message;
        }

        if (result.Outcome == DispatchOutcome.Sent && Health.ApiUrl() is { } url)
        {
            // An accepted command changes what needs the person: read the list now, so a row it settled (an approved workflow) leaves Home instead of waiting for an event.
            await ReadNeedsYou(url, CancellationToken.None).ConfigureAwait(true);
        }

        if (result.Outcome == DispatchOutcome.OpenSheet)
        {
            OpenNeedsYouRow(rowId);
        }
    }

    // Home's Pause service pauses the service the row names; its Complete reports the desk run of the row's instance as approved, the way
    // the inbox bar's Approve does. What can't be sent, or is refused, is said on the row, with the command when a terminal does it.
    private async Task SendHomeAction(string catalogId, NeedsYouRowViewModel row)
    {
        var subject = row.Entry.Subject!;   // why: Decide only calls this for a row with a subject
        if (Health.ApiUrl() is not { } url)
        {
            return;
        }

        row.ActionNote = "";
        if (string.Equals(catalogId, "PauseService", StringComparison.Ordinal) && string.Equals(subject.Kind, NeedsYouKinds.Service, StringComparison.Ordinal))
        {
            Say(row, await _api.PauseService(url, subject.Id, CancellationToken.None).ConfigureAwait(true));
        }
        else if (string.Equals(catalogId, "CompleteStep", StringComparison.Ordinal) && string.Equals(subject.Kind, "instance", StringComparison.Ordinal) && _desk is { } desk)
        {
            await CompleteFromHome(row, desk, subject.Id).ConfigureAwait(true);
        }
    }

    private async Task CompleteFromHome(NeedsYouRowViewModel row, DeskReporter desk, string instance)
    {
        var target = await desk.Load(instance).ConfigureAwait(true);
        if (target is null || target.IsTerminalOnly)
        {
            row.ActionNote = Labels.CompleteInTerminal(target?.Complete?.Cli ?? "");
        }
        else if (target.RequiredOutputs.Count > 0)
        {
            row.ActionNote = Labels.CompleteNeedsOutputInSheet;   // the engine refuses a 'done' without its files: the row's sheet has the inputs
            OpenNeedsYouRowSheet(row.Id);
        }
        else if (await desk.Complete(target, "approve", answers: null).ConfigureAwait(true) is { } answer)
        {
            Say(row, answer);
        }
    }

    private static void Say<T>(NeedsYouRowViewModel row, Result<T> answer)
    {
        if (answer.IsError)
        {
            row.ActionNote = Labels.ActionRefused(answer.Error.Code, answer.Error.Message);
        }
    }
}
