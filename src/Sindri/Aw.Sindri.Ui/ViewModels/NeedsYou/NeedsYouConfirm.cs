using System.ComponentModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.ViewModels.Confirmations;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The confirmations the inbox's action bar hosts (F3 needs-you spec §8.3): for a drifted profile or a changed workflow the typed box, for approve-all the hold button
/// (the approve-all dialog's own <see cref="ApproveAllViewModel"/>: its plan, challenge and redeem, not a second copy), and for anything else the
/// terminal copy. The bar offers a control only when the catalog lets the app confirm that action; a single workflow's approve stays the terminal
/// copy until its typed approve lands. Nothing is sent without the person's typed text or hold, a challenge is asked for once a row shows and again
/// only after it ran out or was used up, and an answer is sent once.
/// </summary>
internal sealed partial class NeedsYouConfirm : ObservableObject
{
    private const string ProfileAction = "ApproveProfile";
    private const string RecipeAction = "ApproveRecipe";
    private const string SessionRefusal = "SND-AUTH-002";
    private const int ShortHash = 6;

    private readonly CatalogViewModel _catalog;
    private readonly Func<Uri?> _url;
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private AppChallengeView? _challenge;
    private string _hash = "";
    private bool _typedDone;   // the typed approval (profile or workflow) went through
    private readonly TypedConfirmBoxViewModel _profileBox;
    private readonly TypedConfirmBoxViewModel _recipeBox;
    private bool _recipeShown;
    private RecipeSummaryView? _recipe;
    private bool _sending;
    private string ProfileRefusal { get; set; } = "";
    private NeedsYouRowViewModel? _row;
    private int _session;   // which row this is; an answer is for the one that asked

    /// <summary>The confirmations over their ports.</summary>
    /// <param name="catalog">The command catalog: its rows say whether the app may confirm each approval.</param>
    /// <param name="url">The API now, or null when none is running.</param>
    /// <param name="api">The API.</param>
    /// <param name="clock">The clock for the challenge's expiry, the hold and the approval job's polling.</param>
    /// <param name="reducedMotion">True when motion is reduced: the hold counts in words and shows no fill. None means motion is allowed.</param>
    internal NeedsYouConfirm(CatalogViewModel catalog, Func<Uri?> url, ISindriApi api, TimeProvider clock, Func<bool>? reducedMotion = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        (_catalog, _url, _api, _clock) = (catalog, url, api, clock);
        _profileBox = new TypedConfirmBoxViewModel(ProfileAction, catalog, clock);
        _recipeBox = new TypedConfirmBoxViewModel(RecipeAction, catalog, clock);
        All = new ApproveAllViewModel(catalog, url, api, clock, reducedMotion);
        All.PropertyChanged += OnAllChanged;
        catalog.PropertyChanged += OnCatalogChanged;
    }

    /// <summary>Raised when what the bar shows of the confirmation changed: the kind, a refusal, or an approval that went through.</summary>
    internal event EventHandler? Changed;

    /// <summary>The typed box of the shown row's approval: a drifted profile's, or a changed workflow's.</summary>
    internal TypedConfirmBoxViewModel TypedBox => _recipeShown ? _recipeBox : _profileBox;

    /// <summary>The approve-all dialog's view model, which also serves the bar's hold.</summary>
    internal ApproveAllViewModel All { get; }

    /// <summary>The hold button's state: the approve-all VM's own.</summary>
    internal HoldButtonViewModel Hold => All.Hold;

    /// <summary>What the shown row offers.</summary>
    internal NeedsYouConfirmKind Kind { get; private set; }

    /// <summary>The refusal of the last answer in words; empty for none.</summary>
    internal string Refusal => Kind == NeedsYouConfirmKind.Hold ? (All.HasError ? All.Error : All.Notice) : ProfileRefusal;

    /// <summary>True once the approval went through.</summary>
    internal bool Done => Kind == NeedsYouConfirmKind.Hold ? All.IsDone : _typedDone;

    /// <summary>True when the row's primary is one of the three approvals the app confirms.</summary>
    /// <param name="entry">The row.</param>
    /// <returns>True when it is.</returns>
    internal static bool IsApproval(NeedsYouEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return AppConfirm.Actions.Contains(entry.PrimaryAction, StringComparer.Ordinal);
    }

