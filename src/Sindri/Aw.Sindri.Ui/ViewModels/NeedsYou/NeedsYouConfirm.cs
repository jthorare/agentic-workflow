using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The approval the inbox's action bar does (F3 needs-you spec §8.3): a drifted profile. It is one plain click that calls the route with no challenge; a refusal the
/// server gives is shown in words. An approval is sent once.
/// </summary>
internal sealed class NeedsYouConfirm : ObservableObject
{
    private const string ProfileAction = "ApproveProfile";
    private const string SessionRefusal = "SND-AUTH-002";

    private static readonly string[] _approvals = [ProfileAction];

    private readonly Func<Uri?> _url;
    private readonly ISindriApi _api;
    private string _hash = "";
    private string _cli = "";   // the row's served command: the working way to do it, named with every refusal
    private bool Sending { get; set; }
    private int _session;   // which row this is; an answer is for the one that asked

    /// <summary>The approvals over their ports.</summary>
    /// <param name="url">The API now, or null when none is running.</param>
    /// <param name="api">The API.</param>
    internal NeedsYouConfirm(Func<Uri?> url, ISindriApi api)
    {
        (_url, _api) = (url, api);
    }

    /// <summary>Raised when what the bar shows of the approval changed: the kind, a refusal, or an approval that went through.</summary>
    internal event EventHandler? Changed;

    /// <summary>What the shown row approves.</summary>
    internal NeedsYouConfirmKind Kind { get; private set; }

    /// <summary>The refusal of the last approval in words; empty for none.</summary>
    internal string Refusal { get; private set; } = "";

    /// <summary>True once the approval went through.</summary>
    internal bool Done { get; private set; }   // the profile approval went through

    /// <summary>True when the shown row can be approved now: there is something to approve and no approval is on its way.</summary>
    internal bool CanApprove => Kind == NeedsYouConfirmKind.Profile && !Sending && !Done;

    /// <summary>True when the row's primary is the approval the app does.</summary>
    /// <param name="entry">The row.</param>
    /// <returns>True when it is.</returns>
    internal static bool IsApproval(NeedsYouEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return _approvals.Contains(entry.PrimaryAction, StringComparer.Ordinal);
    }

    /// <summary>Shows a row: whatever the last one did is dropped.</summary>
    /// <param name="row">The row; null for none.</param>
    /// <returns>A completed task.</returns>
    internal Task Show(NeedsYouRowViewModel? row)
    {
        _session++;
        (_hash, _cli, Done, Sending) = ("", row?.Entry.Cli ?? "", false, false);
        Refusal = "";
        Kind = KindOf(row);
        if (Kind == NeedsYouConfirmKind.Profile)
        {
            _hash = row!.Entry.Subject!.Id;   // why: the kind is Profile only for a row with that subject (the id is the hash)
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    /// <summary>Approves the shown row with one plain call, once.</summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task Approve()
    {
        if (!CanApprove)
        {
            return;
        }

        if (_url() is not { } api)
        {
            return;
        }

        var session = _session;
        (Sending, Refusal) = (true, "");
        Changed?.Invoke(this, EventArgs.Empty);
        (Error? Refused, bool Approved) outcome;
        try
        {
            outcome = await SendProfile(_api, api, _hash).ConfigureAwait(true);
        }
        finally
        {
            if (session == _session)
            {
                Sending = false;
            }
        }

        if (session == _session)
        {
            Settle(outcome.Refused, outcome.Approved);
        }
    }

    // What the server said: an approval is the row's end; anything else is a refusal in words.
    private void Settle(Error? refused, bool approved)
    {
        if (approved)
        {
            Done = true;
        }
        else
        {
            Refusal = RefusalText(refused, _cli);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // The kind a row offers: the profile, and only with the hash to approve.
    private static NeedsYouConfirmKind KindOf(NeedsYouRowViewModel? row)
    {
        if (row is null || !IsApproval(row.Entry))
        {
            return NeedsYouConfirmKind.None;
        }

        var (action, subject) = (row.Entry.PrimaryAction, row.Entry.Subject);
        return string.Equals(action, ProfileAction, StringComparison.Ordinal) && subject is { Id.Length: > 0 } && string.Equals(subject.Kind, NeedsYouKinds.Profile, StringComparison.Ordinal)
            ? NeedsYouConfirmKind.Profile
            : NeedsYouConfirmKind.None;
    }

    /// <summary>The words for a refused approval: the sentence, its code and the row's own command.</summary>
    /// <param name="refused">The refusal; null when the server answered without approving.</param>
    /// <param name="cli">The row's served command.</param>
    /// <returns>The sentence.</returns>
    internal static string RefusalText(Error? refused, string cli) =>
        Labels.ApproveRefusal(refused is null ? "" : refused.Code, refused is null ? Labels.NeedsYouNotApproved : Words(refused), cli);

    // A refusal's words: Sindri's own steps are told in plain words they can't answer. The code and the row's command go with them (Labels.ApproveRefusal).
    private static string Words(Error error) => string.Equals(error.Code, SessionRefusal, StringComparison.Ordinal) ? Labels.ApproveSessionRefused : error.Message;

    /// <summary>Approves the changed settings at the hash the row names: the one send the bar and Home's row share.</summary>
    /// <param name="client">The API.</param>
    /// <param name="api">The control plane's address.</param>
    /// <param name="hash">The profile's hash (the row's subject).</param>
    /// <returns>The refusal, or whether it approved.</returns>
    internal static async Task<(Error? Refused, bool Approved)> SendProfile(ISindriApi client, Uri api, string hash)
    {
        ArgumentNullException.ThrowIfNull(client);
        var profile = await client.ApproveProfile(api, new ApproveProfileRequest(hash), CancellationToken.None).ConfigureAwait(true);
        return (profile.IsError ? profile.Error : null, profile.IsSuccess && profile.Value.Approved);
    }
}
