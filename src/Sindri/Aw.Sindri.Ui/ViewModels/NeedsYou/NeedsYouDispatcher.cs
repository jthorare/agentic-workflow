using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// Runs a ready action of a Home Needs-you row that is not a decision or approve-all: a resume, a desk complete, a service pause, resume or run.
/// One send per action, once per press, never retried; an action it has no send for says so (<see cref="DispatchOutcome.Unhandled"/>).
/// </summary>
internal sealed class NeedsYouDispatcher
{
    private const string InstanceKind = "instance";
    private const string ServiceKind = "service";
    private const string ResumeAction = "ApplyCommand:answer-resume";
    private const string CompleteAction = "CompleteStep";
    private const string PauseAction = "PauseService";
    private const string ResumeServiceAction = "ResumeService";
    private const string RunAction = "RunService";
    private const string ApproveRecipeAction = "ApproveRecipe";
    private const string ApproveProfileAction = "ApproveProfile";

    private readonly ISindriApi _api;
    private readonly Func<Uri?> _apiUrl;
    private readonly InstanceCommander _commands;
    private readonly DeskReporter _desk;
    private readonly HashSet<string> _services = new(StringComparer.Ordinal);
    private readonly HashSet<string> _approvals = new(StringComparer.Ordinal);

    internal NeedsYouDispatcher(ISindriApi api, Func<Uri?> apiUrl, InstanceCommander commands, DeskReporter desk)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(apiUrl);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(desk);
        (_api, _apiUrl, _commands, _desk) = (api, apiUrl, commands, desk);
    }

    /// <summary>Runs the action on the row.</summary>
    /// <param name="entry">The row.</param>
    /// <param name="action">One of its ready actions.</param>
    /// <param name="files">The contents of the desk task's required outputs by key, from the sheet's inputs; null from a row's own button, which has none to give.</param>
    /// <returns>What became of it.</returns>
    internal Task<NeedsYouDispatch> Run(NeedsYouEntry entry, NeedsYouAction action, IReadOnlyDictionary<string, byte[]>? files = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(action);
        var id = action.CatalogId;
        if (SubjectOf(entry, InstanceKind) is { } instance)
        {
            if (string.Equals(id, ResumeAction, StringComparison.Ordinal))
            {
                return Resume(instance, entry.Cli);
            }

            if (string.Equals(id, CompleteAction, StringComparison.Ordinal))
            {
                return Complete(instance, files);
            }
        }

        if (SubjectOf(entry, ServiceKind) is { } service && (string.Equals(id, PauseAction, StringComparison.Ordinal) || string.Equals(id, ResumeServiceAction, StringComparison.Ordinal) || string.Equals(id, RunAction, StringComparison.Ordinal)))
        {
            return Service(id, service);
        }

        return Approval(entry, action);
    }

    // A changed workflow or the changed settings, approved with the same send the inbox bar uses; any other action is said as not available.
    private Task<NeedsYouDispatch> Approval(NeedsYouEntry entry, NeedsYouAction action)
    {
        if (string.Equals(action.CatalogId, ApproveRecipeAction, StringComparison.Ordinal) && SubjectOf(entry, NeedsYouKinds.Recipe) is { } recipe)
        {
            return Approve(ApproveRecipeAction + recipe, entry.Cli, url => NeedsYouConfirm.SendRecipe(_api, url, recipe, entry.Cli));
        }

        return string.Equals(action.CatalogId, ApproveProfileAction, StringComparison.Ordinal) && SubjectOf(entry, NeedsYouKinds.Profile) is { } hash
            ? Approve(ApproveProfileAction + hash, entry.Cli, url => NeedsYouConfirm.SendProfile(_api, url, hash))
            : Task.FromResult(new NeedsYouDispatch(DispatchOutcome.Unhandled, Labels.RowNotAvailable(action.Cli)));
    }

    private async Task<NeedsYouDispatch> Approve(string key, string cli, Func<Uri, Task<(Error? Refused, bool Approved)>> send)
    {
        if (_apiUrl() is not { } url || !_approvals.Add(key))
        {
            return Busy();
        }

        try
        {
            var (refused, approved) = await send(url).ConfigureAwait(true);
            return approved ? new NeedsYouDispatch(DispatchOutcome.Sent, Labels.RowApprovalSent) : new NeedsYouDispatch(DispatchOutcome.Refused, NeedsYouConfirm.RefusalText(refused, cli));
        }
        finally
        {
            _ = _approvals.Remove(key);
        }
    }

    private static string? SubjectOf(NeedsYouEntry entry, string kind) =>
        entry.Subject is { } subject && string.Equals(subject.Kind, kind, StringComparison.Ordinal) && subject.Id.Length > 0 ? subject.Id : null;

    private async Task<NeedsYouDispatch> Resume(string instance, string cli) =>
        Reply(await _commands.Send(ApplyCommandBody.Resume(instance)).ConfigureAwait(true), cli: cli);

    // The desk report is sent when the step has an outcome that needs nothing more from the person (the recommended one, else its only one);
    // otherwise the row's sheet opens, where the action bar asks for the reason or the choice. A task that requires output files needs them given (the sheet's inputs).
    private async Task<NeedsYouDispatch> Complete(string instance, IReadOnlyDictionary<string, byte[]>? files)
    {
        if (await _desk.Load(instance).ConfigureAwait(true) is not { } target)
        {
            return new NeedsYouDispatch(DispatchOutcome.Refused, Labels.CompleteUnreadable);
        }

        if (target.InputFreeOutcome() is not { } outcome)
        {
            return new NeedsYouDispatch(DispatchOutcome.OpenSheet, Labels.CompleteNeedsChoice);
        }

        if (target.RequiredOutputs.Count > 0 && files is null)
        {
            return new NeedsYouDispatch(DispatchOutcome.Refused, Labels.CompleteNeedsOutput);   // a row's own button has no file to give: the sheet has the inputs, and the row says so
        }

        return Reply(await _desk.Complete(target, outcome, answers: null, files).ConfigureAwait(true), Labels.CompleteSent);
    }

    private async Task<NeedsYouDispatch> Service(string action, string name)
    {
        if (_apiUrl() is not { } url || !_services.Add(name))
        {
            return Busy();
        }

        try
        {
            if (string.Equals(action, PauseAction, StringComparison.Ordinal))
            {
                return Reply(await _api.PauseService(url, name, CancellationToken.None).ConfigureAwait(true));
            }

            return string.Equals(action, ResumeServiceAction, StringComparison.Ordinal)
                ? Reply(await _api.ResumeService(url, name, CancellationToken.None).ConfigureAwait(true))
                : Reply(await _api.RunService(url, name, CancellationToken.None).ConfigureAwait(true));
        }
        finally
        {
            _ = _services.Remove(name);
        }
    }

    // A null answer is a send that did not go out: one for this target was in flight, or no endpoint is known. A refusal reads like an approval's: code, words, the row's command.
    private static NeedsYouDispatch Reply<T>(Result<T>? answer, string sent = Labels.RowSent, string cli = "") => answer switch
    {
        null => Busy(),
        { IsError: true } refused => new NeedsYouDispatch(DispatchOutcome.Refused, Labels.ApproveRefusal(refused.Error.Code, refused.Error.Message, cli)),
        _ => new NeedsYouDispatch(DispatchOutcome.Sent, sent),
    };

    private static NeedsYouDispatch Busy() => new(DispatchOutcome.Busy, Labels.RowSending);
}