    /// <summary>Shows a row: whatever the last one asked is dropped, and the challenge the row's control answers is asked for.</summary>
    /// <param name="row">The row; null for none.</param>
    /// <returns>A task that completes when the challenge (and for approve-all the plan) was read.</returns>
    internal Task Show(NeedsYouRowViewModel? row)
    {
        _session++;
        _row = row;
        All.Close();
        (_challenge, _hash, _typedDone, _sending) = (null, "", false, false);
        ProfileRefusal = "";
        _profileBox.Clear();
        _recipeBox.Clear();
        (_recipe, _recipeShown) = (null, false);
        Kind = KindOf(row);
        if (Kind == NeedsYouConfirmKind.Typed)
        {
            _hash = row!.Entry.Subject!.Id;   // why: the kind is Typed only for a row with a profile or recipe subject (the id is the hash or the recipe's id)
            _recipeShown = IsRecipe(row);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return Kind switch
        {
            NeedsYouConfirmKind.Typed => Ask(_session),
            NeedsYouConfirmKind.Hold => All.Open(CancellationToken.None),
            NeedsYouConfirmKind.None or NeedsYouConfirmKind.Terminal => Task.CompletedTask,
            _ => throw new InvalidOperationException("a kind the bar doesn't know"),
        };
    }

    /// <summary>Sends the typed answer of the profile's approval, once. A challenge that ran out is asked for again first and nothing is sent.</summary>
    /// <param name="typed">The typed text, exactly the challenge's.</param>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task SubmitTyped(string typed)
    {
        if (Kind != NeedsYouConfirmKind.Typed || _sending || _typedDone || _challenge is not { } challenge || _url() is not { } api)
        {
            return;
        }

        if (!string.Equals(typed.Trim(), TypedBox.Phrase, StringComparison.Ordinal))
        {
            return;   // only the served text, exactly, is an answer: the box checks it and so does this
        }

        var session = _session;
        if (challenge.ExpiresAt <= _clock.GetUtcNow())
        {
            ProfileRefusal = Labels.NeedsYouTypedExpired;
            await Ask(session).ConfigureAwait(true);
            return;
        }

        await Send(api, challenge, typed, session).ConfigureAwait(true);
    }

    // The answer goes once: the guard is set before the first await, and the challenge is used up by any answer.
    private async Task Send(Uri api, AppChallengeView challenge, string typed, int session)
    {
        (_sending, ProfileRefusal) = (true, "");
        TypedBox.SetSending(sending: true);
        Changed?.Invoke(this, EventArgs.Empty);
        (Error? Refused, bool Approved) outcome;
        try
        {
            outcome = await Approve(api, challenge, typed).ConfigureAwait(true);
        }
        finally
        {
            if (session == _session)
            {
                (_sending, _challenge) = (false, null);
                TypedBox.SetSending(sending: false);
            }
        }

        if (session == _session)
        {
            await Settle(outcome.Refused, outcome.Approved, session).ConfigureAwait(true);
        }
    }

    // What the server said: an approval is the row's end; anything else, a refusal in words, and (but for an agent session) a fresh challenge.
    private async Task Settle(Error? refused, bool approved, int session)
    {
        if (approved)
        {
            _typedDone = true;
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        ProfileRefusal = refused is null ? Labels.NeedsYouNotApproved : Words(refused);
        if (refused is null || !string.Equals(refused.Code, SessionRefusal, StringComparison.Ordinal))
        {
            await Ask(session).ConfigureAwait(true);   // a fresh challenge: asking again helps unless every session is refused
        }
        else
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The hold reached the server's time: the approve-all VM answers with the time held, once.</summary>
    /// <param name="heldMs">How long the button was held.</param>
    /// <returns>A task that completes when the answer, and the approval it started, is done.</returns>
    internal Task Held(int heldMs) => Kind == NeedsYouConfirmKind.Hold ? All.Held(heldMs) : Task.CompletedTask;

    // The kind a row offers: a control only when the catalog lets the app confirm the action, and for the profile only with the hash to approve.
    private NeedsYouConfirmKind KindOf(NeedsYouRowViewModel? row)
    {
        if (row is null || !IsApproval(row.Entry))
        {
            return NeedsYouConfirmKind.None;
        }

        var action = row.Entry.PrimaryAction;
        if (!AppConfirm.IsReady(_catalog, action))
        {
            return NeedsYouConfirmKind.Terminal;
        }

        return string.Equals(action, RecipeAction, StringComparison.Ordinal) ? RecipeKind(row)
            : string.Equals(action, ProfileAction, StringComparison.Ordinal)
                ? row.Entry.Subject is { Id.Length: > 0 } subject && string.Equals(subject.Kind, NeedsYouKinds.Profile, StringComparison.Ordinal) ? NeedsYouConfirmKind.Typed : NeedsYouConfirmKind.Terminal
                : NeedsYouConfirmKind.Hold;
    }

    // The challenge for 'approve profile <hash6>'; its served text is what the box shows and the person types.
    private async Task AskProfile(int session)
    {
        if (_url() is not { } api)
        {
            return;
        }

        // hash6 is the first six characters of the hash, as the server writes the challenge text
        var asked = await _api.IssueAppChallenge(api, new AppChallengeRequest("approve profile " + (_hash.Length > ShortHash ? _hash[..ShortHash] : _hash)), CancellationToken.None).ConfigureAwait(true);
        if (session != _session)
        {
            return;
        }

        if (asked.IsError)
        {
            (_challenge, ProfileRefusal) = (null, Words(asked.Error));
        }
        else
        {
            _challenge = asked.Value;
            TypedBox.Offer(asked.Value);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // A refusal in words: an agent session is told in plain words that the app is the person's.
    private static string Words(Error error) => string.Equals(error.Code, SessionRefusal, StringComparison.Ordinal) ? Labels.ApproveSessionRefused : error.Message;

    // The catalog was read (or read again) after the row showed: a row that waited on it is offered its control now, and one whose row went cli-only is the terminal copy.
    private void OnCatalogChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_row is { } row && string.Equals(e.PropertyName, nameof(CatalogViewModel.Controls), StringComparison.Ordinal) && KindOf(row) != Kind)
        {
            _ = Show(row);
        }
    }

    // The approve-all VM moved on (its phase, a refusal or a line): the bar reads it again.
    private void OnAllChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Kind == NeedsYouConfirmKind.Hold && e.PropertyName is { } name && (string.Equals(name, nameof(ApproveAllViewModel.Phase), StringComparison.Ordinal) || string.Equals(name, nameof(ApproveAllViewModel.Error), StringComparison.Ordinal) || string.Equals(name, nameof(ApproveAllViewModel.Notice), StringComparison.Ordinal)))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
