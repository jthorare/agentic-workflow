using System.ComponentModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The approvals the inbox's action bar does (F3 needs-you spec §8.3): a drifted profile, a changed workflow and approve-all (the approve-all dialog's own
/// <see cref="ApproveAllViewModel"/>: its plan and job, not a second copy). Each is one plain click that calls the route with no challenge; a refusal the
/// server gives is shown in words. An approval is sent once.
/// </summary>
internal sealed partial class NeedsYouConfirm : ObservableObject
{
    private const string ProfileAction = "ApproveProfile";
    private const string RecipeAction = "ApproveRecipe";
    private const string SessionRefusal = "SND-AUTH-002";

    private static readonly string[] _approvals = [ProfileAction, RecipeAction, ApproveAllViewModel.Action];
    private static readonly string[] _allShown = [nameof(ApproveAllViewModel.Phase), nameof(ApproveAllViewModel.Error), nameof(ApproveAllViewModel.Notice), nameof(ApproveAllViewModel.CanApprove)];

    private readonly Func<Uri?> _url;
    private readonly ISindriApi _api;
    private string _hash = "";
    private string _cli = "";   // the row's served command: the working way to do it, named with every refusal
    private bool _approved;   // the profile or workflow approval went through
    private bool _sending;
    private string ProfileRefusal { get; set; } = "";
    private int _session;   // which row this is; an answer is for the one that asked

    /// <summary>The approvals over their ports.</summary>
    /// <param name="catalog">The command catalog, for the approve-all view model's clipboard.</param>
    /// <param name="url">The API now, or null when none is running.</param>
    /// <param name="api">The API.</param>
    /// <param name="clock">The clock for the approval job's polling.</param>
    internal NeedsYouConfirm(CatalogViewModel catalog, Func<Uri?> url, ISindriApi api, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        (_url, _api) = (url, api);
        All = new ApproveAllViewModel(catalog, url, api, clock);
        All.PropertyChanged += OnAllChanged;
    }

    /// <summary>Raised when what the bar shows of the approval changed: the kind, a refusal, or an approval that went through.</summary>
    internal event EventHandler? Changed;

    /// <summary>The approve-all dialog's view model, which also serves the bar's approve-all.</summary>
    internal ApproveAllViewModel All { get; }

    /// <summary>What the shown row approves.</summary>
    internal NeedsYouConfirmKind Kind { get; private set; }

    /// <summary>The refusal of the last approval in words; empty for none.</summary>
    internal string Refusal => Kind == NeedsYouConfirmKind.All ? (All.HasError ? All.Error : All.Notice) : ProfileRefusal;

    /// <summary>True once the approval went through.</summary>
    internal bool Done => Kind == NeedsYouConfirmKind.All ? All.IsDone : _approved;

    /// <summary>True when the shown row can be approved now: there is something to approve and no approval is on its way.</summary>
    internal bool CanApprove => Kind switch
    {
        NeedsYouConfirmKind.Profile or NeedsYouConfirmKind.Recipe => !_sending && !_approved,
        NeedsYouConfirmKind.All => All.CanApprove,
        NeedsYouConfirmKind.None => false,
        _ => throw new InvalidOperationException("a kind the bar doesn't know"),
    };

    /// <summary>True when the row's primary is one of the three approvals the app does.</summary>
    /// <param name="entry">The row.</param>
    /// <returns>True when it is.</returns>
    internal static bool IsApproval(NeedsYouEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return _approvals.Contains(entry.PrimaryAction, StringComparer.Ordinal);
    }

    /// <summary>Shows a row: whatever the last one did is dropped, and for approve-all the plan is read.</summary>
    /// <param name="row">The row; null for none.</param>
    /// <returns>A task that completes when the plan (for approve-all) was read.</returns>
    internal Task Show(NeedsYouRowViewModel? row)
    {
        _session++;
        All.Close();
        (_hash, _cli, _approved, _sending) = ("", row?.Entry.Cli ?? "", false, false);
        ProfileRefusal = "";
        Kind = KindOf(row);
        if (Kind is NeedsYouConfirmKind.Profile or NeedsYouConfirmKind.Recipe)
        {
            _hash = row!.Entry.Subject!.Id;   // why: the kind is Profile or Recipe only for a row with that subject (the id is the hash or the recipe's id)
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return Kind == NeedsYouConfirmKind.All ? All.Open(CancellationToken.None) : Task.CompletedTask;
    }

    /// <summary>Approves the shown row with one plain call, once.</summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task Approve()
    {
        if (!CanApprove)
        {
            return;
        }

        if (Kind == NeedsYouConfirmKind.All)
        {
            await All.Approve().ConfigureAwait(true);
            return;
        }

        if (_url() is not { } api)
        {
            return;
        }

        var session = _session;
        (_sending, ProfileRefusal) = (true, "");
        Changed?.Invoke(this, EventArgs.Empty);
        (Error? Refused, bool Approved) outcome;
        try
        {
            outcome = await Send(api).ConfigureAwait(true);
        }
        finally
        {
            if (session == _session)
            {
                _sending = false;
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
            _approved = true;
        }
        else
        {
            ProfileRefusal = Labels.ApproveRefusal(refused is null ? "" : refused.Code, refused is null ? Labels.NeedsYouNotApproved : Words(refused), _cli);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // The kind a row offers: the profile only with the hash to approve, a workflow only with its recipe.
    private static NeedsYouConfirmKind KindOf(NeedsYouRowViewModel? row)
    {
        if (row is null || !IsApproval(row.Entry))
        {
            return NeedsYouConfirmKind.None;
        }

        var (action, subject) = (row.Entry.PrimaryAction, row.Entry.Subject);
        return string.Equals(action, RecipeAction, StringComparison.Ordinal) ? (IsRecipe(row) ? NeedsYouConfirmKind.Recipe : NeedsYouConfirmKind.None)
            : string.Equals(action, ProfileAction, StringComparison.Ordinal)
                ? subject is { Id.Length: > 0 } && string.Equals(subject.Kind, NeedsYouKinds.Profile, StringComparison.Ordinal) ? NeedsYouConfirmKind.Profile : NeedsYouConfirmKind.None
                : NeedsYouConfirmKind.All;
    }

    // A refusal's words: Sindri's own steps are told in plain words they can't answer. The code and the row's command go with them (Labels.ApproveRefusal).
    private static string Words(Error error) => string.Equals(error.Code, SessionRefusal, StringComparison.Ordinal) ? Labels.ApproveSessionRefused : error.Message;

    // The approve-all VM moved on (its phase, a refusal or a line): the bar reads it again.
    private void OnAllChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Kind == NeedsYouConfirmKind.All && e.PropertyName is { } name && _allShown.Contains(name, StringComparer.Ordinal))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
