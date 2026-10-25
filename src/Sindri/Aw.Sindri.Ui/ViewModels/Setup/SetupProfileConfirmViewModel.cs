using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>
/// The profile row's approval (setup spec §2.1, §8.3): the plain-words summary of what approving lets Sindri do, and one Approve click that sends the live profile's hash, once.
/// A profile that changed meanwhile (SND-PROFILE-003) is said in words and the summary is read again.
/// </summary>
internal sealed class SetupProfileConfirmViewModel : ObservableObject
{
    private const string SessionRefusal = "SND-AUTH-002";
    private const string ChangedRefusal = "SND-PROFILE-003";

    private readonly Func<Uri?> _url;
    private readonly Func<string?> _hash;
    private readonly ISindriApi _api;
    private readonly Func<string> _cli;   // the served catalog's command for this approval, named with every refusal
    private string _approvedHash = string.Empty;   // the hash the person approved here; done holds only while the live profile still has it
    private string _summaryFor = string.Empty;   // the hash the summary in hand was read for
    private bool _wanted;
    private int _session;

    private bool Sending { get; set; }

    /// <summary>Asked when the Approve button left the tree with focus in it: focus goes to the step heading.</summary>
    internal event EventHandler<SetupFocusEventArgs>? FocusRequested;

    internal SetupProfileConfirmViewModel(Func<Uri?> url, Func<string?> hash, ISindriApi api, Func<string> cli)
    {
        (_url, _hash, _api, _cli) = (url, hash, api, cli);
        Summary.PropertyChanged += (_, _) => OnChanged(nameof(HasContent));
    }

    /// <summary>The plain-words summary of what approving lets Sindri do (shown while the row needs the person).</summary>
    public ProfileSummaryViewModel Summary { get; } = new();

    /// <summary>The refusal of the last approval in words; empty for none.</summary>
    public string Refusal
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasRefusal));
        }
    } = string.Empty;

    /// <summary>True when there is a refusal to show.</summary>
    public bool HasRefusal => Refusal.Length > 0;

    /// <summary>True while the live profile is the one that was approved here; a later change to the profile makes it false again.</summary>
    public bool IsDone => _approvedHash.Length > 0 && string.Equals(_approvedHash, Hash, StringComparison.Ordinal);

    /// <summary>True when the confirmation has anything to draw; the step's body gives it no room otherwise.</summary>
    public bool HasContent => ShowsApprove || IsWaiting || HasRefusal || Summary.HasSummary;

    /// <summary>The Approve button's word.</summary>
#pragma warning disable CA1822, MA0041 // why: the view binds it as an instance property of the view model
    public string ApproveLabel => Labels.ApproveSettings;
#pragma warning restore CA1822, MA0041

    /// <summary>True when the Approve button shows: the row asks for it, there is a profile hash and it was not approved yet.</summary>
    public bool ShowsApprove => _wanted && Hash.Length > 0 && !IsDone;

    /// <summary>True when the Approve button can be pressed: shown, and nothing on its way.</summary>
    public bool CanApprove => ShowsApprove && !Sending;

    /// <summary>True when the profile's hash is not known yet.</summary>
    public bool IsWaiting => _wanted && Hash.Length == 0 && !IsDone;

#pragma warning disable CA1822, MA0041 // why: the view binds it as an instance property of the view model
    /// <summary>The line shown while waiting for the hash.</summary>
    public string WaitingText => Labels.SetupProfileReading;
#pragma warning restore CA1822, MA0041

    private string Hash => _hash() ?? string.Empty;

    /// <summary>Follows the row: the summary is read while the row needs the person, dropped when it does not.</summary>
    /// <param name="wanted">True while the profile step needs the person.</param>
    /// <returns>A task that completes when the summary was read (or nothing needed reading).</returns>
    internal Task Follow(bool wanted)
    {
        _wanted = wanted;
        Announce();
        if (!wanted)
        {
            Reset();
            return Task.CompletedTask;
        }

        return ReadSummary(_session);
    }

    /// <summary>Approves the live profile: one plain call with its hash, sent once.</summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task Approve()
    {
        if (!CanApprove || _url() is not { } api)
        {
            return;
        }

        var (session, hash) = (_session, Hash);
        (Sending, Refusal) = (true, string.Empty);
        Announce();
        Result<ProfileApproved> sent;
        try
        {
            sent = await _api.ApproveProfile(api, new ApproveProfileRequest(hash), CancellationToken.None).ConfigureAwait(true);
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
            Settle(sent, hash);
        }
    }

    // An approval is the row's end; anything else is a refusal in words (a changed profile asks for the summary again).
    private void Settle(Result<ProfileApproved> sent, string hash)
    {
        if (sent.IsSuccess && sent.Value.Approved)
        {
            _approvedHash = hash;
            Announce();
            FocusRequested?.Invoke(this, new SetupFocusEventArgs(SetupFocus.StepHeading));   // the Approve button just left the tree with focus in it
            return;
        }

        var refused = sent.IsError ? sent.Error : null;
        Refusal = Labels.ApproveRefusal(refused is null ? "" : refused.Code, refused is null ? Labels.NeedsYouNotApproved : Words(refused), _cli());
        if (refused is { } error && string.Equals(error.Code, ChangedRefusal, StringComparison.Ordinal))
        {
            _summaryFor = string.Empty;
        }

        Announce();
    }

    // The summary for the live profile, read once per hash; a failed read is tried again at the next follow, and the person can still approve without it.
    private async Task ReadSummary(int session)
    {
        if (_url() is not { } api || Hash is not { Length: > 0 } hash || string.Equals(hash, _summaryFor, StringComparison.Ordinal))
        {
            return;
        }

        _summaryFor = hash;
        var read = await _api.ProfileSummary(api, CancellationToken.None).ConfigureAwait(true);
        if (session != _session)
        {
            return;
        }

        if (read.IsError)
        {
            _summaryFor = string.Empty;
            return;
        }

        Summary.Show(read.Value.Summary);
    }

    // A refusal in words: a changed profile asks to read it again, an agent session is told the app is the person's.
    private static string Words(Error error) => error.Code switch
    {
        ChangedRefusal => Labels.SetupProfileChangedWhileReading,
        SessionRefusal => Labels.ApproveSessionRefused,
        _ => error.Message,
    };

    private void Reset()
    {
        _session++;
        (_summaryFor, Sending) = (string.Empty, false);
        Summary.Show(summary: null);
    }

    private void Announce()
    {
        OnChanged(nameof(IsDone));
        OnChanged(nameof(HasContent));
        OnChanged(nameof(ShowsApprove));
        OnChanged(nameof(CanApprove));
        OnChanged(nameof(IsWaiting));
        OnChanged(nameof(HasRefusal));
    }
}
