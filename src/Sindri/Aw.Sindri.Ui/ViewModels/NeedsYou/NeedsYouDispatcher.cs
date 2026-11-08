using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// Runs a ready action of a Home Needs-you row that is not a decision or approve-all: a service pause, resume or run, or a profile approval.
/// One send per action, once per press, never retried; an action it has no send for says so (<see cref="DispatchOutcome.Unhandled"/>).
/// </summary>
internal sealed class NeedsYouDispatcher
{
    private const string ServiceKind = "service";
    private const string PauseAction = "PauseService";
    private const string ResumeServiceAction = "ResumeService";
    private const string RunAction = "RunService";
    private const string ApproveProfileAction = "ApproveProfile";

    private readonly ISindriApi _api;
    private readonly Func<Uri?> _apiUrl;
    private readonly HashSet<string> _services = new(StringComparer.Ordinal);
    private readonly HashSet<string> _approvals = new(StringComparer.Ordinal);

    internal NeedsYouDispatcher(ISindriApi api, Func<Uri?> apiUrl)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(apiUrl);
        (_api, _apiUrl) = (api, apiUrl);
    }

    /// <summary>Runs the action on the row.</summary>
    /// <param name="entry">The row.</param>
    /// <param name="action">One of its ready actions.</param>
    /// <returns>What became of it.</returns>
    internal Task<NeedsYouDispatch> Run(NeedsYouEntry entry, NeedsYouAction action)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(action);
        var id = action.CatalogId;
        if (SubjectOf(entry, ServiceKind) is { } service && (string.Equals(id, PauseAction, StringComparison.Ordinal) || string.Equals(id, ResumeServiceAction, StringComparison.Ordinal) || string.Equals(id, RunAction, StringComparison.Ordinal)))
        {
            return Service(id, service);
        }

        return Approval(entry, action);
    }

    // The changed settings, approved with the same send the inbox bar uses; any other action is said as not available.
    private Task<NeedsYouDispatch> Approval(NeedsYouEntry entry, NeedsYouAction action) =>
        string.Equals(action.CatalogId, ApproveProfileAction, StringComparison.Ordinal) && SubjectOf(entry, NeedsYouKinds.Profile) is { } hash
            ? Approve(ApproveProfileAction + hash, entry.Cli, url => NeedsYouConfirm.SendProfile(_api, url, hash))
            : Task.FromResult(new NeedsYouDispatch(DispatchOutcome.Unhandled, Labels.RowNotAvailable(action.Cli)));

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
    private static NeedsYouDispatch Reply<T>(Result<T>? answer, string sent = "Sent.", string cli = "") => answer switch
    {
        null => Busy(),
        { IsError: true } refused => new NeedsYouDispatch(DispatchOutcome.Refused, Labels.ApproveRefusal(refused.Error.Code, refused.Error.Message, cli)),
        _ => new NeedsYouDispatch(DispatchOutcome.Sent, sent),
    };

    private static NeedsYouDispatch Busy() => new(DispatchOutcome.Busy, Labels.RowSending);
}
