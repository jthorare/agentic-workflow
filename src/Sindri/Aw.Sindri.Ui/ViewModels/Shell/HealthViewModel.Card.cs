using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Health page's status card (<see cref="HealthViewModel"/>, continued): the lease, the API, the profile, the services.</summary>
internal sealed partial class HealthViewModel
{
    /// <summary>
    /// The live region's text (§4.1 "sending"): "Sending…" once a refresh has been in flight for 300 ms, otherwise its
    /// resting text, "Refresh". Never empty (Avalonia #22312: a live element's name set to "" crashes on macOS), and the
    /// resting text is never spoken: it is set only once <see cref="IsSending"/>, the region's live gate, is off.
    /// </summary>
    public string SendingAnnouncement { get; private set => Set(ref field, value); } = Labels.Refresh;

    /// <summary>One sentence for how Sindri is ("Everything is running."); the System page leads with it and Home's strip repeats it.</summary>
    public string Sentence { get; private set => Set(ref field, value); } = Labels.Connecting;

    // One sentence for how Sindri is, by the shell's state; it lives here because Labels doesn't depend on the Shell namespace.
    private readonly record struct SentenceInputs(ShellState State, bool HasHealth, bool Healthy, string Problems, string? Served, string Look);

    private static string SentenceFor(SentenceInputs h) => h.State switch
    {
        ShellState.Normal => NormalSentence(h),
        ShellState.Stale => Labels.StoppedAnswering,
        ShellState.NotRunning => Labels.SindriNotRunning,
        ShellState.Unreadable => Labels.CantReadAnswer,
        ShellState.VersionMismatch => Labels.DifferentVersions,
        ShellState.Connecting => Labels.Connecting,
        _ => throw new ArgumentOutOfRangeException(nameof(h), h.State, message: null),
    };

    private static string NormalSentence(SentenceInputs h)
    {
        if (!h.HasHealth)
        {
            return Labels.CheckingHealth;
        }

        if (h.Healthy)
        {
            return h.Look.Length > 0 ? Labels.NeedsALook(h.Look)   // the server says healthy, a card says otherwise
                : Labels.AllRunning;
        }

        if (!string.IsNullOrWhiteSpace(h.Served))
        {
            return h.Served;   // Sindri's own sentence: "Sindri is degraded: the code index for demo is behind main."
        }

        return h.Problems.Length > 0 ? Labels.NeedsALook(h.Problems) : Labels.RunningNeedsALook;
    }

    /// <summary>Home's health strip's accessible name (R3): the health line, then "stale" while the views are stale.</summary>
    public string StripName { get; private set => Set(ref field, value); } = Labels.Connecting;

    /// <summary>Not running: the reason's detail, on its own mono line under it (the endpoint record's path, shortened, the address, or the record against the answer); or empty.</summary>
    public string ReasonDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The command that fixes "not running".</summary>
    public string Fix { get; } = Labels.StartFix;

    /// <summary>Running: the lease line.</summary>
    public string Lease { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where the API listens.</summary>
    public string Api { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The profile's state in words.</summary>
    public string ProfileState { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The profile's hash and file.</summary>
    public string ProfileDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The scheduled services.</summary>
    public IReadOnlyList<ServiceRow> Services
    {
        get;
        private set => Set(ref field, value);
    } = [];

    /// <summary>True when the card lists any service: the rows shown, not the status' own count, so the header, the rows and this agree (#3623).</summary>
    public bool HasServices => ServiceCard.Count > 0;

    /// <summary>The line that says why only the built-in services run (a profile that won't load); empty when the profile loads or no service is listed.</summary>
    public string ServicesNote { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when <see cref="ServicesNote"/> has a line.</summary>
    public bool HasServicesNote => ServicesNote.Length > 0;

    private string _profileNote = string.Empty;

    /// <summary>The status named the profile's state: remembers the note it asks for and shows it when the card lists services.</summary>
    /// <param name="profile">The status' profile.</param>
    private void ShowProfileNote(ProfileView profile)
    {
        _profileNote = StatusText.ServicesNote(profile);
        ShowServicesNote();
    }

    private void ShowProfile(ProfileView profile)
    {
        (ProfileState, ProfileDetail, LiveProfile) = (StatusText.ProfileState(profile), StatusText.ProfileDetail(profile), profile.Live);
        ModelChoice.Show(profile);
        LabelChoice.Show(profile);
        SurfaceChoice.Show(profile);
        ShowProfileNote(profile);
    }

    private void ShowServicesNote()
    {
        ServicesNote = HasServices ? _profileNote : string.Empty;
        OnChanged(nameof(HasServicesNote));
    }
}
