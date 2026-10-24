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
    private string _summaryFor = string.Empty;   // the hash the summary in hand was read for
    private bool _wanted;
    private int _session;

    private bool Sending { get; set; }

    internal SetupProfileConfirmViewModel(Func<Uri?> url, Func<string?> hash, ISindriApi api)
    {
        (_url, _hash, _api) = (url, hash, api);
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

    /// <summary>True once the approval went through.</summary>
    public bool IsDone { get; private set => Set(ref field, value); }

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
            Settle(sent);
        }
    }

    // An approval is the row's end; anything else is a refusal in words (a changed profile asks for the summary again).
    private void Settle(Result<ProfileApproved> sent)
    {
        if (sent.IsSuccess && sent.Value.Approved)
        {
            IsDone = true;
            Announce();
            return;
        }

        var refused = sent.IsError ? sent.Error : null;
        Refusal = refused is null ? Labels.NeedsYouNotApproved : Words(refused);
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
        OnChanged(nameof(ShowsApprove));
        OnChanged(nameof(CanApprove));
        OnChanged(nameof(IsWaiting));
        OnChanged(nameof(HasRefusal));
    }
}
