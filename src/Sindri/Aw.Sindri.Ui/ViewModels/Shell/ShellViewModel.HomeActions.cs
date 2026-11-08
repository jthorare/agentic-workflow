using Aw.Kernel;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>What happens when an action on a Home Needs-you row is pressed (<see cref="ShellViewModel"/>, continued): a decision option, Home's own sends, or the dispatcher.</summary>
internal sealed partial class ShellViewModel
{
    // A key or a button asks to run an action: a decision option is Decide, sent once with the option, and the sheet shows what came back;
    // Pause service is Home's own send; every other ready action goes to the dispatcher, and the row shows what became of it (never dropped silently).
    private void Decide(NeedsYouActionEventArgs request)
    {
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

    // Pause service has Home's own send (below); the other actions go to the dispatcher.
    private static bool IsHomeSend(string catalogId) =>
        string.Equals(catalogId, "PauseService", StringComparison.Ordinal);

    // The row says "Sending..." while the command is out, then the accepted sentence or the refusal as served.
    private async Task Dispatch(string rowId, NeedsYouAction action)
    {
        if (NeedsYou.Row(rowId) is not { } row || _dispatcher is not { } dispatcher)
        {
            return;
        }

        row.ActionNote = Labels.RowSending;
        var result = await dispatcher.Run(row.Entry, action).ConfigureAwait(true);
        if (NeedsYou.Row(rowId) is { } shown)
        {
            shown.ActionNote = result.Message;
        }

        if (result.Outcome == DispatchOutcome.Sent && Health.ApiUrl() is { } url)
        {
            // An accepted command changes what needs the person: read the list now, so a row it settled (an approved profile) leaves Home instead of waiting for an event.
            await ReadNeedsYou(url, CancellationToken.None).ConfigureAwait(true);
        }
    }

    // Home's Pause service pauses the service the row names. What can't be sent, or is refused, is said on the row, with the command when a terminal does it.
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
    }

    private static void Say<T>(NeedsYouRowViewModel row, Result<T> answer)
    {
        if (answer.IsError)
        {
            row.ActionNote = Labels.ActionRefused(answer.Error.Code, answer.Error.Message);
        }
    }
}
